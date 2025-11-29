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
        Debug.Log($"[EnemySpawner] OnSpawnButtonPressed called with type: {enemyTypeIndex}");
        
        if (Runner == null) 
        {
            Debug.LogError("[EnemySpawner] Runner is null!");
            return;
        }

        Vector2 spawnPos = Vector2.zero;
        if (spawnPoint != null) 
            spawnPos = (Vector2)spawnPoint.position;

        int count = 1;
        if (defaultCounts != null && enemyTypeIndex >= 0 && enemyTypeIndex < defaultCounts.Length)
            count = Mathf.Max(1, defaultCounts[enemyTypeIndex]);

        var local = Networking.PlayerNetwork.Local;
        if (local != null && local.Team == 1)
        {
            int cost = GamePlayManager.Instance.GetUnitCost(enemyTypeIndex);
            Debug.Log($"[EnemySpawner] Checking money. Cost: {cost}, Player Money: {local.Money}");
            
            if (local.Money >= cost)
            {
                Debug.Log($"[EnemySpawner] Money sufficient, calling RPC");
                RPC_RequestSpawnWave(enemyTypeIndex, spawnPos, count, spawnInterval);
            }
            else
            {
                Debug.Log($"[EnemySpawner] Not enough money to spawn unit. Cost: {cost}, Money: {local.Money}");
            }
        }
        else
        {
            Debug.LogWarning($"[EnemySpawner] Local player not found or wrong team. Local: {local != null}, Team: {local?.Team}");
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_RequestSpawnWave(int typeIndex, Vector2 spawnPos, int count, float interval, RpcInfo info = default)
    {
        Debug.Log($"[EnemySpawner] RPC_RequestSpawnWave received on server. typeIndex: {typeIndex}, from player: {info.Source}");

        if (!Runner.IsServer) 
        {
            Debug.LogWarning("[EnemySpawner] RPC called but not on server!");
            return;
        }

        var playerObj = Runner.GetPlayerObject(info.Source);
        var playerNetwork = playerObj?.GetComponent<PlayerNetwork>();
        if (playerNetwork == null) return;

        int cost = GamePlayManager.Instance.GetUnitCost(typeIndex);
        if (playerNetwork.Money < cost)
        {
            Debug.Log($"[EnemySpawner] Server: Not enough money for player {info.Source}. Cost: {cost}, Money: {playerNetwork.Money}");
            return;
        }

        playerNetwork.Money -= cost;

        StartCoroutine(SpawnWaveCoroutine(typeIndex, spawnPos, count, interval));
        
        Debug.Log($"[EnemySpawner] Spawning wave of {count} units type {typeIndex} for player {info.Source}. Money left: {playerNetwork.Money}");
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