using RotationTrainer.Core;
using Xunit;

namespace RotationTrainer.Core.Tests;

public class LevellingSelectionTests
{
    [Fact]
    public void FollowsPlayerUntilChanged()
    {
        var s = new LevellingSelection();
        s.Follow("PLD", 22);
        Assert.Equal("PLD", s.Job);
        Assert.Equal(22, s.Level);
        s.Follow("PLD", 23);
        Assert.Equal(23, s.Level);
        Assert.True(s.FollowsPlayer);
    }

    [Fact]
    public void SelectingStopsFollowing()
    {
        var s = new LevellingSelection();
        s.Follow("PLD", 22);
        s.Select("WAR");
        Assert.False(s.FollowsPlayer);
        s.Follow("PLD", 30);
        Assert.Equal("WAR", s.Job);
        Assert.Equal(22, s.Level);
    }

    [Fact]
    public void UsePlayerResumes()
    {
        var s = new LevellingSelection();
        s.Follow("PLD", 30);
        s.SetLevel(50);
        Assert.False(s.FollowsPlayer);
        s.UsePlayer();
        s.Follow("PLD", 31);
        Assert.True(s.FollowsPlayer);
        Assert.Equal(31, s.Level);
    }

    [Fact]
    public void LevelClampsToRange()
    {
        var s = new LevellingSelection();
        s.SetLevel(150);
        Assert.Equal(100, s.Level);
        s.SetLevel(0);
        Assert.Equal(1, s.Level);
    }

    [Fact]
    public void FollowingNoJobKeepsLastJob()
    {
        var s = new LevellingSelection();
        s.Follow("PLD", 40);
        s.Follow(null, 0);
        Assert.Equal("PLD", s.Job);
        Assert.Equal(40, s.Level);
    }

    [Fact]
    public void SelectingAJobRaisesLevelToItsStart()
    {
        var s = new LevellingSelection();
        s.Follow("PLD", 22);
        s.Select("DRK", startLevel: 30);
        Assert.Equal(30, s.Level);
        s.Select("WAR", startLevel: 1);
        Assert.Equal(30, s.Level);
    }
}
