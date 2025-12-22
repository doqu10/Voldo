using UnityEngine;
using System.Collections;
public class PlayerHealth : MonoBehaviour
{
    public float maxHealth = 100f;
    float currentHealth;
    // SARSINTI AYARLARI
    [Header("Shake Settings")]
    public Transform cameraTransform; // Sallağımız kamera
    public float shakeDuration = 0.15f; // Ne kadar sürsün?
    public float shakeMagnitude = 0.2f; // Ne kadar şiddetli olsun?
    private Vector3 initialCenterPos;
    void Start()
    {
        currentHealth = maxHealth;
        if(cameraTransform != null) 
        initialCenterPos = cameraTransform.localPosition;
    }

    public void TakeDamage(float amount)
    {
        currentHealth -= amount;
        Debug.Log("Player Health: " + currentHealth);
        // --- SARSINTIYI TETİKLE ---
        StopAllCoroutines(); // Eğer üst üste darbe alırsak sarsıntılar karışmasın
        StartCoroutine(Shake());
        // --------------------------

        if (currentHealth <= 0)
        {
            Die();
        }
    }
    IEnumerator Shake()
    {
        float elapsed = 0.0f;

        while (elapsed < shakeDuration)
        {
            // Rastgele küçük değerler üretip kamerayı kaydırıyoruz
            float x = Random.Range(-1f, 1f) * shakeMagnitude;
            float y = Random.Range(-1f, 1f) * shakeMagnitude;

            // Her zaman en baştaki merkez noktasını baz alarak salla
            cameraTransform.localPosition = initialCenterPos + new Vector3(x, y, 0);

            elapsed += Time.deltaTime;
            yield return null; // Bir sonraki kareye kadar bekle
        }

        // Sarsıntı bitince kamerayı orijinal yerine geri koy
        cameraTransform.localPosition = initialCenterPos;
    }
    void Die()
    {
        Debug.Log("PLAYER DIED");
        // ileride: restart, death screen, ragdoll vs
        Time.timeScale = 0f;
    }
}
