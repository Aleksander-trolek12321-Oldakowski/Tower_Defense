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
    
    private float _lastHP;

    public override void Spawned()
    {
        Debug.Log($"[BaseHealth] Base spawned with {HP} HP");
        
        if (healthBarCanvas == null)
            healthBarCanvas = GetComponentInChildren<Canvas>();
        
        if (healthBarFill == null)
        {
            var fillObj = healthBarCanvas?.transform.Find("Background/Fill");
            if (fillObj != null) healthBarFill = fillObj.GetComponent<Image>();
        }
        
        UpdateHealthBar();
        _lastHP = HP;
    }

    public override void FixedUpdateNetwork()
    {
        if (_lastHP != HP)
        {
            _lastHP = HP;
            UpdateHealthBar();
            
            if (HP <= 0 && !IsDestroyed)
            {
                IsDestroyed = true;
                OnBaseDestroyed();
            }
        }
    }

    private void UpdateHealthBar()
    {
        if (healthBarFill == null) return;

        float fillAmount = HP / MaxHP;
        healthBarFill.fillAmount = fillAmount;
        
        if (fillAmount > 0.6f)
            healthBarFill.color = Color.green;
        else if (fillAmount > 0.3f)
            healthBarFill.color = Color.yellow;
        else
            healthBarFill.color = Color.red;
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_TakeDamage(float damage)
    {
        if (!Runner.IsServer) return;
        if (IsDestroyed) return;

        HP = Mathf.Max(0, HP - damage);
        Debug.Log($"[BaseHealth] Base took {damage} damage. HP left: {HP}");
    }

    private void OnBaseDestroyed()
    {
        Debug.Log("[BaseHealth] GAME OVER - Base destroyed!");
        
        if (healthBarCanvas != null)
        {
            healthBarCanvas.gameObject.SetActive(false);
        }
        
    }
}