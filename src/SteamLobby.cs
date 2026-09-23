using System.Security.Cryptography;
using System.Runtime.InteropServices;
using System.Globalization;
using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppSteamworks;
using MelonLoader;
using MvzMp.Networking;
using UnityEngine;

namespace MvzMp;

internal sealed class SteamLobby : IDisposable
{
    private const string ProtocolVersion = "3";
    private const string ModBuild = "0.2.0";
    private const string ProtocolKey = "mvzmp_version";
    private const string BuildKey = "mvzmp_build";
    private const string GameKey = "mvzmp_game";
    private const string TransportKey = "mvzmp_transport";
    private const string EpochKey = "mvzmp_epoch";
    private const int NativeChannel = 27182;
    private const int MaxQueuedBytes = 8 * 1024 * 1024;
    private const int PacketsPerTick = 4;
    private static readonly TimeSpan SendStallTimeout = TimeSpan.FromSeconds(30);

    private Callback<GameLobbyJoinRequested_t>? _joinRequested;
    private Callback<LobbyEnter_t>? _lobbyEntered;
    private Callback<LobbyChatUpdate_t>? _memberChanged;
    private Callback<LobbyChatMsg_t>? _chatMessage;
    private CallResult<LobbyCreated_t>? _createResult;
    private LobbyReassembly _reassembly = new();
    private readonly Queue<QueuedPacket> _outgoing = new();
    private readonly Dictionary<ulong, uint> _nextSequence = new();
    private readonly Dictionary<ulong, ulong> _peerEpochs = new();
    private readonly HashSet<ulong> _announcedPeers = new();
    private CSteamID _lobbyId;
    private ulong _hostSteamId;
    private ulong _localEpoch;
    private ulong[] _members = Array.Empty<ulong>();
    private string? _gameFingerprint;
    private int _queuedBytes;
    private DateTime? _sendStalledSince;
    private bool _ready;
    private bool _creating;
    private bool _disposed;
    private bool _initializationFailed;
    private bool _nativeAvailable;
    private bool _useNative;
    private int _nativeSendDiagnostics;
    private int _nativeReceiveDiagnostics;
    private Callback<SteamNetworkingMessagesSessionRequest_t>? _sessionRequested;

    public bool IsReady => _ready;
    public bool IsInLobby => _lobbyId.m_SteamID != 0;
    public bool IsHost => IsInLobby && LocalSteamId == _hostSteamId;
    public ulong LocalSteamId => _ready ? SteamUser.GetSteamID().m_SteamID : 0;
    public ulong HostSteamId => _hostSteamId;
    public ulong LobbyId => _lobbyId.m_SteamID;
    public IReadOnlyList<ulong> Members => _members;

    public event Action? SessionChanged;
    public event Action<ulong>? PeerJoined;
    public event Action<ulong>? PeerLeft;
    public event Action<ulong, byte[]>? MessageReceived;

