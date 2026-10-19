using System;

namespace ModManager.Core;

/// <summary>Turns the picture a mod embeds as its icon into something the page can show, if it really is a small PNG or JPEG.</summary>
public static class IconImage
{
    public const int MaxBytes = 256 * 1024;

    /// <summary>A banner is wider, so it may be larger than an icon.</summary>
    public const int MaxBannerBytes = 512 * 1024;

    private static readonly byte[] Png = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
    private static readonly byte[] Jpeg = { 0xFF, 0xD8, 0xFF };

    /// <summary>A data URI, or null with the reason. The type is taken from the bytes, never from the name, so nothing else can pass as a picture.</summary>
    public static (string? Uri, string? Problem) DataUri(byte[] bytes, int maxBytes = MaxBytes)
    {
        if (bytes.Length > maxBytes) return (null, "is larger than " + maxBytes / 1024 + " KB");

        var type = StartsWith(bytes, Png) ? "image/png" : StartsWith(bytes, Jpeg) ? "image/jpeg" : null;

        if (type == null) return (null, "is not a PNG or JPEG picture");

        return ("data:" + type + ";base64," + Convert.ToBase64String(bytes), null);
    }

    private static bool StartsWith(byte[] bytes, byte[] magic)
    {
        if (bytes.Length < magic.Length) return false;

        for (var i = 0; i < magic.Length; i++)
        {
            if (bytes[i] != magic[i]) return false;
        }

        return true;
    }
}
