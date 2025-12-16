using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(BossHealth))]
public class S_BossController : MonoBehaviour
{
    // --- Enums and Structs ---

    private enum BossState
    {
        Idle,        // 대기 (플레이어가 인식 범위 밖에 있음)
        Dashing,     // 대쉬로 추적 중
        Backdashing, // 백대쉬 중
        Attacking,   // 공격 중
        Stunned,     // 기절
        Dead         // 사망
    }

    [System.Serializable]
    public struct SoundEffect
    {
        public string name;
        public AudioClip clip;
    }

    [System.Serializable]
    public struct JumpAttackStats
    {
        [Tooltip("점프 공격 시 솟아오르는 힘입니다.")]
        public float upForce;
        [Tooltip("점프 공격 시 수평 이동 속도입니다.")]
        public float horizontalSpeed;
        [Tooltip("점프 공격 시 내려찍는 힘입니다.")]
        public float downForce;
        [Tooltip("애니메이션에 맞춰 상승에 걸리는 시간(초)입니다.")]
        public float ascentDuration;
        [Tooltip("정점에서 멈춰있는 시간(초)입니다. 0이면 멈추지 않습니다.")]
        public float apexDelay;
        [Tooltip("애니메이션에 맞춰 하강에 걸리는 시간(초)입니다.")]
        public float descentDuration;
    }

    // --- Inspector Fields ---

    [Header("디버그")]
    [Tooltip("활성화하면 보스의 현재 상태와 결정 등 상세 로그를 콘솔에 출력합니다.")]
    public bool enableDebugLogs = true;

    [Header("핵심 참조")]
    [Tooltip("플레이어 오브젝트의 Transform을 할당해야 합니다.")]
    public Transform playerTransform;
    [Tooltip("보스 체력 UI 스크립트를 할당해야 합니다.")]
    public BossHealthUI bossHealthUI;
    
    [Header("Zone 콜라이더 참조 (4개)")]
    [Tooltip("1. 보스가 플레이어를 인지하는 가장 바깥쪽 범위입니다.")]
    public Collider2D trackingZoneCollider;
    [Tooltip("2. 플레이어가 너무 가까울 때 백대쉬를 사용하는 가장 안쪽 범위입니다.")]
    public Collider2D backdashZoneCollider;
    [Tooltip("3. 점프 공격(1, 2)을 사용하는 중간 범위입니다.")]
    public Collider2D jumpAttackZoneCollider;
    [Tooltip("4. 일반 공격(3, 4, 5)을 사용하는 근접 범위입니다.")]
    public Collider2D attackZoneCollider;

    [Header("기본 설정")]
    [Tooltip("스프라이트가 기본적으로 왼쪽을 보는지 설정합니다.")]
    public bool spriteFacesLeft = true;
    [Tooltip("플레이어가 이 거리 안에 있으면 보스가 방향을 바꾸지 않습니다. (좌우 흔들림 방지)")]
    [SerializeField] private float deadZone = 0.5f;

