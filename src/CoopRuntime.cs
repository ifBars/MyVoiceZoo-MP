using System.Text.Json;
using Il2Cpp;
using Il2CppSteamworks;
using MelonLoader;
using MvzMp.Game;
using MvzMp.Presentation;
using MvzMp.State;

namespace MvzMp;

internal sealed class CoopRuntime : IDisposable
{
    private sealed record Lease(ulong Peer, string Token, DateTime Expires);
    private readonly SteamLobby _lobby;
    internal readonly VoiceStore Voices = new();
    internal readonly ZooAdapter Zoo;
    private readonly RemotePlayers _players = new();
    private readonly Dictionary<int, Lease> _leases = new();
    private readonly Dictionary<(ulong, string), WireMessage> _replies = new();
    private readonly Dictionary<ulong, DateTime> _lastCommand = new();
    private readonly Dictionary<string, ZooCommand> _pending = new();
    private readonly Dictionary<string, DateTime> _voiceRequests = new();
    private ZooState? _solo, _latest, _waiting, _authoritative;
    private string _fingerprint = "";
    private DateTime _nextState, _nextPose, _nextHello, _lastHost, _nextLease;
    private long _revision, _appliedRevision = -1;
    private ulong _session;
    private bool _guestProtected, _joinedReady, _restoring;
    private int _editing = -1;
    private string _editLease = "";
    public string Status { get; private set; } = "Solo zoo";
    public bool IsGuest => _guestProtected || (_lobby.IsInLobby && !_lobby.IsHost);
    public bool Synchronized => _joinedReady;
    internal int EditingAnimal => _editing;
    internal event Action<string>? CommandFinished;

    public CoopRuntime(SteamLobby lobby)
    {
        _lobby = lobby;
        Zoo = new ZooAdapter(Voices);
        lobby.MessageReceived += Receive;
        lobby.SessionChanged += SessionChanged;
        lobby.PeerLeft += PeerLeft;
    }

    public void Tick()
    {
        if (!Zoo.Ready) return;
        if (_guestProtected && !_lobby.IsInLobby) RestoreSolo();
        if (!_lobby.IsInLobby) return;
        if (_session != _lobby.LobbyId) SessionChanged();
        if (!_lobby.IsHost && _solo == null)
        {
            _solo = Zoo.Capture();
            _guestProtected = true;
            _lastHost = DateTime.UtcNow;
            MelonLogger.Msg("COOP SOLO_BACKUP captured");
        }
        var now = DateTime.UtcNow;
        if (_lobby.IsHost)
        {
            foreach (var id in _leases.Where(x => x.Value.Expires < now).Select(x => x.Key).ToArray()) _leases.Remove(id);
            if (now >= _nextState) { Publish(); _nextState = now.AddSeconds(1); }
        }
        else
        {
            if (!_joinedReady && now >= _nextHello)
            {
                Send(_lobby.HostSteamId, new WireMessage { Kind = "hello" });
                _nextHello = now.AddSeconds(3);
            }
            if ((now - _lastHost).TotalSeconds > 30) { Status = "Host timed out; returning to your zoo"; _lobby.Leave(); return; }
            TryApply();
            if (_editing >= 0 && now >= _nextLease)
            {
                Send(_lobby.HostSteamId, new WireMessage { Kind = "lease", AnimalId = _editing, Lease = _editLease });
                _nextLease = now.AddSeconds(10);
            }
        }
        if (now >= _nextPose)
        {
            var pose = _players.TickLocalPose();
            if (pose is { } p)
            {
                var message = new WireMessage { Kind = "pose", X = p.X, Y = p.Y, Z = p.Z, FacingRight = p.FacingRight, Moving = p.Moving, CostumeId = p.CostumeId };
                _lobby.Broadcast(JsonSerializer.SerializeToUtf8Bytes(message), false);
            }
            _nextPose = now.AddMilliseconds(100);
        }
        _players.Tick();
    }

    private void SessionChanged()
    {
        if (_session == _lobby.LobbyId) return;
        _players.Clear(); _leases.Clear(); _replies.Clear(); _lastCommand.Clear(); _pending.Clear(); _voiceRequests.Clear();
        _joinedReady = false; _latest = _waiting = null; _fingerprint = ""; _revision = 0; _appliedRevision = -1;
        _session = _lobby.LobbyId;
        if (!_lobby.IsInLobby) { RestoreSolo(); return; }
        _guestProtected = !_lobby.IsHost;
        _nextState = _nextHello = DateTime.MinValue; _lastHost = DateTime.UtcNow;
        Status = _lobby.IsHost ? "Hosting your zoo" : "Joining shared zoo…";
    }

