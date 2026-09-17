# AGENTS.md

## Project

This is a cTrader cBot project named `Intradaylimitalpha`.

The strategy runs two independent take-profit grids on the same symbol, a long group and a short
group, each with its own parameters and its own order label (`{OrderLabel}_Long` /
`{OrderLabel}_Short`). Either group can be disabled. There is no entry signal and no stop-loss
calculation. Running both at once needs a hedging account.

Each grid works as follows:

* Entry: every order is a pending order (there is no market entry), spaced from the anchor
  price (or the current price when the anchor is 0).
* Direction: `Long` grids step downward, `Short` grids step upward. A pending order on the
  favourable side of the market is a limit order, otherwise a stop order.
* Every order uses the same lot size and the same take-profit distance (pips).
* Open positions + pending orders are kept at `N`. When a grid position takes profit, a new
  pending order is placed one spacing beyond the farthest grid order.
* Manual closes and stop-outs do not trigger a refill.
* On restart, existing orders with the same label are continued instead of building a new grid.
* Each fill and close is written to a CSV file.

## Parameters

Each group has the same set, prefixed `Long` (多单组) or `Short` (空单组):

| Parameter | Meaning | Min |
| --- | --- | --- |
| `IsLongGridEnabled` / `IsShortGridEnabled` | Turn the group on or off | |
| `…AnchorPrice` | Grid start price, 0 = current price | 0 |
| `…SpacingPips` | Distance between neighbouring grid orders | 100 |
| `…LotsPerOrder` | Lot size of every order | 0.01 |
| `…MaxOrders` | N, this group's positions + pending orders | 1 |
| `…TakeProfitPips` | Distance from entry to take profit | 10 |

## Important user preferences

* Write code comments in English.
* Keep the main Robot class clean.
* Put separate responsibilities into separate classes.
* Only expose real strategy parameters.
* Prefer simple, maintainable C# over over-engineered abstractions.
* Do not mix price geometry, order execution, and CSV writing in the same class.

## Architecture

* `Intradaylimitalpha.cs`: cBot lifecycle and composition root. Reads parameters, resolves the
  order volume, creates the services, starts the grid.
* `Orders/GridPlanner.cs`: pure grid price geometry, unit tested in `tests/Grid.Tests`.
* `Orders/GridOrderExecutor.cs`: places limit/stop orders, keeps the grid at `N`,
  refills on take profit, writes CSV records.
* `OrderLogger/TradeCsvLogger.cs`, `OrderLogger/TradeCsvMigrator.cs`: CSV output and header upgrades.
* `Models/`: plain data types and enums.

## Development workflow

```bash
./scripts/test.sh    # build the cBot and run the unit tests
```

Then load the `.algo` in cTrader and validate behaviour in the backtester. Use the Logs tab to
check order placement and refills.

## Coding style

This project uses Java-style C# formatting. Put opening braces on the same line as the
declaration or control statement. Do not reformat touched code back to Allman-style braces.

```csharp
if (position == null)
    return;
```

Keep methods small, but do not over-abstract.

## Notes about cTrader behavior

### Parameters

`[Parameter(DefaultValue = ...)]` only affects new cBot instances. Existing instances keep their
own saved parameter values; delete and recreate the instance to see a new default.

### Multiple instances

Each running instance has its own state and its own `OnStart()`. Two instances with the same
label on the same symbol would manage the same grid orders, so give each instance its own label.

### File access

Writing the CSV requires `AccessRights.FullAccess`.
