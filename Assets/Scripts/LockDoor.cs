using UnityEngine;

public class LockDoor : MonoBehaviour
{
    [SerializeField] private bool deactivateOnUnlock = true;
    [SerializeField] private bool disableCollidersOnUnlock = true;
    [SerializeField] private bool disableRenderersOnUnlock;

    private bool unlocked;

    public bool IsUnlocked
    {
        get { return unlocked; }
    }

    public virtual void Unlock()
    {
        if (unlocked)
        {
            return;
        }

        unlocked = true;

        if (deactivateOnUnlock)
        {
            gameObject.SetActive(false);
            return;
        }

        if (disableCollidersOnUnlock)
        {
            SetCollidersEnabled(false);
        }

        if (disableRenderersOnUnlock)
        {
            SetRenderersEnabled(false);
        }
    }

    private void SetCollidersEnabled(bool enabled)
    {
        Collider2D[] colliders = GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = enabled;
        }
    }

    private void SetRenderersEnabled(bool enabled)
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].enabled = enabled;
        }
    }
}
