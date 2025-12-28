using UnityEngine;
using System.Collections;

public class WeaponController : MonoBehaviour
{
    [Header("References")]
    public Camera fpsCamera;
    public PlayerController playerController;
    public GameObject impactPrefab;
    public GameObject sparkPrefab;
    public GameObject trailPrefab;
    public Transform firePoint;

    [Header("Weapon Settings")]
    public float range = 100f;
    public float fireRate = 0.1f;         // 0.2'den 0.1'e çektim, daha seri tarar.
    public float recoilStrength = 0.4f;   // 1.5 çok fazlaydı, 0.4 ile daha kontrollü olur.

    [Header("UI")]
    public GameObject crosshair;
    public GameObject hitLine1;
    public GameObject hitLine2;
    public float hitMarkerTime = 0.1f;    // 1 saniye çok uzundu, 0.1 idealdir.

    float nextFireTime = 0f;

    [Header("Ammo Settings")]
    public int magSize = 30;
    public int currentAmmo;
    public int totalAmmo = 90;
    public float reloadTime = 2f;
    bool isReloading = false;

    void Start()
    {
        currentAmmo = magSize;
        hitLine1.SetActive(false);
        hitLine2.SetActive(false);
        crosshair.SetActive(true);
    }

    void Update()
    {
        if (isReloading) return;

        // --- DEĞİŞİKLİK 1: OTOMATİK RELOAD ---
        // Mermi bittiyse VE ateş etmeye çalışıyorsan otomatik reload başlar.
        if (currentAmmo <= 0 && Input.GetMouseButton(0) && totalAmmo > 0)
        {
            StartCoroutine(Reload());
            return;
        }

        // Manuel reload (R tuşu)
        if (Input.GetKeyDown(KeyCode.R) && currentAmmo < magSize && totalAmmo > 0)
        {
            StartCoroutine(Reload());
            return;
        }

        // --- DEĞİŞİKLİK 2: TARAMA MODU ---
        // GetMouseButtonDown yerine GetMouseButton kullanarak basılı tutmayı sağladık.
        if (Input.GetMouseButton(0) && Time.time >= nextFireTime && currentAmmo > 0)
        {
            nextFireTime = Time.time + fireRate;
            Shoot();
        }
    }

    void Shoot()
    {
        currentAmmo--;
        CreateNoise();

        Ray ray = new Ray(fpsCamera.transform.position, fpsCamera.transform.forward);
        RaycastHit hit;
        
        if (Physics.Raycast(ray, out hit, range))
        {
            CreateTrail(hit.point);
            GameObject impact = Instantiate(impactPrefab, hit.point, Quaternion.LookRotation(hit.normal));
            Destroy(impact, 1f);
            // Merminin çarptığı yeri ve yönü alarak kıvılcımı oluştur
            GameObject spark = Instantiate(sparkPrefab, hit.point, Quaternion.LookRotation(hit.normal));
            Destroy(spark, 0.5f); // Yarım saniye sonra dünyadan sil
            EnemyHealth enemy = hit.collider.GetComponent<EnemyHealth>();
            if (enemy != null)
            {
                enemy.TakeDamage(25f);
                // --- DEĞİŞİKLİK 3: HITMARKER FIX ---
                // StopAllCoroutines() yerine sadece hitmarker'ı başlatan bir mantık kurduk.
                StartCoroutine(ShowHitMarker());
            }
        }
        else
        {
            CreateTrail(ray.origin + ray.direction * range);
        }

        playerController.AddRecoil(recoilStrength);
    }

    // Hitmarker için ayrı bir kontrol ekledik ki Reload'ı bozmasın
    IEnumerator ShowHitMarker()
    {
        hitLine1.SetActive(true);
        hitLine2.SetActive(true);
        // crosshair.SetActive(false); // Opsiyonel: Tararken crosshair kaybolmasın dersen bunu kapat.

        yield return new WaitForSeconds(hitMarkerTime);

        hitLine1.SetActive(false);
        hitLine2.SetActive(false);
        // crosshair.SetActive(true);
    }

    IEnumerator Reload()
    {
        isReloading = true;
        Debug.Log("Şarjör Değiştiriliyor...");

        yield return new WaitForSeconds(reloadTime);

        int ammoNeeded = magSize - currentAmmo;
        int ammoToRemove = Mathf.Min(totalAmmo, ammoNeeded);

        totalAmmo -= ammoToRemove;
        currentAmmo += ammoToRemove;

        isReloading = false;
        Debug.Log("Reload Tamamlandı!");
    }

    // CreateTrail ve CreateNoise fonksiyonların gayet iyi, onları aynen koruyabilirsin.
    void CreateTrail(Vector3 targetPos)
    {
        GameObject trail = Instantiate(trailPrefab, firePoint.position, Quaternion.identity);
        LineRenderer lr = trail.GetComponent<LineRenderer>();
        lr.SetPosition(0, firePoint.position);
        lr.SetPosition(1, targetPos);
        Destroy(trail, 0.05f);
    }

    void CreateNoise()
    {
        float noiseRadius = 30f;
        Collider[] colliders = Physics.OverlapSphere(transform.position, noiseRadius);
        foreach (var col in colliders)
        {
            EnemyAI ai = col.GetComponent<EnemyAI>();
            if (ai != null) ai.HearNoise(transform.position);
        }
    }
}