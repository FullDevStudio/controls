using System.Collections.ObjectModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Shouldly;
using Xunit;

namespace Shiny.Maui.Controls.Tests;

[Collection(ApplicationResourcesCollection.Name)]
public class AvatarTests
{
    public AvatarTests() => _ = new Application();


    [Theory]
    [InlineData("Allan Ritchie", "AR")]
    [InlineData("allan", "A")]
    [InlineData("  Grace   Brewster Hopper ", "GH")]
    [InlineData("Jean-Luc Picard", "JP")]
    [InlineData("ada.lovelace@example.com", "AL")]
    [InlineData("Émile Zola", "ÉZ")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void InitialsComeFromTheFirstAndLastWords(string? name, string expected)
        => AvatarText.GetInitials(name).ShouldBe(expected);


    /// <summary>
    /// Pinned, and pinned to the same values in the Blazor tests: the same person has to get the same
    /// colour in a MAUI app and its web twin.
    /// </summary>
    [Theory]
    [InlineData("Allan Ritchie", 53)]
    [InlineData("allan ritchie", 53)]
    [InlineData("Grace Hopper", 147)]
    public void TheHueIsStableAndIgnoresCase(string name, int hue)
        => AvatarText.GetHue(name).ShouldBe(hue);


    [Fact]
    public void ExplicitInitialsWin()
        => new Avatar { Name = "Allan Ritchie", Initials = "AJ" }.DisplayInitials.ShouldBe("AJ");


    [Fact]
    public void SizeSetsTheRequest()
    {
        var avatar = new Avatar { Size = 56 };

        avatar.WidthRequest.ShouldBe(56);
        avatar.HeightRequest.ShouldBe(56);
    }


    [Fact]
    public void NoImageMeansNoImage()
        => new Avatar { Name = "Allan Ritchie" }.IsShowingImage.ShouldBeFalse();


    [Fact]
    public void IsOnlyTappableWhenSomeoneListens()
    {
        var avatar = new Avatar();
        avatar.GestureRecognizers.ShouldBeEmpty();

        EventHandler handler = (_, _) => { };
        avatar.Clicked += handler;
        avatar.GestureRecognizers.Count.ShouldBe(1);

        avatar.Clicked -= handler;
        avatar.GestureRecognizers.ShouldBeEmpty();
    }


    [Fact]
    public void ATapRaisesClickedAndRunsTheCommand()
    {
        var log = new List<string>();
        var avatar = new Avatar
        {
            Command = new Command<string>(p => log.Add("command:" + p)),
            CommandParameter = "me"
        };
        avatar.Clicked += (_, _) => log.Add("clicked");

        avatar.TapCommand.Execute(null);

        log.ShouldBe(["clicked", "command:me"]);
    }


    [Fact]
    public void TheGroupCollapsesEveryonePastMax()
    {
        var group = Group(6);
        group.Max = 3;

        group.VisibleAvatars.Count.ShouldBe(3);
        group.OverflowCount.ShouldBe(3);
        group.OverflowChip.DisplayInitials.ShouldBe("+3");
    }


    [Fact]
    public void TotalCountExtendsTheOverflow()
    {
        var group = Group(4);
        group.TotalCount = 120;

        group.VisibleAvatars.Count.ShouldBe(4);
        group.OverflowCount.ShouldBe(116);
    }


    [Fact]
    public void NoOverflowNoChip()
    {
        var group = Group(3);
        group.Max = 5;

        group.OverflowCount.ShouldBe(0);
        group.VisibleAvatars.Count.ShouldBe(3);
    }


    [Fact]
    public void TheGroupOverlapsAndSizesItsAvatars()
    {
        var group = Group(3);
        group.Size = 32;
        group.Shape = AvatarShape.Rounded;

        var avatars = group.VisibleAvatars;
        avatars.ShouldAllBe(x => x.Size == 32 && x.Shape == AvatarShape.Rounded && x.RingThickness == 2);
        avatars[0].Margin.Left.ShouldBe(0);
        avatars[1].Margin.Left.ShouldBe(-8);
    }


    [Fact]
    public void AnItemsSourceOfStringsNeedsNoTemplate()
    {
        var people = new ObservableCollection<string> { "Allan Ritchie", "Grace Hopper" };
        var group = new AvatarGroup { ItemsSource = people };

        group.VisibleAvatars.Select(x => x.DisplayInitials).ShouldBe(["AR", "GH"]);

        people.Add("Ada Lovelace");
        group.VisibleAvatars.Count.ShouldBe(3);
    }


    [Fact]
    public void ATemplateMustProduceAnAvatar()
    {
        var group = new AvatarGroup { ItemTemplate = new DataTemplate(() => new Label()) };

        Should.Throw<InvalidOperationException>(() => group.ItemsSource = new[] { "x" });
    }


    [Fact]
    public void TheOverflowCommandGetsTheHiddenCount()
    {
        int? received = null;
        var group = Group(5);
        group.Max = 2;
        group.OverflowCommand = new Command<int>(n => received = n);

        group.OverflowChip.TapCommand.Execute(null);

        received.ShouldBe(3);
    }


    static AvatarGroup Group(int count)
    {
        var group = new AvatarGroup();
        for (var i = 0; i < count; i++)
            group.Avatars.Add(new Avatar { Name = "Person " + i });
        return group;
    }
}
