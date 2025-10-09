using System;
using UnityEngine;
using UnityEngine.UI;
using Controller;
using Networking;

namespace UI
{
    public class AbilityBarManager : MonoBehaviour
    {
        public AbilityCard[] cards; // assign 3 cards in inspector
        public int[] abilityCosts = new int[3] { 20, 40, 60 };

        int selectedIndex = -1;

        void Start()
        {
            // hook cards
            for (int i = 0; i < cards.Length; i++)
            {
                int idx = i;
                cards[i].abilityIndex = i;
                cards[i].onClicked = OnCardClicked;
            }

            // subscribe to map clicks
            CameraController.OnMapClick += OnMapClick;

            UpdateAffordability();
        }

        void OnDestroy()
        {
            CameraController.OnMapClick -= OnMapClick;
        }

        void Update()
        {
            // poll money to update affordability UI
            UpdateAffordability();
        }

        void UpdateAffordability()
        {
            var local = PlayerNetwork.Local;
            int money = local != null ? local.Money : 0;
            for (int i = 0; i < cards.Length; i++)
            {
                bool affordable = (money >= abilityCosts[i]);
                cards[i].SetAffordable(affordable);
            }
        }

        void OnCardClicked(int idx)
        {
            // Toggle behaviour: click same -> deselect, click different -> select new
            if (selectedIndex == idx)
            {
                // deselect
                selectedIndex = -1;
                cards[idx].SetSelected(false);
            }
            else
            {
                // deselect previous
                if (selectedIndex != -1)
                    cards[selectedIndex].SetSelected(false);

                selectedIndex = idx;
                cards[selectedIndex].SetSelected(true);
            }
        }

        void OnMapClick(Vector2 worldPos)
        {
            if (selectedIndex == -1) return; // nothing selected
            var local = PlayerNetwork.Local;
            if (local == null) return;
            if (local.Team != 1) return; // only attacker can spawn via abilities

            int cost = abilityCosts[selectedIndex];
            if (local.Money < cost)
            {
                Debug.Log("[AbilityBar] Not enough money");
                return;
            }

            // request spawn via RPC (client -> host)
            local.RPC_RequestSpawnUnit(selectedIndex, worldPos);

            // Optionally deduct locally immediately for responsiveness; real authoritative change should come from host.
            // We'll optimistically deduct here (client-side). Host should also reward/charge accordingly.
            // But we have networked Money — better keep authoritative on host. For prototype we can subtract locally:
            // local.Money -= cost; // can't modify networked property locally except via RPC. Better let host set.
            // For now we deselect after request:
            cards[selectedIndex].SetSelected(false);
            selectedIndex = -1;
        }
    }
}
