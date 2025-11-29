using System.Collections;
using Fusion;
using UnityEngine;
using Networking;
using Controller;

public class Bridge : NetworkBehaviour, IInteractable
{
    [Networked] public bool IsCollapsed { get; set; } = false;
    [Networked] public TickTimer CollapseTimer { get; set; }
    [Networked] public TickTimer CooldownTimer { get; set; }

    [Header("Config")]
    public Sprite normalSprite;
    public Sprite collapsedSprite;
    public float collapseDuration = 8f;
    public float collapseCooldown = 120f;

    [Header("Components")]
    public SpriteRenderer spriteRenderer;
    public Collider2D bridgeCollider;

    [Header("Effects")]
    public GameObject collapseEffect;
    public GameObject repairEffect;

    [HideInInspector] public int bridgeIndex = -1;

    private bool isInitialized = false;

    public override void Spawned()
    {
        base.Spawned();
        
        Debug.Log($"[Bridge] Spawned - Bridge {bridgeIndex}, IsServer: {Runner.IsServer}");

        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        
        if (bridgeCollider == null)
            bridgeCollider = GetComponent<Collider2D>();

        if (GamePlayManager.Instance != null)
        {
            GamePlayManager.Instance.RegisterBridge(this);
        }

        UpdateVisualState();
        
        isInitialized = true;
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        base.Despawned(runner, hasState);
        if (GamePlayManager.Instance != null)
            GamePlayManager.Instance.UnregisterBridge(this);
    }

    public override void FixedUpdateNetwork()
    {
        if (!isInitialized || !Runner.IsServer) return;

        if (IsCollapsed && CollapseTimer.Expired(Runner))
        {
            RepairBridge();
        }

        if (IsCollapsed)
        {
            CheckForUnitsOnBridge();
        }
    }

    void Update()
    {
        if (!isInitialized) return;
        UpdateVisualState();
    }

    private void UpdateVisualState()
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.sprite = IsCollapsed ? collapsedSprite : normalSprite;
        }

        if (bridgeCollider != null)
        {
            bridgeCollider.isTrigger = !IsCollapsed;
        }
    }

    private void CheckForUnitsOnBridge()
    {
        if (!Runner.IsServer) return;
        if (!IsCollapsed) return;

        Collider2D[] hits = Physics2D.OverlapBoxAll(transform.position, bridgeCollider.bounds.size, 0f);
        
        foreach (var hit in hits)
        {
            if (hit == null) continue;
            
            var enemyAI = hit.GetComponent<EnemyAI>();
            var enemyNetwork = hit.GetComponent<EnemyNetwork>();
            
            if (enemyAI != null && enemyNetwork != null && enemyNetwork.Team == 1)
            {
                Debug.Log($"[Bridge] Killing enemy {hit.name} on collapsed bridge");
                enemyAI.RPC_TakeDamage(1000f);
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!Runner.IsServer) return;
        if (!IsCollapsed) return;

        var enemyAI = other.GetComponent<EnemyAI>();
        var enemyNetwork = other.GetComponent<EnemyNetwork>();
        
        if (enemyAI != null && enemyNetwork != null && enemyNetwork.Team == 1)
        {
            Debug.Log($"[Bridge] Trigger kill enemy {other.name}");
            enemyAI.RPC_TakeDamage(1000f);
        }
    }

    public void OnInteract()
    {
        OnInteractAttempt();
    }

    public void OnInteractAttempt()
    {
        if (!isInitialized) return;

        var local = PlayerNetwork.Local;
        if (local == null)
        {
            Debug.LogWarning("[Bridge] No local player found.");
            return;
        }

        if (local.Team != 0)
        {
            Debug.Log("[Bridge] Only defender can collapse bridges.");
            return;
        }

        Debug.Log($"[Bridge] Interact attempt on bridge {bridgeIndex}");
        local.RequestCollapseBridge(bridgeIndex);
    }

    public void Server_CollapseNow()
    {
        if (!Runner.IsServer) 
        {
            Debug.LogWarning("[Bridge] Server_CollapseNow called on client");
            return;
        }

        if (IsCollapsed)
        {
            Debug.Log("[Bridge] Already collapsed");
            return;
        }

        if (!CanCollapse())
        {
            Debug.Log("[Bridge] Cannot collapse - on cooldown");
            return;
        }

        IsCollapsed = true;
        CollapseTimer = TickTimer.CreateFromSeconds(Runner, collapseDuration);
        
        Debug.Log($"[Bridge] Bridge {bridgeIndex} collapsed for {collapseDuration}s");

        if (collapseEffect != null)
        {
            Runner.Spawn(collapseEffect, transform.position, Quaternion.identity);
        }

        UpdateVisualState();
    }

    private void RepairBridge()
    {
        if (!Runner.IsServer) return;

        IsCollapsed = false;
        CooldownTimer = TickTimer.CreateFromSeconds(Runner, collapseCooldown);
        
        Debug.Log($"[Bridge] Bridge {bridgeIndex} repaired. Cooldown: {collapseCooldown}s");

        if (repairEffect != null)
        {
            Runner.Spawn(repairEffect, transform.position, Quaternion.identity);
        }

        UpdateVisualState();
    }

    public bool CanCollapse()
    {
        if (!isInitialized) return false;
        if (IsCollapsed) return false;
        
        if (CooldownTimer.ExpiredOrNotRunning(Runner))
            return true;
        
        return false;
    }

    public string GetBridgeStatus()
    {
        if (!isInitialized) return "Not initialized";
        
        if (IsCollapsed)
        {
            if (CollapseTimer.IsRunning)
            {
                float timeLeft = CollapseTimer.RemainingTime(Runner) ?? 0f;
                return $"Collapsed: {timeLeft:F1}s remaining";
            }
            return "Collapsed";
        }
        else if (CooldownTimer.IsRunning)
        {
            float cooldownLeft = CooldownTimer.RemainingTime(Runner) ?? 0f;
            return $"Available in: {cooldownLeft:F1}s";
        }
        else
        {
            return "Ready to collapse";
        }
    }
}