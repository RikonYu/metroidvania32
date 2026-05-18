using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameController : MonoBehaviour
{
    public static GameController instance;

    private void Awake()
    {
        instance = this;
    }

    // Start is called before the first frame update
    void Start()
    {
        
    }



    // Update is called once per frame
    void Update()
    {
        
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
        worldScale = Mathf.Clamp(scale, 0.01f, 1f);
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
