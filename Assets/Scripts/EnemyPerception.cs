using UnityEngine;

public class EnemyPerception : MonoBehaviour
{
    public bool HeardNoise { get; private set; }
    public Vector3 lastHeardPosition;

    [Header("Vision")]
    public Transform player;
    public float viewDistance = 20f;
    public float viewAngle = 90f;

    public bool CanSeePlayer()
    {
        if (player == null) return false;

        Vector3 dir = (player.position - transform.position).normalized;
        float dist = Vector3.Distance(transform.position, player.position);

        if (dist > viewDistance) return false;
        if (Vector3.Angle(transform.forward, dir) > viewAngle * 0.5f) return false;

        return true;
    }

    // 🔊 EnemyAI burayı çağırıyor
    public void OnHearNoise(Vector3 noisePos)
    {
        HeardNoise = true;
        lastHeardPosition = noisePos;
    }

    public void ClearNoise()
    {
        HeardNoise = false;
    }
}