    [Header("지면 체크")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float groundCheckRadius = 0.2f;

    [Header("대쉬 (이동용)")]
    [Tooltip("플레이어를 향한 전방 대쉬 속도입니다.")]
    public float dashSpeed = 25f;

    [Header("백대쉬")]
    [Tooltip("뒤로 물러나는 속도입니다.")]
    public float backDashSpeed = 20f;
    [Tooltip("뒤로 물러나는 시간입니다.")]
    public float backDashDuration = 0.3f;
    [Tooltip("백대쉬 이후 다음 백대쉬까지의 쿨타임입니다.")]
    public float backDashCooldown = 4.0f;

    [Header("공격 패턴")]
    [Tooltip("점프 공격 1 (낮은 점프)의 스탯입니다.")]
    public JumpAttackStats jumpAttack1Stats = new JumpAttackStats { 
        upForce = 50f, 
        horizontalSpeed = 10f, 
        downForce = 40f, 
        ascentDuration = 0.25f,  // 3 frames @ 12fps
        apexDelay = 0f,          // Attack1은 정점 대기 없음
        descentDuration = 0.333f // 4 frames @ 12fps
    };
    [Tooltip("점프 공격 3 (높은 점프)의 스탯입니다.")]
    public JumpAttackStats jumpAttack3Stats = new JumpAttackStats { 
        upForce = 30f, 
        horizontalSpeed = 15f, 
        downForce = 35f, 
        ascentDuration = 0.75f,  // 9 frames @ 12fps (3->12)
        apexDelay = 0.417f,      // 5 frames @ 12fps (12->17)
        descentDuration = 0.667f // 8 frames @ 12fps (17->25)
    };
    [Tooltip("공격 애니메이션이 응답하지 않을 경우, 강제로 상태를 종료하기까지 대기하는 시간(초)입니다.")]
    public float attackForceFinishTime = 4.0f;
    [Tooltip("다음 행동까지의 최소 대기 시간입니다.")]
    public float actionCooldown = 1.5f;
    
    [Header("2페이즈 체력 회복")]
    [Tooltip("등장 후 서서히 회복할 체력량입니다.")]
    public float healthToRecover = 40f;
    [Tooltip("체력을 모두 회복하는 데 걸리는 시간입니다.")]
    public float healthRecoverTime = 3.0f;

    [Header("사운드")]
    public AudioClip dieSound;
    public AudioClip bossBgm;
    [Tooltip("인스펙터에서 사운드 이름과 클립을 등록하세요.")]
    public List<SoundEffect> soundEffects;

    // --- 내부 컴포넌트 및 상태 변수 ---
    private Rigidbody2D rb;
    private AudioSource audioSource;
    private Animator animator;
    private BossHealth bossHealth;
    
    private Dictionary<string, AudioClip> soundDictionary;
    private float originalGravityScale;

    private BossState currentState = BossState.Idle;
    private float lastActionTime = -10f;
    private float lastBackdashTime = -10f;
    private int lastAttackTriggerID = -1;
    private Vector2 moveDirection;
    private bool isDirectionLocked = false;

    // Zone 감지 플래그
    private bool isPlayerInTrackingZone = false;
    private bool isPlayerInBackdashZone = false;
    private bool isPlayerInJumpAttackZone = false;
    private bool isPlayerInAttackZone = false;
    private bool isGrounded = false;

    // 공격 ID 목록
    private List<int> jumpAttackTriggerIDs;
    private List<int> regularAttackTriggerIDs;
    private Dictionary<int, JumpAttackStats> jumpAttackStatsMap;

    // 애니메이터 파라미터 ID
    private readonly int IsDashingAnimID = Animator.StringToHash("isDashing");
    private readonly int BackDashAnimID = Animator.StringToHash("BackDash");
    private readonly int StunAnimID = Animator.StringToHash("Stun");
    private readonly int DieAnimID = Animator.StringToHash("die");

    // --- MonoBehaviour 생명주기 ---

    void Awake()
    {
        // 컴포넌트 초기화
        rb = GetComponent<Rigidbody2D>();
        audioSource = GetComponent<AudioSource>();
        animator = GetComponent<Animator>();
        bossHealth = GetComponent<BossHealth>();
        originalGravityScale = rb.gravityScale;

        // 2페이즈 보스는 1페이즈와 달리 서서히 체력을 회복하지 않습니다.
        if (bossHealth != null) bossHealth.enablePassiveRegen = false;

        InitializeAttackPatterns();
        InitializeSounds();
    }

    void Start()
    {
        ValidateReferences();
    }

    void Update()
    {
        // 행동 중(공격, 스턴, 백대쉬 등)이거나 사망 상태일 때는 아무것도 하지 않음
        if (currentState == BossState.Attacking || currentState == BossState.Stunned || currentState == BossState.Dead || currentState == BossState.Backdashing)
        {
            return;
        }

        // 플레이어가 추적 범위 밖에 있다면, Idle 상태를 유지하고 모든 추적 행동을 중지합니다.
        if (!isPlayerInTrackingZone)
        {
            if (currentState != BossState.Idle)
            {
                SetState(BossState.Idle);
            }
            return; // Idle 상태에서는 더 이상 진행하지 않음
        }

        // --- 이 아래는 플레이어가 추적 범위 안에 있는 경우입니다. ---

        // 만약 Idle 상태였다면, 즉시 Dashing(추적) 상태로 전환합니다.
        if (currentState == BossState.Idle)
        {
            SetState(BossState.Dashing);
        }

        // Dashing 상태에서는 항상 플레이어를 추적하고, 행동을 결정합니다.
        if (currentState == BossState.Dashing)
        {
            DecideAndPerformAction(); // [수정] 이제 이 메서드가 추적과 공격 결정을 모두 담당합니다.
        }
    }

    void FixedUpdate()
    {
        // 지면 체크
        isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);

        // [개선] T_BossController와 같이, switch 문을 사용하여 상태별 물리 처리를 명확하게 분리합니다.
        switch (currentState)
        {
            case BossState.Dashing:
                // 추적 상태에서는 계산된 방향으로 계속 이동합니다.
                rb.linearVelocity = new Vector2(moveDirection.x * dashSpeed, rb.linearVelocity.y);
                break;
            
            case BossState.Idle:
            case BossState.Stunned:
            case BossState.Dead:
                // 정지해야 하는 상태들에서는 수평 속도를 0으로 고정합니다.
                rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
                break;

            case BossState.Backdashing:
                // 백대쉬는 코루틴에서 직접 속도를 제어하므로, 여기서는 아무것도 하지 않습니다.
                // 이 case가 있기 때문에 Attacking 상태의 '움직임 정지' 로직이 백대쉬에 영향을 주지 않습니다.
                break;

            case BossState.Attacking:
                // [새로운 접근] 이제 모든 공격은 각자의 코루틴에서 움직임을 직접 제어합니다.
                // FixedUpdate에서는 Attacking 상태일 때 아무것도 하지 않습니다.
                // 일반 공격은 RegularAttackRoutine에서 강제로 정지됩니다.
                break;
        }
    }

