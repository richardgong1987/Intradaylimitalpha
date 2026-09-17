using System;
using System.Collections.Generic;
using System.Linq;
using cAlgo.API;

namespace cAlgo.Robots;

// Places and maintains the grid's cTrader orders. It builds the initial grid, and whenever a
// grid position takes profit it adds pending orders beyond the farthest order until open
// positions plus pending orders are back at N. It also writes each fill and close to the CSV.
// All price geometry lives in GridPlanner.
public class GridOrderExecutor {
    private readonly Robot _robot;
    private readonly GridSettingsModel _settings;
    private readonly GridPlanner _planner;
    private readonly TradeCsvLogger _csvLogger;

    private readonly Dictionary<int, double> _positionEntryEquities = new();

    public GridOrderExecutor(Robot robot, GridSettingsModel settings, GridPlanner planner, TradeCsvLogger csvLogger) {
        _robot = robot;
        _settings = settings;
        _planner = planner;
        _csvLogger = csvLogger;

        _robot.Positions.Closed += OnPositionClosed;
        _robot.PendingOrders.Filled += OnPendingOrderFilled;
    }

    public void Stop() {
        _robot.Positions.Closed -= OnPositionClosed;
        _robot.PendingOrders.Filled -= OnPendingOrderFilled;
    }

    public void Start() {
        _robot.Print("*****Grid started | Label: {0}, Direction: {1}, AnchorPrice: {2}, SpacingPips: {3}, VolumeInUnits: {4}, N: {5}, TakeProfitPips: {6}",
            _settings.Label, _settings.Direction, _settings.AnchorPrice, _settings.SpacingPips,
            _settings.VolumeInUnits, _settings.MaxOrders, _settings.TakeProfitPips);

        // Orders from an earlier run are still live after a restart; building a second grid on top
        // of them would double the exposure, so continue that grid instead.
        int existingOrders = CountGridOrders();

        if (existingOrders > 0) {
            _robot.Print("*****Existing grid orders found | Label: {0}, Count: {1}. Continuing that grid instead of building a new one.",
                _settings.Label, existingOrders);
            TopUp(GridPrices());
            return;
        }

        PlacePendingOrders(_planner.InitialPendingPrices(_settings.AnchorPrice, _settings.MaxOrders));
    }

    private void PlacePendingOrders(IEnumerable<double> prices) {
        foreach (double price in prices) {
            if (!PlacePendingOrder(price))
                return;
        }
    }

    // Keeps extending the grid until positions plus pending orders reach N. extendFrom holds the
    // prices the new orders are measured from; each placed order joins it, so the next one goes
    // one spacing further.
    private void TopUp(List<double> extendFrom) {
        while (CountGridOrders() < _settings.MaxOrders && extendFrom.Count > 0) {
            double price = _planner.NextPendingPrice(extendFrom);

            if (!PlacePendingOrder(price))
                return;

            extendFrom.Add(price);
        }
    }

    private bool PlacePendingOrder(double price) {
        double targetPrice = Math.Round(price, _robot.Symbol.Digits);

        if (targetPrice <= 0.0) {
            _robot.Print("*****Pending order skipped | Grid: {0}, Target price {1} is not positive. Reduce N or the spacing.",
                _settings.Label, targetPrice);
            return false;
        }

        PendingOrderTypeModel orderType = _planner.ChooseOrderType(targetPrice, CurrentPrice);
        TradeResult result = orderType == PendingOrderTypeModel.Limit
            ? _robot.PlaceLimitOrder(ToTradeType(), _robot.SymbolName, _settings.VolumeInUnits, targetPrice, _settings.Label, null,
                _settings.TakeProfitPips, ProtectionType.Relative)
            : _robot.PlaceStopOrder(ToTradeType(), _robot.SymbolName, _settings.VolumeInUnits, targetPrice, _settings.Label, null,
                _settings.TakeProfitPips, ProtectionType.Relative);

        if (!result.IsSuccessful) {
            _robot.Print("*****Pending order failed | Grid: {0}, Type: {1}, Price: {2}, Error: {3}", _settings.Label, orderType, targetPrice,
                result.Error);
            return false;
        }

        _robot.Print("*****Pending order placed | Grid: {0}, Type: {1}, Price: {2}, GridOrders: {3}/{4}", _settings.Label, orderType,
            targetPrice, CountGridOrders(), _settings.MaxOrders);
        return true;
    }

