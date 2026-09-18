using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using cAlgo.API;

namespace cAlgo.Robots;

[Robot(TimeZone = TimeZones.TokyoStandardTime, AccessRights = AccessRights.None, AddIndicators = false)]
public class Intradaylimitalpha : Robot {
    [Parameter("订单标签", DefaultValue = "Intradaylimitalpha-label")]
    public string OrderLabel { get; set; }

    [Parameter("最大总订单数", DefaultValue = 20, MinValue = 0)]
    public int MaxPositionsCount { get; set; }

    [Parameter("多单挂单起始价 (0=不开启)", DefaultValue = 0, MinValue = 0, Group = "多单组")]
    public double LongAnchorPrice { get; set; }

    [Parameter("多单挂单间距 (pips)", DefaultValue = 100, MinValue = 100, Group = "多单组")]
    public double LongSpacingPips { get; set; }

    [Parameter("多单每单手数", DefaultValue = 0.01, MinValue = 0.01, Step = 0.01, Group = "多单组")]
    public double LongLotsPerOrder { get; set; }

    [Parameter("多单持仓+挂单总数 N", DefaultValue = 20, MinValue = 1, Group = "多单组")]
    public int LongMaxOrders { get; set; }

    [Parameter("多单止盈距离 (pips)", DefaultValue = 100, MinValue = 10, Group = "多单组")]
    public double LongTakeProfitPips { get; set; }

    [Parameter("空单挂单起始价 (0=不开启)", DefaultValue = 0, MinValue = 0, Group = "空单组")]
    public double ShortAnchorPrice { get; set; }

    [Parameter("空单挂单间距 (pips)", DefaultValue = 100, MinValue = 100, Group = "空单组")]
    public double ShortSpacingPips { get; set; }

    [Parameter("空单每单手数", DefaultValue = 0.01, MinValue = 0.01, Step = 0.01, Group = "空单组")]
    public double ShortLotsPerOrder { get; set; }

    [Parameter("空单持仓+挂单总数 N", DefaultValue = 20, MinValue = 1, Group = "空单组")]
    public int ShortMaxOrders { get; set; }

    [Parameter("空单止盈距离 (pips)", DefaultValue = 100, MinValue = 10, Group = "空单组")]
    public double ShortTakeProfitPips { get; set; }

    [Parameter("启动时清空交易记录CSV", DefaultValue = false, Group = "开发调试")]
    public bool ResetTradeLogOnStart { get; set; }

    [Parameter("debug调试", DefaultValue = false, Group = "开发调试")]
    public bool IsDebug { get; set; }

    [Parameter("输出文件名", DefaultValue = "Intradaylimitalpha.csv", Group = "开发调试")]
    public string FileName { get; set; }

    private readonly List<GridOrderExecutor> _gridExecutors = new();

    protected override void OnStart() {
        // A blank label would make every unlabelled order on the symbol look like part of a grid.
        if (string.IsNullOrWhiteSpace(OrderLabel)) {
            Print("*****OrderLabel must not be empty. cBot stopped.");
            Stop();
            return;
        }

        List<GridSettingsModel> allGrids = AllGridSettings();

        foreach (GridSettingsModel settings in allGrids.Where(settings => !settings.IsEnabled)) {
            CancelPendingOrders(settings.Label);
        }

        List<GridSettingsModel> enabledGrids = allGrids.Where(settings => settings.IsEnabled).ToList();

        if (enabledGrids.Count == 0) {
            Print("*****No group has a start price. cBot stopped.");
            Stop();
            return;
        }

        // Check every enabled group before placing anything, so a bad lot size never leaves one grid half-built.
        if (!enabledGrids.All(IsTradableVolume)) {
            Stop();
            return;
        }

        LaunchDebug();

        var csvLogger = new TradeCsvLogger(ResetTradeLogOnStart, ResolveReportsDirectory(), FileName);
        Print("****CSV logger path: {0}", csvLogger.FilePath);

        foreach (GridSettingsModel settings in enabledGrids) {
            var planner = new GridPlanner(settings.Direction, settings.SpacingPips, Symbol.PipSize);
            _gridExecutors.Add(new GridOrderExecutor(this, settings, planner, csvLogger));
        }

        foreach (GridOrderExecutor executor in _gridExecutors) {
            executor.Start();
        }
    }

