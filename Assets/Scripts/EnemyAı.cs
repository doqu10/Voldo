using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class EnemyAI : MonoBehaviour
{
    float currentStrafeValue = 0f;
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
    private float lastAlertTime; // En son ne zaman arkadaşları uyardı?
    public float alertCooldown = 3f;
    float searchTimer;
    float nextFireTime;
    private Animator anim;
    float alertRadius;
    Collider[] friends;
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
    [Header("Shell Ejection")]
    public GameObject shellPrefab;    // Kovan modeli (Prefab)
    public Transform shellEjectPoint; // Silahın yanındaki çıkış noktası
    public float shellForce = 3f;     // Fırlatma hızı
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
{
    if (anim != null)
    {
        // Hareket hızını animatöre gönderiyoruz
        float currentSpeed = agent.velocity.magnitude;
        anim.SetFloat("Speed", currentSpeed);
        
        // Blend Tree için sağ/sol değerini gönderiyoruz
        anim.SetFloat("StrafeDire", currentStrafeValue); 

        // Duruma göre hız ayarı
       if (currentState == EnemyState.Chase) 
        {
            // Oyuncu uzaksa koş, yakındaysa (Ateş menzilinde) yavaşla/strafe yap
            float distance = Vector3.Distance(transform.position, player.position);
            if (distance > attackRange + 2f) 
            {
                agent.speed = 5.5f; // Koşma hızı (Run_guard_AR devreye girer)
            }
            else 
            {
                agent.speed = 3.5f; // Çatışma/Yürüme hızı
            }
        }
        else 
        {
            agent.speed = 2.0f; // Devriye/Dönüş hızı
        }
    }

    // --- BAKIŞ VE ROTASYON KONTROLÜ ---
    // Eğer ateş ediyorsa veya kovalarken saldırı menzilindeyse oyuncuya kilitlen
    if (isBursting || (currentState == EnemyState.Chase && agent.remainingDistance <= attackRange))
    {
        agent.updateRotation = false; // NavMesh'in otomatik dönmesini kapat
        FaceTarget(player.position);  // Kod ile oyuncuya döndür
    }
    else
    {
        // Diğer durumlarda (devriye, arama vb.) NavMesh kendi dönebilir
        agent.updateRotation = true; 
        
        // Eğer hareket ediyorsa Strafe değerini sıfırla ki düz yürüsün
        if (agent.velocity.magnitude > 0.1f && !isBursting)
        {
             currentStrafeValue = Mathf.Lerp(currentStrafeValue, 0, Time.deltaTime * 5f);
        }
    }

    // Eklenecek Durum Kontrolü: Siperdeyken hızı sıfırla
    if (currentState == EnemyState.TakeCover && agent.remainingDistance < 0.5f)
    {
        anim.SetFloat("Speed", 0);
    }

    // State Makinesi
    switch (currentState)
    {
        case EnemyState.Idle: HandleIdle(); break;
        case EnemyState.Chase: HandleChase(); break;
        case EnemyState.Search: HandleSearch(); break;
        case EnemyState.Return: HandleReturn(); break;
        case EnemyState.TakeCover: HandleTakeCover(); break;
        case EnemyState.SuppressiveFire: HandleSuppressiveFire(); break;
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
    // Chase başladığında çömelmeyi kapat
    if (anim != null) anim.SetBool("IsCrouching", false);

    // --- GRUP HABERLEŞMESİ (Alarm Sistemi) ---
    float alertRadius = 15f; 
    Collider[] friends = Physics.OverlapSphere(transform.position, alertRadius);
    foreach (var f in friends)
    {
        EnemyAI friendAI = f.GetComponent<EnemyAI>();
      if (Time.time > lastAlertTime + alertCooldown)
    {
    // Baştaki 'float' ve 'Collider[]' gibi ifadeleri kaldırdık, sadece isimlerini kullanıyoruz
    alertRadius = 15f; 
    friends = Physics.OverlapSphere(transform.position, alertRadius);
    
    foreach (var friendCollider in friends) // 'f' yerine 'friendCollider' diyerek çakışmayı önledik
    {
        EnemyAI currentFriend = friendCollider.GetComponent<EnemyAI>(); // 'friendAI' yerine 'currentFriend'
        
        if (currentFriend != null && currentFriend != this && 
           (currentFriend.currentState == EnemyState.Idle || currentFriend.currentState == EnemyState.Search))
        {
            currentFriend.AlertFromFriend(player.position);
        }
    }
    lastAlertTime = Time.time; 
    }
    }

    // --- HAREKET VE SALDIRI MANTIĞI ---
    float distance = Vector3.Distance(transform.position, player.position);

    if (CanSeePlayer())
    {
        lastSeenPosition = player.position;
        if (CanSeePlayer())
    {
        lastSeenPosition = player.position;

        // KOŞMA MI YÜRÜME Mİ?
        if (distance > attackRange + 3f) 
        {
            agent.speed = 6.5f; // Hızı biraz daha artırdık ki Run_guard_AR devreye girsin
        }
        else 
        {
            agent.speed = 3.5f; // Ateş ederken sakinleş (Yürüme/Strafe hızı)
        }
        }
        if (distance <= attackRange)
        {
            // --- STRAFE (YAN HAREKET) HESAPLAMA ---
            strafeTimer -= Time.deltaTime;
            if (strafeTimer <= 0)
            {
                currentStrafeValue = Random.Range(-1, 2); // -1: Sol, 0: Düz, 1: Sağ
                strafeTimer = Random.Range(1.5f, 3.0f);
            }

            // Robotun sana bakmasını zorla ve NavMesh'in kendi dönmesini engelle
            agent.updateRotation = false; 
            FaceTarget(player.position);

            // Yanlara doğru bir hedef nokta belirle
            Vector3 sideDirection = transform.right * currentStrafeValue;
            Vector3 movePos = transform.position + sideDirection * 2f;
            agent.SetDestination(movePos);

            // Animatöre değerleri gönder (Blend Tree burada çalışır)
            if (anim != null)
            {
                anim.SetFloat("StrafeDire", currentStrafeValue);
                anim.SetFloat("Speed", agent.velocity.magnitude);
            }

            TryShoot();
        }
        else
        {
            // Menzil dışındaysa doğrudan oyuncuya koş
            agent.updateRotation = true; // Koşarken önüne baksın
            currentStrafeValue = 0;
            agent.SetDestination(player.position);
            
            if (anim != null) anim.SetFloat("StrafeDire", 0);
        }
    }
    else
    {
        // Oyuncu görüşten çıktıysa
        agent.updateRotation = true;
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
        // SİPERDEYKEN OYUNCUYA BAKMA KODU (Sabitlendi)
        Vector3 targetDir = player.position - transform.position;
        targetDir.y = 0; // Yukarı-aşağı bakmasın, sadece yatayda dönsün
        
        if (targetDir != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(targetDir);
            // Robotu anında değil, yumuşakça oyuncuya çevir
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 5f);
        }

        // Çömelme animasyonunu tetikle
        if (anim != null) anim.SetBool("IsCrouching", true);
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
    
    // Ateş etmeye başladığında NavMeshAgent'ı durdur (Kaymayı engeller)
    if (agent != null) 
    {
        agent.isStopped = true;
        agent.velocity = Vector3.zero; // Mevcut ivmeyi de sıfırla ki küt diye dursun
    }

    while (shots > 0)
    {
        // Her mermiden önce yüzünü oyuncuya döndür
        FaceTarget(player.position); 
        
        // Mermiyi fırlat
        FireSingleShot(player.position);
        
        // Efekti oynat
        if(muzzleFlashLight != null) StartCoroutine(FlashEffect());
        
        shots--;
        
        // Mermiler arası bekleme
        yield return new WaitForSeconds(burstDelay);
    }

    // Ateş etme bitti
    isBursting = false;
    
    // Ajanı tekrar serbest bırak (Yürümeye devam etsinler)
    if (agent != null) 
    {
        agent.isStopped = false;
    }

    // Bir sonraki burst için bekleme süresi
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
    }if (anim != null) 
    {
        // Önceki tetikleyiciyi temizle ki yeni gelen emri taze taze alsın
        anim.ResetTrigger("Shoot"); 
        anim.SetTrigger("Shoot"); 
    }
    if (isReloading) return;

    enemyCurrentAmmo--;
    EjectShell();
    
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
    if (currentState != EnemyState.Chase)
    {
        currentState = EnemyState.Search;
        lastSeenPosition = targetPos;
        agent.SetDestination(lastSeenPosition);
        agent.speed = 6.0f; // Sese giderken de koşsun!
    }

}
    void EjectShell()
    {
        if (shellPrefab != null && shellEjectPoint != null)
        {
            // Kovanı oluştur
            GameObject shell = Instantiate(shellPrefab, shellEjectPoint.position, shellEjectPoint.rotation);
            
            // Fizik uygula
            Rigidbody rb = shell.GetComponent<Rigidbody>();
            if (rb != null)
            {
                // Sağa ve hafif yukarı fırlat
                Vector3 forceDir = shellEjectPoint.right * shellForce + shellEjectPoint.up * (shellForce * 0.2f);
                rb.AddForce(forceDir, ForceMode.Impulse);
                
                // Rastgele döndür (havalı görünmesi için)
                rb.AddTorque(Random.insideUnitSphere * 10f, ForceMode.Impulse);
            }
            
            // Sahneyi kirletmemesi için 2 saniye sonra sil
            Destroy(shell, 2f);
        }
    }
    void FaceTarget(Vector3 targetPos)
{
    Vector3 direction = (targetPos - transform.position).normalized;
    direction.y = 0; // Robotun öne/arkaya eğilmesini engelle
    if (direction != Vector3.zero)
    {
        Quaternion lookRotation = Quaternion.LookRotation(direction);
        // Anında değil, çok hızlı bir şekilde (20f) hedefe dön
        transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * 20f);
    }
}
}
