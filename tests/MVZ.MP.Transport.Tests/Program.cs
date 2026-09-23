using MvzMp.Networking;

static void Check(bool condition, string message)
{
    if (!condition)
        throw new Exception(message);
}

static IEnumerable<LobbyPacket> Packets(uint sequence, byte[] payload)
{
    for (var offset = 0; offset < payload.Length || offset == 0; offset += LobbyPackets.ChunkPayload)
    {
        var length = Math.Min(LobbyPackets.ChunkPayload, payload.Length - offset);
        var bytes = LobbyPackets.Encode(sequence, 22, payload.Length, offset, payload.AsSpan(offset, length));
        Check(LobbyPackets.TryDecode(bytes, out var packet), "Encoded packet did not decode.");
        yield return packet;
        if (payload.Length == 0)
            yield break;
    }
}

var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
var payload = Enumerable.Range(0, LobbyPackets.ChunkPayload * 2 + 13).Select(i => (byte)i).ToArray();
var chunks = Packets(1, payload).ToArray();
Check(chunks.Length == 3, "Unexpected chunk count.");
var reassembly = new LobbyReassembly();
Check(!reassembly.Accept(11, chunks[2], now).Any(), "Delivered incomplete message.");
Check(!reassembly.Accept(11, chunks[0], now).Any(), "Delivered incomplete message.");
var delivered = reassembly.Accept(11, chunks[1], now).ToArray();
Check(delivered.Length == 1 && delivered[0].Payload.SequenceEqual(payload), "Out of order chunk reassembly failed.");
Check(!reassembly.Accept(11, chunks[0], now).Any(), "Duplicate message delivered.");

var second = Packets(2, new byte[] { 7 }).Single();
var third = Packets(3, new byte[] { 8 }).Single();
Check(!reassembly.Accept(11, third, now).Any(), "Delivered a later sequence early.");
Check(reassembly.Accept(11, second, now).Select(x => x.Payload[0]).SequenceEqual(new byte[] { 7, 8 }), "Sequence delivery order failed.");
Check(reassembly.Accept(33, Packets(1, Array.Empty<byte>()).Single(), now).Single().Payload.Length == 0, "Zero length payload failed.");

var invalid = LobbyPackets.Encode(1, 22, 1, 0, new byte[] { 1 });
invalid[0] = 0;
Check(!LobbyPackets.TryDecode(invalid, out _), "Corrupt magic accepted.");
Check(!LobbyPackets.TryDecode(new byte[10], out _), "Short packet accepted.");
Check(!LobbyPackets.TryDecode(new byte[LobbyPackets.MaxPacket + 1], out _), "Oversized packet accepted.");
try
{
    LobbyPackets.Encode(1, 22, LobbyPackets.MaxPayload + 1, 0, Array.Empty<byte>());
    throw new Exception("Oversized message accepted.");
}
catch (ArgumentOutOfRangeException) { }

var bounded = new LobbyReassembly();
var max = new byte[LobbyPackets.MaxPayload];
var first = Packets(1, max).First();
Check(!bounded.Accept(44, first, now).Any(), "Incomplete max message delivered.");
Check(!bounded.Accept(55, first, now).Any(), "Incomplete second max message delivered.");
Check(!bounded.Accept(66, first, now).Any(), "Exceeded pending memory limit.");
Check(bounded.PendingBytes == LobbyReassembly.MaxPendingBytes, "Pending memory exceeded limit.");
Check(bounded.PendingMessages == 2, "Excess assembly was retained.");
Check(!bounded.Expire(now.AddSeconds(31)).Any(), "Expired incomplete messages delivered.");
Check(bounded.PendingBytes == 0, "Expired assemblies retained memory.");
Check(!bounded.Accept(66, first, now.AddSeconds(31)).Any(), "Memory not released after expiry.");
bounded.RemovePeer(66);

var gap = new LobbyReassembly();
Check(!gap.Accept(77, Packets(2, new byte[] { 2 }).Single(), now).Any(), "Gap delivered early.");
Check(gap.Expire(now.AddSeconds(31)).Single().Payload[0] == 2, "Gap expiry did not release completed message.");

var boundedCount = new LobbyReassembly();
for (uint sequence = 2; sequence <= LobbyReassembly.MaxPendingMessages + 2; sequence++)
    boundedCount.Accept(88, Packets(sequence, Array.Empty<byte>()).Single(), now).ToArray();
Check(boundedCount.PendingMessages == LobbyReassembly.MaxPendingMessages, "Pending message count exceeded limit.");

Console.WriteLine("Transport framing, ordering, expiry, and limits passed.");
