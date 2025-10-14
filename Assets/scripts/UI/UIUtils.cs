using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UI
{
    public static class UIUtils
    {
        public static bool IsPointerOverUI()
        {
            if (EventSystem.current == null) return false;

            PointerEventData pointerData = new PointerEventData(EventSystem.current);
            if (Input.touchCount > 0)
                pointerData.position = Input.GetTouch(0).position;
            else
                pointerData.position = Input.mousePosition;

            var results = new List<RaycastResult>();
            var grs = Object.FindObjectsOfType<GraphicRaycaster>();
            foreach (var gr in grs)
            {
                results.Clear();
                gr.Raycast(pointerData, results);
                if (results != null && results.Count > 0)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
