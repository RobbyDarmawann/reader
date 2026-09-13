using System.IO.Compression;
using System.Text;

namespace ComicReader.Infrastructure.Extensions;

/// <summary>
/// Parser for the current Keiyoushi/Mihon extension index (index.pb).
/// The file is protobuf wire-format and is normally gzip-compressed.
/// This parser intentionally has no dependency on generated protobuf classes,
/// so ComicReader can read the repository index without importing Android code.
/// </summary>
public static class KeiyoushiIndexParser
{
    public static KeiyoushiRepository Parse(ReadOnlySpan<byte> data)
    {
        var bytes = MaybeGunzip(data);
        var root = ProtoReader.ReadMessage(bytes);

        var name = root.GetString(1) ?? string.Empty;
        var shortName = root.GetString(2) ?? string.Empty;
        var fingerprint = root.GetString(3) ?? string.Empty;

        string? website = null;
        string? discord = null;

        if (root.TryGetMessage(4, out var meta))
        {
            website = meta.GetString(1);
            discord = meta.GetString(2);
        }

        var catalog = root.GetMessage(101);
        var extensions = catalog is null
            ? Array.Empty<KeiyoushiExtension>()
            : catalog.GetMessages(1).Select(ParseExtension).ToArray();

        return new KeiyoushiRepository(
            name,
            shortName,
            fingerprint,
            website,
            discord,
            extensions);
    }

    private static KeiyoushiExtension ParseExtension(ProtoMessage message)
    {
        var name = message.GetString(1) ?? string.Empty;
        var packageName = message.GetString(2) ?? string.Empty;
        var libraryVersion = message.GetString(4) ?? string.Empty;
        var versionCode = message.GetInt64(5);
        var versionName = message.GetString(6) ?? string.Empty;
        var nsfw = (int)message.GetInt64(7);

        string? apkUrl = null;
        string? iconUrl = null;
        string? jarUrl = null;

        if (message.TryGetMessage(3, out var assets))
        {
            apkUrl = assets.GetString(1);
            iconUrl = assets.GetString(2);
            jarUrl = assets.GetString(501);
        }

        var sources = message.GetMessages(8).Select(ParseSource).ToArray();

        return new KeiyoushiExtension(
            name,
            packageName,
            libraryVersion,
            versionCode,
            versionName,
            nsfw,
            apkUrl,
            iconUrl,
            jarUrl,
            sources);
    }

    private static KeiyoushiSource ParseSource(ProtoMessage message)
    {
        var id = unchecked((ulong)message.GetInt64(1));
        var name = message.GetString(2) ?? string.Empty;
        var language = message.GetString(3) ?? string.Empty;
        var baseUrl = message.GetString(4) ?? string.Empty;
        var alternateBaseUrls = message.GetStrings(5);

        return new KeiyoushiSource(
            id,
            name,
            language,
            baseUrl,
            alternateBaseUrls);
    }

    private static byte[] MaybeGunzip(ReadOnlySpan<byte> data)
    {
        if (data.Length >= 2 && data[0] == 0x1F && data[1] == 0x8B)
        {
            using var input = new MemoryStream(data.ToArray());
            using var gzip = new GZipStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            gzip.CopyTo(output);
            return output.ToArray();
        }

        return data.ToArray();
    }

    private sealed class ProtoReader
    {
        private readonly List<ProtoField> _fields;

        private ProtoReader(List<ProtoField> fields) => _fields = fields;

        public static ProtoMessage ReadMessage(byte[] data)
        {
            var fields = new List<ProtoField>();
            var offset = 0;

            while (offset < data.Length)
            {
                var key = ReadVarint(data, ref offset);
                var fieldNumber = (int)(key >> 3);
                var wireType = (int)(key & 7);

                if (fieldNumber <= 0)
                    throw new InvalidDataException("Invalid protobuf field number.");

                switch (wireType)
                {
                    case 0:
                        fields.Add(new ProtoField(fieldNumber, wireType, ReadVarint(data, ref offset), null));
                        break;

                    case 1:
                        EnsureAvailable(data, offset, 8);
                        fields.Add(new ProtoField(fieldNumber, wireType, 0, data.AsSpan(offset, 8).ToArray()));
                        offset += 8;
                        break;

                    case 2:
                        var length = checked((int)ReadVarint(data, ref offset));
                        EnsureAvailable(data, offset, length);
                        fields.Add(new ProtoField(fieldNumber, wireType, 0, data.AsSpan(offset, length).ToArray()));
                        offset += length;
                        break;

                    case 5:
                        EnsureAvailable(data, offset, 4);
                        fields.Add(new ProtoField(fieldNumber, wireType, 0, data.AsSpan(offset, 4).ToArray()));
                        offset += 4;
                        break;

                    default:
                        throw new InvalidDataException($"Unsupported protobuf wire type {wireType}.");
                }
            }

            return new ProtoMessage(fields);
        }

        private static ulong ReadVarint(byte[] data, ref int offset)
        {
            ulong result = 0;
            var shift = 0;

            while (true)
            {
                if (offset >= data.Length || shift > 63)
                    throw new InvalidDataException("Invalid protobuf varint.");

                var b = data[offset++];
                result |= (ulong)(b & 0x7F) << shift;

                if ((b & 0x80) == 0)
                    return result;

                shift += 7;
            }
        }

        private static void EnsureAvailable(byte[] data, int offset, int count)
        {
            if (count < 0 || offset < 0 || offset + count > data.Length)
                throw new InvalidDataException("Invalid protobuf length.");
        }
    }

    private sealed record ProtoField(int Number, int WireType, ulong Varint, byte[]? Bytes);

    private sealed class ProtoMessage
    {
        private readonly List<ProtoField> _fields;

        public ProtoMessage(List<ProtoField> fields) => _fields = fields;

        public string? GetString(int number)
        {
            var field = _fields.FirstOrDefault(x => x.Number == number && x.WireType == 2);
            return field?.Bytes is null ? null : Encoding.UTF8.GetString(field.Bytes);
        }

        public IReadOnlyList<string> GetStrings(int number)
        {
            return _fields
                .Where(x => x.Number == number && x.WireType == 2 && x.Bytes is not null)
                .Select(x => Encoding.UTF8.GetString(x.Bytes!))
                .ToArray();
        }

        public long GetInt64(int number)
        {
            var field = _fields.FirstOrDefault(x => x.Number == number && x.WireType == 0);
            return field is null ? 0 : unchecked((long)field.Varint);
        }

        public ProtoMessage? GetMessage(int number)
        {
            var field = _fields.FirstOrDefault(x => x.Number == number && x.WireType == 2);
            return field?.Bytes is null ? null : ProtoReader.ReadMessage(field.Bytes);
        }

        public bool TryGetMessage(int number, out ProtoMessage message)
        {
            var result = GetMessage(number);
            if (result is null)
            {
                message = null!;
                return false;
            }

            message = result;
            return true;
        }

        public IReadOnlyList<ProtoMessage> GetMessages(int number)
        {
            return _fields
                .Where(x => x.Number == number && x.WireType == 2 && x.Bytes is not null)
                .Select(x => ProtoReader.ReadMessage(x.Bytes!))
                .ToArray();
        }
    }
}
