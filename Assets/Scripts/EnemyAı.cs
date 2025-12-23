using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class EnemyAI : MonoBehaviour
{
    public GameObject trailPrefab;
    public Transform enemyFirePoint;
    [Header("References")]
    public Transform player;
    public PlayerHealth playerHealth;

    public LayerMask obstacleLayer;
    public Light muzzleFlashLight;
    NavMeshAgent agent;
    Vector3 startPosition;
    Vector3 lastSeenPosition;

    float searchTimer;
    float nextFireTime;
    private Animator anim;

    bool isBursting;

    [Header("Vision")]
    public float viewDistance = 15f;
    public float viewAngle = 90f;

    [Header("Combat")]
    public float attackRange = 10f;
    public float damage = 10f;
    [Header("Movement Tweaks")]
    public float strafeSpeed = 3f;
    float strafeTimer;
    Vector3 strafeDirection;
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

    [Header("Cover Settings")]
    public float lowHealthThreshold = 30f; // %30 canın altı tehlike
    bool isTakingCover = false;

    [Header("Patrol Settings")]
    public Transform[] patrolPoints; // Müfettiş (Inspector) panelinden buraya noktaları atacağız
    public float patrolWaitTime = 2f; // Noktaya varınca ne kadar beklesin?
    int currentPointIndex = 0;
    bool isWaiting = false;
    [Header("Suppressive Fire")]
    public float suppressiveFireDuration = 2f; // Ne kadar süre ateş etmeye devam etsin?
    float suppressiveTimer;
    enum EnemyState
    {
        Idle,
        Chase,
        Search,
        Return,
        TakeCover,
        SuppressiveFire
    }

    EnemyState currentState;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        startPosition = transform.position;
        currentState = EnemyState.Idle;
        // Robot, scriptin olduğu objenin alt objesi olduğu için GetComponentInChildren kullanıyoruz
        anim = GetComponentInChildren<Animator>();
    }

    void Update()
    {   if (anim != null){
        // Ajanın o anki hızını al (0 duruyor, 3.5 koşuyor gibi)
        float currentSpeed = agent.velocity.magnitude;
        // Bu hızı animatördeki "Speed" parametresine gönder
        anim.SetFloat("Speed", currentSpeed);
        }
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
            case EnemyState.TakeCover:
                HandleTakeCover();
                break;
            case EnemyState.SuppressiveFire:
                HandleSuppressiveFire();
                break;
        }
    }
    public string GetCurrentState() { return currentState.ToString(); }
    public void OnDamagedByPlayer()
    {
    // Eğer seni görmüyorsa bile ateş ettiğin yöne dönsün ve seni kovalamaya başlasın
    if (currentState != EnemyState.Chase)
    {
        currentState = EnemyState.Chase;
        lastSeenPosition = player.position;
    }
    }
    // ===================== STATES =====================

void HandleIdle()
{
    if (CanSeePlayer())
    {
        StopAllCoroutines();
        isWaiting = false;
        agent.isStopped = false;
        currentState = EnemyState.Chase;
        return;
    }

    if (patrolPoints.Length > 0 && !isWaiting)
    {
        // Ajanın bir hedefi yoksa veya hedefine çok yaklaştıysa
        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
        {
            StartCoroutine(PatrolWait());
        }
    }
}

