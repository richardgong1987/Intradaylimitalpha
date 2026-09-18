namespace cAlgo.Robots;

// One grid's inputs (the long group or the short group), resolved once at start from the cBot parameters.
public class GridSettingsModel {
    // Every order of this grid carries exactly this label, which is how the grid tells its orders apart
    // from the other direction's grid, manual trades, and other bots on the same symbol.
    public string Label { get; init; } = "";

    // The other direction's label; its positions offset this grid's in the position-difference cap.
    public string OppositeLabel { get; init; } = "";
    public TradeDirectionModel Direction { get; init; }
    public double AnchorPrice { get; init; }

    // A group is switched on by giving it a start price; 0 leaves it off.
    public bool IsEnabled => AnchorPrice > 0.0;
    public double SpacingPips { get; init; }
    public double VolumeInUnits { get; init; }

    // N: this grid's open positions plus pending orders are kept at this total.
    public int MaxOrders { get; init; }

    // Cap on |long positions - short positions|; 0 means no cap. See PositionLimitRule.
    public int MaxPositionDifference { get; init; }
    public double TakeProfitPips { get; init; }
}
