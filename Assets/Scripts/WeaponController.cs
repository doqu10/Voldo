using UnityEngine;
using System.Collections;

public class WeaponController : MonoBehaviour
{
    [Header("References")]
    public Camera fpsCamera;
    public PlayerController playerController;

    [Header("Weapon Settings")]
    public float range = 100f;
    public float fireRate = 0.2f;
    public float recoilStrength = 1.5f;

    [Header("UI")]
    public GameObject crosshair;
    public GameObject hitLine1;
    public GameObject hitLine2;
    public float hitMarkerTime = 1f;

    float nextFireTime = 0f;

    void Start()
    {
        hitLine1.SetActive(false);
        hitLine2.SetActive(false);
        crosshair.SetActive(true);
    }

    void Update()
    {
        if (Input.GetMouseButton(0) && Time.time >= nextFireTime)
        {
            nextFireTime = Time.time + fireRate;
            Shoot();
        }
    }

void Shoot()
{
    Ray ray = new Ray(
        fpsCamera.transform.position,
        fpsCamera.transform.forward
    );

    RaycastHit hit;

    if (Physics.Raycast(ray, out hit, range))
    {
        Debug.Log("HIT: " + hit.collider.name);

        EnemyHealth enemy = hit.collider.GetComponent<EnemyHealth>();
        if (enemy != null)
        {
            enemy.TakeDamage(25f);
        }

        StopAllCoroutines();
        StartCoroutine(ShowHitMarker());
    }

    playerController.AddRecoil(recoilStrength);
}
    IEnumerator ShowHitMarker()
    {
        crosshair.SetActive(false);

        hitLine1.SetActive(true);
        hitLine2.SetActive(true);

        yield return new WaitForSeconds(hitMarkerTime);

        hitLine1.SetActive(false);
        hitLine2.SetActive(false);

        crosshair.SetActive(true);
    }
}
