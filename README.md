# 🐟 AquaTrack — Fish Farm & Cash Management System

AquaTrack runs a fish farm end to end: ponds, fish batches, feed, medicine, equipment, buying
and selling, daily cash, reports, and an AI module that turns the farm's own history into advice.

Built with **ASP.NET Core 10 MVC + HTMX**, following **Clean Architecture**.

This guide assumes you've never run a .NET project before. Follow it top to bottom and you'll
have the app open in your browser in a few minutes.

---

## 1. Install what you need (one-time setup)

You only need to do this once per computer.

1. **.NET 10 SDK** — this is what builds and runs the project.
   Download it from <https://dotnet.microsoft.com/download/dotnet/10.0> and install it
   (pick the **SDK**, not just the Runtime).
   To check it worked, open a terminal (PowerShell on Windows, Terminal on Mac/Linux) and run:
   ```bash
   dotnet --version
   ```
   It should print something starting with `10.`.

2. **Git** — this is what downloads ("clones") the project's code.
   Download it from <https://git-scm.com/downloads> if you don't already have it.
   Check it worked with:
   ```bash
   git --version
   ```

3. **A code editor** (optional, but recommended) — any of these work fine:
   - [Visual Studio Code](https://code.visualstudio.com/) (free, lightweight, works on any OS) —
     after installing it, also add the **C# Dev Kit** extension from inside VS Code.
   - [Visual Studio Community](https://visualstudio.microsoft.com/vs/community/) (Windows only,
     free, more full-featured for .NET).

   You don't strictly need an editor to *run* the app — the terminal steps below are enough —
   but you'll want one to look at or change any code.

---

## 2. Get the project onto your computer

Open a terminal, go to the folder where you keep your projects, and run:

```bash
git clone https://github.com/dihanhassan/AquaTracl.git
cd AquaTracl
```

(If you already have the folder — for example it was shared with you directly — just open a
terminal **inside** that folder instead of cloning.)

---

## 3. Run it

From the project's root folder (the one with `AquaTrack.slnx` in it), run:

```bash
dotnet restore
dotnet run --project src/AquaTrack.Web
```

The first run takes a little longer — it's downloading packages and setting up the database.
Once it's ready, the terminal prints a line like:

```
Now listening on: https://localhost:5001
```

Open that address in your browser (the port number can differ on your machine — always use
whatever the terminal printed). If your browser warns about an untrusted certificate, that's
expected for local development; either click through the warning, or run this once beforehand
to make your computer trust it:

```bash
dotnet dev-certs https --trust
```

On first run, the app automatically creates its database and fills it with six months of demo
data (ponds, fish batches, sales, etc.), so every screen already has something to show.

### Log in

| | |
|---|---|
| **Email** | `admin@aquatrack.local` |
| **Password** | `Admin#12345` |

These live in [`src/AquaTrack.Web/appsettings.json`](src/AquaTrack.Web/appsettings.json) under
`AquaTrack:Admin`. **Change the password before running this anywhere other than your own
computer.**

Want to start with an empty database instead of the demo data? Open that same
`appsettings.json` and set `"SeedDemoData": false` before the first run.

To stop the app, go back to the terminal and press `Ctrl+C`.

---

## 4. Using an editor instead of the terminal (optional)

If you installed Visual Studio or VS Code, you can open the project there instead of typing
`dotnet run` every time:

- **Visual Studio**: double-click `AquaTrack.slnx`, wait for it to load, then press `F5` (or
  click the green ▶ "Run" button at the top).
- **VS Code**: open the project's root folder (`File → Open Folder`), then open a terminal
  inside VS Code (`` Ctrl+` ``) and run the same `dotnet run --project src/AquaTrack.Web`
  command as above.

---

## 5. If something doesn't work

- **"dotnet: command not found"** — the SDK isn't installed, or your terminal was open before
  you installed it. Close and reopen the terminal and try again.
- **"The current .NET SDK does not support targeting .NET 10.0"** — you installed an older SDK
  by mistake. Check `dotnet --version` is `10.x` and reinstall if not.
- **Port already in use** — another program is using that port. Stop it, or edit the port in
  `src/AquaTrack.Web/Properties/launchSettings.json`.
- **Browser shows a certificate warning** — run `dotnet dev-certs https --trust` (see above),
  then restart the app.
- **Still stuck** — delete the `aquatrack.db` file (it's just the local demo database, created
  automatically — deleting it is safe and just resets your data) and run `dotnet run` again.

---

## Requirements recap

- .NET 10 SDK
- No separate database server needed — it uses SQLite out of the box, stored as a single
  `aquatrack.db` file next to the app. To use SQL Server instead, set `Database:Provider` to
  `SqlServer` in `appsettings.json`.

---

## What's inside

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

### Roles

| | Admin | Manager | Worker |
|---|:---:|:---:|:---:|
| Users, Settings | ✅ | — | — |
| Purchases, Sales, Expenses, Cash, Reports | ✅ | ✅ | — |
| Ponds, Stock, Feed, Medicine, Equipment | ✅ | ✅ | view |
| Daily Input (feeding, mortality, growth) | ✅ | ✅ | ✅ |

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

### Project layout

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

### HTMX conventions

Every list screen follows the same shape, so a new module is mostly a copy of an existing one:

- `Index` renders the page; `List` returns the `_Table` partial.
- Search, sort and paging `hx-get` the table region only — the URL stays shareable.
- Create and edit `hx-get` a `_Form` partial into one shared modal.
- A save returns `204` with an `HX-Trigger` that closes the modal, shows a toast and refreshes the
  table. A validation failure returns `422` with the form re-rendered and the errors in place.

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
