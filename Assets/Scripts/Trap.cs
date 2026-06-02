using UnityEngine;

public enum TrapFacing
{
    Up,
    Down
}

public class Trap : MonoBehaviour
{
    [SerializeField] private GameObject trap;
    [SerializeField] private GameObject downTrap;
    [SerializeField] private float uptime = 1f;
    [SerializeField] private float downtime;
    [SerializeField] private TrapFacing facing = TrapFacing.Up;
    [SerializeField] private float animationFrameRate = 12f;
    [SerializeField] private float segmentWidth = 1f;
    [SerializeField] private int length = 3;
    [SerializeField] private bool useSpriteRendererSizeAsLength = true;
    [SerializeField] private bool syncColliderSize = true;
    [SerializeField] private Transform visualRoot;
    [SerializeField] private Sprite[] leftFrames;
    [SerializeField] private Sprite[] middleFrames;
    [SerializeField] private Sprite[] rightFrames;

    private float cycleTimer;
    private bool isUp = true;
    private SpriteRenderer sourceRenderer;
    private SpriteRenderer[] visualRenderers;

    public bool IsUp
    {
        get { return isUp; }
    }

    private void Reset()
    {
        CacheDefaultObjects();
        CacheVisualSource();
        SyncLengthFromSpriteRenderer();
        EnsureVisualSegments();
        ApplyState();
    }

    private void OnEnable()
    {
        isUp = true;
        cycleTimer = 0f;
        CacheDefaultObjects();
        CacheVisualSource();
        SyncLengthFromSpriteRenderer();
        EnsureVisualSegments();
        ApplyState();
    }

    private void OnValidate()
    {
        uptime = Mathf.Max(0f, uptime);
        downtime = Mathf.Max(0f, downtime);
        animationFrameRate = Mathf.Max(0f, animationFrameRate);
        segmentWidth = Mathf.Max(0.01f, segmentWidth);
        length = Mathf.Max(3, length);
        CacheDefaultObjects();
        CacheVisualSource();
        SyncLengthFromSpriteRenderer();

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            UnityEditor.EditorApplication.delayCall -= RebuildVisualsInEditor;
            UnityEditor.EditorApplication.delayCall += RebuildVisualsInEditor;
            return;
        }
#endif

