using System.Buffers.Binary;
using System.Text.Json;

namespace TabletExtend.Core;

public sealed record Hello(int Version, string Token);
public sealed record Frame(long Sequence, byte[] Jpeg);

public static class Wire
{
    public const int Version = 1;
    public const byte HelloType = 1, StatusType = 2, FrameType = 3, AckType = 4, ErrorType = 5;
    public const int MaxJpeg = 16 * 1024 * 1024;
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public static byte[] Encode<T>(T value) => JsonSerializer.SerializeToUtf8Bytes(value, Json);

    public static async Task Write(Stream stream, byte type, ReadOnlyMemory<byte> body, CancellationToken ct)
    {
        byte[] header = new byte[5]; header[0] = type;
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(1), body.Length);
        await stream.WriteAsync(header, ct);
        await stream.WriteAsync(body, ct);
    }

    public static async Task<(byte Type, byte[] Body)> Read(Stream stream, int limit, CancellationToken ct)
    {
        byte[] header = new byte[5];
        await stream.ReadExactlyAsync(header, ct);
        int length = BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(1));
        if (length < 0 || length > limit) throw new InvalidDataException("Invalid packet size");
        byte[] body = new byte[length];
        await stream.ReadExactlyAsync(body, ct);
        return (header[0], body);
    }

    public static byte[] FrameBody(Frame frame)
    {
        if (frame.Jpeg.Length > MaxJpeg) throw new InvalidDataException("Frame too large");
        byte[] body = new byte[8 + frame.Jpeg.Length];
        BinaryPrimitives.WriteInt64BigEndian(body, frame.Sequence);
        frame.Jpeg.CopyTo(body, 8);
        return body;
    }
    public static byte[] AckBody(long sequence)
    {
        byte[] body = new byte[8]; BinaryPrimitives.WriteInt64BigEndian(body, sequence); return body;
    }
    public static bool IsAck((byte Type, byte[] Body) packet, long sequence) =>
        packet.Type == AckType && packet.Body.Length == 8 && BinaryPrimitives.ReadInt64BigEndian(packet.Body) == sequence;
}