    private void RestoreSolo()
    {
        if (_restoring || !Zoo.Ready) return;
        _restoring = true;
        try
        {
            using var scope = new GameMutationScope();
            if (NativeHooks.PickedPosition != null) NativeHooks.Picker?.ReturnToStartAndDrop();
            if (_editing >= 0 && _guestProtected)
            {
                var animal = Zoo.Animal(_editing);
                if (animal != null) AnimalManager.Instance.Notify_OnAdoptEditProcessEnd(animal);
                GameManager.Instance._uiManager._adoptView.Hide();
                GameManager.Instance._uiManager._unlockView.Hide();
            }
            _editing = -1; _editLease = "";
            if (_solo != null) { Zoo.Apply(_solo, restoreCostume: true); _solo = null; MelonLogger.Msg("COOP SOLO_RESTORED"); }
            _guestProtected = false;
            Status = "Solo zoo";
        }
        catch (Exception e) { Status = "Could not restore your zoo; saves remain protected"; MelonLogger.Error(e); }
        finally { _restoring = false; }
    }

    private void PeerLeft(ulong peer)
    {
        _players.Remove(peer);
        foreach (var id in _leases.Where(x => x.Value.Peer == peer).Select(x => x.Key).ToArray()) _leases.Remove(id);
        _lastCommand.Remove(peer);
        foreach (var key in _replies.Keys.Where(x => x.Item1 == peer).ToArray()) _replies.Remove(key);
    }

    private void Receive(ulong sender, byte[] bytes)
    {
        if (!_lobby.IsInLobby || !_lobby.Members.Contains(sender) || bytes.Length > 4 * 1024 * 1024) return;
        try
        {
            var m = JsonSerializer.Deserialize<WireMessage>(bytes);
            if (m == null) return;
            if (m.Kind == "pose")
            {
                if (!Zoo.Ready || !ZooAdapter.ValidPosition(m.X, m.Y, m.Z) || !Enum.IsDefined(typeof(CostumeID), m.CostumeId)) return;
                _players.Apply(sender, SteamFriends.GetFriendPersonaName(new CSteamID(sender)), new PlayerPose(m.X, m.Y, m.Z, m.FacingRight, m.Moving, m.CostumeId));
                return;
            }
            if (_lobby.IsHost)
            {
                if (!Zoo.Ready) return;
                switch (m.Kind)
                {
                    case "hello":
                        Publish(); SendSnapshot(sender); MelonLogger.Msg($"COOP PEER_READY {sender}"); break;
                    case "voice-request":
                        if (m.Text.Length != 64) return;
                        var voice = Voices.Get(m.Text);
                        if (voice != null) Send(sender, new WireMessage { Kind = "voice", Voice = voice });
                        break;
                    case "command": Execute(sender, m); break;
                    case "lease":
                        if (_leases.TryGetValue(m.AnimalId, out var lease) && lease.Peer == sender && lease.Token == m.Lease)
                            _leases[m.AnimalId] = lease with { Expires = DateTime.UtcNow.AddSeconds(45) };
                        break;
                }
                return;
            }
            if (sender != _lobby.HostSteamId || _solo == null) return;
            _lastHost = DateTime.UtcNow;
            switch (m.Kind)
            {
                case "state":
                    if (m.State == null || m.State.Revision < _appliedRevision) return;
                    Zoo.Validate(m.State); _waiting = m.State; TryApply(); break;
                case "voice":
                    if (m.Voice == null || !_voiceRequests.ContainsKey(m.Voice.Hash)) return;
                    Voices.Add(m.Voice); _voiceRequests.Remove(m.Voice.Hash); TryApply(); break;
                case "result":
                    if (!_pending.Remove(m.Request, out var command)) return;
                    if (m.Text != "ok") { Rollback(); Status = m.Text; CommandFinished?.Invoke("rejected:" + command.Kind); return; }
                    Status = $"Shared zoo · {_lobby.Members.Count} players";
                    if (command.Kind is "adopt" or "edit")
                    {
                        _editing = command.AnimalId; _editLease = m.Lease;
                        using var scope = new GameMutationScope();
                        Zoo.BeginEditor(_editing, command.Kind == "adopt");
                    }
                    CommandFinished?.Invoke(command.Kind);
                    MelonLogger.Msg($"COOP COMMAND_OK {command.Kind} {command.AnimalId}");
                    break;
                case "heartbeat": break;
            }
        }
        catch (Exception e) { MelonLogger.Warning($"Rejected co-op message: {e.Message}"); }
    }