        EnsureVisualSegments();
        ApplyState();
    }

    private void Update()
    {
        if (downtime <= 0f || uptime <= 0f)
        {
            SetUpState(true);
            cycleTimer = 0f;
            ApplyState();
            return;
        }

        cycleTimer += GameTime.DeltaTime;
        if (isUp && cycleTimer >= uptime)
        {
            SetUpState(false);
            cycleTimer = 0f;
            return;
        }

        if (!isUp && cycleTimer >= downtime)
        {
            SetUpState(true);
            cycleTimer = 0f;
            return;
        }

        ApplyState();
    }

    private void SetUpState(bool up)
    {
        if (isUp == up)
        {
            ApplyState();
            return;
        }

        isUp = up;
        ApplyState();
    }

    private void ApplyState()
    {
        if (HasAnimatedVisuals())
        {
            ApplyAnimatedState();
            SetDownTrapShown(false);
            return;
        }

        SetTrapShown(isUp);
        SetDownTrapShown(!isUp);
    }

    private void SetTrapShown(bool shown)
    {
        GameObject target = GetTrapObject();
        if (target == null)
        {
            return;
        }

        if (target == gameObject)
        {
            SetObjectComponentsEnabled(target, shown);
            return;
        }

        target.SetActive(shown);
    }

    private void SetDownTrapShown(bool shown)
    {
        GameObject target = GetDownTrapObject();
        if (target == null || target == gameObject)
        {
            return;
        }

        target.SetActive(shown);
    }

    private void SetObjectComponentsEnabled(GameObject target, bool enabled)
    {
        Renderer[] renderers = target.GetComponents<Renderer>();
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].enabled = enabled;
        }

        Collider2D[] colliders = target.GetComponents<Collider2D>();
        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = enabled;
        }
    }

    private void ApplyAnimatedState()
    {
        GameObject target = GetTrapObject();
        if (target == null)
        {
            return;
        }

        if (target != gameObject && !target.activeSelf)
        {
            target.SetActive(true);
        }

        CacheVisualSource();
        SyncLengthFromSpriteRenderer();
        EnsureVisualSegments();

        if (sourceRenderer != null)
        {
            sourceRenderer.enabled = false;
        }

        bool colliderEnabled = isUp || downtime <= 0f || uptime <= 0f;
        SetTrapCollidersEnabled(target, colliderEnabled);
        SetFrame(GetCurrentFrameIndex());
    }

    private void SetTrapCollidersEnabled(GameObject target, bool enabled)
    {
        Collider2D[] colliders = target.GetComponents<Collider2D>();
        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = enabled;
        }
    }

    private int GetCurrentFrameIndex()
    {
        int frameCount = GetAnimationFrameCount();
        if (frameCount <= 1)
        {
            return 0;
        }

        if (downtime <= 0f || uptime <= 0f)
        {
            return frameCount - 1;
        }

        if (!isUp)
        {
            return 0;
        }

        if (animationFrameRate <= 0f)
        {
            return frameCount - 1;
        }

        return Mathf.Clamp(Mathf.FloorToInt(cycleTimer * animationFrameRate), 0, frameCount - 1);
    }

    private void SetFrame(int frameIndex)
    {
        if (visualRenderers == null || visualRenderers.Length == 0)
        {
            return;
        }

        int frameCount = GetAnimationFrameCount();
        if (frameCount <= 0)
        {
            return;
        }

        int safeFrame = Mathf.Clamp(frameIndex, 0, frameCount - 1);
        for (int i = 0; i < visualRenderers.Length; i++)
        {
            SpriteRenderer renderer = visualRenderers[i];
            if (renderer == null)
            {
                continue;
            }

            renderer.enabled = true;
            if (i == 0)
            {
                renderer.sprite = leftFrames[safeFrame];
            }
            else if (i == visualRenderers.Length - 1)
            {
                renderer.sprite = rightFrames[safeFrame];
            }
            else
            {
                renderer.sprite = middleFrames[safeFrame];
            }
        }
    }

    private void EnsureVisualSegments()
    {
        if (!HasAnimatedVisuals())
        {
            visualRenderers = null;
            SetVisualRootShown(false);
            return;
        }

        Transform root = GetOrCreateVisualRoot();
        if (root == null)
        {
            return;
        }

        int segmentCount = Mathf.Max(3, length);
        EnsureChildCount(root, segmentCount);

        visualRenderers = new SpriteRenderer[segmentCount];
        float startX = -0.5f * (segmentCount - 1) * segmentWidth;
        for (int i = 0; i < segmentCount; i++)
        {
            Transform child = root.GetChild(i);
            child.name = GetSegmentName(i, segmentCount);
            child.localPosition = new Vector3(startX + i * segmentWidth, 0f, 0f);
            child.localRotation = Quaternion.identity;
            child.localScale = Vector3.one;

            SpriteRenderer renderer = child.GetComponent<SpriteRenderer>();
            if (renderer == null)
            {
                renderer = child.gameObject.AddComponent<SpriteRenderer>();
            }

            CopyRendererSettings(renderer);
            visualRenderers[i] = renderer;
        }

        root.localPosition = Vector3.zero;
        root.localRotation = Quaternion.identity;
        root.localScale = facing == TrapFacing.Down ? new Vector3(1f, -1f, 1f) : Vector3.one;

        SyncSourceRendererSize(segmentCount);
        SyncCollider(segmentCount);
    }

    private void SetVisualRootShown(bool shown)
    {
        if (visualRoot == null)
        {
            return;
        }

        Renderer[] renderers = visualRoot.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].enabled = shown;
        }
    }

    private Transform GetOrCreateVisualRoot()
    {
        GameObject target = GetTrapObject();
        if (target == null)
        {
            return null;
        }

        if (visualRoot != null)
        {
            return visualRoot;
        }

        Transform found = target.transform.Find("TrapVisual");
        if (found != null)
        {
            visualRoot = found;
            return visualRoot;
        }

        GameObject visualObject = new GameObject("TrapVisual");
        visualObject.transform.SetParent(target.transform, false);
        visualRoot = visualObject.transform;
        return visualRoot;
    }

    private void EnsureChildCount(Transform root, int segmentCount)
    {
        while (root.childCount < segmentCount)
        {
            GameObject segmentObject = new GameObject("segment");
            segmentObject.transform.SetParent(root, false);
        }

        for (int i = root.childCount - 1; i >= segmentCount; i--)
        {
            Transform extra = root.GetChild(i);
            if (Application.isPlaying)
            {
                Destroy(extra.gameObject);
            }
            else
            {
                DestroyImmediate(extra.gameObject);
            }
        }
    }

    private string GetSegmentName(int index, int segmentCount)
    {
        if (index == 0)
        {
            return "left";
        }

        if (index == segmentCount - 1)
        {
            return "right";
        }

        return string.Format("middle_{0}", index - 1);
    }

    private void CopyRendererSettings(SpriteRenderer renderer)
    {
        if (sourceRenderer == null || renderer == null)
        {
            return;
        }

        renderer.sharedMaterial = sourceRenderer.sharedMaterial;
        renderer.color = sourceRenderer.color;
        renderer.sortingLayerID = sourceRenderer.sortingLayerID;
        renderer.sortingOrder = sourceRenderer.sortingOrder;
        renderer.maskInteraction = sourceRenderer.maskInteraction;
    }

    private void SyncLengthFromSpriteRenderer()
    {
        if (!useSpriteRendererSizeAsLength)
        {
            return;
        }

        CacheVisualSource();
        if (sourceRenderer == null)
        {
            return;
        }

        length = Mathf.Max(3, Mathf.RoundToInt(sourceRenderer.size.x / segmentWidth));
    }

    private void SyncSourceRendererSize(int segmentCount)
    {
        if (!useSpriteRendererSizeAsLength || sourceRenderer == null)
        {
            return;
        }

        Vector2 size = sourceRenderer.size;
        size.x = segmentCount * segmentWidth;
        sourceRenderer.size = size;
    }

    private void SyncCollider(int segmentCount)
    {
        if (!syncColliderSize)
        {
            return;
        }

        GameObject target = GetTrapObject();
        if (target == null)
        {
            return;
        }

        BoxCollider2D boxCollider = target.GetComponent<BoxCollider2D>();
        if (boxCollider == null)
        {
            return;
        }

        Vector2 size = boxCollider.size;
        size.x = segmentCount * segmentWidth;
        if (sourceRenderer != null)
        {
            size.y = sourceRenderer.size.y;
        }

        boxCollider.size = size;
    }

    private bool HasAnimatedVisuals()
    {
        return GetAnimationFrameCount() > 0;
    }

    private int GetAnimationFrameCount()
    {
        if (leftFrames == null || middleFrames == null || rightFrames == null)
        {
            return 0;
        }

        int frameCount = Mathf.Min(leftFrames.Length, middleFrames.Length, rightFrames.Length);
        for (int i = 0; i < frameCount; i++)
        {
            if (leftFrames[i] == null || middleFrames[i] == null || rightFrames[i] == null)
            {
                return i;
            }
        }

        return frameCount;
    }

    private void CacheVisualSource()
    {
        GameObject target = GetTrapObject();
        sourceRenderer = target != null ? target.GetComponent<SpriteRenderer>() : null;
    }

    private GameObject GetTrapObject()
    {
        return trap != null ? trap : gameObject;
    }

    private GameObject GetDownTrapObject()
    {
        if (downTrap != null)
        {
            return downTrap;
        }

        Transform found = transform.Find("downtrap");
        return found != null ? found.gameObject : null;
    }

    private void CacheDefaultObjects()
    {
        if (trap == null)
        {
            trap = gameObject;
        }

        if (downTrap == null)
        {
            Transform found = transform.Find("downtrap");
            if (found != null)
            {
                downTrap = found.gameObject;
            }
        }
    }

#if UNITY_EDITOR
    private void RebuildVisualsInEditor()
    {
        if (this == null)
        {
            return;
        }

        CacheDefaultObjects();
        CacheVisualSource();
        SyncLengthFromSpriteRenderer();
        EnsureVisualSegments();
        ApplyState();
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}