    // --- 초기화 ---

    void InitializeAttackPatterns()
    {
        jumpAttackTriggerIDs = new List<int> { Animator.StringToHash("Attack1"), Animator.StringToHash("Attack3") };
        regularAttackTriggerIDs = new List<int> { Animator.StringToHash("Attack2"), Animator.StringToHash("Attack4"), Animator.StringToHash("Attack5") };

        jumpAttackStatsMap = new Dictionary<int, JumpAttackStats>
        {
            { jumpAttackTriggerIDs[0], jumpAttack1Stats },
            { jumpAttackTriggerIDs[1], jumpAttack3Stats }
        };
    }

    void InitializeSounds()
    {
        soundDictionary = new Dictionary<string, AudioClip>();
        if (soundEffects != null)
        {
            foreach (var effect in soundEffects)
            {
                if (!string.IsNullOrEmpty(effect.name) && effect.clip != null && !soundDictionary.ContainsKey(effect.name))
                {
                    soundDictionary.Add(effect.name, effect.clip);
                }
            }
        }
    }

    void ValidateReferences()
    {
        if (playerTransform == null)
        {
            var player = FindFirstObjectByType<F_PlayerController>();
            if (player != null) playerTransform = player.transform;
            else Debug.LogError("<b>[S_BossController]</b> 플레이어 Transform을 찾을 수 없습니다!", this);
        }
        if (bossHealthUI == null) Debug.LogError("<b>[S_BossController]</b> BossHealthUI가 할당되지 않았습니다! Inspector를 확인해주세요.", this);
        if (trackingZoneCollider == null) Debug.LogError("<b>[S_BossController]</b> Tracking Zone Collider가 할당되지 않았습니다!", this);
        if (backdashZoneCollider == null) Debug.LogError("<b>[S_BossController]</b> Backdash Zone Collider가 할당되지 않았습니다!", this);
        if (jumpAttackZoneCollider == null) Debug.LogError("<b>[S_BossController]</b> Jump Attack Zone Collider가 할당되지 않았습니다!", this);
        if (attackZoneCollider == null) Debug.LogError("<b>[S_BossController]</b> Attack Zone Collider가 할당되지 않았습니다! Inspector를 확인해주세요.", this);
        if (groundCheck == null) Debug.LogError("<b>[S_BossController]</b> Ground Check Transform이 할당되지 않았습니다! Inspector를 확인해주세요.", this);
    }

    // --- 상태 관리 ---

