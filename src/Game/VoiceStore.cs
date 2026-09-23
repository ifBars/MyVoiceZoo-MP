using System.Security.Cryptography;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using MvzMp.State;
using UnityEngine;

namespace MvzMp.Game;

internal sealed class VoiceStore : IDisposable
{
    internal const int MaxSamples = 1_000_000;
    private readonly Dictionary<string, VoiceData> _data = new();
    private readonly Dictionary<string, AudioClip> _clips = new();
    private readonly Dictionary<int, string> _identities = new();
    private readonly HashSet<string> _created = new();
    private long _bytes;

    public string Capture(AudioClip? clip)
    {
        if (clip == null) return "";
        var id = clip.GetInstanceID();
        if (_identities.TryGetValue(id, out var existing)) return existing;
        var count = checked(clip.samples * clip.channels);
        if (count <= 0 || count > MaxSamples || clip.frequency < 8000 || clip.frequency > 192000 || clip.channels is < 1 or > 2)
            throw new InvalidDataException("Recording exceeds the supported audio limits.");
        var samples = new Il2CppStructArray<float>(count);
        if (!clip.GetData(samples, 0)) throw new InvalidDataException("Cannot read this recording.");
        var pcm = new byte[count * 2];
        for (var i = 0; i < count; i++)
        {
            var sample = float.IsFinite(samples[i]) ? samples[i] : 0;
            var value = (short)Math.Clamp((int)(sample * 32767f), short.MinValue, short.MaxValue);
            pcm[i * 2] = (byte)value;
            pcm[i * 2 + 1] = (byte)(value >> 8);
        }
        var data = new VoiceData { Frequency = clip.frequency, Channels = clip.channels, Samples = clip.samples, Pcm = pcm };
        data.Hash = Hash(data);
        Add(data);
        _clips[data.Hash] = clip;
        _identities[id] = data.Hash;
        return data.Hash;
    }

    public bool Contains(string hash) => hash.Length == 0 || _data.ContainsKey(hash);
    public VoiceData? Get(string hash) => _data.GetValueOrDefault(hash);

    public void Add(VoiceData data)
    {
        if (data.Channels is < 1 or > 2 || data.Frequency is < 8000 or > 192000 || data.Samples <= 0 ||
            (long)data.Samples * data.Channels > MaxSamples || data.Pcm.Length != (long)data.Samples * data.Channels * 2 || data.Hash != Hash(data))
            throw new InvalidDataException("Invalid recording payload.");
        if (_data.ContainsKey(data.Hash)) return;
        if (_bytes + data.Pcm.Length > 128 * 1024 * 1024) throw new InvalidDataException("Session recording cache is full.");
        _data.Add(data.Hash, data);
        _bytes += data.Pcm.Length;
    }

    public AudioClip? Clip(string hash)
    {
        if (hash.Length == 0) return null;
        if (_clips.TryGetValue(hash, out var cached) && cached != null) return cached;
        if (!_data.TryGetValue(hash, out var data)) return null;
        var samples = new Il2CppStructArray<float>(data.Pcm.Length / 2);
        for (var i = 0; i < samples.Length; i++) samples[i] = (short)(data.Pcm[i * 2] | data.Pcm[i * 2 + 1] << 8) / 32768f;
        var clip = AudioClip.Create("MVZ-MP recording", data.Samples, data.Channels, data.Frequency, false);
        if (!clip.SetData(samples, 0)) { UnityEngine.Object.Destroy(clip); throw new InvalidDataException("Cannot create recording."); }
        _clips[hash] = clip;
        _identities[clip.GetInstanceID()] = hash;
        _created.Add(hash);
        return clip;
    }

    private static string Hash(VoiceData data)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(BitConverter.GetBytes(data.Frequency));
        hash.AppendData(BitConverter.GetBytes(data.Channels));
        hash.AppendData(BitConverter.GetBytes(data.Samples));
        hash.AppendData(data.Pcm);
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    public void Dispose() => Clear();
    internal void Clear()
    {
        // Clips still assigned to native animals remain valid until the game unloads.
        _data.Clear(); _clips.Clear(); _identities.Clear(); _created.Clear(); _bytes = 0;
    }
}

