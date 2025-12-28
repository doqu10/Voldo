using UnityEngine;
using UnityEngine.AI;

public class EnemyAnimController : MonoBehaviour
{
    [Header("References")]
    public Animator animator;
    public EnemyAI ai;
    public NavMeshAgent agent;
    public EnemyCombat combat;
    [Header("Animation Settings")]
    public float walkThreshold = 0.1f;
    public float runThreshold = 3f;

    void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (agent == null)
            agent = GetComponent<NavMeshAgent>();
    }

    void Update()
    {
         if (ai == null || animator == null) 
            return;

        if (ai.currentState == EnemyState.Dead)
             return; // 💀 ÖLÜYSE TAMAMEN SUS

        HandleMovementAnimation();
        HandleCombatAnimation();
        //HandleDeathAnimation();
    }

    // =========================
    // HAREKET
    // =========================
    void HandleMovementAnimation()
    {
    // Ajan kapalıysa veya havadaysa çalışma
    if (agent == null || !agent.enabled || !agent.isOnNavMesh) return;

    // Karakterin dünya hızını yerel (local) hıza çeviriyoruz
    Vector3 localVelocity = transform.InverseTransformDirection(agent.velocity);
    
    // Animatördeki "Speed" ileri-geri, "Strafe" sağa-sola parametreleridir.
    animator.SetFloat("Speed", localVelocity.z); 
    animator.SetFloat("Strafe", localVelocity.x);
    }

    // =========================
    // ATEŞ
    // =========================
     void HandleCombatAnimation()
     {
     if (combat == null) return;

        // EnemyCombat içindeki IsShooting bilgisini Animator'daki şaltere (bool) bağladık
        animator.SetBool("IsShooting", combat.IsShooting);
     }
    
    // =========================
    // ÖLÜM
    // =========================
   // void HandleDeathAnimation()
   // {
   //     bool dead = ai.currentState == EnemyState.Dead;
   //     animator.SetBool("IsDead", dead);
   // }
}
