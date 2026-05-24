using UnityEngine;

public class ArrowLock : Lock
{
    public void UnlockFromArrow(Bullet bullet)
    {
        if (!IsValidArrowBullet(bullet))
        {
            return;
        }

        Unlock();
    }

    private static bool IsValidArrowBullet(Bullet bullet)
    {
        return bullet != null && bullet.Source == BulletSource.Player;
    }
}
