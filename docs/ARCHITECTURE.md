# AquaTrack — Architecture

AquaTrack follows Clean Architecture. Dependencies point **inwards only**: an inner layer never
references an outer one, and crossing a boundary outwards always goes through an interface the
inner layer owns.

```
                 ┌─────────────────────────────────────────────┐
                 │              AquaTrack.Web                  │   MVC + HTMX, composition root
                 │  Controllers · Views · ViewModels · Auth     │
                 └───────────────┬─────────────────┬───────────┘
                                 │                 │
                                 ▼                 ▼
          ┌──────────────────────────────┐   ┌──────────────────────────────┐
          │   AquaTrack.Application      │◀──│  AquaTrack.Infrastructure    │
          │  Use cases · AI · Reports    │   │  EF Core · Identity · PDF    │
          │  Interfaces it requires      │   │  Implements those interfaces │
          └──────────────┬───────────────┘   └──────────────┬───────────────┘
                         │                                  │
                         ▼                                  ▼
                 ┌─────────────────────────────────────────────┐
                 │             AquaTrack.Domain                │   Entities · Enums · Rules
                 │          (no outward dependencies)          │
                 └─────────────────────────────────────────────┘
```

## The four projects

### `AquaTrack.Domain`

The business vocabulary, and nothing else. 31 entities (`Pond`, `FishStock`, `FeedItem`,
`CashTransaction`, …), the enums they use, and `BaseEntity`, which gives every record an identity,
an audit trail and a soft-delete flag.

Entities carry the calculations that belong to them rather than exposing raw fields for someone
else to combine — `FishStock.BiomassKg`, `Medicine.IsExpiringSoon`, `Sale.DueAmount`. That keeps
one definition of each concept instead of the same arithmetic repeated across screens.

**References: none.** Not EF Core, not ASP.NET Core. The domain compiles on its own.

### `AquaTrack.Application`

Every business rule and use case in the system:

| Area | What lives here |
|---|---|
| `Services/` | Cash book, stock costing, purchases, sales, expenses, daily input, dashboard, settings, notifications |
| `Ai/` | Feed advisor, growth predictor, finance predictor, anomaly detector, smart assistant |
| `Reporting/` | The 16 report definitions — what each report contains |
| `Common/Interfaces/` | What this layer needs from the outside world |

It declares what it needs and lets someone else supply it:

- **`IApplicationDbContext`** — the DbSets and `SaveChangesAsync`, plus `BeginTransactionAsync`
  returning an `IDatabaseTransaction`. The application layer can run a multi-aggregate write as one
  unit without ever naming an EF Core transaction type.
- **`ICurrentUser`** — who is making the request. Implemented in the web layer, the only place that
  knows about HTTP.
- **`IDateTimeProvider`** — the clock, so date-sensitive logic can be run at a chosen point in time.
- **`IReportExporter`** — turns a finished report into PDF or Excel bytes.

**References: Domain only** (plus EF Core for `DbSet<T>` and the async LINQ operators). Keeping
`DbSet<T>` in the interface is a deliberate trade: it avoids a repository wrapper around every one
of 31 entities while still keeping the provider, the connection string and migrations out of this
layer entirely.

### `AquaTrack.Infrastructure`

The outside world, and only the outside world:

- `Persistence/` — `AquaTrackDbContext` (which *implements* `IApplicationDbContext`), the entity
  configurations, migrations, and the seeders.
- `Identity/` — `ApplicationUser`, `ApplicationRole`, the three role names.
- `Reporting/ReportExporter.cs` — QuestPDF and ClosedXML rendering.

**References: Application + Domain.** The arrow points inwards, which is what lets the database be
swapped (SQLite ↔ SQL Server, by configuration) without a use case changing.

### `AquaTrack.Web`

ASP.NET Core MVC with HTMX. Controllers are thin: they bind input, call one application service,
and choose a view. The composition root in `Program.cs` is the only place that knows all the layers
exist.

**References: Application + Infrastructure.**

## Conventions worth knowing

**The DbContext is the only thing that writes audit data.** `SaveChanges` stamps
`CreatedBy`/`UpdatedBy` from `ICurrentUser` and converts a delete into a soft delete. Services never
think about it, and a global query filter hides deleted rows from every query.

**Money moves through exactly one door.** Every cash movement in the system is posted by
`ICashBookService`, which is what makes `Opening + In − Out = Closing` hold for any date range.
Purchases, sales, expenses and the daily input screen all post through it rather than writing
`CashTransaction` rows themselves.

**Stock is costed at weighted average.** `StockService.ReceiveFeedAsync` rolls the average forward
on each purchase; `IssueFeedAsync` returns the cost at the moment of issue, which is then snapshotted
on the usage row so historical costs do not shift when prices change later.

**The AI module is explainable.** Every prediction returns a `Rationale` string and a confidence
score alongside the number. Nothing is a black box — a farmer can read why the system suggested
25 kg of feed.

## Adding a feature

1. Does it need a new field or concept? → `Domain`.
2. Write the rule as a service in `Application`, depending only on interfaces.
3. Need something from outside (a new store, an export format)? → declare the interface in
   `Application/Common/Interfaces`, implement it in `Infrastructure`.
4. Add the controller and views in `Web`.
5. Register it in the matching `DependencyInjection.cs`.

If step 2 makes you reach for `AquaTrackDbContext`, `HttpContext` or a file path, the rule belongs
behind an interface instead.
