using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class Lock : MonoBehaviour
{
    [SerializeField] private LockDoor lockDoor;
    [SerializeField] private bool disableColliderAfterUnlock = true;

    private bool unlocked;

    protected virtual bool ShouldUseTriggerCollider
    {
        get { return true; }
    }

    public bool IsUnlocked
    {
        get { return unlocked; }
    }

    private void Reset()
    {
        CacheDoorIfMissing();
        EnsureTriggerCollider();
    }

    private void Awake()
    {
        CacheDoorIfMissing();
        EnsureTriggerCollider();
    }

    private void OnValidate()
    {
        CacheDoorIfMissing();
        EnsureTriggerCollider();
    }

    protected virtual void Update()
    {
        if (unlocked || !OnUnlock())
        {
            return;
        }

        Unlock();
    }

    protected virtual bool OnUnlock()
    {
        return false;
    }

    public virtual void Unlock()
    {
        if (unlocked)
        {
            return;
        }

        unlocked = true;
        if (lockDoor != null)
        {
            lockDoor.Unlock();
        }

        if (disableColliderAfterUnlock)
        {
            Collider2D lockCollider = GetComponent<Collider2D>();
            if (lockCollider != null)
            {
                lockCollider.enabled = false;
            }
        }
    }

    private void CacheDoorIfMissing()
    {
        if (lockDoor != null)
        {
            return;
        }

        lockDoor = GetComponentInParent<LockDoor>();
        if (lockDoor == null)
        {
            lockDoor = GetComponentInChildren<LockDoor>(true);
        }
    }

    private void EnsureTriggerCollider()
    {
        Collider2D lockCollider = GetComponent<Collider2D>();
        if (lockCollider != null)
        {
            lockCollider.isTrigger = ShouldUseTriggerCollider;
        }
    }

    protected static bool IsPlayer(Collider2D other)
    {
        return other != null && other.GetComponentInParent<MCController>() != null;
    }
}
