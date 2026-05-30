using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class WaterCleanseObj : MonoBehaviour
{
    [SerializeField] private KeyCode interactKey = KeyCode.E;
    [SerializeField] private bool oneUse = true;
    [SerializeField] private bool used;

    public bool Used
    {
        get { return used; }
    }

    private void Awake()
    {
        EnsureTriggerCollider();
    }

    private void Reset()
    {
        EnsureTriggerCollider();
    }

    private void OnValidate()
    {
        EnsureTriggerCollider();
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if ((oneUse && used) || !Input.GetKeyDown(interactKey))
        {
            return;
        }

        if (other.GetComponentInParent<MCController>() == null)
        {
            return;
        }

        CleanseAllPoisonousWater();
        used = true;
    }

    public int CleanseAllPoisonousWater()
    {
        int cleansedCount = 0;
        WaterZone[] waterZones = Resources.FindObjectsOfTypeAll<WaterZone>();
        for (int i = 0; i < waterZones.Length; i++)
        {
            WaterZone waterZone = waterZones[i];
            if (waterZone == null || !Utils.IsSceneInstance(waterZone.gameObject))
            {
                continue;
            }

            if (waterZone.CleansePoison())
            {
                cleansedCount++;
            }
        }

        return cleansedCount;
    }

    private void EnsureTriggerCollider()
    {
        Collider2D collider2D = GetComponent<Collider2D>();
        if (collider2D != null)
        {
            collider2D.isTrigger = true;
        }
    }
}
