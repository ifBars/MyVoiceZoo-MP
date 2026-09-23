using System.Text;
using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppSteamworks;
using MelonLoader;

namespace MvzMp;

internal sealed class SteamLobby : IDisposable
{
    private const string Protocol = "MVZMP|1|";
    private const int MaxMessageBytes = 4096;
    private const string VersionKey = "mvzmp_version";

    private Callback<GameLobbyJoinRequested_t>? _joinRequested;
    private Callback<LobbyEnter_t>? _lobbyEntered;
    private Callback<LobbyChatUpdate_t>? _memberChanged;
    private Callback<LobbyChatMsg_t>? _chatMessage;
    private CallResult<LobbyCreated_t>? _createResult;
    private CSteamID _lobbyId;
    private bool _ready;

    public bool IsReady => _ready;

    public void TryInitialize()
    {
        if (_ready || !SteamManager.Initialized)
            return;

        _joinRequested = Callback<GameLobbyJoinRequested_t>.Create((Action<GameLobbyJoinRequested_t>)OnJoinRequested);
        _lobbyEntered = Callback<LobbyEnter_t>.Create((Action<LobbyEnter_t>)OnLobbyEntered);
        _memberChanged = Callback<LobbyChatUpdate_t>.Create((Action<LobbyChatUpdate_t>)OnMemberChanged);
        _chatMessage = Callback<LobbyChatMsg_t>.Create((Action<LobbyChatMsg_t>)OnChatMessage);
        _createResult = CallResult<LobbyCreated_t>.Create((Action<LobbyCreated_t, bool>)OnLobbyCreated);
        _ready = true;
        MelonLogger.Msg($"Steam ready; local Steam ID {SteamUser.GetSteamID().m_SteamID}.");
        if (!TryJoinLaunchLobby() && Environment.GetCommandLineArgs().Contains("--mvzmp-host"))
            Host();
    }

    public void Host()
    {
        if (!_ready || _lobbyId.m_SteamID != 0)
            return;

        _createResult!.Set(SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, 4), (Action<LobbyCreated_t, bool>)OnLobbyCreated);
        MelonLogger.Msg("Creating friends-only Steam lobby.");
    }

    public void Invite()
    {
        if (_lobbyId.m_SteamID == 0)
        {
            MelonLogger.Warning("Host a lobby before opening invites.");
            return;
        }

        SteamFriends.ActivateGameOverlayInviteDialog(_lobbyId);
    }

    public void Leave()
    {
        if (_lobbyId.m_SteamID == 0)
            return;

        SteamMatchmaking.LeaveLobby(_lobbyId);
        MelonLogger.Msg($"Left lobby {_lobbyId.m_SteamID}.");
        _lobbyId = default;
    }

    private void OnLobbyCreated(LobbyCreated_t result, bool failed)
    {
        if (failed || result.m_eResult != EResult.k_EResultOK)
        {
            MelonLogger.Error($"Lobby creation failed: {result.m_eResult}; IO failure={failed}.");
            return;
        }

        var id = new CSteamID(result.m_ulSteamIDLobby);
        SteamMatchmaking.SetLobbyData(id, VersionKey, "1");
        MelonLogger.Msg($"Created lobby {id.m_SteamID}.");
    }

    private void OnLobbyEntered(LobbyEnter_t result)
    {
        if (result.m_EChatRoomEnterResponse != 1)
        {
            MelonLogger.Error($"Lobby enter failed: {result.m_EChatRoomEnterResponse}.");
            return;
        }

        _lobbyId = new CSteamID(result.m_ulSteamIDLobby);
        var owner = SteamMatchmaking.GetLobbyOwner(_lobbyId);
        var isHost = owner == SteamUser.GetSteamID();
        var version = SteamMatchmaking.GetLobbyData(_lobbyId, VersionKey);
        MelonLogger.Msg($"Joined lobby {_lobbyId.m_SteamID}; owner={owner.m_SteamID}; members={SteamMatchmaking.GetNumLobbyMembers(_lobbyId)}; role={(isHost ? "host" : "guest")}; protocol={version}.");

        if (!isHost && version != "1")
        {
            MelonLogger.Error("Lobby protocol mismatch; leaving.");
            Leave();
            return;
        }

        Send("HELLO");
    }

    private void OnJoinRequested(GameLobbyJoinRequested_t request)
    {
        Leave();
        SteamMatchmaking.JoinLobby(request.m_steamIDLobby);
    }

    private void OnMemberChanged(LobbyChatUpdate_t update)
    {
        if (update.m_ulSteamIDLobby != _lobbyId.m_SteamID)
            return;

        var members = SteamMatchmaking.GetNumLobbyMembers(_lobbyId);
        MelonLogger.Msg($"Lobby member update; members={members}; changed={update.m_ulSteamIDUserChanged}.");
        if (members > 1 && SteamMatchmaking.GetLobbyOwner(_lobbyId) == SteamUser.GetSteamID())
            Send("HELLO");
    }

    private void OnChatMessage(LobbyChatMsg_t message)
    {
        if (message.m_ulSteamIDLobby != _lobbyId.m_SteamID)
            return;

        var buffer = new Il2CppStructArray<byte>(MaxMessageBytes);
        var sender = default(CSteamID);
        var kind = default(EChatEntryType);
        var count = SteamMatchmaking.GetLobbyChatEntry(_lobbyId, (int)message.m_iChatID, out sender, buffer, buffer.Length, out kind);
        if (count <= 0 || count > MaxMessageBytes || sender == SteamUser.GetSteamID())
            return;

        var bytes = new byte[count];
        for (var i = 0; i < count; i++)
            bytes[i] = buffer[i];
        var text = Encoding.UTF8.GetString(bytes);
        if (text == Protocol + "HELLO")
            MelonLogger.Msg($"Handshake from {sender.m_SteamID} in lobby {_lobbyId.m_SteamID}.");
    }

    private void Send(string command)
    {
        if (_lobbyId.m_SteamID == 0)
            return;

        var bytes = Encoding.UTF8.GetBytes(Protocol + command);
        var buffer = new Il2CppStructArray<byte>(bytes);
        if (!SteamMatchmaking.SendLobbyChatMsg(_lobbyId, buffer, bytes.Length))
            MelonLogger.Warning("Failed to send lobby handshake.");
    }

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
        Leave();
        _createResult?.Dispose();
        _chatMessage?.Dispose();
        _memberChanged?.Dispose();
        _lobbyEntered?.Dispose();
        _joinRequested?.Dispose();
    }
}
