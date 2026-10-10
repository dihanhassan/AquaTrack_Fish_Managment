# AquaTrack — The AI Module

Five features, all computed from the farm's own recorded history. No external API, no model
weights, no network call. Every output carries a **rationale** and a **confidence score**, so a
farmer can read why the system said what it said and decide whether to trust it.

That constraint is deliberate. Advice a farmer cannot interrogate is advice they will not act on.

Code lives in [`src/AquaTrack.Application/Ai/`](../src/AquaTrack.Application/Ai/).

---

## Shared maths

`Statistics.cs` holds the small amount of maths the rest of the module needs:

| Function | Used by |
|---|---|
| `Fit` — ordinary least squares, returning slope, intercept and R² | Growth prediction, finance forecast |
| `Mean`, `StdDev` — sample standard deviation (n−1) | Anomaly detection |
| `ZScore` — how many standard deviations a value sits from a baseline | Anomaly detection |
| `MovingAverage` — mean of the last *n* observations | Finance forecast |

`Fit` returns `null` rather than nonsense when there are fewer than two points or every x is
identical. Callers fall back rather than pretending to have a trend.

---

## AI-1 · Feed Recommendation

**Question:** how much should I feed today?

Fish eat a percentage of their body weight that **falls as they grow** — fry eat 10% a day, a
kilogram fish eats 1.5%. The module encodes that standard table:

| Average weight | Daily rate |
|---|---|
| ≤ 5 g | 10% |
| ≤ 20 g | 8% |
| ≤ 50 g | 6% |
| ≤ 100 g | 5% |
| ≤ 200 g | 4% |
| ≤ 400 g | 3% |
| ≤ 700 g | 2.5% |
| ≤ 1000 g | 2% |
| above | 1.5% |

**Method**

1. Biomass = live quantity × current average weight.
2. Rate = the table value, averaged with the species' configured rate when one is set.
3. Recommendation = biomass × rate.
4. Compare against what was actually fed over the last seven days.

**Confidence** — 0.85 when the batch has a real weight sample, 0.55 when it is still on its
stocking weight. An unsampled batch is a guess and is labelled as one.

**Why the comparison matters:** the number alone is advice; the number *next to what you actually
did* is a finding. Feeding 40% above recommendation is the single most common way a pond loses
money.

---

## AI-2 · Fish Growth Prediction

**Question:** what will these fish weigh in a month, and when are they ready?

**Method**

1. Build points of (days since stocking, sampled weight), with the stocking event as the first.
2. Fit a line through them.
3. Slope = daily gain. Project forward to the horizon.
4. With a species target weight set, divide the remaining gap by the slope to get a harvest date.

**Fallbacks** — too few samples to fit, so use average gain since stocking (confidence 0.4). No
growth history at all, so predict nothing and say why (confidence 0.15).

**Confidence** is `R² × min(points / 5, 1)`, clamped to 0.2–0.95. Both halves matter: a line can
fit two points perfectly and still mean nothing, so the point count discounts it.

---

## AI-3 · Expense and Profit Prediction

**Question:** what will next month look like?

**Method** — blend a linear trend with a three-month moving average, weighted by how well the
trend actually explains the data:

```
blended = trend × R²  +  movingAverage × (1 − R²)
```

A clean trend dominates; a noisy one defers to the average.

Only **completed** months are used, so a part-finished current month cannot drag the projection
down.

**The guard rail.** A straight line extrapolates without limit. A few declining months project
through zero and into negative territory, which would have the farm predicted to earn nothing at
all while still paying for feed. So the result is constrained to between **0.4× and 2×** the recent
average: the trend sets the direction, but cannot leave the neighbourhood of what the farm has
actually been doing.

With no recent activity to anchor to, zero is the honest answer and the clamp steps aside.

**Confidence** rises with the number of completed months that had real activity, 0.25 to 0.85.

---

## AI-4 · Anomaly Detection

**Question:** what is out of line right now?

Everything is measured against **the farm's own baseline**, never an external benchmark, so a
small farm and a large one both get sensible alerts.

| Check | Method |
|---|---|
| Feed cost spike | z-score of the last 7 days against the prior 23, threshold configurable (default 2σ) |
| Medicine usage spike | Same, often the first sign of a disease outbreak |
| High mortality | Weekly deaths as a share of the stocked batch, default threshold 2% |
| Low stock | Feed and medicine at or below reorder level |
| Medicine expiry | Expired, or expiring within the configured window |
| Maintenance due | Scheduled service within seven days |

Statistical checks need **at least seven baseline days** before they will fire. Without that, any
variation looks like a spike, and an alert system that cries wolf gets ignored.

Severity escalates: beyond 1.5× the threshold a warning becomes critical.

Alerts are **deduplicated** by a stable key (`LowStock:Feed:12`), so an alert already open and
unread is not raised again every time the dashboard loads.

---

## AI-5 · Smart Assistant

**Question:** anything, in words.

Not a language model. An **intent classifier** over the farm's vocabulary, in English and Bangla,
where each intent maps to a real query.

```
"এই মাসে সবচেয়ে বেশি খরচ কোন খাতে?"  →  top-expense-category  →  expenses grouped by category
"আজকের profit কত?"                    →  today-summary         →  cash book for today
"কোন পুকুরে production বেশি?"          →  pond-production       →  harvests grouped by pond
```

Keywords are matched case-insensitively and the **longest match wins**, so "feed stock" beats the
generic "stock". Fourteen intents cover expenses, income, production, stock, feed advice,
receivables, forecasts, cash and mortality.

Every answer returns a sentence, a dataset and a chart. **If nothing matches, it says so** rather
than inventing an answer — which is the whole point of doing it this way.

---

## Why not call an LLM?

A farm in rural Bangladesh may have intermittent connectivity and no budget for per-call API
pricing. More importantly, the questions here have *exact* answers sitting in the database — "what
did I spend on feed last month" is a `SUM`, not something to be approximated.

The interfaces are the seam if that changes: `ISmartAssistant` could be implemented by an
LLM-backed service without touching a single caller.

---

## Reading the confidence labels

| Score | Label | What it means |
|---|---|---|
| ≥ 0.8 | High | Enough history; act on it |
| ≥ 0.6 | Good | Reasonable; sanity-check it |
| ≥ 0.4 | Moderate | Directionally useful |
| ≥ 0.2 | Low | Thin data; treat as a hint |
| < 0.2 | Indicative only | Record more data first |

The fastest way to raise every confidence score is to **take weight samples regularly**. Growth
feeds the feed recommendation, the harvest estimate and the biomass valuation — one sample a
fortnight improves all three.
