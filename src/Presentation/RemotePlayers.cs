using Il2Cpp;
using UnityEngine;
using UnityEngine.U2D.Animation;

namespace MvzMp.Presentation;

public readonly record struct PlayerPose(float X, float Y, float Z, bool FacingRight, bool Moving, int CostumeId);

/// <summary>Visual-only replicas. They do not clone Player, physics, input, camera, or audio.</summary>
internal sealed class RemotePlayers : IDisposable
{
    private sealed class Replica
    {
        public GameObject Root = null!;
        public SpriteRenderer Renderer = null!;
        public Animator Animator = null!;
        public SpriteLibrary Library = null!;
        public string DisplayName = string.Empty;
        public PlayerPose Target;
    }

    private readonly Dictionary<ulong, Replica> _replicas = new();
    private Player? _source;
    private bool _disposed;

    public PlayerPose? TickLocalPose()
    {
        var player = GameManager.Instance?._player;
        if (player == null || player.spriteRenderer == null)
            return null;

        var position = player.transform.position;
        var moving = player._input.sqrMagnitude > 0.0001f;
        var costume = CostumeManager.Instance;
        return new PlayerPose(position.x, position.y, position.z, player.isFacingRight, moving,
            costume == null ? 0 : (int)costume.EquippedCostumeID);
    }

    public void Apply(ulong peer, string displayName, PlayerPose pose)
    {
        if (_disposed || peer == 0)
            return;

        if (!_replicas.TryGetValue(peer, out var replica))
        {
            replica = Create(peer);
            if (replica is null)
                return;
            _replicas.Add(peer, replica);
            replica.Root.transform.position = new Vector3(pose.X, pose.Y, pose.Z);
        }

        replica.DisplayName = displayName;
        replica.Target = pose;
        var local = _source;
        if (local != null && local.spriteRenderer != null)
        {
            replica.Renderer.flipX = local.spriteRenderer.flipX;
            var scale = local.spriteRenderer.transform.lossyScale;
            if (local.isFacingRight != pose.FacingRight)
                scale.x = -scale.x;
            replica.Root.transform.localScale = scale;
        }
        replica.Animator.speed = pose.Moving ? 1f : 0f;
    }

    public void Tick()
    {
        if (_disposed)
            return;

        var local = GameManager.Instance?._player;
        if (local == null || local.spriteRenderer == null)
        {
            Clear();
            return;
        }

        if (_source != local)
        {
            Clear();
            _source = local;
            return;
        }

        foreach (var replica in _replicas.Values)
            if (replica.Root == null)
            {
                Clear();
                return;
            }

        foreach (var replica in _replicas.Values)
        {
            var destination = new Vector3(replica.Target.X, replica.Target.Y, replica.Target.Z);
            replica.Root.transform.position = Vector3.Lerp(replica.Root.transform.position, destination,
                Mathf.Clamp01(Time.deltaTime * 12f));

            // The source renderer and library are game-owned assets. A separate GameObject
            // holds only visual components and remains inert from a gameplay perspective.
            if (replica.Renderer.sprite == null)
                replica.Renderer.sprite = local.spriteRenderer.sprite;
            if (replica.Library.spriteLibraryAsset == null && local._spriteLibrary != null)
                replica.Library.spriteLibraryAsset = local._spriteLibrary.spriteLibraryAsset;
        }
    }

    public void Remove(ulong peer)
    {
        if (!_replicas.Remove(peer, out var replica))
            return;
        UnityEngine.Object.Destroy(replica.Root);
    }

    public void Clear()
    {
        foreach (var replica in _replicas.Values)
            UnityEngine.Object.Destroy(replica.Root);
        _replicas.Clear();
        _source = null;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        Clear();
    }

    private Replica? Create(ulong peer)
    {
        var local = GameManager.Instance?._player;
        if (local == null || local.spriteRenderer == null || local.animator == null || local._spriteLibrary == null)
            return null;

        if (_source != local)
        {
            Clear();
            _source = local;
        }

        var root = new GameObject($"MVZ-MP peer {peer}");
        var renderer = root.AddComponent<SpriteRenderer>();
        renderer.sprite = local.spriteRenderer.sprite;
        renderer.sharedMaterial = local.spriteRenderer.sharedMaterial;
        renderer.sortingLayerID = local.spriteRenderer.sortingLayerID;
        renderer.sortingOrder = local.spriteRenderer.sortingOrder;
        renderer.color = local.spriteRenderer.color;

        var library = root.AddComponent<SpriteLibrary>();
        library.spriteLibraryAsset = local._spriteLibrary.spriteLibraryAsset;
        var animator = root.AddComponent<Animator>();
        animator.runtimeAnimatorController = local.animator.runtimeAnimatorController;

        return new Replica
        {
            Root = root,
            Renderer = renderer,
            Animator = animator,
            Library = library,
            DisplayName = string.Empty
        };
    }
}
