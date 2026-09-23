namespace MvzMp.Networking;

internal sealed class LobbyReassembly
{
    internal const int MaxPendingBytes = 8 * 1024 * 1024;
    internal const int MaxPendingMessages = 128;
    private static readonly TimeSpan Expiry = TimeSpan.FromSeconds(30);
    private readonly Dictionary<(ulong Sender, uint Sequence), Assembly> _assemblies = new();
    private readonly Dictionary<ulong, Receiver> _receivers = new();
    private readonly int _chunkSize;
    private int _pendingBytes;
    internal LobbyReassembly(int chunkSize = LobbyPackets.ChunkPayload) => _chunkSize = chunkSize;
    internal int PendingBytes => _pendingBytes;
    internal int PendingMessages => _assemblies.Count + _receivers.Values.Sum(receiver => receiver.Completed.Count);

    internal IEnumerable<(ulong Sender, byte[] Payload)> Accept(ulong sender, LobbyPacket packet, DateTime now)
    {
        if (sender == 0 || packet.Sequence == 0 || packet.Transient)
            yield break;

        if (!_receivers.TryGetValue(sender, out var receiver))
            _receivers[sender] = receiver = new Receiver();
        if (packet.Sequence < receiver.NextSequence || receiver.Completed.ContainsKey(packet.Sequence))
            yield break;

        var key = (sender, packet.Sequence);
        if (!_assemblies.TryGetValue(key, out var assembly))
        {
            if (packet.Total > MaxPendingBytes - _pendingBytes || PendingMessages >= MaxPendingMessages)
                yield break;
            assembly = new Assembly(packet.Total, now, _chunkSize);
            _assemblies.Add(key, assembly);
            _pendingBytes += packet.Total;
        }
        if (assembly.Total != packet.Total || !assembly.Add(packet))
            yield break;

        if (!assembly.Complete)
            yield break;
        receiver.Completed[packet.Sequence] = assembly.Buffer;
        _assemblies.Remove(key);
        // Completed messages remain counted until delivered or expired.
        foreach (var ready in Drain(sender, receiver, now))
            yield return ready;
    }

    internal IEnumerable<(ulong Sender, byte[] Payload)> Expire(DateTime now)
    {
        foreach (var (key, assembly) in _assemblies.ToArray())
        {
            if (now - assembly.Created < Expiry)
                continue;
            _pendingBytes -= assembly.Total;
            _assemblies.Remove(key);
        }
        foreach (var (sender, receiver) in _receivers)
            foreach (var ready in Drain(sender, receiver, now))
                yield return ready;
    }

    internal void RemovePeer(ulong sender)
    {
        foreach (var (key, assembly) in _assemblies.Where(entry => entry.Key.Sender == sender).ToArray())
        {
            _pendingBytes -= assembly.Total;
            _assemblies.Remove(key);
        }
        if (_receivers.Remove(sender, out var receiver))
            foreach (var payload in receiver.Completed.Values)
                _pendingBytes -= payload.Length;
    }

    internal void Clear()
    {
        _assemblies.Clear();
        _receivers.Clear();
        _pendingBytes = 0;
    }

    private IEnumerable<(ulong Sender, byte[] Payload)> Drain(ulong sender, Receiver receiver, DateTime now)
    {
        while (receiver.Completed.Remove(receiver.NextSequence, out var payload))
        {
            _pendingBytes -= payload.Length;
            receiver.NextSequence++;
            receiver.GapSince = null;
            yield return (sender, payload);
        }

        if (receiver.Completed.Count == 0)
        {
            receiver.GapSince = null;
            yield break;
        }

        receiver.GapSince ??= now;
        if (now - receiver.GapSince < Expiry)
            yield break;

        receiver.NextSequence = receiver.Completed.Keys.Min();
        receiver.GapSince = null;
        foreach (var ready in Drain(sender, receiver, now))
            yield return ready;
    }

    private sealed class Receiver
    {
        internal uint NextSequence = 1;
        internal DateTime? GapSince;
        internal SortedDictionary<uint, byte[]> Completed { get; } = new();
    }

    private sealed class Assembly
    {
        private readonly bool[] _chunks;
        private readonly int _chunkSize;
        private int _remaining;
        internal int Total { get; }
        internal DateTime Created { get; }
        internal byte[] Buffer { get; }
        internal bool Complete => _remaining == 0;

        internal Assembly(int total, DateTime now, int chunkSize)
        {
            Total = total;
            Created = now;
            Buffer = new byte[total];
            _chunkSize = chunkSize;
            _chunks = new bool[Math.Max(1, (total + chunkSize - 1) / chunkSize)];
            _remaining = _chunks.Length;
        }

        internal bool Add(LobbyPacket packet)
        {
            var index = packet.Offset / _chunkSize;
            if (index >= _chunks.Length || _chunks[index])
                return false;
            packet.Chunk.CopyTo(Buffer.AsSpan(packet.Offset));
            _chunks[index] = true;
            _remaining--;
            return true;
        }
    }
}
