using Fusion;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class EnemySpawner : NetworkBehaviour
{
    [SerializeField] private NetworkPrefabRef enemyPrefab;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private PathManager startPath;

    [Header("Wave counts (default)")]
    public int zombiesCount = 10;
    public int werewolvesCount = 4;
    public int ghostsCount = 2;
    public int orksCount = 1;

    public static EnemySpawner Instance;

    private void Awake() => Instance = this;

    // UI button click: spawn single enemy of a given type (player pressed button)
    public void OnSpawnButtonPressed(int enemyType)
    {
        if (Runner == null) return;
        RPC_RequestSpawn(enemyType);
    }

    // spawn full wave (UI or server triggers)
    public void OnSpawnWaveButtonPressed()
    {
        if (Runner == null) return;
        RPC_RequestSpawnWave();
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestSpawn(int typeIndex)
    {
        if (!Runner.IsServer) return;

        var enemyObj = Runner.Spawn(enemyPrefab, spawnPoint.position, Quaternion.identity);
        if (enemyObj.TryGetComponent<EnemyAI>(out var ai))
        {
            ai.InitStats((EnemyType)typeIndex);
            ai.SetPath(startPath);
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestSpawnWave()
    {
        if (!Runner.IsServer) return;
        StartCoroutine(SpawnWaveCoroutine());
    }

    private IEnumerator SpawnWaveCoroutine()
    {
        List<(EnemyType type, int count)> plan = new List<(EnemyType, int)>()
        {
            (EnemyType.Zombie, zombiesCount),
            (EnemyType.Werewolf, werewolvesCount),
            (EnemyType.Ghost, ghostsCount),
            (EnemyType.Ork, orksCount)
        };

        foreach (var item in plan)
        {
            for (int i = 0; i < item.count; i++)
            {
                var enemyObj = Runner.Spawn(enemyPrefab, spawnPoint.position, Quaternion.identity);
                if (enemyObj.TryGetComponent<EnemyAI>(out var ai))
                {
                    ai.InitStats(item.type);
                    ai.SetPath(startPath);
                }
                yield return new WaitForSeconds(0.25f);
            }
            yield return new WaitForSeconds(0.5f);
        }
    }
}
