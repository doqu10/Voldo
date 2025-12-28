using UnityEngine;
using UnityEngine.AI;

public class EnemyCover : MonoBehaviour
{
    [Header("References")]
    public EnemyAI ai;
    public NavMeshAgent agent;
    public Transform player;

    [Header("Cover Settings")]
    public float coverSearchRadius = 15f;
    public LayerMask coverMask;
    public float coverStopDistance = 1.2f;

    Vector3 currentCoverPoint;
    bool hasCover = false;

    void Awake()
    {
        if (agent == null)
            agent = GetComponent<NavMeshAgent>();
    }

    void Update()
    {
        if (ai == null || agent == null) return;
        if (ai.currentState != EnemyState.TakeCover) return;

        HandleCover();
    }

    void HandleCover()
    {
        if (!hasCover)
        {
            FindCover();
            return;
        }

        agent.isStopped = false;
        agent.SetDestination(currentCoverPoint);

        if (agent.remainingDistance <= coverStopDistance)
        {
            agent.isStopped = true;
        }
    }

    void FindCover()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, coverSearchRadius, coverMask);

        float bestScore = -Mathf.Infinity;
        Vector3 bestPoint = Vector3.zero;

        foreach (var hit in hits)
        {
            Vector3 dirToPlayer = (player.position - hit.transform.position).normalized;

            // Cover gerçekten oyuncuyu blokluyor mu?
            if (Physics.Raycast(hit.transform.position, dirToPlayer, out RaycastHit rayHit))
            {
                if (rayHit.transform != player)
                {
                    float dist = Vector3.Distance(transform.position, hit.transform.position);
                    float score = -dist; // yakına öncelik ver

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestPoint = hit.transform.position;
                    }
                }
            }
        }

        if (bestScore > -Mathf.Infinity)
        {
            currentCoverPoint = bestPoint;
            hasCover = true;
        }
    }

    public void ResetCover()
    {
        hasCover = false;
    }
}
