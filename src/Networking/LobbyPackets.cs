using System.Buffers.Binary;

namespace MvzMp.Networking;

// Steam lobby chat has a 4 KiB message limit. Keep room for Steam's own framing.
internal static class LobbyPackets
{
    internal const int MaxPayload = 4 * 1024 * 1024;
    internal const int ChunkPayload = 3800;
    internal const int NativeChunkPayload = 64 * 1024;
    internal const int HeaderSize = 46;
    internal const int MaxTransientPayload = 1200;
    internal const int MaxPacket = HeaderSize + ChunkPayload;
    private const uint Magic = 0x34505A4D; // MZP4
    private const byte Version = 4;

    internal static byte[] Encode(uint sequence, ulong recipient, ulong senderEpoch, ulong recipientEpoch, int total, int offset,
        ReadOnlySpan<byte> chunk, int chunkSize = ChunkPayload, bool transient = false)
    {
        if (chunkSize != ChunkPayload && chunkSize != NativeChunkPayload)
            throw new ArgumentOutOfRangeException(nameof(chunkSize));
        if ((transient && (total > MaxTransientPayload || offset != 0 || chunk.Length != total)) ||
            sequence == 0 || recipient == 0 || senderEpoch == 0 || recipientEpoch == 0 ||
            total < 0 || total > MaxPayload ||
            offset < 0 || offset > total || chunk.Length > chunkSize ||
            (total == 0 ? offset != 0 || chunk.Length != 0 :
                offset % chunkSize != 0 || chunk.Length != Math.Min(chunkSize, total - offset)))
            throw new ArgumentOutOfRangeException(nameof(chunk), "Invalid packet dimensions.");

        var packet = new byte[HeaderSize + chunk.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(packet, Magic);
        packet[4] = Version;
        packet[5] = transient ? (byte)1 : (byte)0;
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(6), sequence);
        BinaryPrimitives.WriteUInt64LittleEndian(packet.AsSpan(10), recipient);
        BinaryPrimitives.WriteUInt64LittleEndian(packet.AsSpan(18), senderEpoch);
        BinaryPrimitives.WriteUInt64LittleEndian(packet.AsSpan(26), recipientEpoch);
        BinaryPrimitives.WriteInt32LittleEndian(packet.AsSpan(34), total);
        BinaryPrimitives.WriteInt32LittleEndian(packet.AsSpan(38), offset);
        BinaryPrimitives.WriteInt32LittleEndian(packet.AsSpan(42), chunk.Length);
        chunk.CopyTo(packet.AsSpan(HeaderSize));
        return packet;
    }

    internal static bool TryDecode(ReadOnlySpan<byte> packet, out LobbyPacket decoded, int chunkSize = ChunkPayload)
    {
        decoded = default;
        if ((chunkSize != ChunkPayload && chunkSize != NativeChunkPayload) ||
            packet.Length < HeaderSize || packet.Length > HeaderSize + chunkSize ||
            BinaryPrimitives.ReadUInt32LittleEndian(packet) != Magic || packet[4] != Version || packet[5] > 1)
            return false;

        var transient = packet[5] == 1;
        var sequence = BinaryPrimitives.ReadUInt32LittleEndian(packet.Slice(6));
        var recipient = BinaryPrimitives.ReadUInt64LittleEndian(packet.Slice(10));
        var senderEpoch = BinaryPrimitives.ReadUInt64LittleEndian(packet.Slice(18));
        var recipientEpoch = BinaryPrimitives.ReadUInt64LittleEndian(packet.Slice(26));
        var total = BinaryPrimitives.ReadInt32LittleEndian(packet.Slice(34));
        var offset = BinaryPrimitives.ReadInt32LittleEndian(packet.Slice(38));
        var length = BinaryPrimitives.ReadInt32LittleEndian(packet.Slice(42));
        if (sequence == 0 || recipient == 0 || senderEpoch == 0 || recipientEpoch == 0 ||
            total < 0 || total > MaxPayload ||
            offset < 0 || offset > total || length != packet.Length - HeaderSize ||
            (transient && (total > MaxTransientPayload || offset != 0 || length != total)) ||
            (total == 0 ? offset != 0 || length != 0 :
                offset % chunkSize != 0 || length != Math.Min(chunkSize, total - offset)))
            return false;

        decoded = new LobbyPacket(sequence, recipient, senderEpoch, recipientEpoch, total, offset, packet.Slice(HeaderSize).ToArray(), transient);
        return true;
    }

    internal static bool MatchesSession(LobbyPacket packet, ulong recipient, ulong senderEpoch, ulong recipientEpoch) =>
        packet.Recipient == recipient && packet.SenderEpoch == senderEpoch && packet.RecipientEpoch == recipientEpoch;
}

internal readonly record struct LobbyPacket(uint Sequence, ulong Recipient, ulong SenderEpoch, ulong RecipientEpoch,
    int Total, int Offset, byte[] Chunk, bool Transient = false);
