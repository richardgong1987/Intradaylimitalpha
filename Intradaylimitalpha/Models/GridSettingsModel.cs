namespace cAlgo.Robots;

// The grid inputs the executor needs, resolved once at start from the cBot parameters.
public class GridSettingsModel {
    // Every grid order carries exactly this label, which is how the grid tells its orders apart
    // from manual trades and other bots on the same symbol.
    public string Label { get; init; } = "";
    public TradeDirectionModel Direction { get; init; }
    public double VolumeInUnits { get; init; }

    // N: open positions plus pending orders are kept at this total.
    public int MaxOrders { get; init; }
    public double TakeProfitPips { get; init; }
}
