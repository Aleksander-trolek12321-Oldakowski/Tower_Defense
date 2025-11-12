using System.Collections;
using Fusion;
using UnityEngine;
using Networking;

public class EnemySpawner : NetworkBehaviour
{
    [SerializeField] private NetworkPrefabRef enemyPrefab;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private PathManager startPath;

    [Header("Wave settings")]
    [Tooltip("Delay (seconds) between individual spawns inside a wave")]
    public float spawnInterval = 0.15f;

    // counts per enemy type (index by EnemyType enum)
    public int[] defaultCounts = new int[] { 4, 10, 1, 2 }; // example: Werewolf, Zombie, Ork, Ghost

    public static EnemySpawner Instance;

    private void Awake() => Instance = this;

    public void OnSpawnButtonPressed(int enemyTypeIndex)
    {
        if (Runner == null) return;

        Vector2 spawnPos = Vector2.zero;
        if (spawnPoint != null) spawnPos = (Vector2)spawnPoint.position;

        int count = 1;
        if (defaultCounts != null && enemyTypeIndex >= 0 && enemyTypeIndex < defaultCounts.Length)
            count = Mathf.Max(1, defaultCounts[enemyTypeIndex]);

        RPC_RequestSpawnWave(enemyTypeIndex, spawnPos, count, spawnInterval);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestSpawnWave(int typeIndex, Vector2 spawnPos, int count, float interval)
    {
        if (!Runner.IsServer) return;

        StartCoroutine(SpawnWaveCoroutine(typeIndex, spawnPos, count, interval));
    }

    private IEnumerator SpawnWaveCoroutine(int typeIndex, Vector2 spawnPos, int count, float interval)
    {
        for (int i = 0; i < count; i++)
        {
            Vector2 offset = new Vector2(0.2f * (i % 5), 0.2f * (i / 5));
            Vector3 finalPos = (Vector3)(spawnPos + offset);

            NetworkObject no = null;
            try
            {
                no = Runner.Spawn(enemyPrefab, finalPos, Quaternion.identity, PlayerRef.None);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[EnemySpawner] Spawn failed: " + ex.Message);
            }

            if (no != null)
            {
                if (no.TryGetComponent<EnemyAI>(out var ai))
                {
                    ai.InitStats((EnemyType)typeIndex);
                    ai.SetPath(startPath);

                    ai.SetInitialNetworkPosition(finalPos);
                }


                var enNet = no.GetComponent<EnemyNetwork>();
                if (enNet != null) enNet.Team = 1;
                var tv = no.GetComponent<TeamVisibility>();
                if (tv != null) tv.UpdateVisibility();
            }
            else
            {
                Debug.LogWarning("[EnemySpawner] Runner.Spawn returned null object.");
            }

            yield return new WaitForSeconds(interval);
        }
    }
}
