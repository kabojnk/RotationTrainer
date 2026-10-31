using Xunit;

namespace RotationTrainer.Core.Tests;

public class PadButtonTests
{
    [Theory]
    [InlineData(0, "LT D-Left")]   // LDL: Living Muse in the screenshot
    [InlineData(1, "LT D-Up")]
    [InlineData(3, "LT D-Down")]
    [InlineData(5, "LT A-Up")]     // LAU: Sprint (Y / triangle)
    [InlineData(6, "LT A-Right")]
    [InlineData(8, "RT D-Left")]
    [InlineData(15, "RT A-Down")]
    public void SlotsFollowTheGamesOrder(int slot, string label) =>
        Assert.Equal(label, PadButton.ForSlot(slot).ToString());

    [Fact]
    public void SlotsOutsideASetHaveNoButton()
    {
        Assert.Null(PadButton.ForSlot(-1));
        Assert.Null(PadButton.ForSlot(16));
    }

    [Theory]
    [InlineData("LT D-Left", "LT", false, PadDirection.Left)]
    [InlineData("RT A-Down", "RT", true, PadDirection.Down)]
    public void ParsesItsOwnLabels(string label, string trigger, bool face, PadDirection direction)
    {
        Assert.True(PadButton.TryParse(label, out var b));
        Assert.Equal(new PadButton(trigger, face, direction), b);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Ctrl-3")]
    [InlineData("LT")]
    [InlineData("LB D-Left")]
    [InlineData("LT X-Left")]
    [InlineData("LT D-Sideways")]
    [InlineData("LT D-7")]
    public void RejectsOtherLabels(string? label) => Assert.False(PadButton.TryParse(label, out _));
}