IEnumerator PatrolWait()
{
    isWaiting = true;

    // Bir sonraki noktaya geçişi burada yapıyoruz
    currentPointIndex = (currentPointIndex + 1) % patrolPoints.Length;

    // Yeni hedefi hemen veriyoruz ama bekleme süresi boyunca ajanı durduruyoruz
    agent.SetDestination(patrolPoints[currentPointIndex].position);
    agent.isStopped = true; 

    yield return new WaitForSeconds(patrolWaitTime);

    agent.isStopped = false; 
    isWaiting = false;
}

    void HandleChase()
    {
                // --- GRUP HABERLEŞMESİ ---
            // Her karede değil, sadece kovalama moduna ilk girdiğinde veya belirli aralıklarla yapabiliriz
            // Şimdilik en basit haliyle: etraftaki dostları bul ve uyar
            float alertRadius = 15f; 
            Collider[] friends = Physics.OverlapSphere(transform.position, alertRadius);
            foreach (var f in friends)
            {
                EnemyAI friendAI = f.GetComponent<EnemyAI>();
                // Kendisi hariç diğer AI'ları uyar
                if (friendAI != null && friendAI != this)
                {
                    friendAI.AlertFromFriend(player.position);
                }
            }
        agent.SetDestination(player.position);

        float distance = Vector3.Distance(transform.position, player.position);

        if (CanSeePlayer())
        {
            lastSeenPosition = player.position;

            if (distance <= attackRange)
            {
               agent.SetDestination(transform.position + strafeDirection); // Rastgele yöne git
            TryShoot();
            }
        }
        else
        {
            // Direkt Search yerine önce Baskı Ateşi
            suppressiveTimer = suppressiveFireDuration;
            currentState = EnemyState.SuppressiveFire;
           
        }
    }
    void HandleSuppressiveFire()
{
    // Eğer oyuncu bu sırada tekrar açığa çıkarsa kovalamaya geri dön
    if (CanSeePlayer())
    {
        currentState = EnemyState.Chase;
        return;
    }

    suppressiveTimer -= Time.deltaTime;

    if (suppressiveTimer > 0)
    {
        // Oyuncuyu görmüyor ama son gördüğü yere ateş etmeye devam ediyor!
        TryShootAtPosition(lastSeenPosition);
    }
    else
    {
        // Süre bitti, şimdi aramaya başlayabilir
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

    // 4. Siper Mantığı Fonksiyonu
    void HandleTakeCover()
    {
        // Sipere ulaştık mı kontrol et
        if (!agent.pathPending && agent.remainingDistance < 0.5f)
        {
          isTakingCover = false;
          // Sipere vardık, 2 saniye bekle sonra oyuncuyu tekrar kovala (veya ateş et)
         StartCoroutine(WaitInCover());
        }
    }

    IEnumerator WaitInCover()
    {
        yield return new WaitForSeconds(2f);
        currentState = EnemyState.Chase;
    }
    // 5. EN ÖNEMLİSİ: En İyi Siperi Bulma
    public void FindBestCover()
    {
        GameObject[] covers = GameObject.FindGameObjectsWithTag("Cover");
        Transform bestCover = null;
        float closestDistance = Mathf.Infinity;
    
        foreach (GameObject cover in covers)
        {
            float dist = Vector3.Distance(transform.position, cover.transform.position);
            if (dist < closestDistance)
            {
                // Basitçe en yakını seçiyoruz (Şimdilik)
                closestDistance = dist;
                bestCover = cover.transform;
            }
        }
    
        if (bestCover != null)
        {
            currentState = EnemyState.TakeCover;
            agent.SetDestination(bestCover.position);
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
    void TryShootAtPosition(Vector3 targetPos)
{
    if (isBursting) return;
    if (Time.time < nextFireTime) return;

    int burstCount = Random.Range(minBurstCount, maxBurstCount + 1);
    // Buradaki fark: BurstFire'a hedef konumu da gönderiyoruz
    StartCoroutine(BurstFireAtPosition(shots: burstCount, targetPos: targetPos));
}

IEnumerator BurstFireAtPosition(int shots, Vector3 targetPos)
{
    isBursting = true;
    while (shots > 0)
    {
        FireSingleShot(targetPos); // Belirlenen konuma ateş et
        if(muzzleFlashLight != null) StartCoroutine(FlashEffect());
        shots--;
        yield return new WaitForSeconds(burstDelay);
    }
    isBursting = false;
    nextFireTime = Time.time + burstCooldown;
}
    IEnumerator BurstFire(int shots)
    {
        isBursting = true;

        while (shots > 0)
        {
            FireSingleShot(player.position);
            if(muzzleFlashLight != null) StartCoroutine(FlashEffect()); // Işığı yak
            shots--;
            yield return new WaitForSeconds(burstDelay);
        }

        isBursting = false;
        nextFireTime = Time.time + burstCooldown;
    }
    IEnumerator FlashEffect()
    {
    muzzleFlashLight.enabled = true;
    yield return new WaitForSeconds(0.05f); // Çok kısa süre yanıp sönsün
    muzzleFlashLight.enabled = false;
    }
    void CreateEnemyTrail(Vector3 targetPos)
    {
        GameObject trail = Instantiate(trailPrefab, enemyFirePoint.position, Quaternion.identity);
        LineRenderer lr = trail.GetComponent<LineRenderer>();
        lr.SetPosition(0, enemyFirePoint.position);
        lr.SetPosition(1, targetPos);
        Destroy(trail, 0.05f);
    }
    void FireSingleShot(Vector3 targetPosition)
{   // Animator bileşenine ulaşıp "Shoot" tetikleyicisini çalıştırıyoruz
    if (anim != null) 
    {
        anim.SetTrigger("Shoot"); 
    }
    Vector3 shootDir = (targetPosition - enemyFirePoint.position).normalized;
    shootDir += Random.insideUnitSphere * maxSpread * (1f - accuracy);
    
    Ray ray = new Ray(enemyFirePoint.position, shootDir);
    RaycastHit hit;

    if (Physics.Raycast(ray, out hit, attackRange))
    {
        CreateEnemyTrail(hit.point);
        
        // Eğer çarptığın şey Player ise hasar ver
        if (hit.collider.CompareTag("Player")) 
        { 
            playerHealth.TakeDamage(damage); 
        }
        // Eğer çarptığın şey bir engelse, mermi orada durur (CreateEnemyTrail zaten hit.point'e gidiyor)
    }
    else
    {
        CreateEnemyTrail(ray.origin + ray.direction * attackRange);
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
    public void HearNoise(Vector3 noisePosition)
{
    if (currentState == EnemyState.Chase) return;

    lastSeenPosition = noisePosition;
    currentState = EnemyState.Search;
    searchTimer = searchDuration;

    // Ajanı durdurup yeni hedefe zorla yönlendiriyoruz
    agent.isStopped = false; 
    isWaiting = false; // Devriye bekliyorsa iptal etsin
    agent.SetDestination(lastSeenPosition);
    
    Debug.Log(gameObject.name + ": Sesi duydum, gidiyorum!");
}
public void AlertFromFriend(Vector3 targetPos)
{
    // Eğer zaten kovalıyorsa veya siper alıyorsa kafasını karıştırma
    if (currentState == EnemyState.Chase || currentState == EnemyState.TakeCover) return;

    Debug.Log(gameObject.name + ": Arkadaşım uyardı, yardıma gidiyorum!");
    lastSeenPosition = targetPos;
    currentState = EnemyState.Chase; // Doğrudan kovalama moduna geçebilir veya Search yapabilirsin
}
}
