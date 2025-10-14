using System;
using UnityEngine;
using UnityEngine.UI;
using Controller;

namespace UI
{
    public class AttackerUnitPanelManager : MonoBehaviour
    {
        [Header("UI Elements")]
        public Button[] unitButtons;        // buttons in UI (assign in inspector)
        public Image[] greyMasks;           // same size as buttons; show when not affordable
        public int[] unitCosts;             // cost per unit index (match GamePlayManager.unitPrefabs index)
        public int[] unitIndices;           // which unit index each button represents (usually 0..n-1)

        int selectedIndex = -1;

        void Start()
        {
            for (int i = 0; i < unitButtons.Length; i++)
            {
                int idx = i;
                unitButtons[i].onClick.AddListener(() => OnUnitButtonClicked(idx));
            }

            CameraController.OnMapClick += OnMapClick;
        }

        void OnDestroy()
        {
            CameraController.OnMapClick -= OnMapClick;
        }

        void Update()
        {
            UpdateAffordability();
        }

        void UpdateAffordability()
        {
            var local = Networking.PlayerNetwork.Local;
            int money = local != null ? local.Money : 0;
            for (int i = 0; i < unitButtons.Length; i++)
            {
                int unitIdx = (i < unitIndices.Length) ? unitIndices[i] : i;
                int cost = (i < unitCosts.Length) ? unitCosts[i] : 999999;
                bool affordable = money >= cost;
                unitButtons[i].interactable = affordable;
                if (greyMasks != null && i < greyMasks.Length && greyMasks[i] != null)
                    greyMasks[i].gameObject.SetActive(!affordable);
            }

            // debug once per second to monitor money/affordability
            if (Time.frameCount % 60 == 0)
            {
                Debug.Log($"[AttackerUnitPanel] money={money}, selectedIndex={selectedIndex}");
            }
        }

        public void OnUnitButtonClicked(int buttonIndex)
        {
            if (selectedIndex == buttonIndex)
            {
                SetSelected(buttonIndex, false);
                selectedIndex = -1;
                return;
            }

            if (selectedIndex != -1)
                SetSelected(selectedIndex, false);

            selectedIndex = buttonIndex;
            SetSelected(selectedIndex, true);
        }

        void SetSelected(int btnIndex, bool sel)
        {
            var colors = unitButtons[btnIndex].colors;
            colors.normalColor = sel ? Color.white : Color.grey;
            unitButtons[btnIndex].colors = colors;
        }

        void OnMapClick(Vector2 worldPos)
        {
            if (selectedIndex == -1) return;
            var local = Networking.PlayerNetwork.Local;
            if (local == null) return;
            if (local.Team != 1) return; // only attackers use this panel

            int unitIdx = (selectedIndex < unitIndices.Length) ? unitIndices[selectedIndex] : selectedIndex;
            int cost = (selectedIndex < unitCosts.Length) ? unitCosts[selectedIndex] : int.MaxValue;
            if (local.Money < cost)
            {
                Debug.Log("[AttackerUnitPanel] Not enough money");
                return;
            }

            Debug.Log($"[UI] RequestSpawnUnit: unitIdx={unitIdx} at {worldPos} | playerMoney={local.Money}");
            local.RPC_RequestSpawnUnit(unitIdx, worldPos);

            SetSelected(selectedIndex, false);
            selectedIndex = -1;
        }

        // helper used by runtime-attacher in PlayerNetwork
        public void OnUnitButtonClickedRuntime(int buttonIndex)
        {
            OnUnitButtonClicked(buttonIndex);
        }
    }
}