    private void Publish()
    {
        var state = Zoo.Capture();
        var fingerprint = JsonSerializer.Serialize(state);
        if (fingerprint != _fingerprint)
        {
            _fingerprint = fingerprint; state.Revision = ++_revision; _latest = state;
            _lobby.Broadcast(JsonSerializer.SerializeToUtf8Bytes(new WireMessage { Kind = "state", State = state }));
        }
        else _lobby.Broadcast(JsonSerializer.SerializeToUtf8Bytes(new WireMessage { Kind = "heartbeat" }));
        Status = $"Hosting zoo · {_lobby.Members.Count} players";
    }
    private void SendSnapshot(ulong peer) { if (_latest != null) Send(peer, new WireMessage { Kind = "state", State = _latest }); }
    private void TryApply()
    {
        if (_waiting == null || _solo == null) return;
        foreach (var hash in _waiting.Animals.Select(a => a.Voice).Where(h => !Voices.Contains(h)).Distinct())
        {
            if (!_voiceRequests.TryGetValue(hash, out var time) || (DateTime.UtcNow - time).TotalSeconds > 5)
            {
                if (Send(_lobby.HostSteamId, new WireMessage { Kind = "voice-request", Text = hash })) _voiceRequests[hash] = DateTime.UtcNow;
            }
            return; // One large clip in flight at a time.
        }
        Zoo.Apply(_waiting, _editing);
        _authoritative = _waiting;
        _appliedRevision = _waiting.Revision; _waiting = null;
        if (!_joinedReady) MelonLogger.Msg($"COOP SYNC_READY revision={_appliedRevision}");
        _joinedReady = true;
    }
    private bool Send(ulong peer, WireMessage message) => _lobby.Send(peer, JsonSerializer.SerializeToUtf8Bytes(message));

    internal bool Submit(ZooCommand command)
    {
        if (!IsGuest || !_joinedReady || _pending.Count >= 8) { Status = "Wait for the shared zoo to finish syncing"; return false; }
        if (_pending.Values.Any(c => c.Kind == command.Kind && c.AnimalId == command.AnimalId && c.Value == command.Value)) return false;
        var request = Guid.NewGuid().ToString("N");
        if (!Send(_lobby.HostSteamId, new WireMessage { Kind = "command", Request = request, Command = command })) { Rollback(); Status = "Network is busy; try again"; return false; }
        _pending[request] = command; Status = "Waiting for host…"; return true;
    }

    internal bool BeginEdit(int id, bool adoption)
    {
        if (GameMutationScope.Active || !_lobby.IsInLobby) return true;
        if (IsGuest) { Submit(new ZooCommand { Kind = adoption ? "adopt" : "edit", AnimalId = id }); return false; }
        if (_leases.ContainsKey(id)) { Status = "Another player is editing that animal"; return false; }
        var animal = Zoo.Animal(id);
        if (animal == null || !Wallet.Instance.HasEnoughGold(adoption ? animal.AnimalData.AdoptCost : animal.AnimalData.EditCost)) return true;
        _leases[id] = new Lease(_lobby.LocalSteamId, "local", DateTime.MaxValue);
        _editing = id; return true;
    }
    internal void EndEdit(AdoptView view)
    {
        if (_restoring || view._animal == null || _editing < 0) return;
        var animal = view._animal;
        if (IsGuest)
        {
            var hash = Voices.Capture(animal.Voice);
            Submit(new ZooCommand { Kind = "commit", AnimalId = _editing, Lease = _editLease, Name = animal.Name ?? "", Voice = Voices.Get(hash) });
        }
        else { _leases.Remove(_editing); Zoo.Save(); }
        _editing = -1; _editLease = "";
        if (IsGuest && _authoritative != null) { _waiting = _authoritative; TryApply(); }
    }
    private void Rollback()
    {
        if (_authoritative != null) { _waiting = _authoritative; TryApply(); }
    }
    internal bool Purchase(string kind, int value)
    {
        if (GameMutationScope.Active || !IsGuest) return true;
        Submit(new ZooCommand { Kind = kind, Value = value }); return false;
    }
    internal void Moved(AnimalPos? pos)
    {
        if (pos == null || GameMutationScope.Active || !IsGuest) return;
        var dict = GameManager.Instance._animalPrefabController._animalPosDict;
        foreach (var pair in dict)
            if (pair.Value == pos)
            {
                var p = pos.transform.position;
                Submit(new ZooCommand { Kind = "move", AnimalId = pair.Key, X = p.x, Y = p.y, Z = p.z, SortingOrder = pos.GetCurrentSortingOrder() }); return;
            }
    }

