namespace cAlgo.Robots;

// One grid's inputs (the long group or the short group), resolved once at start from the cBot parameters.
public class GridSettingsModel {
    // Every order of this grid carries exactly this label, which is how the grid tells its orders apart
    // from the other direction's grid, manual trades, and other bots on the same symbol.
    public string Label { get; init; } = "";
    public TradeDirectionModel Direction { get; init; }

    // 0 means "start from the current price".
    public double AnchorPrice { get; init; }
    public double SpacingPips { get; init; }
    public double VolumeInUnits { get; init; }

    // N: this grid's open positions plus pending orders are kept at this total.
    public int MaxOrders { get; init; }
    public double TakeProfitPips { get; init; }
}
