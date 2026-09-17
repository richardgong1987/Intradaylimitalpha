using System;
using System.Diagnostics;
using System.IO;
using cAlgo.API;

namespace cAlgo.Robots;

[Robot(TimeZone = TimeZones.TokyoStandardTime, AccessRights = AccessRights.FullAccess, AddIndicators = false)]
public class Intradaylimitalpha : Robot {
    [Parameter("订单标签", DefaultValue = "ManuallyTrade-label")]
    public string OrderLabel { get; set; }

    [Parameter("开仓方式 (Market=现价开仓, Pending=挂单开仓)", DefaultValue = EntryModeModel.Market, Group = "入场设定")]
    public EntryModeModel EntryMode { get; set; }

    [Parameter("方向 (Long=多单, Short=空单)", DefaultValue = TradeDirectionModel.Long, Group = "入场设定")]
    public TradeDirectionModel Direction { get; set; }

    [Parameter("挂单起始价 (0=现价, 仅挂单开仓)", DefaultValue = 0, MinValue = 0, Group = "入场设定")]
    public double AnchorPrice { get; set; }

    [Parameter("挂单间距 (pips)", DefaultValue = 100, MinValue = 100, Group = "入场设定")]
    public double SpacingPips { get; set; }

    [Parameter("每单手数", DefaultValue = 0.01, MinValue = 0.01, Step = 0.01, Group = "仓位设定")]
    public double LotsPerOrder { get; set; }

    [Parameter("持仓+挂单总数 N", DefaultValue = 20, MinValue = 1, Group = "仓位设定")]
    public int MaxOrders { get; set; }

    [Parameter("止盈距离 (pips)", DefaultValue = 100, MinValue = 10, Group = "止盈设定")]
    public double TakeProfitPips { get; set; }

    [Parameter("启动时清空交易记录CSV", DefaultValue = false, Group = "开发调试")]
    public bool ResetTradeLogOnStart { get; set; }

    [Parameter("debug调试", DefaultValue = false, Group = "开发调试")]
    public bool IsDebug { get; set; }

    [Parameter("输出文件名", DefaultValue = "ManuallyTrades.csv", Group = "开发调试")]
    public string FileName { get; set; }

    private GridOrderExecutor _orderExecutor;

    protected override void OnStart() {
        // A blank label would make every unlabelled order on the symbol look like part of the grid.
        if (string.IsNullOrWhiteSpace(OrderLabel)) {
            Print("*****OrderLabel must not be empty. cBot stopped.");
            Stop();
            return;
        }

        if (!TryResolveVolumeInUnits(out double volumeInUnits)) {
            Stop();
            return;
        }

        LaunchDebug();

        var csvLogger = new TradeCsvLogger(ResetTradeLogOnStart, ResolveReportsDirectory(), FileName);
        Print("****CSV logger path: {0}", csvLogger.FilePath);

        var settings = new GridSettingsModel {
            Label = OrderLabel.Trim(),
            Direction = Direction,
            VolumeInUnits = volumeInUnits,
            MaxOrders = MaxOrders,
            TakeProfitPips = TakeProfitPips
        };
        var planner = new GridPlanner(Direction, SpacingPips, Symbol.PipSize);
        _orderExecutor = new GridOrderExecutor(this, settings, planner, csvLogger);

        Print("*****Grid started | Mode: {0}, Direction: {1}, AnchorPrice: {2}, SpacingPips: {3}, Lots: {4}, N: {5}, TakeProfitPips: {6}",
            EntryMode, Direction, AnchorPrice, SpacingPips, LotsPerOrder, MaxOrders, TakeProfitPips);
        _orderExecutor.Start(EntryMode, AnchorPrice);
    }

    private bool TryResolveVolumeInUnits(out double volumeInUnits) {
        volumeInUnits = Symbol.NormalizeVolumeInUnits(Symbol.QuantityToVolumeInUnits(LotsPerOrder), RoundingMode.ToNearest);

        if (volumeInUnits >= Symbol.VolumeInUnitsMin && volumeInUnits <= Symbol.VolumeInUnitsMax)
            return true;

        Print("*****Lots per order {0} is outside the broker's volume range ({1} - {2} units). cBot stopped.", LotsPerOrder,
            Symbol.VolumeInUnitsMin, Symbol.VolumeInUnitsMax);
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

    // Grid orders are left in place on stop, so a restart continues the same grid.
    protected override void OnStop() {
        _orderExecutor?.Stop();
        Print("*****cBot stopped.*******************");
    }
}
