using UnityEngine;
using System.Collections;
using UnityEngine.AI;


public class EnemyCombat : MonoBehaviour
{
    [Header("References")]
    public EnemyAI ai;
    public EnemyPerception perception;
    public Transform firePoint;
    public Transform player;
    public GameObject trailPrefab;    
    public GameObject shellPrefab;    
    public Transform shellEjectPoint; 
    public bool IsShooting { get; private set; }
    [Header("Ammo Settings")]
    public int magSize = 20;
    private int currentAmmo;
    public float reloadTime = 2.5f; // Animasyonun süresine göre ayarla
    private bool isReloading = false;
    [Header("Advanced Aiming")]
    public Transform spine; // Düşman modelinin içindeki Spine (Omurga) kemiğini buraya sürükleyeceğiz
    public Vector3 aimOffset = new Vector3(0, 1.4f, 0); // Oyuncunun tam ayağına değil, göğsüne nişan alması için
    [Header("Combat Settings")]
    public float attackRange = 15f;
    public float fireRate = 1.5f; // İki burst arası bekleme süresi
    public float damage = 10f;

    private float nextFireTime;
    private bool isBursting = false; // Hata veren değişken burada tanımlı

    void Start() {
    currentAmmo = magSize;
    }   
    void Update()
    {
        if (ai == null || ai.currentState == EnemyState.Dead) return;

    if (ai.currentState == EnemyState.Combat && player != null)
    {
        // Düşmanın sadece kollarını değil, tüm vücudunu oyuncuya çevirir
        Vector3 lookPos = player.position - transform.position;
        lookPos.y = 0; // Düşmanın havaya veya yere doğru eğilmesini engeller
        Quaternion rotation = Quaternion.LookRotation(lookPos);
        transform.rotation = Quaternion.Slerp(transform.rotation, rotation, Time.deltaTime * 10f);
    }

    if (ai.currentState != EnemyState.Combat) return;
    HandleCombat();
    }

    void HandleCombat()
    {
        if (isReloading || ai.currentState == EnemyState.Dead) return;

        if (currentAmmo <= 0)
        {
            StartCoroutine(EnemyReload());
            return;
        }
        if (perception == null || !perception.CanSeePlayer() || player == null) return;

        float distance = Vector3.Distance(transform.position, player.position);
        
        // Ateş etme zamanı geldiyse ve şu an ateş etmiyorsa başla
        if (distance <= attackRange && Time.time >= nextFireTime && !isBursting)
        {
            StartCoroutine(BurstFireWithDodge());
        }
    }

   IEnumerator BurstFireWithDodge()
{
    isBursting = true;
    IsShooting = true;

    Animator anim = GetComponentInChildren<Animator>();
    if (anim != null) anim.SetTrigger("Shoot_Burst");

    NavMeshAgent agent = GetComponent<NavMeshAgent>();
    
    // GÜVENLİK KONTROLÜ: Ajan yoksa veya kapalıysa (ölüyse) durdurma işlemini yapma
    if (agent != null && agent.enabled && agent.isOnNavMesh)
        agent.isStopped = true;

    yield return new WaitForSeconds(0.1f);

    for (int i = 0; i < 3; i++)
    {
        // Eğer ateş ederken düşman öldüyse döngüden çık
        if (ai.currentState == EnemyState.Dead) yield break;

        ShootBullet(); 
        yield return new WaitForSeconds(0.12f);
    }

    // GÜVENLİK KONTROLÜ: Ajan hala hayattaysa hareketine izin ver
    if (agent != null && agent.enabled && agent.isOnNavMesh)
        agent.isStopped = false;

    nextFireTime = Time.time + fireRate;
    isBursting = false;
    IsShooting = false;
}


    void ShootBullet()
    {
    
{
    if (firePoint == null || player == null) return;

    Vector3 dir = (player.position - firePoint.position).normalized;
    RaycastHit hit;
    Vector3 endPoint = firePoint.position + dir * attackRange;

    if (Physics.Raycast(firePoint.position, dir, out hit, attackRange))
    {
        endPoint = hit.point;
        if (hit.collider.CompareTag("Player"))
        {
            PlayerHealth ph = hit.collider.GetComponent<PlayerHealth>();
            if (ph != null) ph.TakeDamage(damage);
        }
        currentAmmo--;
    }

        // MERMİ İZİ (TRAIL)
        if (trailPrefab != null)
        {
            GameObject trail = Instantiate(trailPrefab, firePoint.position, Quaternion.identity);
            LineRenderer lr = trail.GetComponent<LineRenderer>();
            if (lr != null)
            {
                lr.SetPosition(0, firePoint.position);
                lr.SetPosition(1, endPoint);
            }
            Destroy(trail, 0.05f);
        }

        // KOVAN FIRLATMA
        if (shellPrefab != null && shellEjectPoint != null)
        {
            GameObject shell = Instantiate(shellPrefab, shellEjectPoint.position, shellEjectPoint.rotation);
            Rigidbody rb = shell.GetComponent<Rigidbody>();
            if (rb != null) rb.AddForce(shellEjectPoint.right * 3f, ForceMode.Impulse);
            Destroy(shell, 2f);
        }
    }
    }
    IEnumerator EnemyReload()
{
    isReloading = true;
    IsShooting = false;
    
    Animator anim = GetComponentInChildren<Animator>();
    if (anim != null)
    {
        anim.SetTrigger("Reload"); // Animator'da bu isimde bir Trigger açacağız
    }

    yield return new WaitForSeconds(reloadTime);

    currentAmmo = magSize;
    isReloading = false;
}
void LateUpdate()
{
    if (ai == null || ai.currentState == EnemyState.Dead) return;

    if (ai.currentState == EnemyState.Combat && player != null && spine != null)
    {
        // Oyuncunun göğüs hizasını hesapla
        Vector3 targetDirection = (player.position + aimOffset) - spine.position;
        
        // Spine kemiğini bu yöne döndür
        Quaternion targetRotation = Quaternion.LookRotation(targetDirection);
        
        // Eğer modelin ekseni tersse buraya Euler açısı eklememiz gerekebilir (Genelde -90 veya 90)
        spine.rotation = targetRotation;
    }
}
    }