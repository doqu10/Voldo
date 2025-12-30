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
    public Transform shellEjectPoint;
    public GameObject shellPrefab;
    public bool IsShooting { get; private set; }

    [Header("Visual Effects")]
    public GameObject bulletTrail; // Inspector'dan TrailRenderer içeren prefab'ı buraya koy

    [Header("Ammo Settings")]
    public int magSize = 20;
    private int currentAmmo;
    public float reloadTime = 2.5f;
    private bool isReloading = false;

    [Header("Advanced Aiming")]
    public Transform spine;
    public Vector3 aimOffset = new Vector3(0, 1.5f, 0);
    public Vector3 rotationOffset;
    [Range(0, 45)] public float maxBendAngle = 20f;

    [Header("Combat Settings")]
    public float attackRange = 15f;
    [SerializeField] float fireRate = 0.25f; // saniyede 4 mermi 
   
    public float damage = 10f;

    private float nextFireTime;
    private bool isBursting = false;
    
   
    void Start()
    {
        currentAmmo = magSize;
    }

    void Update()
    {
                if (ai.currentState == EnemyState.TakeCover){
            IsShooting = false;
            return;
        }

        if (ai == null || ai.currentState == EnemyState.Dead) return;

        // Vücudun yatayda dönmesi
        if (ai.currentState == EnemyState.Combat && player != null)
        {
            Vector3 lookDirection = player.position - transform.position;
            lookDirection.y = 0;

            if (lookDirection != Vector3.zero)
            {
                Quaternion bodyRotation = Quaternion.LookRotation(lookDirection);
                transform.rotation = Quaternion.Slerp(transform.rotation, bodyRotation, Time.deltaTime * 20f);
            }
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

        if (distance <= attackRange && Time.time >= nextFireTime && !isBursting)
        {
            StartCoroutine(BurstFireWithDodge());
        }
    }

    IEnumerator BurstFireWithDodge()
    {
        yield return new WaitForSeconds(0.2f);
        isBursting = true;
        IsShooting = true;

        Animator anim = GetComponentInChildren<Animator>();
        if (anim != null) anim.SetTrigger("Shoot_Burst");

        NavMeshAgent agent = GetComponent<NavMeshAgent>();
        if (agent != null && agent.enabled && agent.isOnNavMesh)
            agent.isStopped = true;

        yield return new WaitForSeconds(0.1f);

        for (int i = 0; i < 3; i++)
        {
            if (ai.currentState == EnemyState.Dead) yield break;
                if (Time.time >= nextFireTime)
                    {
                        nextFireTime = Time.time + fireRate;
                        ShootBullet();
                    }

            yield return new WaitForSeconds(0.12f);
        }

        if (agent != null && agent.enabled && agent.isOnNavMesh)
            agent.isStopped = false;

        nextFireTime = Time.time + fireRate;
        isBursting = false;
        IsShooting = false;
    }

    void ShootBullet(){
    if (isReloading) return;
    if (firePoint == null || player == null) return;
    currentAmmo--; 
    firePoint.position += firePoint.forward * 0.05f;
    RaycastHit hit;
    Vector3 shotDirection = (player.position - firePoint.position).normalized;

    // Hasar ve Trail İşlemi
    if (Physics.Raycast(firePoint.position, shotDirection, out hit, attackRange))
    {   Debug.Log("Çarptığım şey: " + hit.collider.name);
        CreateTrail(hit.point);
        
            PlayerHealth ph = hit.collider.GetComponent<PlayerHealth>();
            if (ph != null) ph.TakeDamage(damage);
        
    }
    else
    {
        CreateTrail(firePoint.position + shotDirection * attackRange);
    }

    // GERÇEKÇİ KOVAN FIRLATMA
    if (shellPrefab != null && shellEjectPoint != null)
    {
        GameObject shell = Instantiate(shellPrefab, shellEjectPoint.position, shellEjectPoint.rotation);
        Rigidbody rb = shell.GetComponent<Rigidbody>();
        if (rb != null) 
        {
            // Hızı 0.5f'e çektik, artık 100 metre gitmez
            rb.AddForce(shellEjectPoint.right * 0.5f, ForceMode.Impulse);
            rb.AddTorque(Random.insideUnitSphere * 0.1f, ForceMode.Impulse);
        }
        Destroy(shell, 2f);
    }
} // <--- Bu parantezin olduğundan emin ol!

    void CreateTrail(Vector3 targetPoint)
    {
        if (bulletTrail == null) return;

        GameObject trailObj = Instantiate(bulletTrail, firePoint.position, Quaternion.identity);
        TrailRenderer trail = trailObj.GetComponent<TrailRenderer>();

        if (trail != null)
        {
            StartCoroutine(MoveTrail(trail, targetPoint));
        }
    }

    IEnumerator MoveTrail(TrailRenderer trail, Vector3 targetPoint)
    {
        float time = 0;
        Vector3 startPos = trail.transform.position;

        while (time < 1)
        {
            trail.transform.position = Vector3.Lerp(startPos, targetPoint, time);
            time += Time.deltaTime / 0.1f; // 0.1f merminin hızıdır, istersen değiştirebilirsin
            yield return null;
        }
        trail.transform.position = targetPoint;
        Destroy(trail.gameObject, trail.time);
    }

    IEnumerator EnemyReload()
    {    if (isReloading) yield break;
        isReloading = true;
        IsShooting = false;
        Animator anim = GetComponentInChildren<Animator>();
        if (anim != null) anim.SetTrigger("Reload");

        yield return new WaitForSeconds(reloadTime);

        currentAmmo = magSize;
        isReloading = false;
    }

    void LateUpdate()
{
    if (ai == null || ai.currentState == EnemyState.Dead || player == null || spine == null) return;

    if (ai.currentState == EnemyState.Combat)
    {
        // Hedefe olan yönü bul
        Vector3 targetDirection = (player.position + aimOffset) - spine.position;
        
        if (targetDirection != Vector3.zero)
        {
            // Belin sadece hedefe bakmasını sağlayan temiz rotasyon
            Quaternion targetRotation = Quaternion.LookRotation(targetDirection);
            
            // ÖNEMLİ: Sağa-sola (Y) bükülmeyi sıfırlıyoruz ki Update ile çakışmasın
            // Sadece yukarı-aşağı ve eğim kalsın
            spine.rotation = targetRotation * Quaternion.Euler(rotationOffset);
        }
    }
}
}