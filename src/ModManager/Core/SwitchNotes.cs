using System.Collections.Generic;

namespace ModManager.Core;

/// <summary>What the page says after the player flips a mod's switch.</summary>
public static class SwitchNotes
{
    /// <param name="wasOnAtStart">Whether its DLL was in the plugins folder when the game started.</param>
    /// <param name="isOnNextStart">Where its DLL is now, which decides the next start.</param>
    public static BilingualText Moved(bool wasOnAtStart, bool isOnNextStart, IReadOnlyList<string> alsoStops)
    {
        if (wasOnAtStart == isOnNextStart) return new BilingualText("Back as it was: nothing changes at the next start.", "已恢复原样：下次启动时不会有变化。");

        if (isOnNextStart) return new BilingualText("On from the next start. Restart the game to use it.", "下次启动时开启。重启游戏后即可使用。");

        if (alsoStops.Count == 0) return new BilingualText("Off from the next start. Its settings file is kept.", "下次启动时关闭。它的设置文件会保留。");

        var names = string.Join(", ", alsoStops);

        return new BilingualText(
            "Off from the next start. Its settings file is kept. These need it and will not start either: " + names + ".",
            "下次启动时关闭。它的设置文件会保留。以下模组需要它，也不会启动：" + names + "。");
    }

    public static BilingualText TargetTaken(string path) =>
        new("Nothing was moved: a file is already at " + path + ". Remove one of the two copies first.", "没有移动任何文件：" + path + " 已有一个文件。请先删除其中一份。");

    public static BilingualText MoveFailed(string reason) =>
        new("Could not move its DLL, so nothing changed: " + reason, "无法移动它的 DLL，所以没有任何变化：" + reason);

    public static BilingualText CannotSwitch() =>
        new("This mod cannot be turned on or off here.", "无法在这里开启或关闭这个模组。");
}