    private void Execute(ulong peer, WireMessage message)
    {
        if (message.Request.Length != 32 || message.Command == null) return;
        var key = (peer, message.Request);
        if (_replies.TryGetValue(key, out var previous)) { Send(peer, previous); return; }
        var reply = new WireMessage { Kind = "result", Request = message.Request, Text = "ok" };
        try
        {
            var now = DateTime.UtcNow;
            if (_lastCommand.TryGetValue(peer, out var last) && (now - last).TotalMilliseconds < 100) throw new InvalidOperationException("Please wait before another action.");
            _lastCommand[peer] = now;
            var c = message.Command;
            using var scope = new GameMutationScope();
            switch (c.Kind)
            {
                case "adopt": case "edit":
                    var animal = Zoo.Animal(c.AnimalId) ?? throw new InvalidOperationException("Unknown animal.");
                    if (_leases.ContainsKey(c.AnimalId)) throw new InvalidOperationException("Another player is editing that animal.");
                    if (animal.IsCollected != (c.Kind == "edit")) throw new InvalidOperationException("Animal state changed; try again.");
                    var cost = c.Kind == "adopt" ? animal.AnimalData.AdoptCost : animal.AnimalData.EditCost;
                    if (!Wallet.Instance.HasEnoughGold(cost)) throw new InvalidOperationException("Not enough gold.");
                    Wallet.Instance.ReduceGold(cost);
                    if (c.Kind == "adopt") { AnimalManager.Instance.AnimalCollectStateChange(c.AnimalId, true); TutorialManager.Instance.TryEndTutorial(); }
                    reply.Lease = Guid.NewGuid().ToString("N");
                    _leases[c.AnimalId] = new Lease(peer, reply.Lease, now.AddSeconds(45));
                    break;
                case "commit":
                    if (!_leases.TryGetValue(c.AnimalId, out var lease) || lease.Peer != peer || lease.Token != c.Lease) throw new InvalidOperationException("The edit expired; open the animal again.");
                    if (c.Name == null || c.Name.Length > AdoptView.NAME_LENGTH_LIMIT) throw new InvalidDataException("Animal name is too long.");
                    var edited = Zoo.Animal(c.AnimalId)!;
                    if (c.Voice != null) Voices.Add(c.Voice);
                    edited.SetName(c.Name);
                    if (c.Voice != null) edited.SetVoice(Voices.Clip(c.Voice.Hash), true);
                    CostumeManager.Instance.UpdateAnimalsCostumeVoice();
                    _leases.Remove(c.AnimalId); break;
                case "move":
                    if (_leases.ContainsKey(c.AnimalId)) throw new InvalidOperationException("That animal is being edited.");
                    Zoo.Move(c); break;
                case "area": case "camp": case "costume": Zoo.Purchase(c.Kind, c.Value); break;
                default: throw new InvalidDataException("Unknown zoo action.");
            }
            Zoo.Save(); Publish();
            MelonLogger.Msg($"COOP HOST_APPLIED {c.Kind} {c.AnimalId} peer={peer}");
        }
        catch (Exception e) { SendSnapshot(peer); reply.Text = e.Message; MelonLogger.Warning($"Co-op action rejected: {e.Message}"); }
        if (_replies.Count >= 512) _replies.Remove(_replies.Keys.First());
        _replies[key] = reply; Send(peer, reply);
    }

    public void Dispose()
    {
        _lobby.Leave(); RestoreSolo(); _players.Dispose(); Voices.Dispose();
        _lobby.MessageReceived -= Receive; _lobby.SessionChanged -= SessionChanged; _lobby.PeerLeft -= PeerLeft;
    }
}



