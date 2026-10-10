# AquaTrack — User Guide

How to run the farm from the screen. Written for the people who will actually use it: the owner,
the manager, and the worker who records what happened today.

---

## Signing in

There is **one sign-in page for everybody** — `/Account/Login`. There is no separate admin URL.
What you see after signing in depends on your role.

The first account is created automatically the first time the app runs. Its email is
`admin@aquatrack.local` and its password is in `src/AquaTrack.Web/appsettings.json` under
`AquaTrack:Admin:Password`.

> **Change that password immediately.** Sign in, open the menu at the top right, and choose
> *Change password*. The seeded password is in a file that lives in your repository.

### What each role can do

| | Admin | Manager | Worker |
|---|:---:|:---:|:---:|
| Users, Settings | ✅ | — | — |
| Purchases, Sales, Expenses, Cash Book, Reports | ✅ | ✅ | — |
| Ponds, Fish Stock, Species, Feed, Medicine, Equipment | ✅ | ✅ | view only |
| Daily Input, water readings, mortality, growth samples | ✅ | ✅ | ✅ |
| Dashboard, AI Insights, Assistant, Alerts | ✅ | ✅ | ✅ |

A **Worker** can record what happened in the ponds but cannot see or change money. That is the
point of the role: the person feeding fish at 6am does not need access to the cash book.

---

## Setting up a new farm

If you start with an empty database (`AquaTrack:SeedDemoData` set to `false`), do this in order.
Each step depends on the one before it.

### 1. Settings → make the app yours

Set the farm name, the currency, and your **opening cash balance** — the money you had in hand
before you started recording anything. Every balance the cash book reports is built on this figure.

Also set the alert thresholds here. The defaults are sensible; tune them once you know your farm.

### 2. Species → what you culture

Add each fish you raise. The three numbers matter more than they look:

- **Target harvest weight** — what you normally sell at.
- **Grow-out period** — how long that usually takes.
- **Daily feed rate** — percent of body weight per day.

Together they are how the system predicts when a batch will be ready and how much to feed it. Ten
common species are seeded for you; edit them to match your own experience.

### 3. Ponds → where the fish live

Area in decimals, average depth, water type. Everything operational hangs off a pond.

### 4. Suppliers and Customers → who you trade with

Not strictly required — a purchase or sale can be recorded without naming anyone — but without
them you cannot track who owes you money or who you owe.

### 5. Feed and Medicine → what you keep in store

Add the items themselves first (name, unit, reorder level). **Do not type in an opening stock
figure** — there is no field for it, by design. Record a purchase instead. That way the stock
level and its cost both come from a real document.

### 6. Equipment → pumps, aerators, nets

Record what you own and what it cost. Maintenance you log later is added to its running cost, so
you can see what a machine has actually cost you over its life.

### 7. Fish Stock → put fish in the water

Now stock a pond: pick the pond and species, enter how many fish, their average weight, and what
they cost each. From this moment the batch is live and everything else starts tracking it.

---

## The daily routine

This is the screen most people will use most days: **Daily Input**.

Pick a pond and a date. Fill in whatever happened. Leave the rest blank. Press **Save the day**
once.

| Section | What it records | What it does behind the scenes |
|---|---|---|
| Feeding | Which feed, how much | Takes it out of store, costs it at the running average, charges it to the pond |
| Medicine | Which treatment, how much, why | Same as feed, and blocks expired stock |
| Mortality | How many died, likely cause | Reduces the batch, values the loss at what those fish cost to stock |
| Growth sample | Average weight of a netted sample | Updates the batch's average weight, which drives feed advice and harvest forecasts |
| Fish sale | Species, quantity, price, customer | Creates an invoice, records the harvest, posts the cash, updates the customer's balance |
| Other income | Anything that is not a fish sale | Posts cash in |
| Expense | Amount, category, description | Posts the cost and the cash out |

Three things worth knowing:

- **Everything saves together or not at all.** If the feed quantity is more than you have in
  store, nothing is written — you get told what went wrong and can fix it.
- **Saving twice adds to the day; it does not replace it.** There is one log per pond per day,
  and a second save tops it up. The panel at the top shows what is already recorded.
- **The feed advice beside the Feeding section is live.** It is today's recommendation for that
  pond, computed from the current biomass.

---

## Recording money

### Sales

**Sales → New sale.** One invoice can have several lines. On each line you pick the species and,
optionally, the pond and batch it came from.

Naming the batch is worth the extra click: it records the harvest, reduces the batch, and lets
production reporting work. Without it the sale still posts correctly, it just is not traceable to
a pond.

Enter less in **Paid now** than the total and the difference becomes a customer due. Collect it
later from the list with the cash icon.

