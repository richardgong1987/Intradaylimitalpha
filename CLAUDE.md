# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

A **cTrader cBot** (automated trading robot) written in C# against the cAlgo API, targeting
`net6.0`. The strategy runs **two independent take-profit grids** on one symbol — a long group
and a short group, each with its own parameters and label (`{OrderLabel}_Long` /
`{OrderLabel}_Short`). A group runs only when its anchor price is above 0. Starting with a group's anchor price at 0
cancels that group's leftover pending orders but leaves its filled positions open. There is no
entry signal and no stop loss.
Each grid (one `GridOrderExecutor` per enabled group) works like this:

- Entry: every order is a pending order (there is no market entry), spaced from the anchor
  price. Long grids step downward, short grids step upward. Distances are in pips.
- Every order has the same lot size and the same take-profit distance.
- Open positions + pending orders are kept at `N`. When a grid position takes profit, a new
  pending order is added one spacing **beyond the farthest order** (the grid extends; it
  does not refill the gap). Manual closes and stop-outs do not refill.
- On restart, existing orders with the same label are continued (topped up to `N`) instead
  of building a second grid.

`Intradaylimitalpha.cs` is the Robot lifecycle shell that wires the pieces together (the
composition root).

## Module map

Behavior classes live beside the feature they serve; all data types live in `Models/`
(suffixed `Model`):

- `Orders/` — `GridPlanner` (pure price geometry: initial levels, refill price, limit vs
  stop; unit tested); `GridOrderExecutor` places orders, counts live grid orders, refills on
  take profit, and writes the CSV.
- `OrderLogger/` — `TradeCsvLogger` (append-only trade CSV) and `TradeCsvMigrator` (upgrades
  old CSV headers).
- `Models/` — data types: `GridSettingsModel`, `TradeDirectionModel`,
  `PendingOrderTypeModel`, `TradeCsvRecordModel`.

Rule of thumb: classes with no `using cAlgo.API` are pure and testable; keep them that way.

## Build & run

```bash
# Build (from repo root)
dotnet build "Intradaylimitalpha.sln"          # Debug
dotnet build "Intradaylimitalpha.sln" -c Release
```

A successful build produces a `.algo` package under
`Intradaylimitalpha/bin/<Config>/net6.0/`. The `.algo` file is the deployable
cBot artifact loaded by the cTrader desktop platform.

The cBot itself is validated by running it in cTrader's backtester/optimizer, not via a CLI
runner. Iteration loop: edit `.cs` → `dotnet build` → load/refresh the `.algo` in cTrader →
backtest.

Pure (framework-independent) helpers are unit-tested with xUnit under `tests/`:

```bash
./scripts/test.sh                                       # build cBot + run all tests
dotnet test "tests/Grid.Tests/Grid.Tests.csproj"          # tests only
```

The test project is intentionally **not** part of the `.sln` (which cTrader builds) and
targets `net10.0` rather than the cBot's `net6.0` — it links pure source files directly (via
`<Compile Include>`) instead of referencing the cBot project, so tests never pull in the
`cTrader.Automate` / cAlgo.API dependency. Keep new domain/risk logic pure so it can be
tested this way.

The `cTrader.Automate` NuGet package (versioned `*`) supplies the `cAlgo.API.*` assemblies;
restore happens automatically on build.

## Code structure

A cBot is a single class deriving from `cAlgo.API.Robot` in namespace `cAlgo.Robots`,
annotated with `[Robot(...)]`. The framework drives it through lifecycle overrides — there is
no `Main`:

- `OnStart()` — one-time setup (read parameters, attach indicators).
- `OnTick()` — runs on every price update; intraday/entry logic lives here.
- `OnBar()` — runs on each completed bar (override when the strategy is bar-based, e.g.
  computing the prior session's high/low).
- `OnStop()` — teardown.

User-tunable inputs are `public` properties decorated with `[Parameter(...)]`; these surface
in the cTrader UI and the optimizer. Trading actions and market data come from inherited
members (`ExecuteMarketOrder`, `Positions`, `Symbol`, `Bars`, `MarketSeries`, `Print`, etc.).

`[Robot(AccessRights = AccessRights.None)]` means the bot cannot touch the file system or
network — keep it that way unless a feature genuinely requires elevated access.

## Conventions

- Spaces in the project/solution/file names are intentional (cTrader convention) — always
  quote paths in shell commands.
- `bin/`, `obj/`, `.idea/`, `*.user`, and generated `*.algo` files are git-ignored; commit
  only the `.cs`, `.csproj`, and `.sln`.
