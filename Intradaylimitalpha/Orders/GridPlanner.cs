using System;
using System.Collections.Generic;
using System.Linq;

namespace cAlgo.Robots;

// Price geometry of a one-directional grid. Pure: no cAlgo dependency, so it is unit tested.
//
// A long grid steps downward from its anchor (buying lower), a short grid steps upward
// (selling higher). Neighbouring orders are one spacing apart.
public class GridPlanner {
    private readonly TradeDirectionModel _direction;
    private readonly double _spacingPrice;

    public GridPlanner(TradeDirectionModel direction, double spacingPips, double pipSize) {
        if (spacingPips <= 0.0 || pipSize <= 0.0)
            throw new ArgumentOutOfRangeException(nameof(spacingPips), "Grid spacing and pip size must be positive.");

        _direction = direction;
        _spacingPrice = spacingPips * pipSize;
    }

    // One spacing, two spacings, ... away from the anchor. The anchor itself is not an order.
    public List<double> InitialPendingPrices(double anchorPrice, int count) {
        return Enumerable.Range(1, Math.Max(count, 0)).Select(step => StepAway(anchorPrice, step)).ToList();
    }

    // A replacement order extends the grid: it goes one spacing beyond the farthest order,
    // never back into a gap that an order left behind.
    public double NextPendingPrice(IReadOnlyCollection<double> gridPrices) {
        if (gridPrices.Count == 0)
            throw new ArgumentException("Cannot extend an empty grid.", nameof(gridPrices));

        double farthest = _direction == TradeDirectionModel.Long ? gridPrices.Min() : gridPrices.Max();
        return StepAway(farthest, 1);
    }

    // An order on the favourable side of the market (below it for a long) is a limit order;
    // one on the other side can only be a stop order, because a limit there would fill at once.
    public PendingOrderTypeModel ChooseOrderType(double targetPrice, double marketPrice) {
        bool isFavourable = _direction == TradeDirectionModel.Long ? targetPrice <= marketPrice : targetPrice >= marketPrice;
        return isFavourable ? PendingOrderTypeModel.Limit : PendingOrderTypeModel.Stop;
    }

    private double StepAway(double price, int steps) {
        double offset = steps * _spacingPrice;
        return _direction == TradeDirectionModel.Long ? price - offset : price + offset;
    }
}
