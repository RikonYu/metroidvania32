using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class PlatformConfig : MonoBehaviour
{
    [SerializeField] private bool oneWay = true;
    [SerializeField] private bool autoConfigureEffector = true;
    [SerializeField] private Sprite leftSprite;
    [SerializeField] private Sprite middleSprite;
    [SerializeField] private Sprite rightSprite;
    [SerializeField] private float segmentWidth = 1f;
    [SerializeField] private int length = 3;
    [SerializeField] private bool useSpriteRendererSizeAsLength = true;
    [SerializeField] private bool syncColliderSize = true;
    [SerializeField] private Transform visualRoot;

    private SpriteRenderer sourceRenderer;
    private SpriteRenderer[] visualRenderers;

    public bool OneWay
    {
        get { return oneWay; }
    }

    private void Reset()
    {
        ApplyConfiguration();
        ApplySegmentedVisual();
    }

    private void OnValidate()
    {
        segmentWidth = Mathf.Max(0.01f, segmentWidth);
        length = Mathf.Max(3, length);
        ApplyConfiguration();

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            UnityEditor.EditorApplication.delayCall -= RebuildVisualsInEditor;
            UnityEditor.EditorApplication.delayCall += RebuildVisualsInEditor;
            return;
        }
#endif

        ApplySegmentedVisual();
    }

    private void Awake()
    {
        ApplyConfiguration();
        ApplySegmentedVisual();
    }

    private void ApplyConfiguration()
    {
        if (!autoConfigureEffector)
        {
            return;
        }

        Collider2D collider2D = GetComponent<Collider2D>();
        if (collider2D == null)
        {
            return;
        }

        PlatformEffector2D effector = GetComponent<PlatformEffector2D>();
        if (oneWay)
        {
            if (effector == null)
            {
                effector = gameObject.AddComponent<PlatformEffector2D>();
            }

            collider2D.usedByEffector = true;
            effector.useOneWay = true;
            return;
        }

        collider2D.usedByEffector = false;
        if (effector != null)
        {
            effector.enabled = false;
        }
    }

    private void ApplySegmentedVisual()
    {
        CacheVisualSource();

        if (!HasSegmentedVisual())
        {
            if (sourceRenderer != null)
            {
                sourceRenderer.enabled = true;
            }

            SetVisualRootShown(false);
            visualRenderers = null;
            return;
        }

        SyncLengthFromSpriteRenderer();
        EnsureVisualSegments();

        if (sourceRenderer != null)
        {
            sourceRenderer.enabled = false;
        }

        SetSegmentSprites();
    }

    private void EnsureVisualSegments()
    {
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
        root.localScale = Vector3.one;

        SyncSourceRendererSize(segmentCount);
        SyncCollider(segmentCount);
    }

    private Transform GetOrCreateVisualRoot()
    {
        if (visualRoot != null)
        {
            return visualRoot;
        }

        Transform found = transform.Find("PlatformVisual");
        if (found != null)
        {
            visualRoot = found;
            return visualRoot;
        }

        GameObject visualObject = new GameObject("PlatformVisual");
        visualObject.transform.SetParent(transform, false);
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

    private void SetSegmentSprites()
    {
        if (visualRenderers == null)
        {
            return;
        }

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
                renderer.sprite = leftSprite;
            }
            else if (i == visualRenderers.Length - 1)
            {
                renderer.sprite = rightSprite;
            }
            else
            {
                renderer.sprite = middleSprite;
            }
        }
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
        if (!useSpriteRendererSizeAsLength || sourceRenderer == null)
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

        BoxCollider2D boxCollider = GetComponent<BoxCollider2D>();
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

    private bool HasSegmentedVisual()
    {
        return leftSprite != null && middleSprite != null && rightSprite != null;
    }

    private void CacheVisualSource()
    {
        sourceRenderer = GetComponent<SpriteRenderer>();
    }

#if UNITY_EDITOR
    private void RebuildVisualsInEditor()
    {
        if (this == null)
        {
            return;
        }

        ApplySegmentedVisual();
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}
