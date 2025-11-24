using System.Collections.Generic;
using Fusion;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class DestructibleBridge : NetworkBehaviour
{
    [Header("Visuals")]
    public SpriteRenderer spriteRenderer;
    public Sprite normalSprite;
    public Sprite destroyedSprite;

    [Header("Logic")]
    [Tooltip("Layer, na którym są moby (EnemyAI)")]
    public LayerMask enemyLayer;

    [Tooltip("Czas (w sekundach) po którym most się regeneruje")]
    public float regenerateDelay = 45f;

    [Networked] public bool IsDestroyed { get; set; }

    private readonly List<EnemyAI> enemiesOnBridge = new List<EnemyAI>();
    private Collider2D col;
    private float regenTimer = -1f;

    public override void Spawned()
    {
        col = GetComponent<Collider2D>();
        col.isTrigger = true;

        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (Object.HasStateAuthority)
        {
            IsDestroyed = false;
            regenTimer = -1f;
        }

        UpdateSprite();
    }

    public override void FixedUpdateNetwork()
    {
        if (Object.HasStateAuthority)
        {
            if (IsDestroyed && regenTimer > 0f)
            {
                regenTimer -= (float)Runner.DeltaTime;
                if (regenTimer <= 0f)
                {
                    RestoreBridge_Internal();
                }
            }
        }

        UpdateSprite();
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_RequestDestroyBridge()
    {
        if (!Object.HasStateAuthority) return;
        DestroyBridge_Internal();
    }

    private void DestroyBridge_Internal()
    {
        if (IsDestroyed) return;

        IsDestroyed = true;
        regenTimer = regenerateDelay;

        KillEnemiesOnBridge();
    }

    private void RestoreBridge_Internal()
    {
        IsDestroyed = false;
        regenTimer = -1f;
        enemiesOnBridge.Clear();
    }

    private void UpdateSprite()
    {
        if (spriteRenderer == null) return;

        if (IsDestroyed && destroyedSprite != null)
            spriteRenderer.sprite = destroyedSprite;
        else if (!IsDestroyed && normalSprite != null)
            spriteRenderer.sprite = normalSprite;
    }

    private void KillEnemiesOnBridge()
    {
        for (int i = 0; i < enemiesOnBridge.Count; i++)
        {
            var enemy = enemiesOnBridge[i];
            if (enemy == null) continue;

            enemy.RPC_TakeDamage(9999f);
        }

        enemiesOnBridge.Clear();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsEnemyCollider(other)) return;

        var enemy = other.GetComponent<EnemyAI>();
        if (enemy == null) return;

        if (!Object.HasStateAuthority) return;

        if (IsDestroyed)
        {
            enemy.RPC_TakeDamage(9999f);
        }
        else
        {
            if (!enemiesOnBridge.Contains(enemy))
                enemiesOnBridge.Add(enemy);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!IsEnemyCollider(other)) return;

        var enemy = other.GetComponent<EnemyAI>();
        if (enemy == null) return;

        if (!Object.HasStateAuthority) return;

        enemiesOnBridge.Remove(enemy);
    }

    private bool IsEnemyCollider(Collider2D c)
    {
        return ((1 << c.gameObject.layer) & enemyLayer) != 0;
    }

    public void NotifyEnemyDied(EnemyAI enemy)
    {
        if (!Object.HasStateAuthority) return;
        if (enemy == null) return;
        enemiesOnBridge.Remove(enemy);
    }
}