    void SetState(BossState newState)
    {
        if (currentState == newState) return;

        if (enableDebugLogs) Debug.Log($"<color=yellow>상태 변경: {currentState} -> {newState}</color>");

        // 이전 상태 종료 로직
        if (currentState == BossState.Attacking)
        {
            isDirectionLocked = false;
        }
        else if (currentState == BossState.Dashing)
        {
            animator.SetBool(IsDashingAnimID, false);
            moveDirection = Vector2.zero;
            // Dashing 상태가 끝나면 방향 전환 잠금을 해제합니다.
            isDirectionLocked = false;
        }

        // [핵심 수정] 상태가 변경될 때, 이전 상태의 물리적 움직임을 즉시 멈춥니다.
        // 이를 통해 '추적(Dashing)' 상태에서 '공격(Attacking)' 상태로 전환될 때
        // 미끄러지듯 움직이면서 공격하는 문제를 해결합니다. (T_BossController 참조)
        rb.linearVelocity = Vector2.zero;

        currentState = newState;

        // 새로운 상태 진입 로직
        if (currentState == BossState.Attacking)
        {
            FacePlayer();
            isDirectionLocked = true;
        }
        else if (currentState == BossState.Dashing)
        {
            animator.SetBool(IsDashingAnimID, true);
            // [수정] Dashing(이동) 상태에 진입할 때 한 번만 방향을 결정하고 잠급니다.
            // 이렇게 하면 이동 중에 플레이어가 뒤로 넘어가도 보스가 방향을 바꾸지 않습니다.
            FacePlayer();
            if (playerTransform != null)
            {
                moveDirection.x = Mathf.Sign(playerTransform.position.x - transform.position.x);
            }
            isDirectionLocked = true;
        }
    }

    // --- AI 행동 결정 ---

    void DecideAndPerformAction()
    {
        bool canPerformAction = Time.time >= lastActionTime + actionCooldown;
        bool canBackdash = Time.time >= lastBackdashTime + backDashCooldown;

        // --- 행동 결정 (Action Decision) ---
        // 우선순위가 높은 행동을 먼저 시도하고, 행동을 수행했다면 함수를 종료합니다.

        // 우선순위 1: 공격 (공통 쿨다운 적용)
        if (canPerformAction)
        {
            // 중복 공격 방지
            if (animator.GetCurrentAnimatorStateInfo(0).IsTag("Attack"))
            {
                // 애니메이션이 아직 끝나지 않았으면 새로운 행동을 시작하지 않고 대기합니다.
                // 이 return 문이 없으면 아래의 HandleMovementAndFacing()이 실행되어 공격 중 움직이는 문제가 발생합니다.
                return;
            }
            // 일반 공격
            else if (isPlayerInAttackZone)
            {
                if (enableDebugLogs) Debug.Log("AI 결정: 어택 존에서 일반 공격 실행");
                PerformRegularAttack();
                return; // 행동을 했으므로 종료
            }
            // 점프 공격
            else if (isPlayerInJumpAttackZone)
            {
                if (enableDebugLogs) Debug.Log("AI 결정: 점프 어택 존에서 점프 공격 실행");
                PerformJumpAttack();
                return; // 행동을 했으므로 종료
            }
        }

        // 우선순위 2: 백대쉬
        // 공격을 수행하지 않았을 때만 백대쉬를 고려합니다.
        if (isPlayerInBackdashZone && canBackdash)
        {
            if (enableDebugLogs) Debug.Log("AI 결정: 플레이어가 너무 가까워 백대쉬");
            StartCoroutine(BackdashRoutine());
            return; // 행동을 했으므로 종료
        }

        // --- 이동 결정 (Movement Decision) ---
        // 위의 행동들을 수행하지 않았을 경우 (쿨타임 중이거나, 범위 밖일 때) 플레이어를 추적합니다.
        HandleMovementAndFacing();
    }

    // --- 행동 실행 ---

    void PerformRegularAttack()
    {
        int randomIndex = Random.Range(0, regularAttackTriggerIDs.Count);
        int chosenAttackTrigger = regularAttackTriggerIDs[randomIndex];
        StartCoroutine(RegularAttackRoutine(chosenAttackTrigger));
    }

    void PerformJumpAttack()
    {
        // [핵심 수정] 점프 공격 시에도, 시작하기 전에 기존의 추적 움직임을 완전히 멈춥니다.
        moveDirection = Vector2.zero;
        rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);

        SetState(BossState.Attacking);

