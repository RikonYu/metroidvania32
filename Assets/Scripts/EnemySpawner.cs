using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class EnemySpawnerWave
{
    [SerializeField] private List<EnemyController> enemies = new List<EnemyController>();

    public IReadOnlyList<EnemyController> Enemies
    {
        get { return enemies; }
    }
}

[RequireComponent(typeof(Collider2D))]
public class EnemySpawner : MonoBehaviour
{
    [SerializeField] private GameObject door;
    [SerializeField] private GameObject reward;
    [SerializeField] private List<EnemySpawnerWave> waves = new List<EnemySpawnerWave>();

    private int currentWaveIndex = -1;
    private bool hasStarted;
    private bool isCompleted;

    public bool IsCompleted
    {
        get { return isCompleted; }
    }

    private void Awake()
    {
        EnsureTriggerCollider();
        ResetToInitialState();
    }

    private void Reset()
    {
        EnsureTriggerCollider();
    }

    private void OnValidate()
    {
        EnsureTriggerCollider();
    }

    private void Update()
    {
        if (!hasStarted || isCompleted)
        {
            return;
        }

        if (IsCurrentWaveCleared())
        {
            ShowNextWave();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryStart(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryStart(other);
    }

    public void ResetForCampRespawn()
    {
        if (isCompleted)
        {
            return;
        }

        ResetToInitialState();
    }

    public bool ContainsEnemy(EnemyController enemy)
    {
        if (enemy == null)
        {
            return false;
        }

        for (int waveIndex = 0; waveIndex < waves.Count; waveIndex++)
        {
            IReadOnlyList<EnemyController> enemies = GetWaveEnemies(waveIndex);
            for (int enemyIndex = 0; enemyIndex < enemies.Count; enemyIndex++)
            {
                if (enemies[enemyIndex] == enemy)
                {
                    return true;
                }
            }
        }

        return false;
    }

    public static void ResetUnfinishedSpawnersForCampRespawn()
    {
        EnemySpawner[] spawners = Resources.FindObjectsOfTypeAll<EnemySpawner>();
        for (int i = 0; i < spawners.Length; i++)
        {
            EnemySpawner spawner = spawners[i];
            if (spawner == null || !Utils.IsSceneInstance(spawner.gameObject))
            {
                continue;
            }

            spawner.ResetForCampRespawn();
        }
    }

    public static bool IsManagedEnemy(EnemyController enemy)
    {
        if (enemy == null)
        {
            return false;
        }

        EnemySpawner[] spawners = Resources.FindObjectsOfTypeAll<EnemySpawner>();
        for (int i = 0; i < spawners.Length; i++)
        {
            EnemySpawner spawner = spawners[i];
            if (spawner != null && Utils.IsSceneInstance(spawner.gameObject) && spawner.ContainsEnemy(enemy))
            {
                return true;
            }
        }

        return false;
    }

    private void TryStart(Collider2D other)
    {
        if (hasStarted || isCompleted || other == null || other.GetComponentInParent<MCController>() == null)
        {
            return;
        }

        hasStarted = true;
        currentWaveIndex = -1;
        ShowDoor(true);
        ShowNextWave();
    }

    private void ShowNextWave()
    {
        currentWaveIndex++;
        while (currentWaveIndex < waves.Count)
        {
            ShowWave(currentWaveIndex);
            if (!IsCurrentWaveCleared())
            {
                return;
            }

            currentWaveIndex++;
        }

        CompleteSpawner();
    }

    private void ShowWave(int waveIndex)
    {
        IReadOnlyList<EnemyController> enemies = GetWaveEnemies(waveIndex);
        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyController enemy = enemies[i];
            if (enemy == null)
            {
                continue;
            }

            enemy.Respawn();
            enemy.gameObject.SetActive(true);
        }
    }

    private bool IsCurrentWaveCleared()
    {
        if (currentWaveIndex < 0 || currentWaveIndex >= waves.Count)
        {
            return true;
        }

        IReadOnlyList<EnemyController> enemies = GetWaveEnemies(currentWaveIndex);
        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyController enemy = enemies[i];
            if (enemy != null && enemy.IsAlive)
            {
                return false;
            }
        }

        return true;
    }

    private void CompleteSpawner()
    {
        isCompleted = true;
        hasStarted = false;
        currentWaveIndex = -1;
        ShowDoor(false);
        ShowReward(true);
    }

    private void ResetToInitialState()
    {
        hasStarted = false;
        currentWaveIndex = -1;
        ShowDoor(false);
        ShowReward(false);
        ResetAndHideAllEnemies();
    }

    private void ResetAndHideAllEnemies()
    {
        for (int waveIndex = 0; waveIndex < waves.Count; waveIndex++)
        {
            IReadOnlyList<EnemyController> enemies = GetWaveEnemies(waveIndex);
            for (int enemyIndex = 0; enemyIndex < enemies.Count; enemyIndex++)
            {
                EnemyController enemy = enemies[enemyIndex];
                if (enemy == null)
                {
                    continue;
                }

                enemy.Respawn();
                enemy.gameObject.SetActive(false);
            }
        }
    }

    private IReadOnlyList<EnemyController> GetWaveEnemies(int waveIndex)
    {
        if (waveIndex < 0 || waveIndex >= waves.Count || waves[waveIndex] == null)
        {
            return Array.Empty<EnemyController>();
        }

        return waves[waveIndex].Enemies;
    }

    private void ShowDoor(bool visible)
    {
        if (door != null)
        {
            door.SetActive(visible);
        }
    }

    private void ShowReward(bool visible)
    {
        if (reward != null)
        {
            reward.SetActive(visible);
        }
    }

    private void EnsureTriggerCollider()
    {
        Collider2D collider2D = GetComponent<Collider2D>();
        if (collider2D != null)
        {
            collider2D.isTrigger = true;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = isCompleted ? Color.gray : Color.magenta;
        Gizmos.DrawWireCube(transform.position, Vector3.one);
    }
}