    public void TryInitialize()
    {
        if (_ready || _disposed || _initializationFailed || !SteamManager.Initialized)
            return;

        try
        {
            var gameAssembly = Path.Combine(Directory.GetParent(Application.dataPath)!.FullName, "GameAssembly.dll");
            using var file = File.OpenRead(gameAssembly);
            using var sha = SHA256.Create();
            _gameFingerprint = Application.version + ":" + Convert.ToHexString(sha.ComputeHash(file));
        }
        catch (Exception exception)
        {
            _initializationFailed = true;
            MelonLogger.Error($"Cannot fingerprint this game build: {exception.Message}");
            return;
        }

        try
        {
            _joinRequested = Callback<GameLobbyJoinRequested_t>.Create((Action<GameLobbyJoinRequested_t>)OnJoinRequested);
            _lobbyEntered = Callback<LobbyEnter_t>.Create((Action<LobbyEnter_t>)OnLobbyEntered);
            _memberChanged = Callback<LobbyChatUpdate_t>.Create((Action<LobbyChatUpdate_t>)OnMemberChanged);
            _chatMessage = Callback<LobbyChatMsg_t>.Create((Action<LobbyChatMsg_t>)OnChatMessage);
            _sessionRequested = Callback<SteamNetworkingMessagesSessionRequest_t>.Create((Action<SteamNetworkingMessagesSessionRequest_t>)OnSessionRequested);
            _createResult = CallResult<LobbyCreated_t>.Create((Action<LobbyCreated_t, bool>)OnLobbyCreated);
        }
        catch (Exception exception)
        {
            DisposeCallbacks();
            _initializationFailed = true;
            MelonLogger.Error($"Steam callback registration failed: {exception}");
            return;
        }
        try
        {
            if (!Environment.GetCommandLineArgs().Contains("--mvzmp-lobby-chat"))
            {
                var probe = new Il2CppStructArray<IntPtr>(1);
                _nativeAvailable = SteamNetworkingMessages.ReceiveMessagesOnChannel(NativeChannel, probe, 1) >= 0;
                if (probe[0] != IntPtr.Zero)
                    SteamNetworkingMessage_t.Release(probe[0]);
            }
        }
        catch (Exception exception)
        {
            MelonLogger.Warning($"SteamNetworkingMessages unavailable; lobby chat fallback: {exception.Message}");
        }
        _useNative = _nativeAvailable;
        _reassembly = new LobbyReassembly(_useNative ? LobbyPackets.NativeChunkPayload : LobbyPackets.ChunkPayload);
        _ready = true;
        MelonLogger.Msg($"Steam ready; local Steam ID {LocalSteamId}; transport={(_nativeAvailable ? "native" : "chat")}.");
        if (!TryJoinLaunchLobby() && Environment.GetCommandLineArgs().Contains("--mvzmp-host"))
            Host();
    }

    public void Tick()
    {
        if (!_ready || _disposed)
            return;
        if (IsInLobby)
        {
            var owner = SteamMatchmaking.GetLobbyOwner(_lobbyId).m_SteamID;
            if (owner == 0 || owner != _hostSteamId)
            {
                MelonLogger.Warning("Lobby owner changed or disconnected; ending the session to protect zoo authority.");
                Leave();
                return;
            }
            RefreshMembers();
            for (var i = 0; i < (_useNative ? 1 : PacketsPerTick) && _outgoing.Count != 0; i++)
            {
                var queued = _outgoing.Peek();
                if (!_members.Contains(queued.Peer))
                {
                    _outgoing.Dequeue();
                    _queuedBytes -= queued.Bytes.Length;
                    _sendStalledSince = null;
                    continue;
                }
                EResult result;
                try
                {
                    result = _useNative ? SendNative(queued) :
                        (SteamMatchmaking.SendLobbyChatMsg(_lobbyId, new Il2CppStructArray<byte>(queued.Bytes),
                            queued.Bytes.Length) ? EResult.k_EResultOK : EResult.k_EResultFail);
                }
                catch (Exception exception)
                {
                    MelonLogger.Error($"Steam packet send failed: {exception}");
                    Leave();
                    return;
                }
                if (_useNative && result == EResult.k_EResultLimitExceeded)
                {
                    if (_sendStalledSince is null)
                    {
                        _sendStalledSince = DateTime.UtcNow;
                        MelonLogger.Warning($"Steam reliable send buffer full for {queued.Peer}; waiting for capacity.");
                    }
                    else if (DateTime.UtcNow - _sendStalledSince >= SendStallTimeout)
                    {
                        MelonLogger.Error($"Steam reliable send stalled for {SendStallTimeout.TotalSeconds:0} seconds; ending session.");
                        Leave();
                        return;
                    }
                    break;
                }
                if (result != EResult.k_EResultOK)
                {
                    MelonLogger.Error($"Steam rejected packet to {queued.Peer}; message {queued.Sequence}; result={result}.");
                    Leave();
                    return;
                }
                _outgoing.Dequeue();
                _queuedBytes -= queued.Bytes.Length;
                _sendStalledSince = null;
            }
            if (_useNative)
                ReceiveNative();
        }
        foreach (var (sender, payload) in _reassembly.Expire(DateTime.UtcNow))
            MessageReceived?.Invoke(sender, payload);
    }

