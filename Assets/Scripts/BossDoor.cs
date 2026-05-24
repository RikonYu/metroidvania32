using System.Collections.Generic;
using UnityEngine;

public class BossDoor : MonoBehaviour
{
    [SerializeField] private Room room;

    private readonly List<EnemyController> bosses = new List<EnemyController>();
    private bool unlocked;

    private void Awake()
    {
        CacheRoom();
        CacheBosses();
    }

    private void OnEnable()
    {
        if (unlocked)
        {
            gameObject.SetActive(false);
            return;
        }

        CacheRoom();
        CacheBosses();
        UpdateDoorState();
    }

    private void Update()
    {
        UpdateDoorState();
    }

    private void OnValidate()
    {
        CacheRoom();
    }

    private void CacheRoom()
    {
        if (room == null)
        {
            room = GetComponentInParent<Room>(true);
        }
    }

    private void CacheBosses()
    {
        bosses.Clear();
        if (room == null)
        {
            return;
        }

        EnemyController[] enemies = room.GetComponentsInChildren<EnemyController>(true);
        for (int i = 0; i < enemies.Length; i++)
        {
            EnemyController enemy = enemies[i];
            if (enemy != null && enemy.IsBoss)
            {
                bosses.Add(enemy);
            }
        }
    }

    private void UpdateDoorState()
    {
        if (unlocked || bosses.Count == 0)
        {
            return;
        }

        for (int i = bosses.Count - 1; i >= 0; i--)
        {
            EnemyController boss = bosses[i];
            if (boss == null)
            {
                bosses.RemoveAt(i);
                continue;
            }

            if (boss.IsAlive)
            {
                return;
            }

            bosses.RemoveAt(i);
        }

        Unlock();
    }

    private void Unlock()
    {
        if (unlocked)
        {
            return;
        }

        unlocked = true;
        gameObject.SetActive(false);
    }
}
