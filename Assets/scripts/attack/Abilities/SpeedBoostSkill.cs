using UnityEngine;
using System.Collections;

public class SpeedBoostSkill : SkillBase
{
    [Header("Dane Speed Boost")]
    public float speedMultiplier = 1.5f;     
    public float boostDuration = 5f;
    public string unitTag = "Unit";          

    public override void OnButtonPress()
    {
        if (isOnCooldown)
        {
            Debug.Log($"{skillName} jest na cooldownie");
            return;
        }

        StartCoroutine(ActivateSpeedBoost());
        StartCooldown();
    }

    private IEnumerator ActivateSpeedBoost()
    {
        Debug.Log("Speed Boost aktywowany");

        GameObject[] units = GameObject.FindGameObjectsWithTag(unitTag);

        foreach (var unit in units)
        {
            // Później
            // unit.GetComponent<UnitMovement>()?.SetSpeedMultiplier(speedMultiplier);

            Debug.Log($"Zwiększam speeda : {unit.name}");
        }

        yield return new WaitForSeconds(boostDuration);

        foreach (var unit in units)
        {
            // unit.GetComponent<UnitMovement>()?.SetSpeedMultiplier(1f);
            Debug.Log($"Przywracam speeda: {unit.name}");
        }

        Debug.Log("Speed Boost skończony");
    }
}
