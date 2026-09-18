using cAlgo.Robots;
using Xunit;

namespace Grid.Tests;

public class PositionLimitRuleTests {
    [Fact]
    public void Side_below_the_difference_limit_may_keep_filling() {
        Assert.False(PositionLimitRule.IsReached(ownPositions: 4, oppositePositions: 0, maxDifference: 5));
    }

    [Fact]
    public void Side_at_the_difference_limit_is_stopped() {
        Assert.True(PositionLimitRule.IsReached(ownPositions: 5, oppositePositions: 0, maxDifference: 5));
    }

    [Fact]
    public void Side_past_the_limit_after_a_gap_is_still_stopped() {
        Assert.True(PositionLimitRule.IsReached(ownPositions: 7, oppositePositions: 0, maxDifference: 5));
    }

    [Fact]
    public void Opposite_positions_offset_own_positions() {
        Assert.False(PositionLimitRule.IsReached(ownPositions: 8, oppositePositions: 4, maxDifference: 5));
    }

    [Fact]
    public void Lighter_side_is_never_stopped() {
        Assert.False(PositionLimitRule.IsReached(ownPositions: 0, oppositePositions: 9, maxDifference: 5));
    }

    [Fact]
    public void Zero_switches_the_limit_off() {
        Assert.False(PositionLimitRule.IsReached(ownPositions: 50, oppositePositions: 0, maxDifference: 0));
    }
}
