using Il2Cpp;
using Il2CppTMPro;
using MelonLoader;
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
        public List<(SpriteRenderer Source, SpriteRenderer Clone)> Renderers = new();
        public Animator Animator = null!;
        public SpriteLibrary Library = null!;
        public TextMeshPro Name = null!;
        public string DisplayName = string.Empty;
        public PlayerPose Target;
        public int CostumeId = -1;
    }

    private readonly Dictionary<ulong, Replica> _replicas = new();
    private readonly Dictionary<int, SpriteLibraryAsset> _costumeAssets = new();
    private Player? _source;
    private bool _disposed;
    private bool _reportedVisuals;
    private bool _createFailureLogged;

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

        if (replica.DisplayName != displayName)
        {
            replica.DisplayName = displayName;
            replica.Name.text = displayName.Length > 24 ? displayName[..24] : displayName;
        }
        replica.Target = pose;
        var local = _source;
        if (local != null && local.spriteRenderer != null)
        {
            foreach (var (source, clone) in replica.Renderers)
                if (source != null && clone != null)
                    clone.flipX = source.flipX ^ (local.isFacingRight != pose.FacingRight);
            var scale = local.transform.lossyScale;
            scale.x = Mathf.Abs(scale.x);
            replica.Root.transform.localScale = scale;
        }
        replica.Animator.SetBool("isRun", pose.Moving);
        if (replica.CostumeId != pose.CostumeId)
        {
            var asset = GetCostumeAsset(pose.CostumeId);
            if (asset != null)
            {
                replica.Library.spriteLibraryAsset = asset;
                replica.Library.RefreshSpriteResolvers();
                replica.CostumeId = pose.CostumeId;
            }
        }
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
        _costumeAssets.Clear();
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

        // Recreate only the paths occupied by visual components. Animation clips
        // bind by relative transform path, so putting everything on one object
        // would leave clips and SpriteResolver without their native targets.
        var root = new GameObject($"MVZ-MP peer {peer}");
        try
        {
            root.SetActive(false);
            var paths = new Dictionary<int, Transform>();
            var libraryObject = VisualTransform(local._spriteLibrary.transform, local.transform, root.transform, paths);
            var animatorObject = VisualTransform(local.animator.transform, local.transform, root.transform, paths);
            if (libraryObject == null || animatorObject == null)
            {
                UnityEngine.Object.Destroy(root);
                return null;
            }

            var library = libraryObject.gameObject.AddComponent<SpriteLibrary>();
            library.spriteLibraryAsset = local._spriteLibrary.spriteLibraryAsset;
            SpriteRenderer? renderer = null;
            var renderers = new List<(SpriteRenderer Source, SpriteRenderer Clone)>();
            foreach (var nativeRenderer in local.GetComponentsInChildren<SpriteRenderer>(true))
            {
                var visual = VisualTransform(nativeRenderer.transform, local.transform, root.transform, paths);
                if (visual == null) continue;
                var clone = visual.gameObject.AddComponent<SpriteRenderer>();
                clone.sprite = nativeRenderer.sprite;
                clone.sharedMaterial = nativeRenderer.sharedMaterial;
                clone.sortingLayerID = nativeRenderer.sortingLayerID;
                clone.sortingOrder = nativeRenderer.sortingOrder;
                clone.color = nativeRenderer.color;
                clone.flipX = nativeRenderer.flipX;
                renderers.Add((nativeRenderer, clone));
                if (nativeRenderer == local.spriteRenderer) renderer = clone;
            }
            if (renderer == null)
            {
                UnityEngine.Object.Destroy(root);
                return null;
            }

            var resolvers = new List<(SpriteResolver Source, SpriteResolver Clone)>();
            foreach (var nativeResolver in local.GetComponentsInChildren<SpriteResolver>(true))
            {
                var visual = VisualTransform(nativeResolver.transform, local.transform, root.transform, paths);
                if (visual == null || visual.GetComponent<SpriteRenderer>() == null) continue;
                var resolver = visual.gameObject.AddComponent<SpriteResolver>();
                resolvers.Add((nativeResolver, resolver));
            }
            var reportStages = !_reportedVisuals;
            if (reportStages)
            {
                MelonLogger.Msg($"COOP_VISUAL_SOURCE renderer={VisualPath(local.spriteRenderer.transform, local.transform)} animator={VisualPath(local.animator.transform, local.transform)} library={VisualPath(local._spriteLibrary.transform, local.transform)} renderers={renderers.Count} resolvers={resolvers.Count}");
            }

            var animator = animatorObject.gameObject.AddComponent<Animator>();
            animator.runtimeAnimatorController = local.animator.runtimeAnimatorController;
            animator.fireEvents = false;
            if (reportStages) MelonLogger.Msg("COOP_VISUAL_ANIMATOR_READY");

            var nameObject = new GameObject("Peer name");
            nameObject.transform.SetParent(root.transform, false);
            nameObject.transform.localPosition = new Vector3(0f, 0.75f, 0f);
            nameObject.transform.localScale = new Vector3(0.1f, 0.1f, 1f);
            var name = nameObject.AddComponent<TextMeshPro>();
            var nativeText = GameManager.Instance?._uiManager?
                .GetComponentInChildren<TextMeshProUGUI>(true);
            if (nativeText != null) name.font = nativeText.font;
            name.richText = false;
            name.fontSize = 3f;
            name.alignment = TextAlignmentOptions.Center;
            name.color = Color.white;
            name.renderer.sortingLayerID = renderer.sortingLayerID;
            name.renderer.sortingOrder = renderer.sortingOrder + 1;
            if (reportStages) MelonLogger.Msg("COOP_VISUAL_NAME_READY");
            root.SetActive(true);
            foreach (var (source, clone) in resolvers)
                clone.SetCategoryAndLabel(source.GetCategory(), source.GetLabel());
            library.RefreshSpriteResolvers();
            if (reportStages)
            {
                MelonLogger.Msg("COOP_VISUAL_READY");
                _reportedVisuals = true;
            }

            return new Replica
            {
                Root = root,
                Renderer = renderer,
                Renderers = renderers,
                Animator = animator,
                Library = library,
                Name = name,
                DisplayName = string.Empty
            };
        }
        catch (Exception e)
        {
            UnityEngine.Object.Destroy(root);
            if (!_createFailureLogged)
            {
                MelonLogger.Warning($"COOP_VISUAL_CREATE_FAILED {e}");
                _createFailureLogged = true;
            }
            return null;
        }
    }

    private static Transform? VisualTransform(Transform source, Transform sourceRoot, Transform replicaRoot,
        Dictionary<int, Transform> paths)
    {
        if (source == sourceRoot)
            return replicaRoot;
        if (source == null || source.parent == null)
            return null;
        var parent = VisualTransform(source.parent, sourceRoot, replicaRoot, paths);
        if (parent == null)
            return null;
        var id = source.GetInstanceID();
        if (paths.TryGetValue(id, out var existing))
            return existing;
        var obj = new GameObject(source.name);
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = source.localPosition;
        obj.transform.localRotation = source.localRotation;
        obj.transform.localScale = source.localScale;
        paths.Add(id, obj.transform);
        return obj.transform;
    }

    private static string VisualPath(Transform source, Transform root)
    {
        if (source == root) return ".";
        var parts = new Stack<string>();
        for (var current = source; current != null && current != root; current = current.parent)
            parts.Push(current.name);
        return string.Join("/", parts);
    }

    private SpriteLibraryAsset? GetCostumeAsset(int id)
    {
        if (id < 0 || id > 4)
            return null;
        if (_costumeAssets.TryGetValue(id, out var asset) && asset != null)
            return asset;
        var data = DataManager.Instance?.GetCostumeData((CostumeID)id);
        if (data == null || string.IsNullOrEmpty(data.SpriteLibraryPath))
            return null;
        asset = Resources.Load<SpriteLibraryAsset>(data.SpriteLibraryPath);
        if (asset != null)
            _costumeAssets[id] = asset;
        return asset;
    }
}

