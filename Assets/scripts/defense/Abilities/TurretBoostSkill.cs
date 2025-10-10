using UnityEngine;
using System.Collections;

public class TurretBoostSkill : SkillBase
{
    [Header("Dane umiejętności Attack Boost")]
    public float boostMultiplier = 1.5f;      
    public float boostDuration = 5f;        
    public string turretTag = "Turret";     //po tagu bo prosto

    private bool isBoostActive = false;

    public override void OnButtonPress()
    {
        if (isOnCooldown)
        {
            Debug.Log($"{skillName} jest na cooldownie");
            return;
        }

        StartCoroutine(ActivateAttackBoost());
        StartCooldown();
    }

    private IEnumerator ActivateAttackBoost()
    {
        Debug.Log("Turret boost aktywny");

        GameObject[] turrets = GameObject.FindGameObjectsWithTag(turretTag);

        foreach (var turret in turrets)
        {
            // albo TurretController albo inne gówno ale wiadomo o co chodzi
            // turret.GetComponent<TurretController>()?.SetAttackSpeedMultiplier(boostMultiplier);
            Debug.Log($"Boost dla: {turret.name}");

            // Na razie tylko log
        }

        isBoostActive = true;

        yield return new WaitForSeconds(boostDuration);

        foreach (var turret in turrets)
        {
            // turret.GetComponent<TurretController>()?.SetAttackSpeedMultiplier(1f);
            Debug.Log($"Koniec boosta dla: {turret.name}");
        }

        isBoostActive = false;
        Debug.Log("Boost zakończony.");
    }
}