    // Both groups, switched on or not. Columns: direction, start price, spacing (pips), lots per order,
    // N, take profit (pips).
    private List<GridSettingsModel> AllGridSettings() {
        return new List<GridSettingsModel> {
            GridSettings(TradeDirectionModel.Long, LongAnchorPrice, LongSpacingPips, LongLotsPerOrder, LongMaxOrders, LongTakeProfitPips),
            GridSettings(TradeDirectionModel.Short, ShortAnchorPrice, ShortSpacingPips, ShortLotsPerOrder, ShortMaxOrders,
                ShortTakeProfitPips)
        };
    }

    // Each direction gets its own label ("{OrderLabel}_Long" / "{OrderLabel}_Short"), so the two grids
    // count, refill and restart independently even though they share one symbol.
    private GridSettingsModel GridSettings(TradeDirectionModel direction, double anchorPrice, double spacingPips, double lotsPerOrder,
        int maxOrders, double takeProfitPips) {
        return new GridSettingsModel {
            Label = $"{OrderLabel.Trim()}_{direction}",
            Direction = direction,
            AnchorPrice = anchorPrice,
            SpacingPips = spacingPips,
            VolumeInUnits = ToVolumeInUnits(lotsPerOrder),
            MaxOrders = maxOrders,
            TakeProfitPips = takeProfitPips
        };
    }

    // Switching a group off (start price 0) withdraws its unfilled orders from an earlier run. Filled
    // positions are left alone and still close at their own take profit.
    private void CancelPendingOrders(string label) {
        List<PendingOrder> ordersToCancel = PendingOrders.Where(order => order.SymbolName == SymbolName && order.Label == label).ToList();

        foreach (PendingOrder order in ordersToCancel) {
            TradeResult result = CancelPendingOrder(order);

            if (result.IsSuccessful)
                Print("*****Pending order cancelled | Label: {0}, Price: {1}", label, order.TargetPrice);
            else
                Print("*****Pending order cancel failed | Label: {0}, Price: {1}, Error: {2}", label, order.TargetPrice, result.Error);
        }
    }

    private double ToVolumeInUnits(double lots) {
        return Symbol.NormalizeVolumeInUnits(Symbol.QuantityToVolumeInUnits(lots), RoundingMode.ToNearest);
    }

    private bool IsTradableVolume(GridSettingsModel settings) {
        if (settings.VolumeInUnits >= Symbol.VolumeInUnitsMin && settings.VolumeInUnits <= Symbol.VolumeInUnitsMax)
            return true;

        Print("*****Lots per order of {0} ({1} units) is outside the broker's volume range ({2} - {3} units). cBot stopped.",
            settings.Label, settings.VolumeInUnits, Symbol.VolumeInUnitsMin, Symbol.VolumeInUnitsMax);
        return false;
    }

    private void LaunchDebug() {
        if (IsDebug) {
            bool result = Debugger.Launch();
            if (!result) {
                Print("Debugger launch failed");
            }
        }
    }

    // Output folders are separated by run mode so they never overwrite each other: the backtest folder is
    // rebuilt by the scripts on every batch, the demo/live folders are append-only.
    // Backtests pass an absolute FileName through run_conditions, which bypasses this folder (see TradeCsvLogger).
    private string ResolveReportsDirectory() {
        string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        return Path.Combine(documentsPath, ResolveReportsFolderName());
    }

    private string ResolveReportsFolderName() {
        if (IsBacktesting)
            return "trading_reports";

        return Account.IsLive ? "release_trading_reports" : "simulate_trading_reports";
    }

    // Grid orders are left in place on stop, so a restart continues the same grids.
    protected override void OnStop() {
        foreach (GridOrderExecutor executor in _gridExecutors) {
            executor.Stop();
        }

        Print("*****cBot stopped.*******************");
    }
}
