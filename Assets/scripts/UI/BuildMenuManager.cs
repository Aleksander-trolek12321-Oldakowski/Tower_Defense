using System;
using UnityEngine;
using UnityEngine.UI;
using Networking;

namespace UI
{
    public class BuildMenuManager : MonoBehaviour
    {
        [Header("UI")]
        public GameObject rootPanel;
        public Button[] towerButtons;
        public Image[] greyMasks;

        [Header("Tower config")]
        public int[] towerIndices;
        public int[] towerCosts;

        [Header("Debug")]
        public bool logAffordability = true;

        int currentSpotId = -1;
        int frameCounter = 0;

        void Reset()
        {
            if (rootPanel == null)
                rootPanel = this.gameObject;
        }

        void Start()
        {
            if (rootPanel == null)
            {
                Debug.LogWarning("[BuildMenuManager] rootPanel is null; assigning this.gameObject.");
                rootPanel = this.gameObject;
            }

            rootPanel.SetActive(false);

            if (towerButtons != null)
            {
                for (int i = 0; i < towerButtons.Length; i++)
                {
                    int idx = i;
                    if (towerButtons[i] != null)
                    {
                        towerButtons[i].onClick.AddListener(() => OnTowerButtonClicked(idx));
                    }
                }
            }
            else
            {
                Debug.LogWarning("[BuildMenuManager] towerButtons not assigned in inspector.");
            }
        }

        void Update()
        {
            UpdateAffordability();

            frameCounter++;
            if (logAffordability && frameCounter % 60 == 0)
            {
                var local = Networking.PlayerNetwork.Local;
                int money = (local != null) ? local.Money : -999;
                string costsStr = GetCostsString();
                Debug.Log($"[BuildMenuManager] money={money} spot={currentSpotId} costs={costsStr}");
            }
        }

        void UpdateAffordability()
        {
            if (towerButtons == null || towerButtons.Length == 0) return;

            var local = Networking.PlayerNetwork.Local;
            int money = (local != null) ? local.Money : 0;

            for (int i = 0; i < towerButtons.Length; i++)
            {
                if (towerButtons[i] == null) continue;

                int towerIndex = (towerIndices != null && i < towerIndices.Length) ? towerIndices[i] : i;

                int cost = int.MaxValue;
                if (GamePlayManager.Instance != null)
                {
                    cost = GamePlayManager.Instance.GetTowerCost(towerIndex);
                }
                else if (towerCosts != null && i < towerCosts.Length)
                {
                    cost = towerCosts[i];
                }

                bool affordable = (money >= cost);

                towerButtons[i].interactable = affordable;

                // set grey mask (if provided) - active when NOT affordable
                if (greyMasks != null && i < greyMasks.Length && greyMasks[i] != null)
                {
                    greyMasks[i].gameObject.SetActive(!affordable);
                }

                try
                {
                    var colors = towerButtons[i].colors;
                    colors.normalColor = affordable ? Color.white : Color.grey;
                    towerButtons[i].colors = colors;
                }
                catch { /* ignore */ }
            }
        }

        public void Open(int spotId)
        {
            currentSpotId = spotId;

            if (rootPanel == null)
            {
                Debug.LogWarning("[BuildMenuManager] Open called but rootPanel is null.");
                return;
            }

            // Ensure root active
            rootPanel.SetActive(true);

            // Bring to front and ensure canvas settings
            rootPanel.transform.SetAsLastSibling();

            // If there is a Canvas on root or parents, ensure it's enabled and above other UI
            var canvas = rootPanel.GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                canvas.enabled = true;
                try
                {
                    // ensure it's overlay or increase sortingOrder
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    canvas.sortingOrder = 1000;
                }
                catch { }
            }

            Canvas.ForceUpdateCanvases();

            Debug.Log($"[BuildMenu] Opened for spot {spotId} (root active={rootPanel.activeSelf}) canvas={(canvas!=null?canvas.name:"none")} renderMode={(canvas!=null?canvas.renderMode.ToString():"-")}, sorting={ (canvas!=null?canvas.sortingOrder:0) }");

            UpdateAffordability();
        }

        public void Close()
        {
            currentSpotId = -1;
            if (rootPanel != null)
                rootPanel.SetActive(false);

            Debug.Log("[BuildMenu] Closed");
        }

        void OnTowerButtonClicked(int buttonIndex)
        {
            Debug.Log($"[BuildMenu] OnTowerButtonClicked idx={buttonIndex} currentSpot={currentSpotId}");

            var local = Networking.PlayerNetwork.Local;

            if (local == null)
            {
                var pns = FindObjectsOfType<Networking.PlayerNetwork>();
                foreach (var pn in pns)
                {
                    if (pn == null) continue;
                    if (pn.Object != null && pn.Object.HasInputAuthority)
                    {
                        local = pn;
                        Debug.Log("[BuildMenu] Fallback: found local PN via HasInputAuthority.");
                        break;
                    }
                }
            }

            if (local == null || local.Object == null || !local.Object.HasInputAuthority)
            {
                var allPns = FindObjectsOfType<Networking.PlayerNetwork>();
                foreach (var pn in allPns)
                {
                    if (pn == null) continue;
                    try {
                        if (pn.Object != null && pn.Object.HasInputAuthority)
                        {
                            local = pn;
                            Debug.Log("[BuildMenu] Second fallback: selected PN with HasInputAuthority.");
                            break;
                        }
                    } catch { }
                }
            }

            if (local == null || local.Object == null || !local.Object.HasInputAuthority)
            {
                Debug.LogWarning("[BuildMenu] No local player found. Cannot place tower.");
                return;
            }

            if (local.Team != 0)
            {
                Debug.LogWarning("[BuildMenu] Player is not defender - cannot place towers.");
                return;
            }

            if (currentSpotId < 0)
            {
                Debug.LogWarning("[BuildMenu] No spot selected.");
                return;
            }

            int towerIndex = (towerIndices != null && buttonIndex < towerIndices.Length) ? towerIndices[buttonIndex] : buttonIndex;
            int cost = int.MaxValue;
            if (GamePlayManager.Instance != null)
                cost = GamePlayManager.Instance.GetTowerCost(towerIndex);
            else
                Debug.LogWarning("[BuildMenu] GamePlayManager.Instance is NULL when clicking tower button.");

            Debug.Log($"[BuildMenu] Attempting place tower {towerIndex} at spot {currentSpotId}. PlayerMoney={local.Money}, Cost={cost}");

            if (local.Money < cost)
            {
                Debug.Log($"[BuildMenu] Not enough money to place tower {towerIndex}. Have {local.Money}, need {cost}");
                return;
            }

            local.RPC_RequestPlaceTower(towerIndex, currentSpotId);

            Close();
        }


        string GetCostsString()
        {
            if (towerButtons == null) return "";
            string s = "";
            for (int i = 0; i < towerButtons.Length; i++)
            {
                int towerIndex = (towerIndices != null && i < towerIndices.Length) ? towerIndices[i] : i;
                int cost = (GamePlayManager.Instance != null) ? GamePlayManager.Instance.GetTowerCost(towerIndex) : ((towerCosts != null && i < towerCosts.Length) ? towerCosts[i] : -1);
                s += $"[{i}:cost={cost}] ";
            }
            return s;
        }
    }
}
