using System.Collections;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class T_BossController : MonoBehaviour
{
    // 새로운 보스의 상태. 앞으로 패턴을 추가하면서 확장될 것입니다.
    private enum BossState
    {
        Idle,       // 대기
        Deciding,   // 행동 결정 (플레이어 추적)
        Teleporting, // 텔레포트 중
        Attacking,  // 공격 중
        Stunned,    // 기절
        Dead        // 사망
    }

    [Header("참조")]
    public Transform playerTransform;
    public BossHealthUI bossHealthUI;

    [Header("설정")]
    public bool spriteFacesLeft = true;

    [Header("능력치")]
    private BossHealth bossHealth;
    public float moveSpeed = 2f;
    public float actionCooldown = 1.5f;
    [SerializeField] private float stoppingDistance = 3.0f; // 텔레포트가 주력이므로, 걷기로 멈추는 거리는 조금 더 길게 설정

    [Header("텔레포트")]
    public float teleportCooldown = 3.0f;
    public float teleportNearDistance = 4.0f; // 플레이어 근처로 텔레포트할 때의 거리
    public float teleportAwayDistance = 6.0f; // 플레이어에게서 멀어질 때의 거리
    public float teleportAnimOutDuration = 0.3f; // 텔레포트 사라지는 애니메이션 시간
    public float teleportAnimInDuration = 0.3f;  // 텔레포트 나타나는 애니메이션 시간
    public float postTeleportDelay = 0.3f;       // 텔레포트 후 이동 제한 시간

    [Header("사운드")]
    // TODO: 새로운 보스의 사운드 클립들을 여기에 추가합니다.
    public AudioClip attackSound1;
    public AudioClip dieSound;

    // 내부 변수
    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private AudioSource audioSource;
    private Animator animator;
    private CapsuleCollider2D capsuleCollider;

    private BossState currentState = BossState.Idle;
    private bool isPlayerInTrackingRange = false;
    private bool isPlayerInMeleeRange = false; // 근접 공격 범위
    private bool isPlayerTooClose = false;     // 너무 가까운 범위 (후방 텔레포트용)
    private float lastActionTime = 0f;
    private float lastTeleportTime = -99f;
    private Vector2 moveDirection = Vector2.zero;

    // 애니메이터 파라미터 ID (미리 해시값으로 변환하여 성능 최적화)
    private readonly int IsWalkingAnimID = Animator.StringToHash("isWalking");
    private readonly int StunAnimID = Animator.StringToHash("Stun");
    private readonly int DieAnimID = Animator.StringToHash("Die");
    private readonly int Attack1TriggerID = Animator.StringToHash("Attack1");
    private readonly int TeleportOutTriggerID = Animator.StringToHash("TP");
    private readonly int TeleportInTriggerID = Animator.StringToHash("TP_END");


    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        audioSource = GetComponent<AudioSource>();
        animator = GetComponent<Animator>();
        bossHealth = GetComponent<BossHealth>();
        capsuleCollider = GetComponent<CapsuleCollider2D>();

        if (playerTransform == null) Debug.LogError("플레이어 Transform 미할당!");
        if (bossHealthUI == null) Debug.LogError("BossHealthUI가 T_BossController에 할당되지 않았습니다!");
    }

    void Update()
    {
        if (currentState == BossState.Dead || currentState == BossState.Stunned || currentState == BossState.Attacking || currentState == BossState.Teleporting) return;

        switch (currentState)
        {
            case BossState.Idle:
                if (isPlayerInTrackingRange) SetState(BossState.Deciding);
                break;

            case BossState.Deciding:
                if (!isPlayerInTrackingRange)
                {
                    SetState(BossState.Idle);
                    break;
                }

                // [핵심 수정] 어떤 행동을 결정하기 전에, 먼저 현재 위치에 따라 걸을지 멈출지를 처리합니다.
                // 이렇게 하면 공격 직전 프레임에 걷는 것을 멈추게 됩니다.
                HandleMovementAndFacing();

                bool canAttack = Time.time >= lastActionTime + actionCooldown;
                bool canTeleport = Time.time >= lastTeleportTime + teleportCooldown;

                // 최우선 순위: 공격 범위 안에 있고 공격 쿨타임이 지났으면 공격
                if (isPlayerInMeleeRange && canAttack)
                {
                    DecideNextAction();
                    return;
                }

                // 2순위: 너무 가까우면 텔레포트로 회피
                if (isPlayerTooClose && canTeleport)
                {
                    StartCoroutine(TeleportRoutine(true)); // away = true
                    return;
                }

                // 3순위: 공격 범위 밖이고, 텔레포트 쿨타임이 지났으면 플레이어에게 접근
                if (!isPlayerInMeleeRange && canTeleport)
                {
                    StartCoroutine(TeleportRoutine(false)); // away = false
                    return;
                }

                break;
        }
    }

    void FixedUpdate()
    {
        // Deciding 상태일 때만 물리적인 이동을 처리합니다.
        if (currentState == BossState.Deciding)
        {
            rb.linearVelocity = new Vector2(moveDirection.x * moveSpeed, rb.linearVelocity.y);
        }
        // 그 외의 상태(Attacking, Teleporting 등)에서는 물리적인 이동을 확실히 멈춥니다.
        else
        {
            rb.linearVelocity = Vector2.zero;
        }

        // [진단용] 문제가 지속될 경우, 이 코드의 주석을 해제하여 현재 상태와 속도를 매 프레임 추적할 수 있습니다.
         //Debug.Log($"상태: {currentState}, 속도: {rb.linearVelocity}");
    }

    void SetState(BossState newState)
    {
        if (currentState == newState) return;

        // 상태 변경 직전, 이전 상태의 움직임 초기화
        rb.linearVelocity = Vector2.zero;
        moveDirection = Vector2.zero;
        animator.SetBool(IsWalkingAnimID, false);

        currentState = newState;

        // 새로운 상태 진입 시 초기화 로직
        switch (currentState)
        {
            case BossState.Idle:
                break;

            case BossState.Deciding:
                break;

            case BossState.Teleporting:
                // 텔레포트 중에는 아무것도 하지 않음 (코루틴이 제어)
                break;

            case BossState.Attacking:
                // 공격 시작 시 플레이어 방향 고정
                // 만약을 위해 상태 진입 시 한 번 더 속도를 0으로 설정합니다.
                rb.linearVelocity = Vector2.zero;
                FacePlayer();
                break;
        }
    }

    /// <summary>
    /// 플레이어 추적 및 방향 전환을 처리합니다.
    /// </summary>
    void HandleMovementAndFacing()
    {
        if (playerTransform == null) return;

        float distanceToPlayerX = playerTransform.position.x - transform.position.x;

        // 플레이어와의 거리에 따라 이동 방향 결정
        if (Mathf.Abs(distanceToPlayerX) > stoppingDistance)
        {
            moveDirection.x = Mathf.Sign(distanceToPlayerX);
            animator.SetBool(IsWalkingAnimID, true);
        }
        else
        {
            moveDirection.x = 0;
            animator.SetBool(IsWalkingAnimID, false);
        }

        // [수정] 걷거나 멈췄을 때 항상 플레이어를 바라보도록 FacePlayer()를 다시 밖으로 이동시켰습니다.
        FacePlayer();
    }

    /// <summary>
    /// 플레이어를 바라보도록 방향을 전환합니다.
    /// </summary>
    void FacePlayer()
    {
        if (playerTransform == null) return;
        bool isPlayerRight = (playerTransform.position.x - transform.position.x) > 0;
        ApplyScale(isPlayerRight);
    }

    void ApplyScale(bool lookRight)
    {
        float targetScaleX = 1f;
        if (spriteFacesLeft) targetScaleX = lookRight ? -1f : 1f;
        else targetScaleX = lookRight ? 1f : -1f;
        transform.localScale = new Vector3(targetScaleX, transform.localScale.y, transform.localScale.z);
    }

    /// <summary>
    /// 다음 공격 패턴을 결정합니다.
    /// </summary>
    void DecideNextAction()
    {
        // 공격 결정 즉시, 현재 프레임에서 계산되었을 수 있는 이동 값을 확실히 제거합니다.
        moveDirection = Vector2.zero;
        animator.SetBool(IsWalkingAnimID, false);

        SetState(BossState.Attacking);

        // 현재는 근접 공격(Attack1)만 사용. 향후 다른 공격 범위에 따라 분기 가능
        animator.SetTrigger(Attack1TriggerID);
        Debug.Log("새로운 공격 패턴 시작!");

        // 공격 애니메이션이 멈추는 경우를 대비한 안전장치
        StartCoroutine(ForceFinishAttackRoutine(4.0f));
    }

    // --- 외부 호출 함수 (데미지, 사망, 스턴 등) ---

    public void TakeDamage(int damage)
    {
        if (currentState == BossState.Dead) return;
        bossHealth.TakeDamage(damage);
        Debug.Log($"<color=red>보스 피격! 데미지: {damage}</color>");
    }

    public void Die()
    {
        StopAllCoroutines();
        SetState(BossState.Dead);
        rb.linearVelocity = Vector2.zero;
        rb.bodyType = RigidbodyType2D.Kinematic;
        if (capsuleCollider != null) capsuleCollider.enabled = false;
        animator.SetTrigger(DieAnimID);
        PlaySound(dieSound);
        Destroy(gameObject, 2.0f); // 2초 후 오브젝트 파괴
    }

    public void GetStunned()
    {
        if (currentState == BossState.Dead) return;
        StopAllCoroutines();
        StartCoroutine(StunRoutine());
    }

    // --- 애니메이션 이벤트 함수 ---

    // TODO: 공격 판정 활성화/비활성화, 사운드 재생 등 애니메이션 이벤트를 여기에 추가합니다.
    // public void AnimationEvent_ActivateHitbox() { ... }
    // public void AnimationEvent_PlayAttackSound() { ... }

    /// <summary>
    /// 모든 공격 애니메이션의 마지막에 호출되어 보스를 다시 행동 결정 상태로 만듭니다.
    /// </summary>
    public void AnimationEvent_AttackFinished()
    {
        if (currentState == BossState.Dead) return;
        lastActionTime = Time.time;
        SetState(BossState.Deciding);
    }

    // --- 코루틴 ---

    private IEnumerator TeleportRoutine(bool away)
    {
        SetState(BossState.Teleporting);

        // 1. 텔레포트 시작 (사라지기)
        animator.SetTrigger(TeleportOutTriggerID);
        yield return new WaitForSeconds(teleportAnimOutDuration);

        // 2. 위치 계산 및 이동
        Vector2 targetPosition;
        if (away)
        {
            // 플레이어로부터 멀어지는 위치 계산 (Y축 고정)
            float horizontalDirectionFromPlayer = Mathf.Sign(transform.position.x - playerTransform.position.x);
            // 플레이어와 X축이 정확히 같을 경우, 랜덤한 방향으로 멀어집니다.
            if (horizontalDirectionFromPlayer == 0)
            {
                horizontalDirectionFromPlayer = (Random.value > 0.5f) ? 1f : -1f;
            }
            float targetX = playerTransform.position.x + (horizontalDirectionFromPlayer * teleportAwayDistance);
            targetPosition = new Vector2(targetX, transform.position.y);
        }
        else
        {
            // 플레이어의 좌/우 랜덤한 위치 계산 (Y축 고정)
            float randomDirection = (Random.value > 0.5f) ? 1f : -1f;
            float targetX = playerTransform.position.x + (randomDirection * teleportNearDistance);
            targetPosition = new Vector2(targetX, transform.position.y);
        }
        transform.position = targetPosition;

        // 3. 텔레포트 종료 (나타나기)
        FacePlayer(); // 나타난 위치에서 즉시 플레이어를 바라봄
        animator.SetTrigger(TeleportInTriggerID);
        yield return new WaitForSeconds(teleportAnimInDuration);

        // 4. 텔레포트 후 딜레이 (이동 제한)
        yield return new WaitForSeconds(postTeleportDelay);

        lastTeleportTime = Time.time;
        SetState(BossState.Deciding);
    }

    private IEnumerator StunRoutine()
    {
        SetState(BossState.Stunned);
        animator.SetTrigger(StunAnimID);
        rb.linearVelocity = Vector2.zero;
        yield return new WaitForSeconds(1.5f);
        if (currentState == BossState.Stunned)
        {
            AnimationEvent_AttackFinished();
        }
    }

    private IEnumerator ForceFinishAttackRoutine(float limitTime)
    {
        yield return new WaitForSeconds(limitTime);
        if (currentState == BossState.Attacking)
        {
            Debug.LogWarning("공격 상태 강제 종료!");
            AnimationEvent_AttackFinished();
        }
    }

    // --- 트리거/충돌 감지 함수 ---

    public void OnPlayerEnterTrackingRange()
    {
        isPlayerInTrackingRange = true;
        if (bossHealthUI != null) bossHealthUI.Show();
    }
    public void OnPlayerExitTrackingRange() { isPlayerInTrackingRange = false; isPlayerInMeleeRange = false; isPlayerTooClose = false; }
    public void OnPlayerEnterMeleeRange() { isPlayerInMeleeRange = true; }
    public void OnPlayerExitMeleeRange() { isPlayerInMeleeRange = false; }
    public void OnPlayerEnterTooCloseRange() { isPlayerTooClose = true; }
    public void OnPlayerExitTooCloseRange() { isPlayerTooClose = false; }

    // --- 유틸리티 함수 ---

    private void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }
}