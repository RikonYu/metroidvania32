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
        PlayExplosion(GetExplosionCenter(firstHit));
        base.Explode(firstHit);
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