        int randomIndex = Random.Range(0, jumpAttackTriggerIDs.Count);
        int chosenAttackTrigger = jumpAttackTriggerIDs[randomIndex];
        lastAttackTriggerID = chosenAttackTrigger;

        JumpAttackStats stats = jumpAttackStatsMap[chosenAttackTrigger];
        StartCoroutine(JumpAttackRoutine(chosenAttackTrigger, stats));
    }

    void HandleMovementAndFacing()
    {
        if (playerTransform == null) return;

        // [수정] Dashing 또는 Attacking 중에는 isDirectionLocked가 true가 되므로,
        // 아래의 방향 전환 및 이동 방향 계산 로직이 실행되지 않습니다.
        // Dashing의 방향은 SetState 진입 시점에 한 번만 결정됩니다.
        if (isDirectionLocked) return;

        float distanceToPlayerX = Mathf.Abs(playerTransform.position.x - transform.position.x);

        // Dashing 상태에서 플레이어를 향해 이동 방향 설정
        moveDirection.x = Mathf.Sign(playerTransform.position.x - transform.position.x);

        // 데드존 밖에 있을 때만 방향을 바꿉니다.
        if (distanceToPlayerX > deadZone)
        {
            FacePlayer();
        }
    }

    void FacePlayer()
    {
        if (playerTransform == null || isDirectionLocked) return;

        // [핵심 수정] T_BossController와 같이, 애니메이터가 "Attack" 태그를 가진 상태를 재생 중일 때는
        // 방향 전환을 하지 않도록 하여 안정성을 높입니다.
        if (animator.GetCurrentAnimatorStateInfo(0).IsTag("Attack"))
        {
            return;
        }

        bool isPlayerRight = (playerTransform.position.x - transform.position.x) > 0;
        float targetSign = spriteFacesLeft ? (isPlayerRight ? -1f : 1f) : (isPlayerRight ? 1f : -1f);
        
        if (Mathf.Sign(transform.localScale.x) != targetSign)
        {
            transform.localScale = new Vector3(-transform.localScale.x, transform.localScale.y, transform.localScale.z);
        }
    }

    // --- 코루틴 ---

    private IEnumerator BackdashRoutine()
    {
        // 1. 백대쉬 시작: 상태 변경 및 쿨다운 타이머 시작
        SetState(BossState.Backdashing);
        lastBackdashTime = Time.time;

        // 2. 방향 결정 및 애니메이션 실행
        FacePlayer(); // 플레이어를 바라본 후 뒤로 이동
        animator.SetTrigger(BackDashAnimID);

        // 3. 물리적 이동
        float moveDirection = -Mathf.Sign(playerTransform.position.x - transform.position.x);
        rb.linearVelocity = new Vector2(moveDirection * backDashSpeed, 0);
        
        // 4. 지정된 시간만큼 뒤로 이동
        yield return new WaitForSeconds(backDashDuration);

        // 5. 이동 정지 및 1초간 행동 정지
        rb.linearVelocity = Vector2.zero;
        yield return new WaitForSeconds(1.0f);

        // 6. 상태 복귀
        // 상태가 강제로 바뀌지 않았다면 (예: 스턴) 추적 상태로 복귀
        if (currentState == BossState.Backdashing)
        {
            SetState(isPlayerInTrackingZone ? BossState.Dashing : BossState.Idle);
        }
    }

    private IEnumerator HealOverTime(float amount, float duration)
    {
        if (bossHealth == null || duration <= 0) yield break;

        float timer = 0f;
        float healPerSecond = amount / duration;

        if (enableDebugLogs) Debug.Log($"체력 회복 시작: {amount}만큼 {duration}초에 걸쳐 회복합니다.");

        while (timer < duration)
        {
            // 사망 상태가 되면 회복을 중단합니다.
            if (currentState == BossState.Dead)
            {
                if (enableDebugLogs) Debug.Log("사망으로 인해 체력 회복 중단.");
                yield break;
            }
            bossHealth.Heal(healPerSecond * Time.deltaTime);
            timer += Time.deltaTime;
            yield return null;
        }
        if (enableDebugLogs) Debug.Log("체력 회복 완료.");
    }

    private IEnumerator RegularAttackRoutine(int attackTriggerID)
    {
        // 1. 공격 시작: 상태 변경 및 모든 움직임 즉시 정지
        SetState(BossState.Attacking);
        moveDirection = Vector2.zero;
        rb.linearVelocity = Vector2.zero;

        // 2. 애니메이션 실행
        lastAttackTriggerID = attackTriggerID;
        animator.SetTrigger(attackTriggerID);

        // 3. 애니메이션이 "Attack" 상태로 완전히 전환될 때까지 대기
        // 이를 통해 상태 전환 직후의 프레임에서 발생하는 문제를 방지합니다.
        yield return null; // 한 프레임 대기하여 애니메이터가 상태를 업데이트할 시간을 줍니다.
        yield return new WaitUntil(() => animator.GetCurrentAnimatorStateInfo(0).IsTag("Attack"));

        // 4. 애니메이션이 끝날 때까지 (즉, "Attack" 태그를 가진 상태가 아닐 때까지) 대기
        // 이 루프 동안에는 다른 로직이 끼어들 수 없으므로, 보스는 제자리에 고정됩니다.
        while (animator.GetCurrentAnimatorStateInfo(0).IsTag("Attack"))
        {
            // 만약을 위해 매 프레임 속도를 0으로 고정하여 다른 외부 요인에 의한 움직임을 완벽하게 차단합니다.
            rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
            yield return null;
        }

        // 5. 공격 종료 처리
        // 루프가 끝났다는 것은 애니메이션이 종료되었음을 의미합니다.
        // 스턴/사망 등으로 상태가 강제로 바뀌지 않았다면, 공격 종료 처리를 실행합니다.
        if (currentState == BossState.Attacking)
        {
            AnimationEvent_AttackFinished();
        }
    }

    private IEnumerator JumpAttackRoutine(int attackTriggerID, JumpAttackStats stats)
    {
        StartCoroutine(ForceFinishAttackRoutine(attackForceFinishTime));
        isDirectionLocked = true;

        // 1. 점프 준비
        FacePlayer();
        float horizontalDirection = Mathf.Sign(playerTransform.position.x - transform.position.x);
        
        yield return new WaitUntil(() => isGrounded);

        // 2. 상승 (지정된 시간 동안)
        rb.gravityScale = 0f;
        rb.linearVelocity = new Vector2(horizontalDirection * stats.horizontalSpeed, stats.upForce);
        animator.SetTrigger(attackTriggerID);

        yield return new WaitForSeconds(stats.ascentDuration);

        // 3. 정점 대기 (Apex Hold) - apexDelay가 0보다 클 때만 실행
        if (stats.apexDelay > 0)
        {
            rb.linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(stats.apexDelay);
        }

        // 4. 하강 (지정된 시간 동안)
        // 정점에서 플레이어 방향으로 다시 조준합니다.
        float descentDirection = Mathf.Sign(playerTransform.position.x - transform.position.x);
        rb.linearVelocity = new Vector2(descentDirection * stats.horizontalSpeed, -stats.downForce);

        yield return new WaitForSeconds(stats.descentDuration);

        // 5. 착지 처리
        rb.gravityScale = originalGravityScale;
        rb.linearVelocity = Vector2.zero;

        if (!isGrounded)
        {
            Debug.LogWarning("점프 공격: 지정된 시간 후에도 착지하지 못했습니다. 지면과의 거리를 확인하세요.");
        }
        // 공격 종료는 AnimationEvent_AttackFinished 또는 ForceFinishAttackRoutine이 처리
    }

    private IEnumerator ForceFinishAttackRoutine(float limitTime)
    {
        yield return new WaitForSeconds(limitTime);
        if (currentState == BossState.Attacking)
        {
            Debug.LogWarning($"공격 상태 강제 종료! 애니메이션 이벤트가 누락되었을 수 있습니다.");
            AnimationEvent_AttackFinished();
        }
    }

    private IEnumerator StunRoutine()
    {
        SetState(BossState.Stunned);
        animator.SetTrigger(StunAnimID);
        yield return new WaitForSeconds(1.5f);
        if (currentState == BossState.Stunned)
        {
            SetState(isPlayerInTrackingZone ? BossState.Dashing : BossState.Idle);
        }
    }
    
    private IEnumerator DelayedDestroyRoutine(float delay)
    {
        yield return new WaitForSeconds(delay);
        Destroy(gameObject);
    }

    // --- 외부 호출 및 이벤트 ---
    
    public void TakeDamage(int damage)
    {
        if (currentState == BossState.Dead) return;
        bossHealth.TakeDamage(damage);
    }

    /// <summary>
    /// 1페이즈 보스가 호출하여 2페이즈를 시작시킵니다.
    /// 체력을 10으로 설정하고, UI를 표시한 뒤, 서서히 체력을 회복합니다.
    /// </summary>
    public void ActivateAndStartHealthRecovery()
    {
        if (bossHealth != null)
        {
            bossHealth.SetInitialHealth(10);
        }
        if (bossHealthUI != null)
        {
            bossHealthUI.Show();
        }
        StartCoroutine(HealOverTime(healthToRecover, healthRecoverTime));
    }

    public void Die()
    {
        StopAllCoroutines();
        SetState(BossState.Dead);
        DisableAllAttackHitboxes();
        if (audioSource != null) audioSource.Stop();
        rb.bodyType = RigidbodyType2D.Kinematic;
        GetComponent<Collider2D>().enabled = false;
        animator.SetTrigger(DieAnimID);
        PlaySound(dieSound);
        StartCoroutine(DelayedDestroyRoutine(6.0f));
    }

    public void GetStunned()
    {
        if (currentState == BossState.Dead || currentState == BossState.Stunned) return;

        // [수정] StopAllCoroutines() 대신 개별 코루틴을 중지하여, HealOverTime 코루틴이 계속 실행되도록 합니다.
        StopCoroutine("RegularAttackRoutine");
        StopCoroutine("JumpAttackRoutine");
        StopCoroutine("BackdashRoutine");
        StopCoroutine("ForceFinishAttackRoutine");

        StartCoroutine(StunRoutine());
    }

    public void AnimationEvent_AttackFinished()
    {
        if (currentState == BossState.Dead) return;
        StopCoroutine("ForceFinishAttackRoutine");
        lastAttackTriggerID = -1;
        lastActionTime = Time.time; // [핵심 수정] 공격이 '끝난' 시점에 쿨타임 타이머를 시작합니다.
        SetState(isPlayerInTrackingZone ? BossState.Dashing : BossState.Idle);
    }

    public void PlaySoundEffect(string soundName)
    {
        if (soundDictionary.TryGetValue(soundName, out AudioClip clip))
        {
            PlaySound(clip);
        }
    }

    // --- Zone 트리거 감지 ---
    // BossZoneTrigger 스크립트에서 호출
    public void OnPlayerEnterZone(string zoneType)
    {
        if (enableDebugLogs) Debug.Log($"플레이어 진입: {zoneType}");
        switch (zoneType)
        {
            case "Tracking":
                isPlayerInTrackingZone = true;
                bossHealthUI?.Show();
                if (audioSource != null && bossBgm != null && !audioSource.isPlaying)
                {
                    audioSource.clip = bossBgm;
                    audioSource.loop = true;
                    audioSource.Play();
                }
                break;
            case "Backdash": isPlayerInBackdashZone = true; break;
            case "JumpAttack": isPlayerInJumpAttackZone = true; break;
            case "Attack": isPlayerInAttackZone = true; break;
        }
    }

    public void OnPlayerExitZone(string zoneType)
    {
        if (enableDebugLogs) Debug.Log($"플레이어 이탈: {zoneType}");
        switch (zoneType)
        {
            case "Tracking":
                isPlayerInTrackingZone = false;
                isPlayerInBackdashZone = false;
                isPlayerInJumpAttackZone = false;
                isPlayerInAttackZone = false;
                SetState(BossState.Idle);
                if (audioSource != null && audioSource.clip == bossBgm) audioSource.Stop();
                break;
            case "Backdash": isPlayerInBackdashZone = false; break;
            case "JumpAttack": isPlayerInJumpAttackZone = false; break;
            case "Attack": isPlayerInAttackZone = false; break;
        }
    }

    // --- 유틸리티 ---
    private void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }

    private void DisableAllAttackHitboxes()
    {
        S_BossHitbox[] hitboxes = GetComponentsInChildren<S_BossHitbox>(true);
        foreach (var hitbox in hitboxes)
        {
            hitbox.gameObject.SetActive(false);
        }
    }
}