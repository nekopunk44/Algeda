using Domain.Entities;
using Domain.Enums;

namespace Application.Tests;

public class DealPriorityTests
{
    [Fact]
    public void SetPriority_ShouldMarkDealAsActiveUntilDeadline()
    {
        var deal = CreateIncomingDeal();
        var realtorId = Guid.Parse("10000000-0000-0000-0000-000000000001");
        var untilUtc = DateTime.UtcNow.AddHours(24);

        deal.SetPriority(realtorId, untilUtc);

        Assert.Equal(realtorId, deal.PriorityRealtorId);
        Assert.Equal(untilUtc, deal.PriorityUntilUtc);
        Assert.True(deal.HasActivePriority(DateTime.UtcNow));
    }

    [Fact]
    public void HasActivePriority_ShouldReturnFalseAfterDeadline()
    {
        var deal = CreateIncomingDeal();
        var untilUtc = DateTime.UtcNow.AddHours(24);

        deal.SetPriority(Guid.Parse("10000000-0000-0000-0000-000000000001"), untilUtc);

        Assert.False(deal.HasActivePriority(untilUtc.AddTicks(1)));
    }

    [Fact]
    public void AcceptIncoming_ShouldClearPriority()
    {
        var deal = CreateIncomingDeal();
        var realtorId = Guid.Parse("10000000-0000-0000-0000-000000000001");
        deal.SetPriority(realtorId, DateTime.UtcNow.AddHours(24));

        deal.AcceptIncoming(realtorId);

        Assert.Null(deal.PriorityRealtorId);
        Assert.Null(deal.PriorityUntilUtc);
    }

    [Fact]
    public void RejectIncoming_ShouldClearPriority()
    {
        var deal = CreateIncomingDeal();
        var realtorId = Guid.Parse("10000000-0000-0000-0000-000000000001");
        deal.SetPriority(realtorId, DateTime.UtcNow.AddHours(24));

        deal.RejectIncoming(realtorId);

        Assert.Null(deal.PriorityRealtorId);
        Assert.Null(deal.PriorityUntilUtc);
    }

    [Fact]
    public void ReassignRealtor_ShouldClearPriority()
    {
        var deal = CreateIncomingDeal();
        var realtorId = Guid.Parse("10000000-0000-0000-0000-000000000001");
        deal.SetPriority(realtorId, DateTime.UtcNow.AddHours(24));

        deal.ReassignRealtor(realtorId);

        Assert.Null(deal.PriorityRealtorId);
        Assert.Null(deal.PriorityUntilUtc);
    }

    private static Deal CreateIncomingDeal()
    {
        return Deal.CreateIncoming(
            Guid.Parse("20000000-0000-0000-0000-000000000001"),
            DealSource.Matching,
            Guid.Parse("30000000-0000-0000-0000-000000000001"),
            Guid.Parse("40000000-0000-0000-0000-000000000001"),
            "request");
    }
}
