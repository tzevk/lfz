using LFZ.Domain.Entities;
using LFZ.Domain.Enums;
using LFZ.Domain.Rules;
using Xunit;

namespace LFZ.Domain.Tests;

public class PlotRulesTests
{
    private static Plot MakePlot(
        PlotStatus status = PlotStatus.Free,
        bool isLocked = false,
        bool multiTenantEnabled = false) => new()
    {
        Id = 1,
        Code = "i001",
        DisplayName = "Insignia",
        LandUseType = "Industrial",
        Status = status,
        IsLocked = isLocked,
        MultiTenantBlockEnabled = multiTenantEnabled
    };

    // ------------------------------------------------------------------
    // Transition matrix
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(PlotStatus.Free, PlotStatus.PendingReview, true)]
    [InlineData(PlotStatus.Free, PlotStatus.Occupied, true)]
    [InlineData(PlotStatus.Free, PlotStatus.Blocked, true)]
    [InlineData(PlotStatus.PendingReview, PlotStatus.Occupied, true)]
    [InlineData(PlotStatus.PendingReview, PlotStatus.Free, true)]
    [InlineData(PlotStatus.Blocked, PlotStatus.Free, true)]
    [InlineData(PlotStatus.Occupied, PlotStatus.Free, true)]
    [InlineData(PlotStatus.Occupied, PlotStatus.Blocked, false)]
    [InlineData(PlotStatus.Occupied, PlotStatus.PendingReview, false)]
    [InlineData(PlotStatus.PendingReview, PlotStatus.Blocked, false)]
    [InlineData(PlotStatus.Unavailable, PlotStatus.Free, false)]
    [InlineData(PlotStatus.Unavailable, PlotStatus.Occupied, false)]
    public void CanTransition_enforces_workflow_matrix(PlotStatus from, PlotStatus to, bool expected)
    {
        Assert.Equal(expected, PlotRules.CanTransition(from, to));
    }

    [Theory]
    [InlineData(PlotStatus.Free)]
    [InlineData(PlotStatus.Occupied)]
    [InlineData(PlotStatus.Unavailable)]
    public void CanTransition_allows_self_transition(PlotStatus status)
    {
        Assert.True(PlotRules.CanTransition(status, status));
    }

    // ------------------------------------------------------------------
    // EnsureCanChange
    // ------------------------------------------------------------------

    [Fact]
    public void EnsureCanChange_throws_for_locked_plot()
    {
        var plot = MakePlot(isLocked: true);
        var exception = Assert.Throws<PlotRuleViolationException>(() => PlotRules.EnsureCanChange(plot));
        Assert.Contains("locked", exception.Message);
    }

    [Fact]
    public void EnsureCanChange_throws_for_unavailable_plot()
    {
        var plot = MakePlot(PlotStatus.Unavailable);
        Assert.Throws<PlotRuleViolationException>(() => PlotRules.EnsureCanChange(plot));
    }

    [Fact]
    public void EnsureCanChange_accepts_free_unlocked_plot()
    {
        PlotRules.EnsureCanChange(MakePlot());
    }

    // ------------------------------------------------------------------
    // Requesting
    // ------------------------------------------------------------------

    [Fact]
    public void EnsureCanBeRequested_accepts_free_plot()
    {
        PlotRules.EnsureCanBeRequested(MakePlot(PlotStatus.Free));
    }

    [Theory]
    [InlineData(PlotStatus.Occupied)]
    [InlineData(PlotStatus.Blocked)]
    [InlineData(PlotStatus.PendingReview)]
    public void EnsureCanBeRequested_rejects_non_free_plot(PlotStatus status)
    {
        Assert.Throws<PlotRuleViolationException>(() => PlotRules.EnsureCanBeRequested(MakePlot(status)));
    }

    // ------------------------------------------------------------------
    // Allocation
    // ------------------------------------------------------------------

    [Fact]
    public void EnsureCanBeAllocated_accepts_free_plot()
    {
        PlotRules.EnsureCanBeAllocated(MakePlot(PlotStatus.Free));
    }

    [Theory]
    [InlineData(PlotStatus.Occupied)]
    [InlineData(PlotStatus.Blocked)]
    [InlineData(PlotStatus.PendingReview)]
    public void EnsureCanBeAllocated_rejects_non_free_plot(PlotStatus status)
    {
        Assert.Throws<PlotRuleViolationException>(() => PlotRules.EnsureCanBeAllocated(MakePlot(status)));
    }

    // ------------------------------------------------------------------
    // Blocking
    // ------------------------------------------------------------------

    [Fact]
    public void EnsureCanBeBlocked_accepts_free_plot()
    {
        PlotRules.EnsureCanBeBlocked(MakePlot(PlotStatus.Free));
    }

    [Theory]
    [InlineData(PlotStatus.Occupied)]
    [InlineData(PlotStatus.Blocked)]
    [InlineData(PlotStatus.PendingReview)]
    public void EnsureCanBeBlocked_rejects_already_engaged_plot(PlotStatus status)
    {
        Assert.Throws<PlotRuleViolationException>(() => PlotRules.EnsureCanBeBlocked(MakePlot(status)));
    }

    // ------------------------------------------------------------------
    // Request review
    // ------------------------------------------------------------------

    [Fact]
    public void EnsureRequestIsReviewable_accepts_pending_request_on_pending_plot()
    {
        var plot = MakePlot(PlotStatus.PendingReview);
        var request = new PlotRequest { Id = 7, Plot = plot, Status = PlotRequestStatus.Pending };

        PlotRules.EnsureRequestIsReviewable(request);
    }

    [Theory]
    [InlineData(PlotRequestStatus.Approved)]
    [InlineData(PlotRequestStatus.Rejected)]
    [InlineData(PlotRequestStatus.Cancelled)]
    public void EnsureRequestIsReviewable_rejects_decided_request(PlotRequestStatus status)
    {
        var plot = MakePlot(PlotStatus.PendingReview);
        var request = new PlotRequest { Id = 7, Plot = plot, Status = status };

        Assert.Throws<PlotRuleViolationException>(() => PlotRules.EnsureRequestIsReviewable(request));
    }

    [Fact]
    public void EnsureRequestIsReviewable_rejects_request_when_plot_not_pending()
    {
        var plot = MakePlot(PlotStatus.Free);
        var request = new PlotRequest { Id = 7, Plot = plot, Status = PlotRequestStatus.Pending };

        Assert.Throws<PlotRuleViolationException>(() => PlotRules.EnsureRequestIsReviewable(request));
    }

    // ------------------------------------------------------------------
    // Multi-tenant block exception
    // ------------------------------------------------------------------

    [Fact]
    public void MultiTenantBlock_requires_plot_flag_and_global_flag()
    {
        var plot = MakePlot(multiTenantEnabled: true);
        PlotRules.EnsureMultiTenantBlockAllowed(plot, featureEnabledGlobally: true);
    }

    [Fact]
    public void MultiTenantBlock_rejected_when_plot_flag_disabled()
    {
        var plot = MakePlot(multiTenantEnabled: false);
        Assert.Throws<PlotRuleViolationException>(
            () => PlotRules.EnsureMultiTenantBlockAllowed(plot, featureEnabledGlobally: true));
    }

    [Fact]
    public void MultiTenantBlock_rejected_when_feature_disabled_globally()
    {
        var plot = MakePlot(multiTenantEnabled: true);
        Assert.Throws<PlotRuleViolationException>(
            () => PlotRules.EnsureMultiTenantBlockAllowed(plot, featureEnabledGlobally: false));
    }

    [Fact]
    public void MultiTenantBlock_rejected_for_occupied_plot()
    {
        var plot = MakePlot(PlotStatus.Occupied, multiTenantEnabled: true);
        Assert.Throws<PlotRuleViolationException>(
            () => PlotRules.EnsureMultiTenantBlockAllowed(plot, featureEnabledGlobally: true));
    }
}
