using UnityEngine;
using System.Collections;

public class EnemyHealth : MonoBehaviour
{
    [Header("Health Settings")]
    public float maxHealth = 100f;
    private float currentHealth;

    [Header("References")]
    public Renderer[] enemyRenderers;

    private Animator anim;
    private UnityEngine.AI.NavMeshAgent agent;
    private EnemyAI enemyAI;

    void Start()
    {
        currentHealth = maxHealth;
        anim = GetComponentInChildren<Animator>();
        agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        enemyAI = GetComponent<EnemyAI>();
    }

    public void TakeDamage(float damage)
    {
        if (currentHealth <= 0f) return;

        currentHealth -= damage;
        StartCoroutine(FlashEffect());

        if (enemyAI != null) enemyAI.OnTakeDamage(currentHealth, maxHealth);

        if (currentHealth <= 0f) Die();
    }

    void Die()
    {
        // Önce AI durumunu ölü yap ki Update'ler sussun
        if (enemyAI != null) {
            enemyAI.OnTakeDamage(0, 100); // State'i Dead'e zorla
            enemyAI.enabled = false;
        }

        // Animasyonu tetikle (Parametrenin "Die" olduğundan emin ol)
        if (anim != null) anim.SetTrigger("Die");

        // Diğer scriptleri kapat
        if (GetComponent<EnemyCombat>()) GetComponent<EnemyCombat>().enabled = false;
        if (GetComponent<EnemyMovement>()) GetComponent<EnemyMovement>().enabled = false;

        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
            agent.enabled = false;
        }

        GetComponent<Collider>().enabled = false;
        Destroy(gameObject, 5f);
    }

    IEnumerator FlashEffect()
    {
        if (enemyRenderers == null) yield break;
        foreach (var r in enemyRenderers) {
            r.material.EnableKeyword("_EMISSION");
            r.material.SetColor("_EmissionColor", Color.white * 2f);
        }
        yield return new WaitForSeconds(0.1f);
        foreach (var r in enemyRenderers) r.material.SetColor("_EmissionColor", Color.black);
    }
}