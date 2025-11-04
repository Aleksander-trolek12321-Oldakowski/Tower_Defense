using UnityEngine;
using System.Linq;

namespace debug
{
    public class SceneHealthLogger : MonoBehaviour
    {
        float lastCheck = 0f;
        void Update()
        {
            if (Time.time - lastCheck < 1.5f) return;
            lastCheck = Time.time;

            var allRenderers = FindObjectsOfType<Renderer>();
            int active = allRenderers.Count(r => r.enabled && r.gameObject.activeInHierarchy);
            int total = allRenderers.Length;
            Debug.Log($"[SceneHealth] Renderers active: {active}/{total}");

            var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            int inactiveRoots = roots.Count(g => !g.activeInHierarchy);
            Debug.Log($"[SceneHealth] Root objects inactive: {inactiveRoots}/{roots.Length}");

            if (inactiveRoots > 0)
            {
                var names = roots.Where(g => !g.activeInHierarchy).Select(g => g.name).ToArray();
                Debug.Log("[SceneHealth] Inactive roots: " + string.Join(", ", names));
            }
        }
    }
}
