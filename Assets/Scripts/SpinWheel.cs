using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class SpinWheel : MonoBehaviour
{
    private const string HarmMaskChildName = "__spinwheel_harm_upper_mask";

    private static Texture2D harmMaskTexture;
    private static Sprite harmMaskSprite;

    [Header("Spin")]
    [SerializeField] private float rotationSpeedDegreesPerSecond = 180f;
    [SerializeField] private float freezeDuration = Consts.FreezeDuration;

    [Header("Harm")]
    [SerializeField] private Transform harm;
    [SerializeField] private int harmHalfCircleSegments = 16;
    [SerializeField] private bool applyHazardLayerToHarm = true;
    [SerializeField] private bool harmColliderIsTrigger = true;
    [SerializeField] private bool maskHarmSpriteToUpperHalf = true;

    [Header("Debug")]
    [SerializeField] private bool isFrozen;

    private float freezeTimeRemaining;

#if UNITY_EDITOR
    private bool configureQueued;
#endif

    public bool IsFrozen
    {
        get { return isFrozen; }
    }

    private void Reset()
    {
        CacheHarmChild();
        NormalizeValues();
        ConfigureHarmChild();
    }

    private void Awake()
    {
        CacheHarmChild();
        NormalizeValues();
        ConfigureHarmChild();
    }

    private void OnEnable()
    {
        if (freezeTimeRemaining <= 0f)
        {
            isFrozen = false;
        }
    }

    private void OnValidate()
    {
        NormalizeValues();
        CacheHarmChild();

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            QueueConfigureHarmChild();
        }
#endif
    }

    private void Update()
    {
        UpdateFrozenState();
        if (isFrozen)
        {
            return;
        }

        transform.Rotate(0f, 0f, rotationSpeedDegreesPerSecond * GameTime.DeltaTime);
    }

    public void ApplyFrozen()
    {
        freezeTimeRemaining = freezeDuration;
        isFrozen = freezeTimeRemaining > 0f;
    }

    public void ClearFrozen()
    {
        isFrozen = false;
        freezeTimeRemaining = 0f;
    }

    private void UpdateFrozenState()
    {
        if (!isFrozen)
        {
            return;
        }

        freezeTimeRemaining = Mathf.Max(0f, freezeTimeRemaining - GameTime.DeltaTime);
        if (freezeTimeRemaining <= 0f)
        {
            isFrozen = false;
        }
    }

    private void ConfigureHarmChild()
    {
        if (harm == null)
        {
            return;
        }

        CircleCollider2D circleCollider = harm.GetComponent<CircleCollider2D>();
        float radius = 0.5f;
        Vector2 offset = Vector2.zero;
        if (circleCollider != null)
        {
            radius = circleCollider.radius;
            offset = circleCollider.offset;
            circleCollider.enabled = false;
        }

        PolygonCollider2D polygonCollider = harm.GetComponent<PolygonCollider2D>();
        if (polygonCollider == null)
        {
            polygonCollider = harm.gameObject.AddComponent<PolygonCollider2D>();
        }

        polygonCollider.isTrigger = harmColliderIsTrigger;
        polygonCollider.pathCount = 1;
        polygonCollider.SetPath(0, BuildUpperHalfCirclePath(radius, offset));
        polygonCollider.enabled = true;

        if (applyHazardLayerToHarm)
        {
            GameLayers.ApplyTo(harm.gameObject, GameLayers.Hazard);
        }

        ConfigureHarmSpriteMask(radius, offset);
    }

    private void ConfigureHarmSpriteMask(float radius, Vector2 offset)
    {
        SpriteRenderer[] spriteRenderers = harm.GetComponentsInChildren<SpriteRenderer>(true);
        if (spriteRenderers.Length == 0)
        {
            return;
        }

        if (!maskHarmSpriteToUpperHalf)
        {
            SetHarmSpriteMaskInteraction(spriteRenderers, SpriteMaskInteraction.None);
            return;
        }

        SpriteMask mask = GetOrCreateHarmSpriteMask();
        if (mask == null)
        {
            return;
        }

        float diameter = Mathf.Max(0.01f, radius * 2f);
        mask.transform.localPosition = new Vector3(offset.x, offset.y + radius * 0.5f, 0f);
        mask.transform.localRotation = Quaternion.identity;
        mask.transform.localScale = new Vector3(diameter, Mathf.Max(0.01f, radius), 1f);
        mask.sprite = GetHarmMaskSprite();
        mask.isCustomRangeActive = true;
        mask.alphaCutoff = 0.5f;
        ConfigureMaskSortingRange(mask, spriteRenderers);
        SetHarmSpriteMaskInteraction(spriteRenderers, SpriteMaskInteraction.VisibleInsideMask);
    }

    private SpriteMask GetOrCreateHarmSpriteMask()
    {
        Transform maskTransform = harm.Find(HarmMaskChildName);
        if (maskTransform == null)
        {
            GameObject maskObject = new GameObject(HarmMaskChildName);
            maskObject.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;
            maskTransform = maskObject.transform;
            maskTransform.SetParent(harm, false);
        }

        SpriteMask mask = maskTransform.GetComponent<SpriteMask>();
        if (mask == null)
        {
            mask = maskTransform.gameObject.AddComponent<SpriteMask>();
        }

        return mask;
    }

    private static void ConfigureMaskSortingRange(SpriteMask mask, SpriteRenderer[] spriteRenderers)
    {
        SpriteRenderer firstRenderer = null;
        int minOrder = int.MaxValue;
        int maxOrder = int.MinValue;
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            SpriteRenderer spriteRenderer = spriteRenderers[i];
            if (spriteRenderer == null)
            {
                continue;
            }

            if (firstRenderer == null)
            {
                firstRenderer = spriteRenderer;
            }

            minOrder = Mathf.Min(minOrder, spriteRenderer.sortingOrder);
            maxOrder = Mathf.Max(maxOrder, spriteRenderer.sortingOrder);
        }

        if (firstRenderer == null)
        {
            return;
        }

        mask.frontSortingLayerID = firstRenderer.sortingLayerID;
        mask.backSortingLayerID = firstRenderer.sortingLayerID;
        mask.frontSortingOrder = maxOrder + 1;
        mask.backSortingOrder = minOrder - 1;
    }

    private static void SetHarmSpriteMaskInteraction(SpriteRenderer[] spriteRenderers, SpriteMaskInteraction maskInteraction)
    {
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] != null)
            {
                spriteRenderers[i].maskInteraction = maskInteraction;
            }
        }
    }

    private static Sprite GetHarmMaskSprite()
    {
        if (harmMaskSprite != null)
        {
            return harmMaskSprite;
        }

        harmMaskTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        harmMaskTexture.hideFlags = HideFlags.HideAndDontSave;
        harmMaskTexture.SetPixel(0, 0, Color.white);
        harmMaskTexture.Apply();

        harmMaskSprite = Sprite.Create(harmMaskTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        harmMaskSprite.hideFlags = HideFlags.HideAndDontSave;
        return harmMaskSprite;
    }

    private Vector2[] BuildUpperHalfCirclePath(float radius, Vector2 offset)
    {
        int segmentCount = Mathf.Max(2, harmHalfCircleSegments);
        Vector2[] path = new Vector2[segmentCount + 1];
        for (int i = 0; i <= segmentCount; i++)
        {
            float angle = Mathf.PI - Mathf.PI * i / segmentCount;
            path[i] = offset + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }

        return path;
    }

    private void CacheHarmChild()
    {
        if (harm != null)
        {
            return;
        }

        Transform found = transform.Find("harm");
        if (found != null)
        {
            harm = found;
        }
    }

    private void NormalizeValues()
    {
        freezeDuration = Mathf.Max(0f, freezeDuration);
        harmHalfCircleSegments = Mathf.Max(2, harmHalfCircleSegments);
    }

#if UNITY_EDITOR
    private void QueueConfigureHarmChild()
    {
        if (configureQueued)
        {
            return;
        }

        configureQueued = true;
        EditorApplication.delayCall += ConfigureHarmChildAfterValidation;
    }

    private void ConfigureHarmChildAfterValidation()
    {
        configureQueued = false;
        if (this == null || Application.isPlaying)
        {
            return;
        }

        ConfigureHarmChild();
    }
#endif
}
