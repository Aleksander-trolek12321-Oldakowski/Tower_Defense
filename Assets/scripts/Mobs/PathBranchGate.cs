using System.Collections.Generic;
using UnityEngine;

public class PathBranchGate : MonoBehaviour
{
    private static Dictionary<PathManager, List<PathBranchGate>> gatesByPath = new();
    private static Dictionary<PathManager, List<EnemyAI>> waitingEnemiesByPath = new();

    [Header("Konfiguracja")]
    [Tooltip("PathManager, na końcu którego ten gate ma się aktywować")]
    public PathManager targetPath;

    [Tooltip("Index gałęzi w targetPath.branches, którą wybiera ten gate")]
    public int branchIndex = 0;

    private void OnEnable()
    {
        RegisterGate();
    }

    private void OnDisable()
    {
        UnregisterGate();
    }

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

        if (!list.Contains(this))
            list.Add(this);
    }

    private void UnregisterGate()
    {
        if (targetPath == null) return;

        if (gatesByPath.TryGetValue(targetPath, out var list))
        {
            list.Remove(this);
            if (list.Count == 0)
                gatesByPath.Remove(targetPath);
        }
    }

    public static void NotifyEnemyArrived(EnemyAI enemy, PathManager path)
    {
        if (enemy == null || path == null) return;

        if (!waitingEnemiesByPath.TryGetValue(path, out var list))
        {
            list = new List<EnemyAI>();
            waitingEnemiesByPath[path] = list;
        }

        if (!list.Contains(enemy))
            list.Add(enemy);

        if (gatesByPath.TryGetValue(path, out var gates))
        {
            foreach (var gate in gates)
            {
                if (gate != null)
                    gate.gameObject.SetActive(true);
            }
        }
    }

    public static void NotifyEnemyRemoved(EnemyAI enemy, PathManager path)
    {
        if (enemy == null || path == null) return;
        if (!waitingEnemiesByPath.TryGetValue(path, out var list)) return;

        if (list.Remove(enemy))
        {
            list.RemoveAll(e => e == null);
            if (list.Count == 0)
                HideGatesForPath(path);
        }
    }

    public void OnGateClicked()
    {
        if (targetPath == null) return;

        if (!waitingEnemiesByPath.TryGetValue(targetPath, out var enemies) || enemies.Count == 0)
            return;

        foreach (var enemy in enemies)
        {
            if (enemy == null) continue;
            enemy.RPC_SetBranch(branchIndex);
        }

        enemies.Clear();
        HideGatesForPath(targetPath);
    }

    private static void HideGatesForPath(PathManager path)
    {
        if (!gatesByPath.TryGetValue(path, out var gates)) return;

        foreach (var gate in gates)
        {
            if (gate != null)
                gate.gameObject.SetActive(false);
        }
    }
}
