using System.Collections.Generic;
using UnityEngine;

public class PathBranchGate : MonoBehaviour
{
    private static Dictionary<PathManager, List<PathBranchGate>> gatesByPath = new();
    private static Dictionary<PathManager, int> forcedBranchByPath = new();

    [Header("Konfiguracja")]
    public PathManager targetPath;
    public int branchIndex = 0;
    public GameObject arrowVisual;

    void Awake()
    {
        var sr = GetComponentInChildren<SpriteRenderer>();
        UpdateArrowVisibilityLocal();
    }

    private void OnEnable() => RegisterGate();
    private void OnDisable() => UnregisterGate();

    private void RegisterGate()
    {
        if (targetPath == null)
        {
            Debug.LogWarning($"[PathBranchGate] {name} has no targetPath assigned.");
            return;
        }

        if (!gatesByPath.TryGetValue(targetPath, out var list))
        {
            list = new List<PathBranchGate>();
            gatesByPath[targetPath] = list;
        }

        if (!list.Contains(this)) list.Add(this);

        gameObject.SetActive(true);
    }

    private void UnregisterGate()
    {
        if (targetPath == null) return;
        if (gatesByPath.TryGetValue(targetPath, out var list))
        {
            list.Remove(this);
            if (list.Count == 0) gatesByPath.Remove(targetPath);
        }
    }

    public void OnGateClicked()
    {
        var local = Networking.PlayerNetwork.Local;
        if (local != null && local.Team != 1)
        {
            Debug.Log("[PathBranchGate] Only attacker can use gates. Click ignored.");
            return;
        }

        Debug.Log($"[PathBranchGate] Gate clicked for path {targetPath.name}, branch {branchIndex}");

        if (Networking.PlayerNetwork.Local != null)
        {
            Networking.PlayerNetwork.Local.RPC_RequestSetBranch(targetPath != null ? targetPath.name : "", branchIndex);
        }
        else
        {
            Debug.LogWarning("[PathBranchGate] No local player network found!");
        }
    }

    public static void SetForcedBranchForPath(PathManager path, int branchIdx)
    {
        if (path == null) return;
        forcedBranchByPath[path] = branchIdx;
        Debug.Log($"[PathBranchGate] Forced branch for path {path.name} set to {branchIdx}");

        ApplyBranchToAllEnemiesOnPath(path, branchIdx);
    }

    public static int GetForcedBranchForPath(PathManager path)
    {
        if (path == null) return -1;
        if (forcedBranchByPath.TryGetValue(path, out var v)) return v;
        return -1;
    }

    public static void ClearForcedBranchForPath(PathManager path)
    {
        if (path == null) return;
        forcedBranchByPath.Remove(path);
    }

    private static void ApplyBranchToAllEnemiesOnPath(PathManager path, int branchIndex)
    {
        if (path == null) return;
        
        var allEnemies = FindObjectsOfType<EnemyAI>();
        int appliedCount = 0;
        
        foreach (var enemy in allEnemies)
        {
            if (enemy != null)
            {
                enemy.RPC_SetBranch(branchIndex);
                appliedCount++;
            }
        }
        
        Debug.Log($"[PathBranchGate] Applied branch {branchIndex} to {appliedCount} enemies on path {path.name}");
    }

    public void UpdateArrowVisibilityLocal()
    {
        if (arrowVisual == null)
        {
            var sr = GetComponentInChildren<SpriteRenderer>(true);
            if (sr != null) arrowVisual = sr.gameObject;
        }

        if (arrowVisual != null)
        {
            arrowVisual.SetActive(true);

            var sr = arrowVisual.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.enabled = true;
                try
                {
                    sr.sortingLayerName = "Default";
                    sr.sortingOrder = 500;
                }
                catch {  }
            }

            var pos = arrowVisual.transform.position;
            pos.z = 0f;
            arrowVisual.transform.position = pos;
        }
    }
}