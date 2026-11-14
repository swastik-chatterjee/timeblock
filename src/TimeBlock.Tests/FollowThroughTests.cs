using TimeBlock.Core.Services;

namespace TimeBlock.Tests;

public class FollowThroughTests
{
    [Fact]
    public void AllDoneIsOne()
    {
        Assert.Equal(1.0, TimeBlockStats.FollowThroughRate(done: 5, partial: 0, skipped: 0, missed: 0));
    }

    [Fact]
    public void PartialCountsHalf()
    {
        Assert.Equal(0.75, TimeBlockStats.FollowThroughRate(done: 1, partial: 1, skipped: 0, missed: 0));
    }

    [Fact]
    public void MissedAndSkippedDragRateDown()
    {
        Assert.Equal(0.25, TimeBlockStats.FollowThroughRate(done: 1, partial: 0, skipped: 1, missed: 2));
    }

    [Fact]
    public void EmptyWeekHasNoPenalty()
    {
        Assert.Equal(1.0, TimeBlockStats.FollowThroughRate(0, 0, 0, 0));
    }

    [Fact]
    public void PasswordHashRoundTrips()
    {
        var hash = PasswordHasher.Hash("correct horse battery staple");
        Assert.NotEqual("correct horse battery staple", hash);
        Assert.True(PasswordHasher.Verify("correct horse battery staple", hash));
        Assert.False(PasswordHasher.Verify("wrong password", hash));
    }
}
