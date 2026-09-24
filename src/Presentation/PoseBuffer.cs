namespace MvzMp.Presentation;

public readonly record struct PlayerPose(float X, float Y, float Z, bool FacingRight, bool Moving,
    int CostumeId, double Time = 0, int SortingLayer = 0, int SortingOrder = 0, int CharacterId = 0);

/// <summary>Plays a short history of authoritative positions; never predicts through obstacles.</summary>
internal sealed class PoseBuffer
{
    private const double Delay = .15;
    private readonly List<PlayerPose> _samples = new();
    private double _clockOffset, _lastRenderTime = double.NegativeInfinity;

    public bool Add(PlayerPose pose, double receivedAt)
    {
        if (!double.IsFinite(pose.Time) || !float.IsFinite(pose.X) || !float.IsFinite(pose.Y) || !float.IsFinite(pose.Z)) return false;
        if (_samples.Count > 0 && pose.Time <= _samples[^1].Time) return false;
        if (_samples.Count == 0 || pose.Time - _samples[^1].Time > 1 || DistanceSquared(pose, _samples[^1]) > 9)
        {
            _samples.Clear();
            _clockOffset = receivedAt - pose.Time;
            _lastRenderTime = double.NegativeInfinity;
        }
        // Minimum observed transit offset absorbs variable delivery delay without clock synchronization.
        _clockOffset = Math.Min(_clockOffset, receivedAt - pose.Time);
        _samples.Add(pose);
        if (_samples.Count > 32) _samples.RemoveAt(0);
        return true;
    }

    public PlayerPose Sample(double now)
    {
        if (_samples.Count == 0) return default;
        var time = Math.Max(_lastRenderTime, now - _clockOffset - Delay);
        _lastRenderTime = time;
        while (_samples.Count > 2 && _samples[1].Time <= time) _samples.RemoveAt(0);
        var a = _samples[0];
        if (time <= a.Time) return a;
        if (time >= _samples[^1].Time) return _samples[^1]; // Hold on packet loss, never extrapolate.
        var b = _samples[1];
        var t = (float)Math.Clamp((time - a.Time) / (b.Time - a.Time), 0, 1);
        return a with { X = a.X + (b.X - a.X) * t, Y = a.Y + (b.Y - a.Y) * t, Z = a.Z + (b.Z - a.Z) * t };
    }

    private static float DistanceSquared(PlayerPose a, PlayerPose b) =>
        (a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y) + (a.Z - b.Z) * (a.Z - b.Z);
}
