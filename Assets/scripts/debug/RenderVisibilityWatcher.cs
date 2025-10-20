using System;
using System.Collections.Generic;
using UnityEngine;

namespace debug
{
    public class RenderVisibilityWatcher : MonoBehaviour
    {
        public float checkInterval = 0.5f;
        public int maxReportedChangesPerTick = 20;
        public bool logAllRenderersAtStart = true;

        Camera mainCam;
        Dictionary<Renderer, RendererState> states = new Dictionary<Renderer, RendererState>();
        float lastCheck = 0f;

        void Start()
        {
            mainCam = Camera.main;
            if (mainCam == null)
                Debug.LogWarning("[RVW] No Camera.main found at Start!");

            CaptureInitial();
        }

        void CaptureInitial()
        {
            states.Clear();
            var all = FindObjectsOfType<Renderer>(true);
            foreach (var r in all) states[r] = GetState(r);

            if (logAllRenderersAtStart)
            {
                Debug.Log($"[RVW] Initial renderer count: {all.Length}");
                int i = 0;
                foreach (var r in all)
                {
                    Debug.Log(RendererSummary(r, states[r]));
                    if (++i >= 50) { Debug.Log("[RVW] (truncated initial list after 50 entries)"); break; }
                }
            }
        }

        void Update()
        {
            if (Time.time - lastCheck < checkInterval) return;
            lastCheck = Time.time;

            if (mainCam == null) mainCam = Camera.main;

            var all = FindObjectsOfType<Renderer>(true);
            int reported = 0;
            foreach (var r in all)
            {
                RendererState newState = GetState(r);
                if (!states.ContainsKey(r))
                {
                    states[r] = newState;
                    Debug.Log($"[RVW] New renderer discovered: {RendererSummary(r, newState)}");
                    continue;
                }
                var old = states[r];
                // if any important flag changed, log it
                if (HasSignificantChange(old, newState))
                {
                    Debug.LogWarning($"[RVW] Renderer state changed: {r.gameObject.name}\n  OLD: {RendererSummaryText(old)}\n  NEW: {RendererSummaryText(newState)}\n  Camera cullingMask: {DescribeMask(mainCam != null ? mainCam.cullingMask : -1)}\n  StackTrace(at detection):\n{Environment.StackTrace}");
                    states[r] = newState;
                    reported++;
                    if (reported >= maxReportedChangesPerTick) break;
                }
            }
        }

        bool HasSignificantChange(RendererState a, RendererState b)
        {
            // visibleToCamera / enabled / active / layer / sorting / shader / material alpha / bounds center diff large
            if (a.inFrustum != b.inFrustum) return true;
            if (a.rendererEnabled != b.rendererEnabled) return true;
            if (a.activeInHierarchy != b.activeInHierarchy) return true;
            if (a.layer != b.layer) return true;
            if (a.sortingLayer != b.sortingLayer) return true;
            if (a.sortingOrder != b.sortingOrder) return true;
            if (a.shaderName != b.shaderName) return true;
            if (!Mathf.Approximately(a.materialAlpha, b.materialAlpha)) return true;
            if (Vector3.Distance(a.boundsCenter, b.boundsCenter) > 0.01f) return true;
            return false;
        }

        RendererState GetState(Renderer r)
        {
            RendererState s = new RendererState();
            s.renderer = r;
            s.gameObjectName = r.gameObject.name;
            s.layer = r.gameObject.layer;
            s.activeInHierarchy = r.gameObject.activeInHierarchy;
            s.rendererEnabled = r.enabled;
            s.boundsCenter = r.bounds.center;
            s.boundsSize = r.bounds.size;
            s.sortingLayer = GetSortingLayerName(r);
            s.sortingOrder = GetSortingOrder(r);
            s.shaderName = GetShaderName(r);
            s.materialAlpha = GetMaterialAlpha(r);
            s.inFrustum = IsInCameraFrustum(mainCam, r);
            s.isVisibleFlag = r.isVisible;
            return s;
        }

        static bool IsInCameraFrustum(Camera cam, Renderer r)
        {
            if (cam == null || r == null) return false;
            Plane[] planes = GeometryUtility.CalculateFrustumPlanes(cam);
            return GeometryUtility.TestPlanesAABB(planes, r.bounds);
        }

        static string GetSortingLayerName(Renderer r)
        {
            try
            {
                // SpriteRenderer/tilemap has sortingLayerName
                var sr = r as SpriteRenderer;
                if (sr != null) return sr.sortingLayerName;
                // fallback via reflection for other renderers
                var prop = r.GetType().GetProperty("sortingLayerName");
                if (prop != null) return (string)prop.GetValue(r, null);
            }
            catch { }
            return "(none)";
        }

        static int GetSortingOrder(Renderer r)
        {
            try
            {
                var sr = r as SpriteRenderer;
                if (sr != null) return sr.sortingOrder;
                var prop = r.GetType().GetProperty("sortingOrder");
                if (prop != null) return (int)prop.GetValue(r, null);
            }
            catch { }
            return 0;
        }

        static string GetShaderName(Renderer r)
        {
            try
            {
                if (r.sharedMaterial != null) return r.sharedMaterial.shader.name;
            }
            catch { }
            return "(no shader)";
        }

        static float GetMaterialAlpha(Renderer r)
        {
            try
            {
                var mat = r.sharedMaterial;
                if (mat == null) return 1f;
                if (mat.HasProperty("_Color"))
                {
                    Color c = mat.color;
                    return c.a;
                }
                // try common property names
                if (mat.HasProperty("_BaseColor"))
                {
                    Color c = mat.GetColor("_BaseColor");
                    return c.a;
                }
            }
            catch { }
            return 1f;
        }

        static string DescribeMask(int mask)
        {
            if (mask == -1) return "Everything";
            List<string> names = new List<string>();
            for (int i = 0; i < 32; i++)
            {
                if ((mask & (1 << i)) != 0)
                {
                    var nm = LayerMask.LayerToName(i);
                    names.Add(string.IsNullOrEmpty(nm) ? i.ToString() : nm);
                    if (names.Count >= 8) break;
                }
            }
            return string.Join(",", names);
        }

        string RendererSummary(Renderer r, RendererState s)
        {
            return $"[RVW] {r.gameObject.name} layer={LayerMask.LayerToName(s.layer)} active={s.activeInHierarchy} enabled={s.rendererEnabled} inFrustum={s.inFrustum} isVisibleFlag={s.isVisibleFlag} sorting={s.sortingLayer}/{s.sortingOrder} shader={s.shaderName} alpha={s.materialAlpha} bounds={s.boundsCenter.ToString("F3")}/{s.boundsSize.ToString("F3")}";
        }

        string RendererSummaryText(RendererState s)
        {
            return $"name={s.gameObjectName} layer={s.layer} active={s.activeInHierarchy} enabled={s.rendererEnabled} inFrustum={s.inFrustum} isVisibleFlag={s.isVisibleFlag} sorting={s.sortingLayer}/{s.sortingOrder} shader={s.shaderName} alpha={s.materialAlpha} boundsCenter={s.boundsCenter}";
        }

        class RendererState
        {
            public Renderer renderer;
            public string gameObjectName;
            public int layer;
            public bool activeInHierarchy;
            public bool rendererEnabled;
            public bool inFrustum;
            public bool isVisibleFlag;
            public Vector3 boundsCenter;
            public Vector3 boundsSize;
            public string sortingLayer;
            public int sortingOrder;
            public string shaderName;
            public float materialAlpha;
        }
    }
}
