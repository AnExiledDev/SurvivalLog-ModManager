using System;
using System.Text;

namespace ModManager.Core;

/// <summary>
/// Reads a BepInDependency straight from its attribute bytes. Decoding the flags enum the normal way needs the assembly
/// that declares it (BepInEx.Core), which a file reader cannot always find; the bytes alone are enough.
/// </summary>
public static class DependencyBlob
{
    /// <summary>BepInDependency.DependencyFlags.SoftDependency.</summary>
    private const int SoftFlag = 2;

    private const byte NullString = 0xFF;

    /// <summary>
    /// BepInDependency(guid, flags) is soft when the soft flag is set; BepInDependency(guid, versionRange) is hard with that
    /// range. Null when the bytes are not that shape or the guid is empty.
    /// </summary>
    public static DeclaredRelation? Parse(byte[] blob, bool isVersionRange)
    {
        var reader = new BlobReader(blob);

        if (!reader.ReadProlog()) return null;

        var guid = reader.ReadString();

        if (string.IsNullOrEmpty(guid)) return null;

        if (isVersionRange)
        {
            var range = reader.ReadString();

            return new DeclaredRelation(RelationKind.Needs, guid, string.IsNullOrEmpty(range) ? null : range);
        }

        var flags = reader.ReadInt32();

        if (flags is not { } value) return null;

        return new DeclaredRelation((value & SoftFlag) != 0 ? RelationKind.WorksWith : RelationKind.Needs, guid);
    }

    /// <summary>Fixed custom attribute arguments in ECMA-335 order; every read returns null instead of running past the end.</summary>
    private sealed class BlobReader
    {
        private readonly byte[] blob;
        private int position;

        public BlobReader(byte[] blob) => this.blob = blob;

        public bool ReadProlog()
        {
            if (blob.Length < 2 || blob[0] != 0x01 || blob[1] != 0x00) return false;

            position = 2;

            return true;
        }

        public string? ReadString()
        {
            if (position >= blob.Length) return null;

            if (blob[position] == NullString)
            {
                position++;

                return null;
            }

            var length = ReadPackedLength();

            if (length is not { } count || position + count > blob.Length) return null;

            var text = Encoding.UTF8.GetString(blob, position, count);

            position += count;

            return text;
        }

        public int? ReadInt32()
        {
            if (position + 4 > blob.Length) return null;

            var value = BitConverter.ToInt32(blob, position);

            position += 4;

            return value;
        }

        /// <summary>One, two or four bytes, big-endian, with the top bits saying which.</summary>
        private int? ReadPackedLength()
        {
            var first = blob[position];

            if ((first & 0x80) == 0)
            {
                position += 1;

                return first;
            }

            if ((first & 0xC0) == 0x80)
            {
                if (position + 2 > blob.Length) return null;

                var twoByte = ((first & 0x3F) << 8) | blob[position + 1];

                position += 2;

                return twoByte;
            }

            if (position + 4 > blob.Length) return null;

            var fourByte = ((first & 0x1F) << 24) | (blob[position + 1] << 16) | (blob[position + 2] << 8) | blob[position + 3];

            position += 4;

            return fourByte;
        }
    }
}
