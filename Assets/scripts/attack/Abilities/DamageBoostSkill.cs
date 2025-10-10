using UnityEngine;
using System.Collections;

public class DamageBoostSkill : SkillBase
{
    [Header("Dane Damage Boost")]
    public float damageMultiplier = 1.5f;    
    public float boostDuration = 5f;        
    public string unitTag = "Unit";          

    public override void OnButtonPress()
    {
        if (isOnCooldown)
        {
            Debug.Log($"{skillName} jest na cooldownie!");
            return;
        }

        StartCoroutine(ActivateDamageBoost());
        StartCooldown();
    }

    private IEnumerator ActivateDamageBoost()
    {
        Debug.Log("💥 Damage Boost aktywowany!");

        GameObject[] units = GameObject.FindGameObjectsWithTag(unitTag);

        foreach (var unit in units)
        {
            // Odkomentować potem
            // unit.GetComponent<UnitStats>()?.SetDamageMultiplier(damageMultiplier);

            Debug.Log($"Boost obrażeń: {unit.name}");
        }

        yield return new WaitForSeconds(boostDuration);

        foreach (var unit in units)
        {
            // unit.GetComponent<UnitStats>()?.SetDamageMultiplier(1f);
            Debug.Log($"Koniec boosta obrażeń: {unit.name}");
        }

        Debug.Log("Koniec boosta");
    }
}
