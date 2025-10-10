using UnityEngine;

public class SkillBase : MonoBehaviour
{
    [Header("dane umiejętności")]
    public string skillName = "Nowa umiejętność";
    public Sprite skillIcon;
    public float cooldown = 2f;

    protected bool isOnCooldown = false;
    private float cooldownTimer = 0f;

    public virtual void OnButtonPress()
    {
        if (isOnCooldown)
        {
            Debug.Log($"{skillName} jest na cooldownie ({cooldownTimer:F1}s)");
            return;
        }

        UseSkill();
        StartCooldown();
    }

    // do nadpisania przez inne skrypty
    protected virtual void UseSkill()
    {
        Debug.Log($"{skillName} użyto");
    }

    protected void StartCooldown()
    {
        isOnCooldown = true;
        cooldownTimer = cooldown;
    }

    protected virtual void Update()
    {
        if (isOnCooldown)
        {
            cooldownTimer -= Time.deltaTime;
            if (cooldownTimer <= 0f)
            {
                isOnCooldown = false;
                cooldownTimer = 0f;
                Debug.Log($"{skillName} ready");
            }
        }
    }

    public bool IsReady() => !isOnCooldown;
}
