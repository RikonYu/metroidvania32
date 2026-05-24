using UnityEngine;

public class Trap : MonoBehaviour
{
    [SerializeField] private GameObject trap;
    [SerializeField] private GameObject downTrap;
    [SerializeField] private float uptime = 1f;
    [SerializeField] private float downtime;

    private float cycleTimer;
    private bool isUp = true;

    public bool IsUp
    {
        get { return isUp; }
    }

    private void Reset()
    {
        CacheDefaultObjects();
        ApplyState();
    }

    private void OnEnable()
    {
        isUp = true;
        cycleTimer = 0f;
        CacheDefaultObjects();
        ApplyState();
    }

    private void OnValidate()
    {
        uptime = Mathf.Max(0f, uptime);
        downtime = Mathf.Max(0f, downtime);
        CacheDefaultObjects();
        ApplyState();
    }

    private void Update()
    {
        if (downtime <= 0f || uptime <= 0f)
        {
            SetUpState(true);
            cycleTimer = 0f;
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
        }
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
}
