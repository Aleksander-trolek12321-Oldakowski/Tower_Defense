using UnityEngine;
using TMPro;
using Fusion;
using Networking;

public class MoneyUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI moneyText;
    private PlayerNetwork _player;

    void Update()
    {
        if (_player == null)
        {
            _player = PlayerNetwork.Local;
            return;
        }

        UpdateMoneyUI(_player.Money);
    }

    private void UpdateMoneyUI(int money)
    {
        if (moneyText != null)
        {
            moneyText.text = $"{money}";
        }
    }
}