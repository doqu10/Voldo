using UnityEngine;
using UnityEngine.AI;

public class EnemyMovement : MonoBehaviour
{
    [Header("References")]
    public EnemyAI ai;
    public EnemyPerception perception;
    public NavMeshAgent agent;
    public Transform player;

    [Header("Speed Settings")]
    public float walkSpeed = 2f;
    public float runSpeed = 4.5f;
    [Header("Patrol Settings")]
    public Transform[] patrolPoints; // Müfettiş (Inspector) üzerinden devriye noktalarını buraya sürükleyeceğiz
    private int currentPointIndex = 0;

    [Header("Dodge Settings")]
    private float dodgeTimer;
    private int dodgeDirection = 1; // 1 sağa, -1 sola

    [Header("Investigate")]
    public float investigateStopDistance = 1.5f;

    void Awake()
    {
        if (agent == null)
            agent = GetComponent<NavMeshAgent>();
    }

    void Update()
    {
    // KORUMA: Ajan yoksa veya kapalıysa hiçbir kodu çalıştırma
    if (ai == null || agent == null || !agent.enabled || !agent.isOnNavMesh) return;

    if (ai.currentState == EnemyState.Dead)
    {
        agent.isStopped = true;
        return;
    }

    HandleMovement();
    }

    void HandleMovement()
    {
        switch (ai.currentState)
        {
            case EnemyState.Idle:
                StopMovement();
                break;

            case EnemyState.Patrol: // Burası boştu, doldurduk
                Patrol();
            break;
            

            case EnemyState.Investigate:
                Investigate();
                break;

            case EnemyState.Chase:
                ChasePlayer();
                break;

            case EnemyState.Combat:
                CombatMovement();
                break;

            case EnemyState.TakeCover:
                RunToCover();
                break;
        }
    }

    // =========================
    // STATE DAVRANIŞLARI
    // =========================

    void StopMovement()
    {
        agent.isStopped = true;
    }

    void Investigate()
    {
        if (!perception.HeardNoise || !agent.isOnNavMesh) return; // NavMesh kontrolü eklendi

    agent.isStopped = false;
    agent.speed = walkSpeed;
    agent.SetDestination(perception.lastHeardPosition);

    if (agent.remainingDistance <= investigateStopDistance)
    {
        agent.isStopped = true;
    }
    }

    void ChasePlayer()
    {
        if (player == null || !agent.isOnNavMesh) return; // Bu satırı güncelle
    agent.isStopped = false;
    agent.speed = runSpeed;
    agent.SetDestination(player.position);
    }

    

    void RunToCover()
    {
        // ŞİMDİLİK SADE
        // Cover sistemi ayrı scriptte gelecek
        agent.isStopped = false;
        agent.speed = runSpeed;
    }
    void Patrol()
{
    if (patrolPoints.Length == 0) return;

    agent.isStopped = false;
    agent.speed = walkSpeed;
    agent.SetDestination(patrolPoints[currentPointIndex].position);

    // Noktaya ulaştıysak bir sonraki noktaya geç
    if (agent.remainingDistance <= agent.stoppingDistance)
    {
        currentPointIndex = (currentPointIndex + 1) % patrolPoints.Length;
    }
}

void CombatMovement()
{
    if (player == null || !agent.isOnNavMesh) return;

    // Ajanın durmadığından emin ol
    agent.isStopped = false;
    agent.speed = walkSpeed;

    // Zamanlayıcıyı güncelle
    dodgeTimer += Time.deltaTime;
    if (dodgeTimer > 1.5f) // 2 saniye yerine 1.5 yapalım, daha hareketli olsun
    {
        dodgeDirection *= -1;
        dodgeTimer = 0;
    }

    // Oyuncunun çevresinde sağa veya sola doğru bir pozisyon hesapla
    // Sadece transform.right yetmez, oyuncuya göre sağını hesaplamalıyız
    Vector3 relativeRight = transform.right * dodgeDirection * 3f;
    Vector3 targetPos = transform.position + relativeRight;

    // Hedefe git
    agent.SetDestination(targetPos);

    // Her zaman oyuncuya bak (Daha pürüzsüz bakış)
    Vector3 direction = (player.position - transform.position).normalized;
    direction.y = 0;
    if (direction != Vector3.zero)
    {
        Quaternion lookRotation = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * 8f);
    }
}
}
