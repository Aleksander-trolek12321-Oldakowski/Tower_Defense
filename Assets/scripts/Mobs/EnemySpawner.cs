using Fusion;
using UnityEngine;

public class EnemySpawner : NetworkBehaviour
{
    [SerializeField] private NetworkPrefabRef enemyPrefab;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private PathManager startPath;

    public static EnemySpawner Instance;

    private void Awake() => Instance = this;

    // 🔹 UI button click
    public void OnSpawnButtonPressed(int enemyType)
    {
        if (Runner == null) return;
        RPC_RequestSpawn(enemyType);
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
}
