using System;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.Android.Gradle.Manifest;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace debug {
    [DisallowMultipleComponent]
    public class DeactivationWatcher : MonoBehaviour
    {
        Dictionary<GameObject, bool> states = new Dictionary<GameObject, bool>();
        float checkInterval = 0.5f;
        float last = 0f;

        void Start()
        {
            CacheAllRoots();
        }

        void CacheAllRoots()
        {
            states.Clear();
            var roots = SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (var g in roots)
                states[g] = g.activeInHierarchy;
        }

        void Update()
        {
            if (Time.time - last < checkInterval) return;
            last = Time.time;

            var roots = SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (var g in roots)
            {
                bool prev = states.ContainsKey(g) ? states[g] : g.activeInHierarchy;
                bool now = g.activeInHierarchy;
                if (prev != now)
                {
                    states[g] = now;
                    if (!now)
                    {
                        UnityEngine.Debug.LogWarning($"[DeactivationWatcher] Root went INACTIVE: {g.name}\nStackTrace:\n{GetStackTraceForDeactivate()}");
                    }
                    else
                    {
                        UnityEngine.Debug.Log($"[DeactivationWatcher] Root went ACTIVE: {g.name}");
                    }
                }
            }
        }

        // helper giving a compact managed stack trace (skips Unity internals)
        string GetStackTraceForDeactivate()
        {
            var st = new StackTrace(true);
            var frames = st.GetFrames();
            if (frames == null) return "(no stacktrace)";
            List<string> lines = new List<string>();
            foreach (var f in frames)
            {
                var file = f.GetFileName();
                if (string.IsNullOrEmpty(file)) continue;
                // skip unity internal frames by pattern
                if (file.Contains("/Unity/") || file.Contains("\\Unity\\")) continue;
                lines.Add($"{System.IO.Path.GetFileName(file)}:{f.GetFileLineNumber()}  {f.GetMethod()}");
                if (lines.Count >= 10) break;
            }
            return lines.Count > 0 ? string.Join("\n", lines) : "(no relevant managed stacktrace)";
        }
    }
}
