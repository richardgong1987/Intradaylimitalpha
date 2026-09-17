namespace cAlgo.Robots;

// How a fresh grid starts.
// Market: the first order fills at the current price, the rest are pending orders spaced from that fill.
// Pending: every order is a pending order, spaced from the anchor price (or the current price when none is set).
public enum EntryModeModel {
    Market,
    Pending
}
