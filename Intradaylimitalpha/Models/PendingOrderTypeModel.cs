namespace cAlgo.Robots;

// Pending order kind, kept free of cAlgo.API so the planner stays pure.
public enum PendingOrderTypeModel {
    Limit,
    Stop
}