    public void Host()
    {
        if (!_ready || _creating || IsInLobby)
            return;
        _useNative = _nativeAvailable;
        _reassembly = new LobbyReassembly(_useNative ? LobbyPackets.NativeChunkPayload : LobbyPackets.ChunkPayload);
        _creating = true;
        _createResult!.Set(SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, 4), (Action<LobbyCreated_t, bool>)OnLobbyCreated);
        MelonLogger.Msg("Creating friends-only Steam lobby.");
    }

    public void Invite()
    {
        if (!IsInLobby)
        {
            MelonLogger.Warning("Host or join a lobby before opening invites.");
            return;
        }
        SteamFriends.ActivateGameOverlayInviteDialog(_lobbyId);
    }

    public void Leave()
    {
        if (!IsInLobby)
            return;
        var previousMembers = _members;
        var announcedPeers = _announcedPeers.ToArray();
        if (_useNative)
            foreach (var peer in previousMembers)
                if (peer != LocalSteamId)
                {
                    var identity = new SteamNetworkingIdentity();
                    identity.SetSteamID64(peer);
                    SteamNetworkingMessages.CloseSessionWithUser(ref identity);
                }
        SteamMatchmaking.LeaveLobby(_lobbyId);
        MelonLogger.Msg($"Left lobby {_lobbyId.m_SteamID}.");
        _lobbyId = default;
        _hostSteamId = 0;
        _members = Array.Empty<ulong>();
        _outgoing.Clear();
        _queuedBytes = 0;
        _sendStalledSince = null;
        _nextSequence.Clear();
        _peerEpochs.Clear();
        _announcedPeers.Clear();
        _localEpoch = 0;
        _reassembly.Clear();
        foreach (var peer in announcedPeers)
            PeerLeft?.Invoke(peer);
        SessionChanged?.Invoke();
    }

    public bool Send(ulong peer, byte[] payload, bool reliable = true)
    {
        ArgumentNullException.ThrowIfNull(payload);
        if (!IsInLobby || peer == LocalSteamId || !_members.Contains(peer) ||
            !_peerEpochs.TryGetValue(peer, out var recipientEpoch))
            return false;
        if (payload.Length > LobbyPackets.MaxPayload)
            throw new ArgumentOutOfRangeException(nameof(payload), "Application messages are limited to 4 MiB.");
        var chunkSize = _useNative ? LobbyPackets.NativeChunkPayload : LobbyPackets.ChunkPayload;
        var count = Math.Max(1, (payload.Length + chunkSize - 1) / chunkSize);
        var bytes = payload.Length + count * LobbyPackets.HeaderSize;
        if (bytes > MaxQueuedBytes - _queuedBytes)
        {
            MelonLogger.Warning($"Transport queue full; dropped {payload.Length} byte message for {peer}.");
            return false;
        }
        var sequence = _nextSequence.TryGetValue(peer, out var last) ? last + 1 : 1;
        if (sequence == 0)
            sequence = 1;
        _nextSequence[peer] = sequence;
        for (var offset = 0; offset < payload.Length || offset == 0; offset += chunkSize)
        {
            var length = Math.Min(chunkSize, payload.Length - offset);
            var packet = LobbyPackets.Encode(sequence, peer, _localEpoch, recipientEpoch,
                payload.Length, offset, payload.AsSpan(offset, length), chunkSize);
            _outgoing.Enqueue(new QueuedPacket(peer, sequence, packet));
            _queuedBytes += packet.Length;
            if (payload.Length == 0)
                break;
        }
        // Chunk reassembly and ordered delivery currently require reliable sends.
        _ = reliable;
        return true;
    }

    public bool Broadcast(byte[] payload, bool reliable = true)
    {
        var accepted = true;
        foreach (var peer in _members)
            if (peer != LocalSteamId)
                accepted &= Send(peer, payload, reliable);
        return accepted;
    }

    private void OnLobbyCreated(LobbyCreated_t result, bool failed)
    {
        _creating = false;
        if (failed || result.m_eResult != EResult.k_EResultOK)
        {
            MelonLogger.Error($"Lobby creation failed: {result.m_eResult}; IO failure={failed}.");
            return;
        }
        var id = new CSteamID(result.m_ulSteamIDLobby);
        SteamMatchmaking.SetLobbyData(id, ProtocolKey, ProtocolVersion);
        SteamMatchmaking.SetLobbyData(id, BuildKey, ModBuild);
        SteamMatchmaking.SetLobbyData(id, GameKey, _gameFingerprint!);
        SteamMatchmaking.SetLobbyData(id, TransportKey, _useNative ? "native" : "chat");
        MelonLogger.Msg($"Created lobby {id.m_SteamID}.");
    }

    private void OnLobbyEntered(LobbyEnter_t result)
    {
        if (result.m_EChatRoomEnterResponse != 1)
        {
            MelonLogger.Error($"Lobby enter failed: {result.m_EChatRoomEnterResponse}.");
            return;
        }
        var lobby = new CSteamID(result.m_ulSteamIDLobby);
        var owner = SteamMatchmaking.GetLobbyOwner(lobby).m_SteamID;
        var transport = SteamMatchmaking.GetLobbyData(lobby, TransportKey);
        if (owner == 0 ||
            SteamMatchmaking.GetLobbyData(lobby, ProtocolKey) != ProtocolVersion ||
            SteamMatchmaking.GetLobbyData(lobby, BuildKey) != ModBuild ||
            SteamMatchmaking.GetLobbyData(lobby, GameKey) != _gameFingerprint ||
            (transport != "native" && transport != "chat") ||
            (transport == "native" && !_nativeAvailable))
        {
            MelonLogger.Error("Lobby protocol, mod build, or game build mismatch; leaving.");
            SteamMatchmaking.LeaveLobby(lobby);
            return;
        }
        if (IsInLobby)
            Leave();
        _useNative = transport == "native";
        _reassembly = new LobbyReassembly(_useNative ? LobbyPackets.NativeChunkPayload : LobbyPackets.ChunkPayload);
        _lobbyId = lobby;
        _hostSteamId = owner;
        do
        {
            _localEpoch = BitConverter.ToUInt64(RandomNumberGenerator.GetBytes(8));
        } while (_localEpoch == 0);
        SteamMatchmaking.SetLobbyMemberData(_lobbyId, EpochKey, _localEpoch.ToString("X16", CultureInfo.InvariantCulture));
        RefreshMembers(notify: false);
        SessionChanged?.Invoke();
        AnnounceReadyPeers();
        MelonLogger.Msg($"Joined lobby {LobbyId}; owner={owner}; members={_members.Length}; role={(IsHost ? "host" : "guest")}.");
    }

    private void OnJoinRequested(GameLobbyJoinRequested_t request)
    {
        Leave();
        SteamMatchmaking.JoinLobby(request.m_steamIDLobby);
    }

    private void OnMemberChanged(LobbyChatUpdate_t update)
    {
        if (update.m_ulSteamIDLobby == LobbyId && IsInLobby)
            RefreshMembers();
    }

    private void RefreshMembers(bool notify = true)
    {
        var count = SteamMatchmaking.GetNumLobbyMembers(_lobbyId);
        var current = new HashSet<ulong>();
        for (var i = 0; i < count; i++)
        {
            var id = SteamMatchmaking.GetLobbyMemberByIndex(_lobbyId, i).m_SteamID;
            if (id != 0)
                current.Add(id);
        }
        var previous = _members;
        _members = current.ToArray();
        foreach (var peer in previous)
        {
            if (peer == LocalSteamId || current.Contains(peer))
                continue;
            ResetPeer(peer, notify);
            if (_useNative)
            {
                var identity = new SteamNetworkingIdentity();
                identity.SetSteamID64(peer);
                SteamNetworkingMessages.CloseSessionWithUser(ref identity);
            }
        }
        foreach (var peer in _members)
        {
            if (peer == LocalSteamId)
                continue;
            if (!TryReadPeerEpoch(peer, out var epoch))
            {
                if (_peerEpochs.ContainsKey(peer))
                    ResetPeer(peer, notify);
                continue;
            }
            if (_peerEpochs.TryGetValue(peer, out var prior) && prior != epoch)
                ResetPeer(peer, notify);
            _peerEpochs[peer] = epoch;
        }
        if (notify)
            AnnounceReadyPeers();
    }

    private bool TryReadPeerEpoch(ulong peer, out ulong epoch) =>
        ulong.TryParse(SteamMatchmaking.GetLobbyMemberData(_lobbyId, new CSteamID(peer), EpochKey),
            NumberStyles.HexNumber, CultureInfo.InvariantCulture, out epoch) && epoch != 0;

    private void ResetPeer(ulong peer, bool notify)
    {
        _nextSequence.Remove(peer);
        _reassembly.RemovePeer(peer);
        _peerEpochs.Remove(peer);
        DropQueuedForPeer(peer);
        if (_announcedPeers.Remove(peer) && notify)
            PeerLeft?.Invoke(peer);
    }

    private void DropQueuedForPeer(ulong peer)
    {
        if (_outgoing.Count == 0)
            return;
        if (_outgoing.Peek().Peer == peer)
            _sendStalledSince = null;
        var retained = _outgoing.Where(packet => packet.Peer != peer).ToArray();
        _outgoing.Clear();
        _queuedBytes = 0;
        foreach (var packet in retained)
        {
            _outgoing.Enqueue(packet);
            _queuedBytes += packet.Bytes.Length;
        }
    }

    private void AnnounceReadyPeers()
    {
        foreach (var peer in _members)
            if (peer != LocalSteamId && _peerEpochs.ContainsKey(peer) && _announcedPeers.Add(peer))
                PeerJoined?.Invoke(peer);
    }

    private void OnChatMessage(LobbyChatMsg_t message)
    {
        if (!IsInLobby || _useNative || message.m_ulSteamIDLobby != LobbyId)
            return;
        var buffer = new Il2CppStructArray<byte>(LobbyPackets.MaxPacket);
        var sender = default(CSteamID);
        var kind = default(EChatEntryType);
        var count = SteamMatchmaking.GetLobbyChatEntry(_lobbyId, (int)message.m_iChatID, out sender, buffer, buffer.Length, out kind);
        if (count < LobbyPackets.HeaderSize || count > LobbyPackets.MaxPacket ||
            kind != EChatEntryType.k_EChatEntryTypeChatMsg || sender.m_SteamID == LocalSteamId ||
            !_members.Contains(sender.m_SteamID))
            return;
        var bytes = new byte[count];
        for (var i = 0; i < count; i++)
            bytes[i] = buffer[i];
        if (!LobbyPackets.TryDecode(bytes, out var packet) || !MatchesSession(sender.m_SteamID, packet))
            return;
        foreach (var (peer, payload) in _reassembly.Accept(sender.m_SteamID, packet, DateTime.UtcNow))
            MessageReceived?.Invoke(peer, payload);
    }

    private EResult SendNative(QueuedPacket queued)
    {
        var identity = new SteamNetworkingIdentity();
        identity.SetSteamID64(queued.Peer);
        var pointer = Marshal.AllocHGlobal(queued.Bytes.Length);
        try
        {
            Marshal.Copy(queued.Bytes, 0, pointer, queued.Bytes.Length);
            var result = SteamNetworkingMessages.SendMessageToUser(ref identity, pointer, (uint)queued.Bytes.Length,
                Constants.k_nSteamNetworkingSend_Reliable, NativeChannel);
            if (_nativeSendDiagnostics++ < 8)
                MelonLogger.Msg($"Native send peer={queued.Peer} seq={queued.Sequence} bytes={queued.Bytes.Length} result={result}.");
            return result;
        }
        finally
        {
            Marshal.FreeHGlobal(pointer);
        }
    }

    private void ReceiveNative()
    {
        try
        {
            var pointers = new Il2CppStructArray<IntPtr>(16);
            for (var batch = 0; batch < 4; batch++)
            {
                var count = SteamNetworkingMessages.ReceiveMessagesOnChannel(NativeChannel, pointers, pointers.Length);
                if (count < 0 || count > pointers.Length)
                    throw new InvalidOperationException($"Invalid native receive count {count}.");
                for (var i = 0; i < count; i++)
                {
                    var pointer = pointers[i];
                    if (pointer == IntPtr.Zero)
                        continue;
                    try
                    {
                        var message = SteamNetworkingMessage_t.FromIntPtr(pointer);
                        var sender = message.m_identityPeer.GetSteamID64();
                        if (_nativeReceiveDiagnostics++ < 8)
                            MelonLogger.Msg($"Native receive peer={sender} bytes={message.m_cbSize} channel={message.m_nChannel}.");
                        if (message.m_cbSize < LobbyPackets.HeaderSize ||
                            message.m_cbSize > LobbyPackets.HeaderSize + LobbyPackets.NativeChunkPayload ||
                            !_members.Contains(sender))
                            continue;
                        var bytes = new byte[message.m_cbSize];
                        Marshal.Copy(message.m_pData, bytes, 0, bytes.Length);
                        if (!LobbyPackets.TryDecode(bytes, out var packet, LobbyPackets.NativeChunkPayload) ||
                            !MatchesSession(sender, packet))
                            continue;
                        foreach (var (peer, payload) in _reassembly.Accept(sender, packet, DateTime.UtcNow))
                            MessageReceived?.Invoke(peer, payload);
                    }
                    finally
                    {
                        SteamNetworkingMessage_t.Release(pointer);
                        pointers[i] = IntPtr.Zero;
                    }
                }
                if (count < pointers.Length)
                    break;
            }
        }
        catch (Exception exception)
        {
            MelonLogger.Error($"SteamNetworkingMessages receive failed: {exception}");
            Leave();
        }
    }

    private void OnSessionRequested(SteamNetworkingMessagesSessionRequest_t request)
    {
        var peer = request.m_identityRemote.GetSteamID64();
        var identity = request.m_identityRemote;
        MelonLogger.Msg($"Native session request peer={peer} lobby={LobbyId} member={_members.Contains(peer)}.");
        if (_useNative && IsInLobby && _members.Contains(peer))
            SteamNetworkingMessages.AcceptSessionWithUser(ref identity);
        else
            SteamNetworkingMessages.CloseSessionWithUser(ref identity);
    }

    private bool MatchesSession(ulong sender, LobbyPacket packet) =>
        _peerEpochs.TryGetValue(sender, out var epoch) &&
        LobbyPackets.MatchesSession(packet, LocalSteamId, epoch, _localEpoch);

    private bool TryJoinLaunchLobby()
    {
        var args = Environment.GetCommandLineArgs();
        for (var i = 0; i + 1 < args.Length; i++)
        {
            if (args[i] != "+connect_lobby" || !ulong.TryParse(args[i + 1], out var id))
                continue;
            SteamMatchmaking.JoinLobby(new CSteamID(id));
            MelonLogger.Msg($"Joining invite lobby {id} from launch arguments.");
            return true;
        }
        return false;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        Leave();
        _disposed = true;
        DisposeCallbacks();
    }

    private void DisposeCallbacks()
    {
        _createResult?.Dispose();
        _createResult = null;
        _sessionRequested?.Dispose();
        _sessionRequested = null;
        _chatMessage?.Dispose();
        _chatMessage = null;
        _memberChanged?.Dispose();
        _memberChanged = null;
        _lobbyEntered?.Dispose();
        _lobbyEntered = null;
        _joinRequested?.Dispose();
        _joinRequested = null;
    }

    private readonly record struct QueuedPacket(ulong Peer, uint Sequence, byte[] Bytes);
}
