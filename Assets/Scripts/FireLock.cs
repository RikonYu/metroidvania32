using UnityEngine;

public class FireLock : Lock
{
    public void UnlockFromChargedFireExplosion(Bullet bullet)
    {
        if (!IsValidUnlockBullet(bullet))
        {
            return;
        }

        Unlock();
    }

    private static bool IsValidUnlockBullet(Bullet bullet)
    {
        return bullet != null
            && bullet.Source == BulletSource.Player
            && bullet.IsCharged
            && bullet.Elemental == BulletElement.Fire;
    }
}
