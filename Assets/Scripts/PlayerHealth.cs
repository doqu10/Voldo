using UnityEngine;
using System.Collections;

public class PlayerHealth : MonoBehaviour
{
    public float maxHealth = 100f;
    private float currentHealth;

    [Header("Shake Settings")]
    public Transform cameraTransform;
    public float shakeDuration = 0.15f;
    public float shakeMagnitude = 0.2f;
    private Vector3 initialCenterPos;

    [Header("Stamina Settings")]
    public float maxStamina = 20f;
    private float currentStamina;

    public float CurrentStamina {
        get { return currentStamina; }
        set { currentStamina = Mathf.Clamp(value, 0, maxStamina); }
    }

    void Start() {
        currentHealth = maxHealth;
        if(cameraTransform != null) initialCenterPos = cameraTransform.localPosition;
        currentStamina = maxStamina;
    }

    public void TakeDamage(float amount) {
        currentHealth -= amount;
        if (cameraTransform != null) {
            StopAllCoroutines();
            StartCoroutine(Shake());
        }
        if (currentHealth <= 0) Die();
    }

    IEnumerator Shake() {
        float elapsed = 0.0f;
        while (elapsed < shakeDuration) {
            float x = Random.Range(-1f, 1f) * shakeMagnitude;
            float y = Random.Range(-1f, 1f) * shakeMagnitude;
            cameraTransform.localPosition = initialCenterPos + new Vector3(x, y, 0);
            elapsed += Time.deltaTime;
            yield return null;
        }
        cameraTransform.localPosition = initialCenterPos;
    }

    void Die() {
        Debug.Log("PLAYER DIED!");
        // Buraya Game Over ekranı veya sahne yeniden yükleme kodu ekleyebilirsin.
        UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
    }
}