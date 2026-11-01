using CreditScoring.Api.Ui;

namespace CreditScoring.Tests.Ui;

public class AvatarTests
{
    [Theory]
    [InlineData("Marko Petrović", "MP")]
    [InlineData("  ana  ", "A")]
    [InlineData("Ana Marija van Dijk", "AD")]
    [InlineData("đorđe", "Đ")]
    public void Initials_are_first_and_last_name_letters(string name, string expected)
    {
        Assert.Equal(expected, Avatar.Initials(name));
    }

    [Fact]
    public void Tone_is_stable_for_a_name_and_within_the_palette()
    {
        var tone = Avatar.Tone("Marko Petrović");

        Assert.Equal(tone, Avatar.Tone("Marko Petrović"));
        Assert.InRange(tone, 0, Avatar.ToneCount - 1);
    }

    [Fact]
    public void Different_names_use_more_than_one_tone()
    {
        var tones = new[] { "Ana", "Marko", "Jelena", "Stefan", "Milica", "Nikola", "Ivana" }.Select(Avatar.Tone).Distinct();

        Assert.True(tones.Count() > 1);
    }
}
