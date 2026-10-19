using ModManager.Core;
using Xunit;

namespace ModManager.Tests;

public class IconIndexTests
{
    [Fact]
    public void An_item_finds_its_web_icon_by_file_name_preferring_the_folder_its_path_names()
    {
        var index = IconIndex.Build(new[] { "Res\\Food\\apple.png", "Res\\Old\\apple.png", "Res\\Tool\\rope.png" });

        Assert.Equal("Res/Old/apple.png", index.Resolve("WebUI/Res/Old/apple.png"));
        Assert.Equal("Res/Food/apple.png", index.Resolve("apple"));
        Assert.Equal("Res/Tool/rope.png", index.Resolve("UI/Icons/rope.png"));
    }

    [Fact]
    public void An_item_without_a_matching_icon_has_none()
    {
        var index = IconIndex.Build(new[] { "Res/Food/apple.png" });

        Assert.Null(index.Resolve("pear"));
        Assert.Null(index.Resolve(null));
        Assert.Null(IconIndex.Empty.Resolve("apple"));
    }
}
