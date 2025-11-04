using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Controller;
using Networking;

namespace UI
{
    public enum AbilityType
    {
        DamageBoost = 0,
        FreezeTurret = 1,
        SpeedBoost = 2,
        Fireball = 3,
        FreezeArea = 4,
        TurretBoost = 5
    }

    public class AbilityPanelManager : MonoBehaviour
    {
        [Header("UI")]
        public Button[] abilityButtons;          
        public Image[] cooldownOverlays;        

        [Header("Optional local cooldowns (visual only)")]
        public float[] abilityCooldowns;         

        // internal
        int aimingIndex = -1;                    
        float[] localCooldownTimers;

        void Start()
        {
            int n = abilityButtons != null ? abilityButtons.Length : 0;
            localCooldownTimers = new float[n];

            for (int i = 0; i < n; i++)
            {
                int idx = i;
                if (abilityButtons[i] != null)
                    abilityButtons[i].onClick.AddListener(() => OnAbilityButtonClicked(idx));
            }

            CameraController.OnMapClick += OnMapClick;
        }

        void OnDestroy()
        {
            CameraController.OnMapClick -= OnMapClick;
            if (abilityButtons != null)
            {
                foreach (var b in abilityButtons)
                {
                    if (b != null) b.onClick.RemoveAllListeners();
                }
            }
        }

        void Update()
        {
            // update lokalnych timerów cooldownu (visual)
            for (int i = 0; i < localCooldownTimers.Length; i++)
            {
                if (localCooldownTimers[i] > 0f)
                {
                    localCooldownTimers[i] -= Time.deltaTime;
                    if (localCooldownTimers[i] <= 0f)
                        localCooldownTimers[i] = 0f;
                }

                if (cooldownOverlays != null && i < cooldownOverlays.Length && cooldownOverlays[i] != null)
                {
                    cooldownOverlays[i].gameObject.SetActive(localCooldownTimers[i] > 0f);
                }

                if (abilityButtons != null && i < abilityButtons.Length && abilityButtons[i] != null)
                {
                    abilityButtons[i].interactable = localCooldownTimers[i] <= 0f;
                }
            }
        }

        void OnAbilityButtonClicked(int index)
        {
            var local = Networking.PlayerNetwork.Local;
            if (local == null) { Debug.LogWarning("[AbilityPanel] No local PlayerNetwork"); return; }

            AbilityType type = (index >= 0 && index < System.Enum.GetValues(typeof(AbilityType)).Length) ? (AbilityType)index : AbilityType.DamageBoost;

            // area / target abilities -> enter aiming mode
            if (type == AbilityType.Fireball || type == AbilityType.FreezeArea || type == AbilityType.FreezeTurret)
            {
                aimingIndex = index;
                Debug.Log($"[AbilityPanel] Celowanie: {type} - kliknij na mapę (lub prawy przycisk żeby anulować)");
                return;
            }

            // immediate ability -> send RPC with worldPos = zero
            local.RPC_RequestUseAbility(index, Vector2.zero);
            StartLocalCooldown(index);
        }

        void OnMapClick(Vector2 worldPos)
        {
            if (aimingIndex == -1) return;
            var local = Networking.PlayerNetwork.Local;
            if (local == null) return;

            // send RPC with position
            local.RPC_RequestUseAbility(aimingIndex, worldPos);
            StartLocalCooldown(aimingIndex);
            aimingIndex = -1;
        }

        public void CancelAiming()
        {
            aimingIndex = -1;
        }

        void StartLocalCooldown(int index)
        {
            if (abilityCooldowns != null && index < abilityCooldowns.Length)
                localCooldownTimers[index] = abilityCooldowns[index];
            else
                localCooldownTimers[index] = 3f; // fallback
        }
    }
}
