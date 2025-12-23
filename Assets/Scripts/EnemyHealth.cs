using UnityEngine;
using System.Collections;

public class EnemyHealth : MonoBehaviour
{
    [Header("Health Settings")]
    public float maxHealth = 100f;
    private Animator anim; // Animasyonları kontrol edecek değişken
    private UnityEngine.AI.NavMeshAgent agent; // NavMesh'i kontrol edecek değişken
    public Renderer[] enemyRenderers; // Robotun tüm dış parçalarını buraya atacağız
    float currentHealth;
    public float GetCurrentHealth()
{
    return currentHealth;
}

    void Start()
    {
        currentHealth = maxHealth;
        // Bileşenleri en başta önbelleğe alalım
        agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        anim = GetComponentInChildren<Animator>();
    }

    // EnemyHealth.cs içinde
public void TakeDamage(float damage)
{
    StartCoroutine(FlashEffect());
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
    // Animator kontrolü (Hatanın ana kaynağı burası olabilir)
    if (anim != null) 
    {
        anim.SetTrigger("Die");
    }
    else 
    {
        // Eğer hala bulamıyorsa manuel olarak tekrar aramayı dene
        anim = GetComponentInChildren<Animator>();
        if(anim != null) anim.SetTrigger("Die");
    }

    // NavMeshAgent kontrolü
    if (agent != null) 
    {
        agent.enabled = false;
    }

    // AI Script kontrolü (İsminin EnemyAI olduğundan emin ol)
    EnemyAI aiScript = GetComponent<EnemyAI>();
    if (aiScript != null) aiScript.enabled = false;

    // Collider kontrolü
    Collider col = GetComponent<Collider>();
    if (col != null) col.enabled = false;

    Destroy(gameObject, 5f);
}
    IEnumerator FlashEffect()
{
    // Tüm parçaları beyaza boya (veya parlak yap)
    foreach (var r in enemyRenderers)
    {
        r.material.EnableKeyword("_EMISSION"); // Parlamayı aç
        r.material.SetColor("_EmissionColor", Color.white * 2f); // Bembeyaz yap
    }

    yield return new WaitForSeconds(0.1f); // 0.1 saniye bekle

    // Eski haline döndür
    foreach (var r in enemyRenderers)
    {
        r.material.SetColor("_EmissionColor", Color.black); // Parlamayı kapat
    }
}
}
