using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class EnemyAI : MonoBehaviour
{
    [Header("References")]
    public Transform player;
    public PlayerHealth playerHealth;

    public LayerMask obstacleLayer;

    NavMeshAgent agent;
    Vector3 startPosition;
    Vector3 lastSeenPosition;

    float searchTimer;
    float nextFireTime;

    bool isBursting;

    [Header("Vision")]
    public float viewDistance = 15f;
    public float viewAngle = 90f;

    [Header("Combat")]
    public float attackRange = 10f;
    public float damage = 10f;

    [Header("Accuracy")]
    [Range(0f, 1f)]
    public float accuracy = 0.7f;
    public float maxSpread = 0.15f;

    [Header("Burst Fire")]
    public int minBurstCount = 2;
    public int maxBurstCount = 4;
    public float burstDelay = 0.1f;
    public float burstCooldown = 1.2f;

    [Header("Search")]
    public float searchDuration = 3f;

    enum EnemyState
    {
        Idle,
        Chase,
        Search,
        Return
    }

    EnemyState currentState;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        startPosition = transform.position;
        currentState = EnemyState.Idle;
    }

    void Update()
    {
        switch (currentState)
        {
            case EnemyState.Idle:
                HandleIdle();
                break;

            case EnemyState.Chase:
                HandleChase();
                break;

            case EnemyState.Search:
                HandleSearch();
                break;

            case EnemyState.Return:
                HandleReturn();
                break;
        }
    }

    // ===================== STATES =====================

    void HandleIdle()
    {
        if (CanSeePlayer())
        {
            currentState = EnemyState.Chase;
        }
    }

    void HandleChase()
    {
        agent.SetDestination(player.position);

        float distance = Vector3.Distance(transform.position, player.position);

        if (CanSeePlayer())
        {
            lastSeenPosition = player.position;

            if (distance <= attackRange)
            {
                TryShoot();
            }
        }
        else
        {
            searchTimer = searchDuration;
            agent.SetDestination(lastSeenPosition);
            currentState = EnemyState.Search;
        }
    }

    void HandleSearch()
    {
        if (CanSeePlayer())
        {
            currentState = EnemyState.Chase;
            return;
        }

        searchTimer -= Time.deltaTime;

        if (searchTimer <= 0f)
        {
            agent.SetDestination(startPosition);
            currentState = EnemyState.Return;
        }
    }

    void HandleReturn()
    {
        if (CanSeePlayer())
        {
            currentState = EnemyState.Chase;
            return;
        }

        if (!agent.pathPending && agent.remainingDistance < 0.5f)
        {
            currentState = EnemyState.Idle;
        }
    }

    // ===================== COMBAT =====================

    void TryShoot()
    {
        if (isBursting) return;
        if (Time.time < nextFireTime) return;
        if (playerHealth == null) return;

        int burstCount = Random.Range(minBurstCount, maxBurstCount + 1);
        StartCoroutine(BurstFire(burstCount));
    }

    IEnumerator BurstFire(int shots)
    {
        isBursting = true;

        while (shots > 0)
        {
            FireSingleShot();
            shots--;
            yield return new WaitForSeconds(burstDelay);
        }

        isBursting = false;
        nextFireTime = Time.time + burstCooldown;
    }

    void FireSingleShot()
    {
        Vector3 shootDir = (player.position - transform.position).normalized;

        shootDir += Random.insideUnitSphere * maxSpread * (1f - accuracy);
        shootDir.Normalize();

        Ray ray = new Ray(transform.position + Vector3.up, shootDir);
        RaycastHit hit;

        Debug.DrawRay(ray.origin, ray.direction * attackRange, Color.red, 0.2f);

        if (Physics.Raycast(ray, out hit, attackRange))
        {
            if (hit.collider.CompareTag("Player"))
            {
                playerHealth.TakeDamage(damage);
            }
        }
    }

    // ===================== VISION =====================

    bool CanSeePlayer()
    {
        Vector3 direction = (player.position - transform.position).normalized;
        float distance = Vector3.Distance(transform.position, player.position);

        if (distance > viewDistance)
            return false;

        if (Vector3.Angle(transform.forward, direction) > viewAngle / 2f)
            return false;

        if (Physics.Raycast(transform.position + Vector3.up, direction, distance, obstacleLayer))
            return false;

        return true;
    }
}
