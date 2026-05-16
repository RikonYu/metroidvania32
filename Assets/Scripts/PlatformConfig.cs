using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class PlatformConfig : MonoBehaviour
{
    [SerializeField] private bool oneWay = true;
    [SerializeField] private bool autoConfigureEffector = true;

    public bool OneWay
    {
        get { return oneWay; }
    }

    private void Reset()
    {
        ApplyConfiguration();
    }

    private void OnValidate()
    {
        ApplyConfiguration();
    }

    private void Awake()
    {
        ApplyConfiguration();
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
}
