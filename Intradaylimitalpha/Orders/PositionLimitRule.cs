namespace cAlgo.Robots;

// Caps how far one direction's open positions may run ahead of the other's:
// |long positions - short positions| <= maxDifference. A maxDifference of 0 switches the cap off.
public static class PositionLimitRule {
    // True when this side may not take on another position, so its pending orders must come off the book.
    public static bool IsReached(int ownPositions, int oppositePositions, int maxDifference) {
        return maxDifference > 0 && ownPositions - oppositePositions >= maxDifference;
    }
}
