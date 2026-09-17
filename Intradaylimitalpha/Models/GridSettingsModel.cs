namespace cAlgo.Robots;

// One grid's inputs (one long or short group), resolved once at start from the cBot parameters.
public class GridSettingsModel {
    // Every order of this grid carries exactly this label, which is how the grid tells its orders apart
    // from the other groups' grids, manual trades, and other bots on the same symbol.
    public string Label { get; init; } = "";

    // The group part of the label ("Long", "Long2", "Short3"); written to the CSV so trades show which group they came from.
    public string GroupName { get; init; } = "";
    public TradeDirectionModel Direction { get; init; }
    public double AnchorPrice { get; init; }

    // A group is switched on by giving it a start price; 0 leaves it off.
    public bool IsEnabled => AnchorPrice > 0.0;
    public double SpacingPips { get; init; }
    public double VolumeInUnits { get; init; }

    // N: this grid's open positions plus pending orders are kept at this total.
    public int MaxOrders { get; init; }
    public double TakeProfitPips { get; init; }
}
