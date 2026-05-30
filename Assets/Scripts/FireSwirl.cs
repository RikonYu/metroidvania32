using UnityEngine;

public class FireSwirl : MonoBehaviour
{
    [SerializeField] private GameObject baseObject;
    [SerializeField] private GameObject swirlObject;
    [SerializeField] private float swirlDuration = 3f;

    private float swirlTimer;

    private void Reset()
    {
        CacheChildren();
    }

    private void Awake()
    {
        CacheChildren();
        HideSwirlIfPlaying();
    }

    private void OnEnable()
    {
        CacheChildren();
        HideSwirlIfPlaying();
    }

    private void OnValidate()
    {
        swirlDuration = Mathf.Max(0f, swirlDuration);
        CacheChildren();
    }

    private void Update()
    {
        if (swirlTimer <= 0f)
        {
            return;
        }

        swirlTimer = Mathf.Max(0f, swirlTimer - GameTime.DeltaTime);
        if (swirlTimer <= 0f)
        {
            HideSwirl();
        }
    }

    public bool TryActivateFromFireArrow(Bullet bullet, Collider2D hitCollider)
    {
        if (!IsValidFireArrow(bullet) || !IsBaseCollider(hitCollider))
        {
            return false;
        }

        ShowSwirl();
        return true;
    }

    private bool IsValidFireArrow(Bullet bullet)
    {
        return bullet != null
            && bullet.Source == BulletSource.Player
            && bullet.Elemental == BulletElement.Fire;
    }

    private bool IsBaseCollider(Collider2D hitCollider)
    {
        CacheChildren();
        return hitCollider != null
            && baseObject != null
            && hitCollider.transform.IsChildOf(baseObject.transform);
    }

    private void ShowSwirl()
    {
        CacheChildren();
        swirlTimer = Mathf.Max(0f, swirlDuration);
        if (swirlTimer <= 0f)
        {
            HideSwirl();
            return;
        }

        if (swirlObject != null && !swirlObject.activeSelf)
        {
            swirlObject.SetActive(true);
        }
    }

    private void HideSwirl()
    {
        swirlTimer = 0f;
        if (swirlObject != null && swirlObject.activeSelf)
        {
            swirlObject.SetActive(false);
        }
    }

    private void HideSwirlIfPlaying()
    {
        if (Application.isPlaying)
        {
            HideSwirl();
        }
    }

    private void CacheChildren()
    {
        if (baseObject == null)
        {
            Transform baseChild = FindDirectChildIgnoreCase("base");
            if (baseChild != null)
            {
                baseObject = baseChild.gameObject;
            }
        }

        if (swirlObject == null)
        {
            Transform swirlChild = FindDirectChildIgnoreCase("swirl");
            if (swirlChild != null)
            {
                swirlObject = swirlChild.gameObject;
            }
        }
    }

    private Transform FindDirectChildIgnoreCase(string childName)
    {
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (string.Equals(child.name, childName, System.StringComparison.OrdinalIgnoreCase))
            {
                return child;
            }
        }

        return null;
    }
}
