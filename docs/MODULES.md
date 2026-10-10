# AquaTrack — Module Reference

What each screen does, what it writes, and the rules it enforces. Read alongside
[ARCHITECTURE.md](ARCHITECTURE.md) for how the layers fit together.

---

## Dashboard

The landing page after sign-in. Answers "how is the farm doing right now" in one screen.

**Cards:** total fish stock and biomass · feed stock and value · medicine stock value with expiry
count · total production · today's income, expense and profit · cash in hand with receivables and
payables · low-stock count.

**Charts:** income vs expense over six months · expense split for the current month · daily sales
over fourteen days · feed usage by pond · production by pond.

**Alerts panel:** the most recent open alerts from the anomaly detector.

The KPI row re-fetches itself every two minutes, so a dashboard left open stays current without
rebuilding the charts underneath it. Opening the dashboard also re-runs the detectors, which is
what keeps alerts fresh without a background scheduler.

---

## Daily Input

The screen a worker uses every day, and the fastest way to get data in.

Pick a **pond** and a **date**, then fill in only what happened:

| Section | Writes |
|---|---|
| Feeding | `FeedUsage` per line, issuing stock at the running average cost |
| Medicine | `MedicineUsage` per line, same costing |
| Mortality | `FishMortality`, valued at the batch's stocking cost |
| Growth sample | `FishGrowth`, which updates the batch's average weight |
| Fish sale | A full `Sale` through the sale service, including harvest write-back |
| Other income | `OtherIncome` plus a cash-in entry |
| Expense | `Expense` plus a cash-out entry |

All of it saves as **one transaction** — either the whole day is recorded or none of it is.

**Rules**

- One log per pond per day. Saving again adds to that day rather than duplicating it, and the
  "already recorded today" strip shows what is on record.
- Feeding more than is in store is rejected before anything is written.
- Future dates are rejected.
- A pond with exactly one active batch selects it automatically; with several, the user chooses.
- The feed advisor's recommendation for that pond is shown beside the feeding section.

---

## Ponds

The culture units everything else hangs off.

Area in **decimals** (the unit Bangladeshi farms actually use), depth, water type, status and
location. The list shows live fish, biomass and stocking density per pond; the detail page adds
this month's feed and medicine cost, the batches in the water, and recent water readings.

**Rules** — a pond holding active stock cannot be deleted. Pond codes are optional but unique.

---

## Fish Stock

A **batch** is one lot of fish stocked into one pond, and is the unit everything operational
attaches to.

Four events change a batch, each recorded separately and each re-deriving the batch totals rather
than editing them:

| Event | Effect |
|---|---|
| **Growth sample** | Sets the current average weight; feeds the growth chart and the AI forecast |
| **Mortality** | Reduces live quantity, increases the mortality counter, records the value lost |
| **Harvest** | Reduces live quantity, increases the harvested counter |
| **Adjustment** | Signed correction after a physical count, transfer or data fix |

**Mortality and harvest are counted separately.** A batch that has been fully sold reads as 100%
harvested, not 100% lost — the difference matters for both reporting and the anomaly detector.

**Rules** — recording more deaths or harvest than the batch holds is rejected. A batch that has
been sold from cannot be deleted, only marked harvested.

---

## Species

The catalogue, and the place the AI module gets its constants:

- **Target harvest weight** and **grow-out days** → the harvest-date estimate
- **Daily feed rate** (% of body weight) → blended with the standard feeding table

Ten Bangladeshi polyculture species are seeded on first run with realistic figures.

---

## Feed

A feed SKU with two movements:

- **Purchase** — raises stock, rolls the weighted-average cost forward, posts a Feed expense, pays
  it from the cash book, and adds any unpaid balance to the supplier's payable.
- **Usage** — issues stock to a pond at the average cost *in force at that moment*, and snapshots
  that cost on the row.

That snapshot is why last month's feed cost does not move when this month's price changes.

**Rules** — issuing more than is in store is rejected. An item with stock remaining cannot be
deleted.

---

## Medicine

The same shape as Feed, with expiry on top.

The list filters to **low stock**, **expiring within 30 days**, or **expired**. A newer purchase
batch advances the expiry date the alerts watch. Treating fish with expired stock is blocked at
the form.

---

## Equipment

Machines, their spare parts, and their service history.

Recording maintenance posts the repair cost as a Maintenance expense and pays it from the cash
book, so upkeep appears in the accounts instead of vanishing. It can also update the machine's
condition in the same step.

The detail page shows **total cost of ownership** — purchase price plus every repair since — which
is the number that tells you when to replace rather than repair.

---

