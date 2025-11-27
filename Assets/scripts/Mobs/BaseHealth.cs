using Fusion;
using UnityEngine;
using UnityEngine.UI;

public class BaseHealth : NetworkBehaviour
{
    [Networked] public float HP { get; set; } = 100f;
    [Networked] public float MaxHP { get; set; } = 100f;
    [Networked] public bool IsDestroyed { get; set; } = false;

    [Header("Health Bar References")]
    public Canvas healthBarCanvas;
    public Image healthBarFill;

    private float _lastShownHP = float.MinValue;

    public override void Spawned()
    {
        Debug.Log($"[BaseHealth] Spawned (Runner.IsServer={(Runner!=null?Runner.IsServer:false)}) HP={HP}");

        if (healthBarCanvas == null)
            healthBarCanvas = GetComponentInChildren<Canvas>();

        if (healthBarFill == null && healthBarCanvas != null)
        {
            var fillObj = healthBarCanvas.transform.Find("Background/Fill");
            if (fillObj != null) healthBarFill = fillObj.GetComponent<Image>();
        }

        UpdateHealthBarImmediate();
    }

    public override void FixedUpdateNetwork()
    {
        if (Runner != null && Runner.IsServer)
        {
            if (HP <= 0f && !IsDestroyed)
            {
                IsDestroyed = true;
                OnBaseDestroyedServerSide();
            }
        }
    }

    void Update()
    {
        if (_lastShownHP != HP)
        {
            UpdateHealthBarImmediate();
            _lastShownHP = HP;
        }

        if (HP <= 0f && healthBarCanvas != null && healthBarCanvas.gameObject.activeSelf)
        {
            OnBaseDestroyedClientSide();
        }
    }

    private void UpdateHealthBarImmediate()
    {
        if (healthBarFill == null)
        {
            if (healthBarCanvas != null)
            {
                var fillObj = healthBarCanvas.transform.Find("Background/Fill");
                if (fillObj != null) healthBarFill = fillObj.GetComponent<Image>();
            }
        }

        if (healthBarFill == null) return;

        float fillAmount = (MaxHP <= 0f) ? 0f : Mathf.Clamp01(HP / MaxHP);
        healthBarFill.fillAmount = fillAmount;

        if (fillAmount > 0.6f)
            healthBarFill.color = Color.green;
        else if (fillAmount > 0.3f)
            healthBarFill.color = Color.yellow;
        else
            healthBarFill.color = Color.red;
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_TakeDamage(float damage, RpcInfo info = default)
    {
        if (!Runner.IsServer) return;
        if (IsDestroyed) return;

        HP = Mathf.Max(0f, HP - damage);
        Debug.Log($"[BaseHealth] RPC_TakeDamage: -{damage}, new HP={HP}");

        if (HP <= 0f)
        {
            GameRoundManager.Instance?.EndGame(false);
        }
    }

    private void OnBaseDestroyedServerSide()
    {
        Debug.Log("[BaseHealth] OnBaseDestroyedServerSide: base destroyed on server.");
    }

    private void OnBaseDestroyedClientSide()
    {
        Debug.Log("[BaseHealth] OnBaseDestroyedClientSide: hiding healthbar on client.");
        if (healthBarCanvas != null)
            healthBarCanvas.gameObject.SetActive(false);
    }

    private void OnBaseDestroyed()
    {
        OnBaseDestroyedClientSide();
    }
}
