using System.Collections.Generic;
using System.Linq;

namespace ModManager.Core;

/// <summary>One frame of waiting for the key a hotkey setting should get.</summary>
public abstract record CaptureStep
{
    private CaptureStep()
    {
    }

    public sealed record Waiting : CaptureStep;

    public sealed record Cancelled : CaptureStep;

    /// <summary>The value to write, in BepInEx's form: "F7", or "F7 + LeftControl" for a shortcut.</summary>
    public sealed record Captured(string Value) : CaptureStep;
}

public static class KeyChords
{
    /// <summary>Keys that may be held before a shortcut's main key, in the order they are written after it.</summary>
    public static readonly IReadOnlyList<string> Modifiers = new[] { "LeftControl", "RightControl", "LeftShift", "RightShift", "LeftAlt", "RightAlt" };

    /// <summary>The left and right mouse buttons click the page while it waits, so they never become the key.</summary>
    private static readonly HashSet<string> Ignored = new() { "None", "Mouse0", "Mouse1" };

    /// <summary>
    /// <paramref name="pressed"/> are the keys that went down this frame, <paramref name="held"/> the modifiers held
    /// down. Esc always cancels. A single-key setting takes the first key pressed, a modifier included; a shortcut
    /// waits for a key that is not a modifier and adds the modifiers held with it.
    /// </summary>
    public static CaptureStep Step(IReadOnlyList<string> pressed, IReadOnlyList<string> held, bool allowsModifiers)
    {
        var candidates = pressed.Where(key => !Ignored.Contains(key)).ToList();

        if (pressed.Contains("Escape")) return new CaptureStep.Cancelled();

        if (!allowsModifiers) return candidates.Count > 0 ? new CaptureStep.Captured(candidates[0]) : new CaptureStep.Waiting();

        var main = candidates.FirstOrDefault(key => !Modifiers.Contains(key));

        if (main == null) return new CaptureStep.Waiting();

        var withHeld = new[] { main }.Concat(Modifiers.Where(held.Contains));

        return new CaptureStep.Captured(string.Join(" + ", withHeld));
    }
}
