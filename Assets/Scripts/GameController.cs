using UnityEngine;

public class GameController : MonoBehaviour
{
    public static GameController instance;

    [Header("Health Bottles")]
    [SerializeField] private int maxHealthBottles = Consts.DefaultMaxHealthBottles;
    [SerializeField] private int currentHealthBottles = Consts.DefaultCurrentHealthBottles;

    [Header("Abilities")]
    [SerializeField] private bool canDoubleJump;

    public static GameController Instance
    {
        get { return instance; }
    }

    public int MaxHealthBottles
    {
        get { return maxHealthBottles; }
    }

    public int CurrentHealthBottles
    {
        get { return currentHealthBottles; }
    }

    public bool CanDoubleJump
    {
        get { return canDoubleJump; }
    }

    private void Awake()
    {
        instance = this;
        ClampHealthBottles();
    }

    private void OnValidate()
    {
        ClampHealthBottles();
    }

    public bool TryUseHealthBottle()
    {
        if (currentHealthBottles <= 0)
        {
            return false;
        }

        currentHealthBottles--;
        return true;
    }

    public void RestoreHealthBottlesToFull()
    {
        currentHealthBottles = maxHealthBottles;
    }

    public void IncreaseHealthBottleCapacity(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        maxHealthBottles += amount;
        currentHealthBottles = Mathf.Min(maxHealthBottles, currentHealthBottles + amount);
    }

    private void ClampHealthBottles()
    {
        maxHealthBottles = Mathf.Max(0, maxHealthBottles);
        currentHealthBottles = Mathf.Clamp(currentHealthBottles, 0, maxHealthBottles);
    }
}

public static class GameTime
{
    private static object slowOwner;
    private static float worldScale = 1f;
    private static float slowUntil;

    public static float WorldScale
    {
        get
        {
            RefreshSlowState();
            return worldScale;
        }
    }

    public static float DeltaTime
    {
        get { return Time.deltaTime * WorldScale; }
    }

    public static float FixedDeltaTime
    {
        get { return Time.fixedDeltaTime * WorldScale; }
    }

    public static void SetSlow(object owner, float scale, float duration)
    {
        if (owner == null || duration <= 0f)
        {
            return;
        }

        slowOwner = owner;
        worldScale = Mathf.Clamp(scale, Consts.MinWorldScale, 1f);
        slowUntil = Time.time + duration;
    }

    public static void ClearSlow(object owner)
    {
        if (owner != null && slowOwner != owner)
        {
            return;
        }

        slowOwner = null;
        worldScale = 1f;
        slowUntil = 0f;
    }

    private static void RefreshSlowState()
    {
        if (worldScale < 1f && Time.time >= slowUntil)
        {
            ClearSlow(slowOwner);
        }
    }
}
