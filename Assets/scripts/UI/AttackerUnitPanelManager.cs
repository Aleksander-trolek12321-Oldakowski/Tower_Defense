using System;
using UnityEngine;
using UnityEngine.UI;
using Controller;

namespace UI
{
    public class AttackerUnitPanelManager : MonoBehaviour
    {
        [Header("UI Elements")]
        public Button[] unitButtons;        
        public Image[] greyMasks;           
        public int[] unitCosts;             
        public int[] unitIndices;           

        [Header("Selection Visual")]
        public Image[] selectionBorders;    

        [Header("Spawn Settings")]
        public Transform defaultSpawnPoint;
        public bool useMultipleSpawnPoints = false;

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
            if (local == null) return;

            int money = local.Money;
            
            for (int i = 0; i < unitButtons.Length; i++)
            {
                int cost = GetUnitCost(i);
                bool affordable = money >= cost;
                
                if (greyMasks != null && i < greyMasks.Length && greyMasks[i] != null)
                    greyMasks[i].gameObject.SetActive(!affordable);
                
                unitButtons[i].interactable = affordable;
            }
        }

        public void OnUnitButtonClicked(int buttonIndex)
        {
            var local = Networking.PlayerNetwork.Local;
            if (local == null) return;

            int cost = GetUnitCost(buttonIndex);
            if (local.Money < cost)
            {
                Debug.Log($"[AttackerUnitPanel] Not enough money for unit {buttonIndex}. Cost: {cost}, Money: {local.Money}");
                return;
            }

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
            if (selectionBorders != null && btnIndex < selectionBorders.Length)
            {
                selectionBorders[btnIndex].gameObject.SetActive(sel);
            }
            else
            {
                var colors = unitButtons[btnIndex].colors;
                colors.normalColor = sel ? new Color(0.8f, 1f, 0.8f) : Color.white;
                unitButtons[btnIndex].colors = colors;
            }
        }

        void OnMapClick(Vector2 worldPos)
        {
            if (selectedIndex == -1) return;
            
            var local = Networking.PlayerNetwork.Local;
            if (local == null) return;
            if (local.Team != 1) return;

            int unitIdx = GetUnitIndex(selectedIndex);
            int cost = GetUnitCost(selectedIndex);

            if (local.Money < cost)
            {
                Debug.Log($"[AttackerUnitPanel] Not enough money to spawn unit. Cost: {cost}, Money: {local.Money}");
                SetSelected(selectedIndex, false);
                selectedIndex = -1;
                return;
            }

            Vector2 spawnPos = GetSpawnPosition();

            local.RPC_RequestSpawnUnit(unitIdx, spawnPos);
            Debug.Log($"[AttackerUnitPanel] Spawning unit {unitIdx} at spawn position {spawnPos}");

            SetSelected(selectedIndex, false);
            selectedIndex = -1;
            UpdateAffordability();
        }

        private Vector2 GetSpawnPosition()
        {
            if (defaultSpawnPoint != null)
                return (Vector2)defaultSpawnPoint.position;

            if (Networking.GamePlayManager.Instance != null && 
                Networking.GamePlayManager.Instance.attackerSpawnPoints != null && 
                Networking.GamePlayManager.Instance.attackerSpawnPoints.Length > 0)
            {
                if (useMultipleSpawnPoints)
                {
                    int randomIndex = UnityEngine.Random.Range(0, Networking.GamePlayManager.Instance.attackerSpawnPoints.Length);
                    return (Vector2)Networking.GamePlayManager.Instance.attackerSpawnPoints[randomIndex].position;
                }
                else
                {
                    return (Vector2)Networking.GamePlayManager.Instance.attackerSpawnPoints[0].position;
                }
            }

            Debug.LogWarning("[AttackerUnitPanel] No spawn point assigned, using (0,0)");
            return Vector2.zero;
        }

        public void OnUnitButtonClickedRuntime(int index)
        {
            Vector2 spawnPos = GetSpawnPosition();

            var local = Networking.PlayerNetwork.Local;
            if (local != null)
            {
                int cost = GetUnitCost(index);
                if (local.Money >= cost)
                {
                    local.RPC_RequestSpawnUnit(index, spawnPos);
                    UpdateAffordability();
                }
                else
                {
                    Debug.Log($"[AttackerUnitPanel] Not enough money for runtime spawn. Cost: {cost}, Money: {local.Money}");
                }
            }
            else
            {
                Debug.LogWarning("[AttackerUnitPanel] No local PlayerNetwork available to request spawn.");
            }
        }

        private int GetUnitCost(int buttonIndex)
        {
            if (buttonIndex < unitCosts.Length)
                return unitCosts[buttonIndex];
            
            int unitIdx = GetUnitIndex(buttonIndex);
            return Networking.GamePlayManager.Instance != null ? 
                Networking.GamePlayManager.Instance.GetUnitCost(unitIdx) : 999999;
        }

        private int GetUnitIndex(int buttonIndex)
        {
            if (buttonIndex < unitIndices.Length)
                return unitIndices[buttonIndex];
            return buttonIndex;
        }
    }
}