### Purchases

**Purchases → New purchase.** One document can mix feed, medicine, equipment and other costs.

The **Type** on each line decides what happens:

- *Feed* or *Medicine* — must name an item; its stock goes up and its average cost is recalculated.
- *Equipment* / *Equipment part* — can name a machine the part belongs to.
- *Fish fry* / *Other* — recorded as a cost only.

Again, paying less than the total creates a supplier due.

### Expenses

For costs with no purchase document: wages, electricity, transport, repairs.

Expenses marked **auto** came from another document (a purchase, a feed usage, a maintenance
record). They are read-only here — to change one, change the document that created it. This is
deliberate: it stops the expense list and the document it came from drifting apart.

### Cash Book

Every money movement in the system lands here, in date order, with a running balance. Sales,
purchases, expenses and daily input all post into it automatically.

Add an entry manually only for things no document covers: an owner withdrawal, a loan, a
correction.

The figure at the bottom is the arithmetic the page is claiming, spelled out:
`Opening + In − Out = Closing`. If it does not add up, something is wrong and you can see where.

---

## Reports

Sixteen reports, grouped by subject. Pick a date range at the top; it follows you into whichever
report you open.

Every report exports to **PDF**, **Excel** or **CSV**. What you see on screen and what downloads
are built from the same data, so they cannot disagree.

| Group | Reports |
|---|---|
| Fish | Pond-wise stock, growth, mortality, production |
| Feed | Purchases, usage, current stock |
| Medicine | Stock and expiry, usage |
| Sales | All sales, by customer, by species |
| Finance | Purchases, expenses, profit and loss, cash flow |

---

## AI Insights

Five features, all computed from your own recorded history. Nothing is sent anywhere, and every
figure shows the reasoning that produced it alongside a confidence rating.

**Feed recommendation** — how much to feed each batch today, from its live biomass and a
percentage-of-body-weight table. It also shows what you actually fed over the last week, so you
can see whether you are over or under.

**Growth prediction** — a line fitted through the batch's own weight samples, projected forward.
More samples means higher confidence. With no samples it says so rather than guessing.

**Expense and profit forecast** — next month's income and expense, from your completed months.
The trend sets the direction; it is held within a sensible distance of what you have actually been
doing, so a few bad months cannot project you into absurdity.

**Anomaly detection** — compares the last week against your own 30-day baseline. It flags feed
cost spikes, unusual medicine use, high mortality, low stock, expiring medicine and maintenance
that is due. These are what fill the bell in the top bar.

**Assistant** — ask a question in plain English or Bangla:

> *Which category cost the most this month?*
> *কোন পুকুরে production বেশি?*
> *What is today's profit?*

Each question is matched to a real query. If it cannot match one, it says so rather than inventing
an answer.

---

## Alerts

The bell in the top bar. It checks itself every ninety seconds and whenever you open the
dashboard.

Alerts are raised by the anomaly detector, not written by hand. The same alert will not be raised
twice while it is still unread, so a low-stock item does not fill the list.

---

## Things the system will not let you do

These are guard rails, not bugs. Each one exists because the alternative produces figures that are
quietly wrong.

| You try to | What happens | Why |
|---|---|---|
| Feed more than you have in store | Rejected, nothing saved | Stock would go negative |
| Sell more than a batch holds | Rejected | The batch count would go negative |
| Treat fish with expired medicine | Rejected | Safety, and the stock should be written off |
| Delete a pond with fish in it | Rejected | The stock figures would stop adding up |
| Delete a customer who owes money | Rejected | The debt would vanish |
| Edit an auto-generated expense | Read-only | It would disagree with its source document |
| Deactivate or delete your own admin account | Rejected | You would lock yourself out |
| Remove the last administrator | Rejected | Nobody could administer the system |
| Record anything dated in the future | Rejected | Reports would include things that have not happened |

Nothing is ever truly deleted. Every record is marked deleted and hidden, so history and past
reports stay intact.

---

## If something goes wrong

**A form will not save.** A message appears at the top right telling you what is wrong, and the
offending fields are outlined in red. Nothing was saved, so nothing is half-done.

**A number looks wrong on the dashboard.** Open the Cash Book for the same period. The running
balance shows every movement in order, and the equation at the bottom shows the arithmetic.

**Stock is wrong.** Open the batch. Its growth, mortality, harvest and adjustment history are all
listed. The current figure is recomputed from those records — so if one of them is wrong, correct
that record and the total follows.

**A physical count disagrees with the system.** Open the batch and use *Adjust*. The correction is
kept as its own record with a reason, rather than quietly overwriting the number.
