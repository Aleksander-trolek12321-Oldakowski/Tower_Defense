using System;
using System.Collections.Generic;
using UnityEngine;

namespace debug
{
    public class CameraInspectorLogger : MonoBehaviour
    {
        Dictionary<Camera, string> lastStates = new Dictionary<Camera, string>();
        float checkInterval = 1f;
        float last = 0f;

        void Update()
        {
            if (Time.time - last < checkInterval) return;
            last = Time.time;

            var cams = Camera.allCameras;
            Debug.Log($"[CameraInspector] Found {cams.Length} cameras.");
            foreach (var c in cams)
            {
                if (c == null) continue;
                string state = DescribeCamera(c);
                if (!lastStates.ContainsKey(c) || lastStates[c] != state)
                {
                    Debug.Log($"[CameraInspector] Camera change / new: name='{c.name}' enabled={c.enabled} depth={c.depth} cullingMask={DescribeMask(c.cullingMask)} clear={c.clearFlags} ortho={c.orthographic} size={c.orthographicSize} targetTex={(c.targetTexture != null)} viewport={c.rect}");
                    lastStates[c] = state;
                }
            }
        }

        string DescribeMask(int mask)
        {
            // list up to 8 layers that are on
            var layers = new List<string>();
            for (int i = 0; i < 32; i++)
            {
                if ((mask & (1 << i)) != 0)
                {
                    try { layers.Add(LayerMask.LayerToName(i)); }
                    catch { layers.Add(i.ToString()); }
                    if (layers.Count >= 8) break;
                }
            }
            return string.Join(",", layers);
        }

        string DescribeCamera(Camera c)
        {
            return $"{c.enabled}|{c.depth}|{c.cullingMask}|{c.clearFlags}|{c.orthographic}|{c.orthographicSize}|{(c.targetTexture != null)}|{c.rect}";
        }
    }
}