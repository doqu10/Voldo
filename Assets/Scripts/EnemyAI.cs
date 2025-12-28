using UnityEngine;

public class EnemyAI : MonoBehaviour
{
    [Header("State")]
    public EnemyState currentState { get; private set; }

    [Header("References")]
    public Transform player;
    public EnemyPerception perception;

    [Header("Decision Settings")]
    public float combatRange = 15f;
    [Range(0f, 1f)] public float coverHealthThreshold = 0.3f;

    float healthPercent = 1f;

    void Awake()
    {
        currentState = EnemyState.Idle;
    }

    void Update()
    {
        if (currentState == EnemyState.Dead) 
            return;

        EvaluateState();
    }

    void EvaluateState()
    {
        if (healthPercent <= 0f)
        {
            SetState(EnemyState.Dead);
            return;
        }

        if (perception != null && perception.CanSeePlayer())
        {
            float dist = Vector3.Distance(transform.position, player.position);

            if (dist <= combatRange)
                SetState(EnemyState.Combat);
            else
                SetState(EnemyState.Chase);

            return;
        }

        if (perception != null && perception.HeardNoise)
        {
            SetState(EnemyState.Investigate);
            return;
        }

        if (healthPercent <= coverHealthThreshold)
        {
            SetState(EnemyState.TakeCover);
            return;
        }

       SetState(EnemyState.Patrol);
    }

    void SetState(EnemyState newState)
    {
        if (currentState == newState) 
            return;

        currentState = newState;
        Debug.Log($"{name} -> {currentState}");
    }

    // EnemyHealth tarafından çağrılır
    public void OnTakeDamage(float currentHealth, float maxHealth)
    {
        healthPercent = currentHealth / maxHealth;
    }
    public void HearNoise(Vector3 noisePosition)
{
    if (perception != null)
    {
        perception.OnHearNoise(noisePosition);
    }
}

}
