using Jajcarz.Domain.Products;

namespace Jajcarz.Domain.Tests;

public class ConflictPolicyTests
{
    private const string Panel = "device:abc";
    private const string User = "web:hubert";

    private static readonly DateTimeOffset Start = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    private readonly Product _product = new(Guid.NewGuid(), "Mleko", null, User, Start, rev: 10);

    private static StateChange FromPanel(
        ProductState state, DateTimeOffset receivedAt, long? ageMs = null, long? baseRev = null, string? key = null) =>
        new(state, Panel, receivedAt, ageMs, baseRev, key);

    private static StateChange FromWeb(ProductState state, DateTimeOffset receivedAt) =>
        new(state, User, receivedAt, AgeMs: 0);

    [Fact]
    public void NewerChangeIsAccepted()
    {
        var change = FromPanel(ProductState.LOW, Start.AddMinutes(10), ageMs: 1000);

        Assert.Equal(ChangeOutcome.Accepted, ConflictPolicy.Decide(_product, null, change));
    }

    [Fact]
    public void OccurredAtIsReceiveTimeMinusAge()
    {
        var change = FromPanel(ProductState.LOW, Start.AddMinutes(10), ageMs: 60_000);

        Assert.Equal(Start.AddMinutes(9), change.OccurredAt);
    }

    [Fact]
    public void OlderOfflineChangeLosesToNewerWebChange()
    {
        _product.Apply(FromWeb(ProductState.OK, Start.AddMinutes(10)), rev: 11);

        var change = FromPanel(ProductState.OUT, Start.AddMinutes(20), ageMs: (long)TimeSpan.FromMinutes(20).TotalMilliseconds);

        Assert.Equal(ChangeOutcome.Rejected, ConflictPolicy.Decide(_product, null, change));
    }

    [Fact]
    public void NewerOfflineChangeBeatsOlderWebChange()
    {
        _product.Apply(FromWeb(ProductState.OK, Start.AddMinutes(10)), rev: 11);

        var change = FromPanel(ProductState.OUT, Start.AddMinutes(20), ageMs: (long)TimeSpan.FromMinutes(5).TotalMilliseconds);

        Assert.Equal(ChangeOutcome.Accepted, ConflictPolicy.Decide(_product, null, change));
    }

    [Fact]
    public void ChangeAtTheSameMomentAsCurrentOneLoses()
    {
        var change = FromPanel(ProductState.LOW, Start, ageMs: 0);

        Assert.Equal(ChangeOutcome.Rejected, ConflictPolicy.Decide(_product, null, change));
    }

    [Fact]
    public void ChangeAfterRestartIsAcceptedWhenNothingChangedMeanwhile()
    {
        var change = FromPanel(ProductState.LOW, Start.AddMinutes(1), baseRev: 10);

        Assert.Equal(ChangeOutcome.Accepted, ConflictPolicy.Decide(_product, null, change));
    }

    [Fact]
    public void ChangeAfterRestartUsesReceiveTime()
    {
        var change = FromPanel(ProductState.LOW, Start.AddMinutes(1), baseRev: 10);

        Assert.Equal(Start.AddMinutes(1), change.OccurredAt);
    }

    [Fact]
    public void ChangeAfterRestartLosesToChangeMadeMeanwhile()
    {
        _product.Apply(FromWeb(ProductState.OK, Start.AddMinutes(1)), rev: 11);

        var change = FromPanel(ProductState.OUT, Start.AddMinutes(2), baseRev: 10);

        Assert.Equal(ChangeOutcome.Rejected, ConflictPolicy.Decide(_product, null, change));
    }

    [Fact]
    public void ChangeWithoutAgeAndBaseRevIsRejected()
    {
        var change = FromPanel(ProductState.LOW, Start.AddMinutes(1));

        Assert.Equal(ChangeOutcome.Rejected, ConflictPolicy.Decide(_product, null, change));
    }

    [Fact]
    public void NegativeAgeIsRejected()
    {
        var change = FromPanel(ProductState.LOW, Start.AddMinutes(1), ageMs: -1000);

        Assert.Equal(ChangeOutcome.Rejected, ConflictPolicy.Decide(_product, null, change));
    }

    [Fact]
    public void ClickFromBeforeBindingLosesEvenIfProductIsOld()
    {
        var boundAt = Start.AddHours(2);

        var change = FromPanel(ProductState.OUT, Start.AddHours(3), ageMs: (long)TimeSpan.FromHours(2).TotalMilliseconds);

        Assert.Equal(ChangeOutcome.Rejected, ConflictPolicy.Decide(_product, boundAt, change));
    }

    [Fact]
    public void ClickAfterBindingIsAccepted()
    {
        var boundAt = Start.AddHours(2);

        var change = FromPanel(ProductState.OUT, Start.AddHours(3), ageMs: 1000);

        Assert.Equal(ChangeOutcome.Accepted, ConflictPolicy.Decide(_product, boundAt, change));
    }

    [Fact]
    public void ChangeAfterRestartFromBeforeBindingLoses()
    {
        _product.MarkBound(rev: 11);

        var change = FromPanel(ProductState.OUT, Start.AddHours(3), baseRev: 10);

        Assert.Equal(ChangeOutcome.Rejected, ConflictPolicy.Decide(_product, Start.AddHours(2), change));
    }

    [Fact]
    public void BindingKeepsAuthorAndTimeOfLastChange()
    {
        _product.Apply(FromWeb(ProductState.LOW, Start.AddMinutes(1)), rev: 11);

        _product.MarkBound(rev: 12);

        Assert.Equal(ProductState.LOW, _product.State);
        Assert.Equal(12, _product.Rev);
        Assert.Equal(User, _product.ChangedBy);
        Assert.Equal(Start.AddMinutes(1), _product.ChangedAt);
    }

    [Fact]
    public void RepeatedChangeIsRecognised()
    {
        var first = FromPanel(ProductState.LOW, Start.AddMinutes(1), ageMs: 1000, key: "device:abc:17");
        _product.Apply(first, rev: 11);

        var retry = first with { ReceivedAt = Start.AddMinutes(1).AddSeconds(5), AgeMs = 6000 };

        Assert.Equal(ChangeOutcome.Duplicate, ConflictPolicy.Decide(_product, null, retry));
    }

    [Fact]
    public void RepeatAfterOtherChangeIsNotDuplicate()
    {
        var first = FromPanel(ProductState.LOW, Start.AddMinutes(1), ageMs: 1000, key: "device:abc:17");
        _product.Apply(first, rev: 11);
        _product.Apply(FromWeb(ProductState.OK, Start.AddMinutes(2)), rev: 12);

        var retry = first with { ReceivedAt = Start.AddMinutes(3), AgeMs = 121_000 };

        Assert.Equal(ChangeOutcome.Rejected, ConflictPolicy.Decide(_product, null, retry));
    }

    [Fact]
    public void SameSeqWithOtherStateIsNotDuplicate()
    {
        _product.Apply(FromPanel(ProductState.LOW, Start.AddMinutes(1), ageMs: 1000, key: "device:abc:1"), rev: 11);

        var afterRestart = FromPanel(ProductState.OUT, Start.AddMinutes(2), ageMs: 1000, key: "device:abc:1");

        Assert.Equal(ChangeOutcome.Accepted, ConflictPolicy.Decide(_product, null, afterRestart));
    }
}
