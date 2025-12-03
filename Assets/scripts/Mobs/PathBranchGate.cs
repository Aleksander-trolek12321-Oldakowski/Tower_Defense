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

    [Header("Visuals")]
    public Sprite activeSprite;
    public Sprite inactiveSprite;

    SpriteRenderer arrowSpriteRenderer;

    void Awake()
    {
        if (arrowVisual == null)
        {
            arrowSpriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
            if (arrowSpriteRenderer != null)
                arrowVisual = arrowSpriteRenderer.gameObject;
        }
        else
        {
            arrowSpriteRenderer = arrowVisual.GetComponent<SpriteRenderer>();
        }

        UpdateArrowVisualState();
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

        UpdateArrowVisualState();
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
            Debug.Log("[PathBranchGate] Only Attacker can use gates. Click ignored.");
            return;
        }

        Debug.Log($"[PathBranchGate] Gate clicked for path {targetPath?.name}, branch {branchIndex}");

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

        if (gatesByPath.TryGetValue(path, out var list))
        {
            foreach (var g in list)
            {
                if (g != null) g.UpdateArrowVisualState();
            }
        }

        if (Networking.GamePlayManager.Instance != null && Networking.GamePlayManager.Instance.Runner != null && Networking.GamePlayManager.Instance.Runner.IsServer)
        {
            Networking.GamePlayManager.Instance.RPC_NotifyPathBranch(path.name, branchIdx);
        }
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

        if (gatesByPath.TryGetValue(path, out var list))
        {
            foreach (var g in list)
                g?.UpdateArrowVisualState();
        }

        if (Networking.GamePlayManager.Instance != null && Networking.GamePlayManager.Instance.Runner != null && Networking.GamePlayManager.Instance.Runner.IsServer)
        {
            Networking.GamePlayManager.Instance.RPC_NotifyPathBranch(path.name, -1);
        }
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

    public static void ClientSetForcedBranchForPath(string pathName, int branchIdx)
    {
        if (string.IsNullOrEmpty(pathName)) return;

        if (!PathManager.Instances.TryGetValue(pathName, out var path))
        {
            Debug.LogWarning($"[PathBranchGate] Client cannot find PathManager '{pathName}' (did you use different names?).");
            return;
        }

        if (gatesByPath.TryGetValue(path, out var list))
        {
            foreach (var g in list)
            {
                if (g == null) continue;

                bool isActive;
                if (branchIdx >= 0)
                    isActive = (branchIdx == g.branchIndex);
                else
                    isActive = (g.branchIndex == 0);

                if (g.arrowSpriteRenderer == null && g.arrowVisual != null)
                    g.arrowSpriteRenderer = g.arrowVisual.GetComponent<SpriteRenderer>();

                if (g.arrowSpriteRenderer != null)
                {
                    if (g.activeSprite != null && g.inactiveSprite != null)
                        g.arrowSpriteRenderer.sprite = isActive ? g.activeSprite : g.inactiveSprite;

                    g.arrowSpriteRenderer.enabled = true;
                    try
                    {
                        g.arrowSpriteRenderer.sortingLayerName = "Default";
                        g.arrowSpriteRenderer.sortingOrder = 500;
                    }
                    catch { }
                }

                if (g.arrowVisual != null)
                {
                    g.arrowVisual.SetActive(true);
                    var pos = g.arrowVisual.transform.position;
                    pos.z = 0f;
                    g.arrowVisual.transform.position = pos;
                }
            }
        }
    }

    public void UpdateArrowVisualState()
    {
        bool isActive = false;

        if (targetPath == null)
        {
            isActive = (branchIndex == 0);
        }
        else
        {
            int forced = GetForcedBranchForPath(targetPath);

            if (forced >= 0)
            {
                isActive = (forced == branchIndex);
            }
            else
            {
                isActive = (branchIndex == 0);
            }
        }

        if (arrowSpriteRenderer == null && arrowVisual != null)
            arrowSpriteRenderer = arrowVisual.GetComponent<SpriteRenderer>();

        if (arrowSpriteRenderer != null)
        {
            if (activeSprite != null && inactiveSprite != null)
            {
                arrowSpriteRenderer.sprite = isActive ? activeSprite : inactiveSprite;
            }

            arrowSpriteRenderer.enabled = true;
            try
            {
                arrowSpriteRenderer.sortingLayerName = "Default";
                arrowSpriteRenderer.sortingOrder = 500;
            }
            catch { }
        }

        if (arrowVisual != null)
        {
            arrowVisual.SetActive(true);
            var pos = arrowVisual.transform.position;
            pos.z = 0f;
            arrowVisual.transform.position = pos;
        }
    }
}
