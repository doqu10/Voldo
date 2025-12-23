using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class EnemyAI : MonoBehaviour
{
    int currentStrafeValue = 0;
    public string GetCurrentState() { return currentState.ToString(); }
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
    [Header("Ammo")]
    public int enemyCurrentAmmo = 30;
    public int enemyMagSize = 30;
    bool isReloading = false;
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
    // --- STRAFE (ZİKZAK) ZAMANLAYICI KONTROLÜ ---
    strafeTimer -= Time.deltaTime;
    if (strafeTimer <= 0)
    {
        // currentStrafeValue sınıfın başında tanımladığımız değişken (-1, 0, 1)
        currentStrafeValue = Random.Range(-1, 2); 
        
        // Rastgele bir süre boyunca (1.5 - 3 sn) seçilen yöne kayacak
        strafeTimer = Random.Range(1.5f, 3.0f); 
        
        // Yön hesaplama: Sağ, Sol veya Düz
        if (currentStrafeValue != 0) 
            strafeDirection = (currentStrafeValue == -1) ? -transform.right : transform.right;
        else
            strafeDirection = Vector3.zero;
    }

    // Animator Blend Tree parametresini güncelle (StrafeDirection parametresi)
    if (anim != null) 
    {
        anim.SetFloat("StrafeDirection", currentStrafeValue);
    }

    // --- GRUP HABERLEŞMESİ (Alarm Sistemi) ---
    float alertRadius = 15f; 
    Collider[] friends = Physics.OverlapSphere(transform.position, alertRadius);
    foreach (var f in friends)
    {
        EnemyAI friendAI = f.GetComponent<EnemyAI>();
        if (friendAI != null && friendAI != this)
        {
            friendAI.AlertFromFriend(player.position);
        }
    }

    // --- HAREKET VE SALDIRI MANTIĞI ---
    float distance = Vector3.Distance(transform.position, player.position);

    if (CanSeePlayer())
    {
        lastSeenPosition = player.position;

        if (distance <= attackRange)
        {
            // Oyuncuya çok yakınsa zikzak yaparak (Strafe) hareket et
            agent.SetDestination(transform.position + strafeDirection * 2f);
            
            // Oyuncuya bakmasını sağla (NavMesh bazen yan dönebilir strafe yaparken)
            Vector3 lookPos = player.position - transform.position;
            lookPos.y = 0;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookPos), Time.deltaTime * 5f);
            
            TryShoot(); // Ateş et
        }
        else
        {
            // Menzil dışındaysa doğrudan oyuncuya koş
            agent.SetDestination(player.position);
        }
    }
    else
    {
        // Oyuncu görüşten çıktıysa Baskı Ateşi moduna geç
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
    if (!agent.pathPending && agent.remainingDistance < 0.5f)
    {
        // Sipere vardığında oyuncuya bak
        Vector3 direction = (player.position - transform.position).normalized;
        direction.y = 0; 
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), Time.deltaTime * 5f);

        // Siperdeyken de ateş etmeye çalış!
        if (CanSeePlayer())
        {
            TryShoot();
        }
        
        if (!isTakingCover) // Coroutine'in birden fazla kez başlamasını engelle
        {
            isTakingCover = true;
            StartCoroutine(WaitInCover());
        }
    }
}

    IEnumerator WaitInCover()
    {
    // 1. Sipere varınca eğil
    if (anim != null) anim.SetBool("IsCrouching", true);
    
    // Siperde 2 ile 4 saniye arası rastgele bir süre bekle
    yield return new WaitForSeconds(Random.Range(2f, 4f));

    // 2. Dikizleme Modu: Ayağa kalk ve ateş et
    if (anim != null) anim.SetBool("IsCrouching", false);
    
    // Ayağa kalkması için kısa bir süre tanı
    yield return new WaitForSeconds(0.5f);

    // 3 mermi atacak kadar bekle (TryShoot zaten Burst çalıştığı için mermi yağdıracaktır)
    if (CanSeePlayer())
    {
        TryShoot();
    }
    
    yield return new WaitForSeconds(1.5f);

    // 3. Tekrar siper al mı yoksa kovalamaya devam mı?
    // Canı hala çok düşükse siperde kalmaya devam etsin
    if (GetComponent<EnemyHealth>().GetCurrentHealth() < 30f)
    {
        StartCoroutine(WaitInCover()); // Döngüye girer, tekrar eğilir
    }
    else
    {
        isTakingCover = false;
        currentState = EnemyState.Chase;
    }
    }
    // 5. EN ÖNEMLİSİ: En İyi Siperi Bulma
    public void FindBestCover()
{
    GameObject[] covers = GameObject.FindGameObjectsWithTag("Cover");
    Transform bestCover = null;
    float closestDistance = Mathf.Infinity;

    foreach (GameObject cover in covers)
    {
        // 1. Mesafe kontrolü
        float dist = Vector3.Distance(transform.position, cover.transform.position);
        
        // 2. Görüş Kontrolü (Line of Sight)
        // Oyuncu ile siper noktası arasında bir engel var mı?
        Vector3 directionToPlayer = (player.position - cover.transform.position).normalized;
        float distanceToPlayer = Vector3.Distance(cover.transform.position, player.position);

        // Siperden oyuncuya doğru bir ışın yolluyoruz
        // Eğer bu ışın bir şeye çarpıyorsa (obstacleLayer), oyuncu orayı göremiyor demektir -> İYİ SİPER!
        if (Physics.Raycast(cover.transform.position + Vector3.up, directionToPlayer, distanceToPlayer, obstacleLayer))
        {
            if (dist < closestDistance)
            {
                closestDistance = dist;
                bestCover = cover.transform;
            }
        }
    }

    if (bestCover != null)
    {
        currentState = EnemyState.TakeCover;
        agent.SetDestination(bestCover.position);
        Debug.Log("Güvenli siper bulundu: " + bestCover.name);
    }
    else
    {
        // Güvenli siper yoksa kaçmaya devam et veya en yakına razı ol
        Debug.Log("Güvenli siper bulunamadı, rastgele kaçış!");
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
    IEnumerator EnemyReload()
    {
        isReloading = true;
        if (anim != null) anim.SetTrigger("Reload");
        
        Debug.Log("Şarjör bitti, siper aranıyor!");
        FindBestCover(); // Reload yaparken kaç

        yield return new WaitForSeconds(2.5f);
        enemyCurrentAmmo = enemyMagSize;
        isReloading = false;
        currentState = EnemyState.Chase;
    }
    void FireSingleShot(Vector3 targetPosition)
{   // Animator bileşenine ulaşıp "Shoot" tetikleyicisini çalıştırıyoruz
    if (anim != null) 
    {
        anim.SetTrigger("Shoot"); 
    }
    if (isReloading) return;

    enemyCurrentAmmo--;
    
    if (enemyCurrentAmmo <= 0)
    {
        StartCoroutine(EnemyReload());
        return;
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
