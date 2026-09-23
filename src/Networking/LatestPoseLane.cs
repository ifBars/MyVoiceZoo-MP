namespace MvzMp.Networking;

// Transient poses never consume a reliable sequence or wait for missing packets.
internal sealed class LatestPoseLane
{
    private readonly Dictionary<ulong, uint> _sent = new();
    private readonly Dictionary<ulong, uint> _received = new();
    private readonly Dictionary<ulong, byte[]> _pending = new();
    internal int PendingCount => _pending.Count;

    internal void Queue(ulong peer, ulong localEpoch, ulong remoteEpoch, byte[] payload)
    {
        var sequence = _sent.TryGetValue(peer, out var last) ? unchecked(last + 1) : 1;
        if (sequence == 0) sequence = 1;
        var packet = LobbyPackets.Encode(sequence, peer, localEpoch, remoteEpoch,
            payload.Length, 0, payload, transient: true);
        _sent[peer] = sequence;
        _pending[peer] = packet;
    }

    internal (ulong Peer, byte[] Bytes)[] Drain()
    {
        var result = _pending.Select(item => (item.Key, item.Value)).ToArray();
        _pending.Clear();
        return result;
    }

    internal bool Accept(ulong sender, LobbyPacket packet)
    {
        if (!packet.Transient || packet.Sequence == 0 || packet.Offset != 0 ||
            packet.Total != packet.Chunk.Length || packet.Total > LobbyPackets.MaxTransientPayload)
            return false;
        if (_received.TryGetValue(sender, out var last) && unchecked((int)(packet.Sequence - last)) <= 0)
            return false;
        _received[sender] = packet.Sequence;
        return true;
    }

    internal void RemovePeer(ulong peer)
    {
        _sent.Remove(peer);
        _received.Remove(peer);
        _pending.Remove(peer);
    }

    internal void Clear()
    {
        _sent.Clear();
        _received.Clear();
        _pending.Clear();
    }
}
