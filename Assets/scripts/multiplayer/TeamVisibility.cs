using System;
using System.Reflection;
using UnityEngine;

namespace Networking
{
    public class TeamVisibility : MonoBehaviour
    {
        [Tooltip("Root GameObject holding sprites/visuals that should be toggled.")]
        public GameObject visuals;

        [Tooltip("If you don't have a networked component exposing OwnerTeam/Team, set this manual value.")]
        public int manualOwnerTeam = -1;

        [Tooltip("If true, object will be visible ONLY to its owner team. If false, visible to everyone.")]
        public bool restrictToOwnerTeam = true;

        // cached reflection access
        Component teamSourceComponent;
        PropertyInfo teamProp;
        FieldInfo teamField;

        Coroutine delayedCoroutine;

        void Start()
        {
            // Try find common network components
            teamSourceComponent = GetComponent("TowerNetwork") as Component;
            if (teamSourceComponent == null)
                teamSourceComponent = GetComponent("EnemyNetwork") as Component;

            // if not found, scan components for Team/OwnerTeam prop/field
            if (teamSourceComponent == null)
            {
                var comps = GetComponents<Component>();
                foreach (var c in comps)
                {
                    if (c == null) continue;
                    var tProp = c.GetType().GetProperty("OwnerTeam");
                    var tProp2 = c.GetType().GetProperty("Team");
                    var tField = c.GetType().GetField("OwnerTeam");
                    var tField2 = c.GetType().GetField("Team");
                    if (tProp != null || tProp2 != null || tField != null || tField2 != null)
                    {
                        teamSourceComponent = c;
                        teamProp = tProp ?? tProp2;
                        teamField = tField ?? tField2;
                        break;
                    }
                }
            }
            else
            {
                // populate prop/field for known component
                teamProp = teamSourceComponent.GetType().GetProperty("OwnerTeam") ?? teamSourceComponent.GetType().GetProperty("Team");
                teamField = teamSourceComponent.GetType().GetField("OwnerTeam") ?? teamSourceComponent.GetType().GetField("Team");
            }

            // do a small delayed update to allow network replication to arrive in some cases
            if (delayedCoroutine != null) StopCoroutine(delayedCoroutine);
            delayedCoroutine = StartCoroutine(DelayedUpdateVisibility());
        }

        System.Collections.IEnumerator DelayedUpdateVisibility()
        {
            yield return new WaitForSeconds(0.15f);
            DoUpdateVisibility();
            delayedCoroutine = null;
        }

        /// <summary>
        /// Public: force immediate visibility update (call this after host sets OwnerTeam).
        /// </summary>
        public void UpdateVisibility()
        {
            // cancel pending coroutine to avoid race
            if (delayedCoroutine != null)
            {
                StopCoroutine(delayedCoroutine);
                delayedCoroutine = null;
            }
            DoUpdateVisibility();
        }

        void DoUpdateVisibility()
        {
            if (!restrictToOwnerTeam)
            {
                if (visuals != null) visuals.SetActive(true);
                return;
            }

            int ownerTeam = manualOwnerTeam;

            // read from component via reflection if available
            if (teamSourceComponent != null)
            {
                try
                {
                    if (teamProp != null)
                        ownerTeam = (int)teamProp.GetValue(teamSourceComponent);
                    else if (teamField != null)
                        ownerTeam = (int)teamField.GetValue(teamSourceComponent);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[TeamVisibility] Reflection read failed: {e.Message}");
                }
            }

            int localTeam = GetLocalPlayerTeam();

            bool show = (ownerTeam == -1) ? true : (ownerTeam == localTeam);
            if (visuals != null) visuals.SetActive(show);
        }

        int GetLocalPlayerTeam()
        {
            // Look for PlayerNetwork instance which exposes Team (reflection as fallback)
            var playerNets = FindObjectsOfType<MonoBehaviour>();
            foreach (var mb in playerNets)
            {
                var t = mb.GetType();
                if (t.Name == "PlayerNetwork")
                {
                    // try property "Team"
                    var prop = t.GetProperty("Team");
                    if (prop != null)
                    {
                        try
                        {
                            var val = prop.GetValue(mb);
                            if (val is int) return (int)val;
                        }
                        catch { }
                    }
                    // try field "Team"
                    var field = t.GetField("Team");
                    if (field != null)
                    {
                        try
                        {
                            var val = field.GetValue(mb);
                            if (val is int) return (int)val;
                        }
                        catch { }
                    }

                    // if PlayerNetwork uses different API, adapt here
                }
            }

            // fallback unknown
            return -1;
        }
    }
}