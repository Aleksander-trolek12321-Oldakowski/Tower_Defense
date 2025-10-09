using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System;

namespace UI
{
    public class AbilityCard : MonoBehaviour
    {
        public int abilityIndex = 0;
        public int cost = 10;

        public Image iconImage;
        public Image backgroundImage;
        public Image greyMask; // Image over the card used to show "greyed out" state
        public Button button;

        public Action<int> onClicked; // callback to AbilityBarManager

        bool isSelected = false;

        void Start()
        {
            if (button != null)
                button.onClick.AddListener(OnClick);
        }

        public void OnClick()
        {
            onClicked?.Invoke(abilityIndex);
        }

        public void SetSelected(bool selected)
        {
            isSelected = selected;
            // simple visual: border or background color change
            if (backgroundImage != null)
                backgroundImage.color = selected ? Color.white : Color.grey;
        }

        public void SetAffordable(bool affordable)
        {
            if (greyMask != null)
                greyMask.gameObject.SetActive(!affordable);
        }

        // optional helper to update visuals (icon etc.)
        public void Setup(Sprite icon, int costValue)
        {
            if (iconImage != null) iconImage.sprite = icon;
            cost = costValue;
        }
    }
}
