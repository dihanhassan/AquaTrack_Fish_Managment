# 🐟 AquaTrack — Fish Farm & Cash Management System

A web-based system that runs a fish farm end to end: ponds, fish batches, feed, medicine,
equipment, buying and selling, daily cash, reporting, and an AI module that turns the farm's own
history into advice.

Built with **ASP.NET Core 10 MVC + HTMX**, following **Clean Architecture**.

---

## Running it

```bash
dotnet run --project src/AquaTrack.Web
```

Open <https://localhost:7xxx> (the exact port is printed on startup). On first run the app
creates the database, seeds roles, an admin account, reference data, and six months of demo
activity so every screen has something to show.

The seeded sign-in is `admin@aquatrack.local`; its password is in
[`appsettings.json`](src/AquaTrack.Web/appsettings.json) under `AquaTrack:Admin:Password`.
**Change it before this runs anywhere but your own machine.**

To start empty instead, set `AquaTrack:SeedDemoData` to `false`.

### Requirements

- .NET 10 SDK
- No database server needed — SQLite by default. To use SQL Server, set `Database:Provider`
  to `SqlServer` in `appsettings.json`.

---

## Architecture

Dependencies point inwards only. See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for the detail.

```
AquaTrack.Web            MVC controllers, Razor views, HTMX, composition root
  ├── AquaTrack.Application    every use case, the AI module, report definitions
  │     └── AquaTrack.Domain   entities, enums, business rules (no dependencies)
  └── AquaTrack.Infrastructure EF Core, Identity, PDF/Excel rendering
        └── AquaTrack.Application (implements the interfaces it declares)
```

The Application layer never names a `DbContext`, an `HttpContext` or a file path. It declares
`IApplicationDbContext`, `ICurrentUser`, `IDateTimeProvider` and `IReportExporter`, and
Infrastructure supplies them.

---

## Modules

| Module | What it does |
|---|---|
| **Dashboard** | Live KPIs, income vs expense, daily sales, expense split, pond production, open alerts |
| **Daily Input** | One screen per pond per day: feeding, medicine, mortality, weight sample, sale, income, expense — saved as a single transaction |
| **Ponds** | Area, depth, water type, status, with live stock and feed cost per pond |
| **Fish Stock** | Batches plus the four events that change one: growth sample, mortality, harvest, manual adjustment |
| **Species** | The catalogue, carrying the growth constants the AI module reads |
| **Feed** | Items, purchases (raise stock, roll average cost), usage (issue to a pond at cost) |
| **Medicine** | As feed, plus expiry tracking and a block on treating with expired stock |
| **Equipment** | Machines, spare parts and maintenance history |
| **Purchases / Sales** | Multi-line documents that post stock, expense, cash and party balances together |
| **Expenses / Cash** | Categorised costs and the single cash book every module posts through |
| **Reports** | 16 reports, each exportable to PDF, Excel or CSV |
| **AI Insights** | Feed advice, growth forecast, finance forecast, anomaly alerts |
| **Assistant** | Plain-language questions answered from the farm's own data |
| **Users / Settings** | Accounts, roles and farm configuration (Admin only) |

---

## Roles

| | Admin | Manager | Worker |
|---|:---:|:---:|:---:|
| Users, Settings | ✅ | — | — |
| Purchases, Sales, Expenses, Cash, Reports | ✅ | ✅ | — |
| Ponds, Stock, Feed, Medicine, Equipment | ✅ | ✅ | view |
| Daily Input (feeding, mortality, growth) | ✅ | ✅ | ✅ |

---

## How the numbers hold together

**One cash book.** Every money movement in the system is posted through `ICashBookService`.
That is what makes `Opening + In − Out = Closing` true for any date range, and why a sale, a feed
purchase and a wage payment all land in the same ledger.

**Weighted-average stock costing.** A purchase rolls the average cost forward; a usage is costed at
the average in force at that moment and snapshots it, so last month's feed cost does not change
when this month's price does.

**Mortality is not harvest.** A batch tracks deaths and harvested fish as separate counters, so a
fully-sold batch reads as 100% harvested, not 100% lost.

**Nothing is hard-deleted.** Every record carries an audit trail and a soft-delete flag, and a
global query filter hides deleted rows. History and reports survive a delete.

---

## The AI module

Everything is computed from the farm's own data and returns a rationale and a confidence score —
no black boxes, and no external API required.

- **Feed recommendation** — biomass × a percentage-of-body-weight table, blended with the species
  rate, compared against what was actually fed last week.
- **Growth prediction** — least-squares fit over the batch's weight samples, with the fit quality
  feeding the confidence score; falls back to average gain when samples are too few.
- **Expense / profit forecast** — linear trend blended with a moving average, weighted by how well
  the trend actually explains the history.
- **Anomaly detection** — z-scores against the farm's own 30-day baseline for feed cost, medicine
  usage and mortality, plus low stock, expiry and maintenance checks.
- **Smart assistant** — intent matching over farm vocabulary in English and Bangla, each intent
  mapped to a real query.

---

## Project layout

```
src/
  AquaTrack.Domain/          entities, enums, business rules
  AquaTrack.Application/     use cases, AI, report definitions, interfaces
  AquaTrack.Infrastructure/  EF Core, Identity, migrations, seeders, exporters
  AquaTrack.Web/             controllers, views, HTMX conventions, design system
tests/
  AquaTrack.Tests/
docs/
  ARCHITECTURE.md
```

## HTMX conventions

Every list screen follows the same shape, so a new module is mostly a copy of an existing one:

- `Index` renders the page; `List` returns the `_Table` partial.
- Search, sort and paging `hx-get` the table region only — the URL stays shareable.
- Create and edit `hx-get` a `_Form` partial into one shared modal.
- A save returns `204` with an `HX-Trigger` that closes the modal, shows a toast and refreshes the
  table. A validation failure returns `422` with the form re-rendered and the errors in place.
