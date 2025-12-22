using UnityEngine;

public class EnemyHealth : MonoBehaviour
{
    [Header("Health Settings")]
    public float maxHealth = 100f;

    float currentHealth;

    void Start()
    {
        currentHealth = maxHealth;
    }

    // EnemyHealth.cs içinde
public void TakeDamage(float damage)
{
    currentHealth -= damage;
    EnemyAI ai = GetComponent<EnemyAI>();

    if (ai != null)
    {
        // Senaryo 1: Beklemediği anda hasar alırsa (Pusu)
        // Senaryo 2: Canı belli bir limitin altına düşerse
        if (currentHealth < maxHealth * 0.3f || ai.GetCurrentState() != "Chase")
        {
            ai.FindBestCover();
        }
    }

    if (currentHealth <= 0f) Die();
}

    void Die()
    {
        Debug.Log("Enemy Dead!");
        Destroy(gameObject);
    }
}
