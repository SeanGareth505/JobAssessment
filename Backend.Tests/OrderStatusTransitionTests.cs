using Backend.Data;
using Xunit;

namespace Backend.Tests;

public class OrderStatusTransitionTests
{
    private static bool IsValidTransition(OrderStatus from, OrderStatus to)
    {
        return (from, to) switch
        {
            (OrderStatus.Pending, OrderStatus.Paid) => true,
            (OrderStatus.Pending, OrderStatus.Cancelled) => true,
            (OrderStatus.Paid, OrderStatus.Fulfilled) => true,
            (OrderStatus.Paid, OrderStatus.Cancelled) => true,
            _ => false
        };
    }

    [Theory]
    [InlineData(OrderStatus.Pending, OrderStatus.Paid, true)]
    [InlineData(OrderStatus.Pending, OrderStatus.Cancelled, true)]
    [InlineData(OrderStatus.Paid, OrderStatus.Fulfilled, true)]
    [InlineData(OrderStatus.Paid, OrderStatus.Cancelled, true)]
    [InlineData(OrderStatus.Pending, OrderStatus.Fulfilled, false)]
    [InlineData(OrderStatus.Fulfilled, OrderStatus.Paid, false)]
    [InlineData(OrderStatus.Cancelled, OrderStatus.Paid, false)]
    public void ValidTransitions_AcceptExpected(OrderStatus from, OrderStatus to, bool expected)
    {
        Assert.Equal(expected, IsValidTransition(from, to));
    }
}