## Suppliers and Customers

Parties with running balances:

- **Supplier → payable**: increased by the unpaid part of a purchase, reduced by payments.
- **Customer → receivable**: increased by the unpaid part of a sale, reduced by collections.

Customers can be filtered to **only those with dues**, which is the list you work from when
chasing payment. Neither can be deleted while a balance is outstanding.

---

## Purchases

A purchase document can mix feed, medicine, equipment, fish fry and other costs on one invoice.
Each line's **type** decides what it touches:

| Line type | Effect |
|---|---|
| Feed | Raises feed stock, rolls average cost |
| Medicine | Raises medicine stock, rolls average cost, may advance expiry |
| Equipment part | Adds to or tops up that machine's spare part |
| Equipment, Fish fry, Other | Cost only; the item is created from its own screen |

The document posts an expense categorised by whichever line type accounts for most of its value,
pays the paid portion from the cash book, and books the rest as a supplier payable. Deleting one
reverses all of it.

---

## Sales

Multi-line fish invoices. A line that names a **batch** also writes a harvest against it, so
selling fish reduces the pond's stock in the same action.

Choosing a pond on a line narrows the batch dropdown without reloading the form. Selling more than
a batch holds is rejected before posting.

The invoice posts the collected amount to the cash book and books the rest as a customer
receivable, settled later from the list.

---

## Expenses

Costs by category, with a **breakdown bar chart** that answers the spec's three questions directly:
today's spend, this month's spend, and which category is largest.

Expenses raised automatically by a purchase, a usage or maintenance are marked `auto` and are
**read-only here** — editing them would put this list and the source document out of step. Edit the
document instead.

**Other income** (pond lease, scrap, subsidy) is recorded from the same screen and posts a cash-in
entry.

**Categories** are managed on their own page. The ten seeded categories are referenced by the
automatic posting rules, so they can be renamed or deactivated but not deleted.

---

## Cash Book

Every money movement in the system, in order, with a running balance.

The page states its own arithmetic — `Opening + In − Out = Closing` — so a reader can check it adds
up rather than taking the closing figure on trust.

Entries posted by another document are marked and cannot be deleted here; delete the document and
the entry goes with it. Manual entries (owner withdrawal, loan, correction) are added directly.

---

## Reports

Sixteen reports in five groups, each exportable to **PDF**, **Excel** or **CSV**.

| Group | Reports |
|---|---|
| Fish | Pond-wise stock · Growth · Mortality · Production |
| Feed | Purchase · Usage · Stock |
| Medicine | Stock and expiry · Usage |
| Sales | Sales · Customer-wise · Fish-wise |
| Finance | Purchases · Expenses · Profit and loss · Cash flow |

The date range picked on the catalogue page follows you into whichever report you open. The grid on
screen and the exported file are rendered from the same `ReportTable`, so they cannot drift apart.

---

## AI Insights

See [AI.md](AI.md) for how each model works. The screen shows all four, each with its reasoning and
a confidence rating:

- **AI-1 Feed recommendation** — per batch, compared against what was actually fed
- **AI-2 Growth prediction** — weight in 30 days, and when each batch reaches its target
- **AI-3 Expense and profit forecast** — next month, charted against the history it learned from
- **AI-4 Anomaly detection** — what is currently out of line with the farm's own normal

---

## Assistant

**AI-5.** Plain-language questions answered from stored data, in English or Bangla.

Each question is matched to an intent that maps to a real query. Answers come back with a figure, a
sentence, and usually a chart and table. Nothing is generated — if no intent matches, the assistant
says so rather than inventing an answer.

---

## Alerts

The anomaly detector's output, stored so it can be reviewed and cleared.

The topbar bell re-renders on a ninety-second poll. Alerts are deduplicated: an alert that is
already open and unread is not raised again.

---

## Users (Admin)

Accounts and roles. Two guards against locking everyone out:

- You cannot deactivate or delete **your own** account.
- You cannot remove the **last administrator**.

Deactivating blocks sign-in without deleting anything, which is the right action when someone
leaves. Records they entered stay attributed to their name.

---

## Settings (Admin)

Farm name, owner, address and phone · currency code and symbol · opening cash balance · and the
four thresholds the anomaly detector reads:

| Setting | Effect |
|---|---|
| Low stock warning days | How far ahead to warn |
| Medicine expiry warning days | How long before expiry to flag |
| High mortality threshold (%) | Weekly deaths above this share of a batch raise an alert |
| Cost spike sensitivity (sigma) | Standard deviations above baseline before a cost is unusual |

Settings are cached and the cache is invalidated on write, because they are read on nearly every
request.
