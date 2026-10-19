using System.Linq;
using System.Text;

namespace ModManager.Core;

/// <summary>A setting's description in each language; either falls back to the other when the author wrote only one.</summary>
public sealed record Description(string English, string Chinese);

/// <summary>Display text made from what BepInEx knows about a setting, since most mods give nothing more.</summary>
public static class Wording
{
    /// <summary>
    /// Splits a description written as English lines then Chinese lines (the usual layout for mods in this game) by
    /// whether a line holds Chinese characters.
    /// </summary>
    public static Description SplitDescription(string text)
    {
        var lines = text.Replace("\r", "").Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0).ToArray();
        var english = string.Join("\n", lines.Where(line => !HasChinese(line)));
        var chinese = string.Join("\n", lines.Where(HasChinese));

        return new Description(english.Length > 0 ? english : chinese, chinese.Length > 0 ? chinese : english);
    }

    /// <summary>"IdleFreezeDelay" becomes "Idle freeze delay", "LogUIEvents" becomes "Log UI events", "Slot2" becomes "Slot 2".</summary>
    public static string LabelFromKey(string key)
    {
        var words = SplitWords(key);

        if (words.Length == 0) return key;

        var label = new StringBuilder(words[0]);

        foreach (var word in words.Skip(1)) label.Append(' ').Append(IsAcronym(word) ? word : word.ToLowerInvariant());

        return label.ToString();
    }

    private static bool HasChinese(string line) => line.Any(c => c is >= '\u4e00' and <= '\u9fff' or >= '\u3000' and <= '\u303f' or >= '\uff00' and <= '\uffef');

    private static bool IsAcronym(string word) => word.Length > 1 && word.All(c => !char.IsLetter(c) || char.IsUpper(c));

    private static string[] SplitWords(string key)
    {
        var spaced = new StringBuilder();

        for (var i = 0; i < key.Length; i++)
        {
            var c = key[i];

            if (c is '_' or '.' or '-' or ' ')
            {
                spaced.Append(' ');

                continue;
            }

            if (i > 0 && StartsWord(key, i)) spaced.Append(' ');

            spaced.Append(c);
        }

        return spaced.ToString().Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
    }

    /// <summary>A word starts at a lower-to-upper step, at the last capital of an acronym, and where letters meet digits.</summary>
    private static bool StartsWord(string key, int i)
    {
        var previous = key[i - 1];
        var current = key[i];
        var hasNext = i + 1 < key.Length;

        if (char.IsUpper(current) && char.IsLower(previous)) return true;

        if (char.IsUpper(current) && char.IsUpper(previous) && hasNext && char.IsLower(key[i + 1])) return true;

        return char.IsDigit(current) != char.IsDigit(previous) && char.IsLetterOrDigit(previous);
    }
}
