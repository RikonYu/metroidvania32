using System.Collections.Generic;
using UnityEngine;

public class FireBullet : Bullet
{
    [SerializeField] private GameObject explosion;
    [SerializeField] private float explosionLifetime = 0.5f;
    [SerializeField] private bool detachExplosionOnExplode = true;

    private void Reset()
    {
        CacheExplosion();
        HideExplosion();
    }

    protected override void Awake()
    {
        CacheExplosion();
        HideExplosion();
        SetElemental(BulletElement.Fire);
        base.Awake();
    }

    protected override void OnEnable()
    {
        CacheExplosion();
        HideExplosion();
        SetElemental(BulletElement.Fire);
        base.OnEnable();
    }

    protected override void OnValidate()
    {
        explosionLifetime = Mathf.Max(0f, explosionLifetime);
        CacheExplosion();
        SetElemental(BulletElement.Fire);
        base.OnValidate();
    }

    public override void Configure(
        BulletSource bulletSource,
        Vector2 bulletDirection,
        float bulletSpeed,
        bool bulletIsHyperbolic,
        bool bulletIsPiercing,
        BulletElement bulletElement,
        bool bulletIsCharged)
    {
        base.Configure(
            bulletSource,
            bulletDirection,
            bulletSpeed,
            bulletIsHyperbolic,
            bulletIsPiercing,
            BulletElement.Fire,
            bulletIsCharged);
    }

    protected override void Explode(Collider2D firstHit)
    {
        Vector2 center = GetExplosionCenter(firstHit);
        PlayExplosion(center);
        ApplyExplosionHeat(center);
        base.Explode(firstHit);
    }

    private void ApplyExplosionHeat(Vector2 center)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(center, ExplosionRadius);
        HashSet<IceObstacle> affectedIce = new HashSet<IceObstacle>();
        HashSet<EnemyController> affectedEnemies = new HashSet<EnemyController>();
        HashSet<PlayerRespawn> affectedPlayers = new HashSet<PlayerRespawn>();
        HashSet<SpinWheel> affectedSpinWheels = new HashSet<SpinWheel>();

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null)
            {
                continue;
            }

            IceObstacle iceObstacle = hit.GetComponentInParent<IceObstacle>();
            if (iceObstacle != null && affectedIce.Add(iceObstacle))
            {
                Destroy(iceObstacle.gameObject);
                continue;
            }

            EnemyController enemy = Utils.GetEnemyTarget(hit);
            if (enemy != null && affectedEnemies.Add(enemy))
            {
                enemy.ClearFrozenOrSlowed();
            }

            PlayerRespawn playerRespawn = Utils.GetPlayerTarget(hit);
            if (playerRespawn != null && affectedPlayers.Add(playerRespawn))
            {
                playerRespawn.ClearFrozen();
            }

            SpinWheel spinWheel = hit.GetComponentInParent<SpinWheel>();
            if (spinWheel != null && affectedSpinWheels.Add(spinWheel))
            {
                spinWheel.ClearFrozen();
            }
        }
    }

    private void CacheExplosion()
    {
        if (explosion != null)
        {
            return;
        }

        Transform explosionChild = transform.Find("Explosion");
        if (explosionChild != null)
        {
            explosion = explosionChild.gameObject;
        }
    }

    private void HideExplosion()
    {
        if (explosion != null)
        {
            explosion.SetActive(false);
        }
    }

    private void PlayExplosion(Vector2 center)
    {
        CacheExplosion();
        if (explosion == null)
        {
            return;
        }

        Transform explosionTransform = explosion.transform;
        if (detachExplosionOnExplode)
        {
            explosionTransform.SetParent(null, true);
        }

        explosionTransform.position = center;
        explosionTransform.rotation = Quaternion.identity;
        explosion.SetActive(true);

        if (explosionLifetime > 0f)
        {
            Destroy(explosion, explosionLifetime);
        }
    }
}
