using UnityEngine;

namespace MvzMp.Presentation;

/// <summary>A small overlay driven by the mod's lobby state and actions.</summary>
internal sealed class CoopPanel : IDisposable
{
    private readonly Func<string> _status;
    private readonly Func<bool> _connected;
    private readonly Action _host;
    private readonly Action _invite;
    private readonly Action _leave;
    private bool _disposed;

    public CoopPanel(Func<string> status, Func<bool> connected, Action host, Action invite, Action leave)
    {
        _status = status ?? throw new ArgumentNullException(nameof(status));
        _connected = connected ?? throw new ArgumentNullException(nameof(connected));
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _invite = invite ?? throw new ArgumentNullException(nameof(invite));
        _leave = leave ?? throw new ArgumentNullException(nameof(leave));
    }

    // Kept for the entrypoint's regular update cycle. All GUI work belongs in OnGUI.
    public void Tick() { }

    public void Draw()
    {
        if (_disposed || Event.current is null)
            return;

        const float width = 260f;
        var x = Mathf.Max(0f, Screen.width - width - 16f);
        var rect = new Rect(x, 16f, width, 114f);
        GUILayout.BeginArea(rect, GUI.skin.box);
        try
        {
            GUILayout.Label("CO-OP", GUI.skin.label);
            GUILayout.Label(_status(), GUI.skin.label, GUILayout.Height(35f));

            GUILayout.BeginHorizontal();
            try
            {
                var connected = _connected();
                GUI.enabled = !connected;
                if (GUILayout.Button("Host")) _host();
                GUI.enabled = connected;
                if (GUILayout.Button("Invite")) _invite();
                if (GUILayout.Button("Leave")) _leave();
            }
            finally
            {
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }
        }
        finally
        {
            GUILayout.EndArea();
        }
    }

    public void Dispose() => _disposed = true;
}
