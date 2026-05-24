using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class Swirl : MonoBehaviour
{
    [SerializeField] private int forceDirection = GameDirection.Up;
    [SerializeField] private float speed = 8f;
    [SerializeField] private float uptime = 1f;
    [SerializeField] private float downtime;

    private readonly List<MCController> activePlayers = new List<MCController>();
    private float cycleTimer;
    private bool isActive = true;

    public int ForceDirection
    {
        get { return GameDirection.NormalizeOrDefault(forceDirection, GameDirection.Up); }
    }

    public float Speed
    {
        get { return speed; }
    }

    public bool IsVertical
    {
        get { return IsVerticalDirection(); }
    }

    public bool IsActive
    {
        get { return isActive; }
    }

    private void OnEnable()
    {
        isActive = true;
        cycleTimer = 0f;
    }

    private void OnDisable()
    {
        DeactivateForPlayers();
        activePlayers.Clear();
    }

    private void OnValidate()
    {
        forceDirection = GameDirection.NormalizeOrDefault(forceDirection, GameDirection.Up);
        speed = Mathf.Max(0f, speed);
        uptime = Mathf.Max(0f, uptime);
        downtime = Mathf.Max(0f, downtime);
    }

    private void Update()
    {
        if (downtime <= 0f || uptime <= 0f)
        {
            if (!isActive)
            {
                isActive = true;
            }

            cycleTimer = 0f;
            return;
        }

        cycleTimer += GameTime.DeltaTime;
        if (isActive && cycleTimer >= uptime)
        {
            isActive = false;
            cycleTimer = 0f;
            DeactivateForPlayers();
            return;
        }

        if (!isActive && cycleTimer >= downtime)
        {
            isActive = true;
            cycleTimer = 0f;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        ApplyToCollider(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        ApplyToCollider(other);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        ExitCollider(other);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision != null)
        {
            ApplyToCollider(collision.collider);
        }
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (collision != null)
        {
            ApplyToCollider(collision.collider);
        }
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        if (collision != null)
        {
            ExitCollider(collision.collider);
        }
    }

    private void ApplyToCollider(Collider2D other)
    {
        if (other == null || !isActive)
        {
            return;
        }

        MCController player = other.GetComponentInParent<MCController>();
        if (player != null)
        {
            TrackPlayer(player);
            player.EnterSwirl(this);
            return;
        }

        Bullet bullet = other.GetComponentInParent<Bullet>();
        if (bullet != null)
        {
            bullet.SetWorldVelocity(GetAppliedVelocity(bullet.WorldVelocity));
        }
    }

    private void ExitCollider(Collider2D other)
    {
        if (other == null)
        {
            return;
        }

        MCController player = other.GetComponentInParent<MCController>();
        if (player != null)
        {
            activePlayers.Remove(player);
            player.ExitSwirl(this);
        }
    }

    private void TrackPlayer(MCController player)
    {
        if (player != null && !activePlayers.Contains(player))
        {
            activePlayers.Add(player);
        }
    }

    private void DeactivateForPlayers()
    {
        for (int i = activePlayers.Count - 1; i >= 0; i--)
        {
            MCController player = activePlayers[i];
            if (player != null)
            {
                player.ExitSwirl(this);
            }
        }
    }

    private Vector2 GetAppliedVelocity(Vector2 currentVelocity)
    {
        Vector2 forceVelocity = (Vector2)GameDirection.ToVector3(forceDirection) * speed;
        if (IsVerticalDirection())
        {
            return new Vector2(currentVelocity.x, forceVelocity.y);
        }

        return new Vector2(forceVelocity.x, currentVelocity.y);
    }

    private bool IsVerticalDirection()
    {
        return forceDirection == GameDirection.Up || forceDirection == GameDirection.Down;
    }
}
