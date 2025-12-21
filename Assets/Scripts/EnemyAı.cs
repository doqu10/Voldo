using UnityEngine;
using UnityEngine.AI;

public class EnemyAI : MonoBehaviour
{
    [Header("References")]
    public Transform player;

    [Header("AI Settings")]
    public float detectRange = 15f;
    public float stopDistance = 2f;

    NavMeshAgent agent;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    void Update()
    {
        float distance = Vector3.Distance(transform.position, player.position);

        if (distance <= detectRange)
        {
            agent.stoppingDistance = stopDistance;
            agent.SetDestination(player.position);
        }
        else
        {
            agent.ResetPath();
        }
    }
}
