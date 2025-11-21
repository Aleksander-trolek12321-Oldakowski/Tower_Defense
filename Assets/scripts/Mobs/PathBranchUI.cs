using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PathBranchUI : MonoBehaviour
{
    public static PathBranchUI Instance;

    [Tooltip("Przyciski-strzałki. Indeks = indeks gałęzi w PathManager.branches")]
    public List<Button> branchButtons = new List<Button>();

    private PathManager currentPath;
    private List<EnemyAI> waitingEnemies = new();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        gameObject.SetActive(false);

        for (int i = 0; i < branchButtons.Count; i++)
        {
            int index = i;
            branchButtons[i].onClick.AddListener(() => OnBranchClicked(index));
        }
    }

    /// <summary>
    /// Wołane z EnemyAI, gdy mob dojdzie do końca ścieżki, która ma gałęzie.
    /// </summary>
    public static void ShowForEnemy(EnemyAI enemy, PathManager path)
    {
        if (Instance == null || enemy == null || path == null) return;

        // jeśli to pierwsze wywołanie – ustaw aktualny path
        if (Instance.currentPath == null)
        {
            Instance.currentPath = path;
            Instance.waitingEnemies.Clear();
        }

        // jeśli UI jest już używane dla innego patha – ignorujemy (prosta wersja)
        if (Instance.currentPath != path)
            return;

        if (!Instance.waitingEnemies.Contains(enemy))
            Instance.waitingEnemies.Add(enemy);

        Instance.RefreshButtons(path);
        Instance.gameObject.SetActive(true);
    }

    private void RefreshButtons(PathManager path)
    {
        int branchCount = path.GetBranchCountSafe();

        for (int i = 0; i < branchButtons.Count; i++)
        {
            bool active = i < branchCount;
            branchButtons[i].gameObject.SetActive(active);
        }
    }

    private void OnBranchClicked(int branchIndex)
    {
        if (currentPath == null) return;

        // wyślij RPC do wszystkich mobów, które na ten wybór czekają
        foreach (var enemy in waitingEnemies)
        {
            if (enemy == null) continue;
            enemy.RPC_SetBranch(branchIndex);
        }

        waitingEnemies.Clear();
        currentPath = null;
        gameObject.SetActive(false);
    }
}
