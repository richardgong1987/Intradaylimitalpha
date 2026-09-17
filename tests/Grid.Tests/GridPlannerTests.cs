using System;
using System.Collections.Generic;
using cAlgo.Robots;
using Xunit;

namespace Grid.Tests;

public class GridPlannerTests {
    private const int Precision = 6;

    // 100 pips on a 0.01 pip size = 1.00 in price.
    private static GridPlanner LongGrid() => new(TradeDirectionModel.Long, 100, 0.01);
    private static GridPlanner ShortGrid() => new(TradeDirectionModel.Short, 100, 0.01);

    [Fact]
    public void Long_grid_steps_downward_from_the_anchor() {
        List<double> prices = LongGrid().InitialPendingPrices(2000.0, 3);

        Assert.Equal(3, prices.Count);
        Assert.Equal(1999.0, prices[0], Precision);
        Assert.Equal(1998.0, prices[1], Precision);
        Assert.Equal(1997.0, prices[2], Precision);
    }

    [Fact]
    public void Short_grid_steps_upward_from_the_anchor() {
        List<double> prices = ShortGrid().InitialPendingPrices(2000.0, 2);

        Assert.Equal(2001.0, prices[0], Precision);
        Assert.Equal(2002.0, prices[1], Precision);
    }

    [Fact]
    public void Market_entry_with_n_of_one_places_no_pending_orders() {
        Assert.Empty(LongGrid().InitialPendingPrices(2000.0, 0));
    }

    [Fact]
    public void Long_refill_goes_one_spacing_below_the_lowest_order() {
        // Take profit at 2000 left orders at 1999..1996; the refill extends the grid, not the gap at 2000.
        double next = LongGrid().NextPendingPrice(new[] { 2000.0, 1999.0, 1998.0, 1997.0, 1996.0 });

        Assert.Equal(1995.0, next, Precision);
    }

    [Fact]
    public void Short_refill_goes_one_spacing_above_the_highest_order() {
        double next = ShortGrid().NextPendingPrice(new[] { 2001.0, 2003.0, 2002.0 });

        Assert.Equal(2004.0, next, Precision);
    }

    [Fact]
    public void Refill_goes_beyond_the_closed_order_when_it_was_the_farthest() {
        // Every order filled; the lowest (1996) took profit, so it is still the grid's reach.
        double next = LongGrid().NextPendingPrice(new[] { 1999.0, 1998.0, 1997.0, 1996.0 });

        Assert.Equal(1995.0, next, Precision);
    }

    [Fact]
    public void Extending_an_empty_grid_is_rejected() {
        Assert.Throws<ArgumentException>(() => LongGrid().NextPendingPrice(Array.Empty<double>()));
    }

    [Theory]
    [InlineData(1999.0, 2000.0, PendingOrderTypeModel.Limit)]
    [InlineData(2001.0, 2000.0, PendingOrderTypeModel.Stop)]
    public void Long_order_below_the_market_is_a_limit_and_above_is_a_stop(double target, double market, PendingOrderTypeModel expected) {
        Assert.Equal(expected, LongGrid().ChooseOrderType(target, market));
    }

    [Theory]
    [InlineData(2001.0, 2000.0, PendingOrderTypeModel.Limit)]
    [InlineData(1999.0, 2000.0, PendingOrderTypeModel.Stop)]
    public void Short_order_above_the_market_is_a_limit_and_below_is_a_stop(double target, double market, PendingOrderTypeModel expected) {
        Assert.Equal(expected, ShortGrid().ChooseOrderType(target, market));
    }

    [Fact]
    public void Non_positive_spacing_is_rejected() {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GridPlanner(TradeDirectionModel.Long, 0, 0.01));
    }
}
