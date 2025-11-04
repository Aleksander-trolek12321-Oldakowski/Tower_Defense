using UnityEngine;
using System.Collections;

public class FreezeTurretSkill : SkillBase
{
    [Header("Dane Freeze")]
    public float freezeDuration = 5f;      
    public string turretTag = "Turret";    

    private bool isSelectingTarget = false;

    public override void OnButtonPress()
    {
        if (isOnCooldown)
        {
            Debug.Log($"{skillName} jest na cooldownie");
            return;
        }

        isSelectingTarget = true;
        Debug.Log("Wybierz turreta do zamrożenia (kliknij na niego)");
    }

    protected override void Update()
    {
        base.Update();

        if (isSelectingTarget && Input.GetMouseButtonDown(0))
        {
            Vector2 mousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            RaycastHit2D hit = Physics2D.Raycast(mousePosition, Vector2.zero);

            if (hit.collider != null && hit.collider.CompareTag(turretTag))
            {
                StartCoroutine(FreezeTarget(hit.collider.gameObject));
                isSelectingTarget = false;
                StartCooldown();
            }
            else
            {
                Debug.Log("To nie jest turret");
            }
        }
    }

    private IEnumerator FreezeTarget(GameObject turret)
    {
        Debug.Log($"Zamrażam {turret.name} na {freezeDuration} sekund.");

        Rigidbody2D rb = turret.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.constraints = RigidbodyConstraints2D.FreezeAll;
        }

        MonoBehaviour[] scripts = turret.GetComponents<MonoBehaviour>();
        foreach (var s in scripts)
        {
            if (s != this) s.enabled = false; 
        }

        SpriteRenderer sr = turret.GetComponent<SpriteRenderer>();
        if (sr != null) sr.color = Color.cyan;

        yield return new WaitForSeconds(freezeDuration);

        // Odmrożenie
        if (rb != null)
        {
            rb.constraints = RigidbodyConstraints2D.None;
        }

        foreach (var s in scripts)
        {
            if (s != this) s.enabled = true;
        }

        if (sr != null) sr.color = Color.white;

        Debug.Log($"{turret.name} został odmrożony!");
    }
}