    private void OnPendingOrderFilled(PendingOrderFilledEventArgs args) {
        if (args?.Position == null || !IsGridOrder(args.PendingOrder.SymbolName, args.PendingOrder.Label))
            return;

        RecordEntry(args.Position, args.PendingOrder.OrderType.ToString());
    }

    private void OnPositionClosed(PositionClosedEventArgs args) {
        if (args?.Position == null || !IsGridOrder(args.Position.SymbolName, args.Position.Label))
            return;

        RecordClose(args);

        // Only a take profit refills the grid. A manual close or a stop-out means the trader or the
        // broker is shrinking the grid, and refilling would fight that.
        if (args.Reason != PositionCloseReason.TakeProfit)
            return;

        // The closed order still counts as part of the grid's reach, so the refill lands beyond it
        // when it was the farthest order.
        List<double> extendFrom = GridPrices();
        extendFrom.Add(args.Position.EntryPrice);
        TopUp(extendFrom);
    }

    private void RecordEntry(Position position, string orderKind) {
        double entryEquity = _robot.Account.Equity;
        _positionEntryEquities[position.Id] = entryEquity;
        _csvLogger.AppendEntry(position, _settings.GroupName, orderKind, entryEquity, _robot.SymbolName,
            _robot.TimeFrame.ToString());
    }

    private void RecordClose(PositionClosedEventArgs args) {
        Position position = args.Position;
        double entryEquity = _positionEntryEquities.TryGetValue(position.Id, out double equity) ? equity : 0.0;
        string closeRecordId = _csvLogger.AppendClose(position, args.Reason, position.Id.ToString(), _robot.SymbolName,
            _robot.TimeFrame.ToString(), _robot.Server.Time, GetClosePrice(position), entryEquity, _robot.Account.Equity);

        _positionEntryEquities.Remove(position.Id);
        _robot.Print("*****Grid position closed | Grid: {0}, Id: {1}, Reason: {2}, ProfitLoss: {3}", _settings.Label, closeRecordId,
            args.Reason, position.NetProfit);
    }

    private int CountGridOrders() {
        return GridPositions().Count() + GridPendingOrders().Count();
    }

    // Where the grid currently reaches: pending orders by their target, positions by their fill.
    private List<double> GridPrices() {
        return GridPendingOrders().Select(order => order.TargetPrice).Concat(GridPositions().Select(position => position.EntryPrice))
            .ToList();
    }

    // Reads live broker state rather than in-memory lists, so counts stay right across restarts
    // and manual edits.
    private IEnumerable<Position> GridPositions() {
        return _robot.Positions.Where(position => IsGridOrder(position.SymbolName, position.Label));
    }

    private IEnumerable<PendingOrder> GridPendingOrders() {
        return _robot.PendingOrders.Where(order => IsGridOrder(order.SymbolName, order.Label));
    }

    private bool IsGridOrder(string symbolName, string label) {
        return symbolName == _robot.SymbolName && label == _settings.Label;
    }

    // The side of the book a new order would trade against: longs buy at the ask, shorts sell at the bid.
    private double CurrentPrice => _settings.Direction == TradeDirectionModel.Long ? _robot.Symbol.Ask : _robot.Symbol.Bid;

    private double GetClosePrice(Position position) {
        HistoricalTrade[] closedTrades = _robot.History.FindByPositionId(position.Id);

        if (closedTrades != null && closedTrades.Length > 0)
            return closedTrades.OrderByDescending(trade => trade.ClosingTime).First().ClosingPrice;

        for (int i = position.Deals.Count - 1; i >= 0; i--) {
            Deal deal = position.Deals[i];

            if (deal.PositionImpact == DealPositionImpact.Closing && deal.ExecutionPrice.HasValue)
                return deal.ExecutionPrice.Value;
        }

        return 0.0;
    }

    private TradeType ToTradeType() {
        return _settings.Direction == TradeDirectionModel.Long ? TradeType.Buy : TradeType.Sell;
    }
}
