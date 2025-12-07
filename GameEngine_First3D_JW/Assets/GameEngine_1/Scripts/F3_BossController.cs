using System.Collections;
using UnityEngine;

public class F3_BossController : MonoBehaviour
{
    // 보스의 행동 상태를 정의합니다.
    private enum BossState
    {
        Idle,       // 행동 전 대기 상태
        Deciding,   // 다음 행동을 결정하는 상태
        Moving,     // 좌우로 움직이는 중
        Attacking,  // 일반 공격 중
        StrongAttacking, // 강한 공격 중
        Piercing,   // 찌르기 공격 중 (뒤로 물러나고 돌진하는 움직임 포함)
        Stunned     // 패링 당해서 기절한 상태
    }

    [Header("참조")]
    public Transform playerTransform; // 플레이어의 Transform (Inspector에서 할당)

    [Header("능력치")]
    [SerializeField] private int health = 50;  // 보스 체력
    public float moveSpeed = 2f;         // 이동 속도
    public float chargeSpeed = 8f;       // 돌진 속도
    public float actionCooldown = 1.5f;    // 행동과 행동 사이의 대기 시간
    [SerializeField] private float stoppingDistance = 2.0f; // 이 거리 안으로 들어오면 추적을 멈춥니다.
    public GameObject normalAttackHitbox; // 일반 공격 판정 (Inspector에서 할당)
    public GameObject pierceAttackHitbox; // 전방 찌르기 공격 판정 (Inspector에서 할당)

    [Header("찌르기 공격 (Attack 2) 설정")]
    public float pierceBackwardDistance = 1.0f; // 찌르기 전 뒤로 물러나는 거리
    public float pierceBackwardSpeed = 3.0f;    // 찌르기 전 뒤로 물러나는 속도
    public float pierceForwardDistance = 3.0f;  // 찌르기 시 앞으로 돌진하는 거리 (뒤로 물러난 지점부터)
    public float pierceForwardSpeed = 10.0f;    // 찌르기 시 앞으로 돌진하는 속도
    public float pierceBackwardPause = 0.2f;    // 뒤로 물러난 후 잠시 멈추는 시간
    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private BossState currentState = BossState.Idle;

    private bool isPlayerInTrackingRange = false; // 플레이어 감지 여부
    private bool isPlayerInAttackRange = false;   // 플레이어 공격 범위 감지 여부
    private float lastActionTime = 0f;            // 마지막 행동 시간
    private Color originalColor;                  // 원래 스프라이트 색상
    private Vector3 pierceHitboxInitialLocalPos;  // 찌르기 히트박스의 초기 로컬 위치
    private CapsuleCollider2D capsuleCollider;    // 보스의 메인 콜라이더
    private Vector2 moveDirection = Vector2.zero; // FixedUpdate에서 사용할 이동 방향
    private Animator animator;
    
    // 방향 전환 딜레이 관련 변수
    private float timeToSwitchDirection = 0.2f; // 이 시간 동안 플레이어가 반대편에 있어야 방향 전환
    private float directionSwitchTimer = 0f;

    // 애니메이터 파라미터 ID (성능 최적화)
    private readonly int AttackAnimID = Animator.StringToHash("AttackAnimID");
    private readonly int IsWalkingAnimID = Animator.StringToHash("isWalking");
    private readonly int StunAnimID = Animator.StringToHash("Stun");
    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        originalColor = spriteRenderer.color;
        animator = GetComponent<Animator>();

        capsuleCollider = GetComponent<CapsuleCollider2D>();
        if (playerTransform == null)
        {
            Debug.LogError("플레이어 Transform이 BossController에 할당되지 않았습니다!");
        }

        // 모든 공격 판정을 시작할 때 비활성화합니다.
        if (normalAttackHitbox != null) normalAttackHitbox.SetActive(false);
        // 찌르기 공격 판정을 시작할 때 비활성화합니다.
        if (pierceAttackHitbox != null)
        {
            pierceAttackHitbox.SetActive(false);
            pierceHitboxInitialLocalPos = pierceAttackHitbox.transform.localPosition;
        }
    }

    void Update()
    {
        // 스턴 상태일 때는 아무것도 하지 않습니다.
        if (currentState == BossState.Stunned) return;

        // FSM(상태 머신)을 매 프레임 실행합니다.
        switch (currentState)
        {
            case BossState.Idle:
                // 플레이어가 감지 범위에 들어오면 행동을 시작합니다.
                if (isPlayerInTrackingRange)
                {
                    SetState(BossState.Deciding);
                }
                break;

            case BossState.Deciding:
                // 플레이어가 범위를 벗어나면 다시 대기 상태로 돌아갑니다.
                if (!isPlayerInTrackingRange)
                {
                    moveDirection = Vector2.zero; // 이동 중지
                    animator.SetBool(IsWalkingAnimID, false);
                    SetState(BossState.Idle);
                    break;
                }

                // 플레이어를 향해 계속 이동합니다.
                if (playerTransform != null)
                {
                    float distanceToPlayerX = playerTransform.position.x - transform.position.x;

                    // 플레이어와의 거리가 stoppingDistance보다 멀 때만 이동합니다.
                    if (Mathf.Abs(distanceToPlayerX) > stoppingDistance)
                    {
                        moveDirection.x = Mathf.Sign(distanceToPlayerX);
                        animator.SetBool(IsWalkingAnimID, true);
                    }
                    else
                    {
                        moveDirection.x = 0; // 거리가 가까우면 이동을 멈춥니다.
                        animator.SetBool(IsWalkingAnimID, false);
                    }
                }

                // 플레이어가 공격 범위 안에 있고, 쿨타임이 지났다면 공격을 시도합니다.
                // 플레이어가 공격 범위 안에 있을 때만 공격을 시도합니다.
                if (isPlayerInAttackRange && Time.time >= lastActionTime + actionCooldown)
                {
                    DecideNextAction();
                }
                break;
        }
        
        // 공격 중일 때는 이동 방향을 강제로 0으로 설정합니다.
        if (currentState == BossState.Attacking || currentState == BossState.Piercing) // 찌르기 공격 중에도 이동 중지
        {
            moveDirection = Vector2.zero;
        }

        // 플레이어를 바라보도록 방향 전환 (Idle 상태가 아닐 때만)
        if (currentState != BossState.Idle && playerTransform != null)
        {
            // 공격 중이 아닐 때(Moving, Deciding)만 플레이어를 바라보도록 하여, 공격 모션 중 방향이 바뀌는 어색함을 없앱니다.
            if (currentState != BossState.Attacking && currentState != BossState.StrongAttacking && currentState != BossState.Piercing)
            {
                FacePlayer();
            }
        }
    }

    // 물리 계산은 FixedUpdate에서 처리합니다.
    void FixedUpdate()
    {
        // 스턴 상태가 아닐 때만 이동합니다.
        if (currentState != BossState.Stunned)
            rb.linearVelocity = new Vector2(moveDirection.x * moveSpeed, rb.linearVelocity.y);
    }

    // 상태를 변경하고 로그를 출력하는 함수
    void SetState(BossState newState)
    {
        // 이미 같은 상태라면 변경하지 않습니다.
        if (currentState == newState) return;

        currentState = newState;
        Debug.Log($"보스 상태 변경: {newState}");
    }

    // 플레이어를 바라보는 함수
    void FacePlayer()
    {
        float directionToPlayer = playerTransform.position.x - transform.position.x;
        bool shouldFaceRight = directionToPlayer > 0; // 플레이어가 오른쪽에 있는가?
        bool isFacingRight = !spriteRenderer.flipX;   // 현재 오른쪽을 보고 있는가?

        // 현재 바라보는 방향과 플레이어의 방향이 다를 경우
        if (isFacingRight != shouldFaceRight)
        {
            directionSwitchTimer += Time.deltaTime;

            // 타이머가 설정된 시간을 초과하면 방향을 전환합니다.
            if (directionSwitchTimer >= timeToSwitchDirection)
            {
                spriteRenderer.flipX = !spriteRenderer.flipX; // 방향 전환
                UpdatePierceHitboxPosition();
                directionSwitchTimer = 0f; // 타이머 리셋
            }
        }
        else
        {
            // 플레이어가 현재 바라보는 방향에 있다면 타이머를 리셋합니다.
            directionSwitchTimer = 0f;
        }
    }

    // 찌르기 히트박스 위치를 현재 바라보는 방향에 맞게 업데이트하는 함수
    void UpdatePierceHitboxPosition()
    {
        if (pierceAttackHitbox == null) return;

        bool isFacingRight = !spriteRenderer.flipX;
        float newX = isFacingRight ? Mathf.Abs(pierceHitboxInitialLocalPos.x) : -Mathf.Abs(pierceHitboxInitialLocalPos.x);

        pierceAttackHitbox.transform.localPosition = new Vector3(
            newX,
            pierceHitboxInitialLocalPos.y,
            pierceHitboxInitialLocalPos.z);
    }
    // 다음 행동을 무작위로 결정하는 함수
    void DecideNextAction()
    {
        // 상태를 먼저 Attacking으로 변경하여 중복 호출을 방지합니다.
        SetState(BossState.Attacking);
        
        moveDirection = Vector2.zero; // 공격 전 잠시 멈춤
        animator.SetBool(IsWalkingAnimID, false); // 공격 시작 전 걷기 애니메이션 중지

        // 1: 일반공격, 2: 찌르기공격, 3: 강한공격
        int actionChoice = Random.Range(1, 4); 
        animator.SetInteger(AttackAnimID, actionChoice);

        if (actionChoice == 2) // 찌르기 공격일 경우, 움직임 코루틴 시작
        {
            StartCoroutine(PierceAttackMovementRoutine());
        }

    }

    // --- 행동 패턴 구현 (코루틴) ---
    private IEnumerator AttackRoutine()
    {
        // 이 코루틴은 이제 애니메이션 이벤트로 대체되므로 사용하지 않습니다.
        // 필요 시 유지할 수 있으나, 현재 로직에서는 제거해도 무방합니다.
        yield return null;
    }

    private IEnumerator StrongAttackRoutine()
    {
        // 이 코루틴은 이제 애니메이션 이벤트로 대체되므로 사용하지 않습니다.
        yield return null;
    }

    private IEnumerator PierceRoutine()
    {
        // 이 코루틴은 이제 애니메이션 이벤트로 대체되므로 사용하지 않습니다.
        yield return null;
    }

    // --- 패링 및 스턴 로직 ---

    // 데미지를 받는 함수 (외부에서 호출 가능)
    public void TakeDamage(int damage)
    {
        // 여기서는 패링 성공 시에만 데미지를 입히므로 별도 조건 없이 체력을 감소시킵니다.
        health -= damage;
        Debug.Log($"<color=red>보스 피격! 남은 체력: {health}</color>");

        if (health <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        Debug.Log("보스가 쓰러졌습니다!");
        Destroy(gameObject); // 보스 오브젝트 파괴
    }

    // 외부(F_PlayerController)에서 호출될 스턴 함수
    public void GetStunned()
    {
        // 이미 스턴 상태라면 아무것도 하지 않습니다.
        if (currentState == BossState.Stunned) return;

        StopAllCoroutines(); // 진행 중인 모든 공격 행동을 즉시 중단합니다.

        // 애니메이터의 공격 의도를 즉시 초기화하여 스턴 후 동일한 공격이 반복되는 것을 방지합니다.
        animator.SetInteger(AttackAnimID, 0);

        StartCoroutine(StunRoutine());
    }

    private IEnumerator StunRoutine()
    {
        SetState(BossState.Stunned);
        animator.SetTrigger(StunAnimID); // Stun 애니메이션 트리거 발동 (이 부분은 그대로 둡니다)
        rb.linearVelocity = Vector2.zero; // 물리적 움직임을 멈춥니다.
        Debug.Log("보스: 크윽... (스턴 상태)");

        // 기절 애니메이션이 끝날 때까지 기다립니다.
        // 이 시간은 애니메이션 클립의 실제 길이와 일치시키는 것이 가장 좋습니다.
        yield return new WaitForSeconds(1.5f); // 기절 애니메이션 길이에 맞춰 조정

        AnimationEvent_AttackFinished();
    }

    // --- 애니메이션 이벤트 호출 함수들 ---

    // 일반 공격 (Attack 1)
    public void AnimationEvent_PerformNormalAttack()
    {
        Debug.Log("애니메이션 이벤트: 일반 공격 실행");
        if (normalAttackHitbox != null)
        {
            normalAttackHitbox.SetActive(true);
        }
    }

    // 강한 공격 (Attack 3) - 돌진 시작
    public void AnimationEvent_StartCharge()
    {
        Debug.Log("애니메이션 이벤트: 돌진 시작");
        StartCoroutine(ChargeRoutine());
    }

    // 찌르기 공격 (Attack 2) - 히트박스 제어
    public void AnimationEvent_SetPierceHitbox(int active)
    {
        Debug.Log($"애니메이션 이벤트: 찌르기 히트박스 {(active == 1 ? "활성화" : "비활성화")}");
        if (pierceAttackHitbox != null)
        {
            pierceAttackHitbox.SetActive(active == 1);
        }
    }

    // 모든 공격 애니메이션의 끝에서 호출될 함수
    public void AnimationEvent_AttackFinished()
    {
        Debug.Log("애니메이션 이벤트: 공격 종료, 결정 상태로 복귀");
        AnimationEvent_DeactivateAllHitboxes(); // 모든 히트박스 비활성화
        animator.SetInteger(AttackAnimID, 0); // 파라미터를 0으로 리셋하여 Any State로 돌아갈 준비
        animator.SetBool(IsWalkingAnimID, false); // Idle 상태로 돌아가도록 걷기 애니메이션을 중지합니다.

        // 공격이 끝났으므로 다음 행동을 결정하는 상태로 전환합니다.
        // 단, 스턴 상태가 아닐 때만 Deciding으로 돌아갑니다. (스턴 중단 시 예외 처리)
        // 스턴 상태에서 이 함수가 호출될 수 있으므로, 현재 상태를 확인합니다.
        // 스턴 상태였거나, 다른 공격 상태였거나 모두 이 함수를 통해 Deciding 상태로 돌아갑니다.
        animator.SetInteger(AttackAnimID, 0); // 파라미터를 0으로 리셋하여 기본 상태로 돌아갈 준비
        lastActionTime = Time.time; // 행동이 끝난 시점부터 쿨타임 계산 시작
        SetState(BossState.Deciding);
    }

    // 모든 활성화된 히트박스를 끄는 함수
    public void AnimationEvent_DeactivateAllHitboxes()
    {
        if (normalAttackHitbox != null && normalAttackHitbox.activeSelf)
            normalAttackHitbox.SetActive(false);
        if (pierceAttackHitbox != null && pierceAttackHitbox.activeSelf)
            pierceAttackHitbox.SetActive(false);
    }

    // 강한 공격(돌진) 로직을 위한 코루틴
    private IEnumerator ChargeRoutine()
    {
        float directionToPlayer = (playerTransform.position.x > transform.position.x) ? 1f : -1f;
        rb.linearVelocity = new Vector2(directionToPlayer * chargeSpeed, 0);

        float chargeDuration = 0.8f;
        float chargeTimer = 0f;
        bool hasHit = false;

        while (chargeTimer < chargeDuration)
        {
            if (!hasHit)
            {
                ContactFilter2D filter = new ContactFilter2D().NoFilter();
                filter.SetLayerMask(LayerMask.GetMask("Player"));
                Collider2D[] hitPlayers = new Collider2D[1];
                if (capsuleCollider.Overlap(filter, hitPlayers) > 0)
                {
                    Debug.Log("돌진 공격이 플레이어에게 적중!");
                    hitPlayers[0].GetComponent<F_PlayerController>()?.HandleAttack(2, gameObject);
                    hasHit = true;
                }
            }
            chargeTimer += Time.deltaTime;
            yield return null;
        }
        rb.linearVelocity = Vector2.zero;
    }

    // 찌르기 공격의 움직임을 처리하는 코루틴
    private IEnumerator PierceAttackMovementRoutine()
    {
        SetState(BossState.Piercing); // 찌르기 상태로 전환
        rb.linearVelocity = Vector2.zero; // 움직임 시작 전 정지

        // 현재 보스가 바라보는 방향을 기준으로 움직임 방향 결정
        float direction = spriteRenderer.flipX ? -1f : 1f; // -1: 왼쪽, 1: 오른쪽

        // --- 1. 뒤로 물러나기 ---
        float backwardMoveDuration = pierceBackwardDistance / pierceBackwardSpeed;
        float timer = 0f;
        Vector2 initialPosition = transform.position;
        Vector2 targetBackwardPosition = initialPosition + new Vector2(-direction * pierceBackwardDistance, 0);

        while (timer < backwardMoveDuration)
        {
            // 뒤로 물러나는 속도로 이동
            rb.linearVelocity = new Vector2(-direction * pierceBackwardSpeed, rb.linearVelocity.y);
            timer += Time.deltaTime;
            yield return null;
        }
        rb.linearVelocity = Vector2.zero; // 정지
        transform.position = targetBackwardPosition; // 정확한 위치 보정

        // --- 2. 잠시 멈춤 ---
        yield return new WaitForSeconds(pierceBackwardPause);

        // --- 3. 앞으로 돌진하기 ---
        float forwardChargeDuration = pierceForwardDistance / pierceForwardSpeed;
        timer = 0f;
        Vector2 targetForwardPosition = targetBackwardPosition + new Vector2(direction * pierceForwardDistance, 0);

        while (timer < forwardChargeDuration)
        {
            // 앞으로 돌진하는 속도로 이동
            rb.linearVelocity = new Vector2(direction * pierceForwardSpeed, rb.linearVelocity.y);
            timer += Time.deltaTime;
            yield return null;
        }
        rb.linearVelocity = Vector2.zero; // 정지
        transform.position = targetForwardPosition; // 정확한 위치 보정

        // 애니메이션이 끝날 때 AnimationEvent_AttackFinished()가 호출되어 상태를 Deciding으로 되돌리고 히트박스를 비활성화합니다.
        // 따라서 이 코루틴에서는 별도의 상태 변경이나 히트박스 제어를 하지 않습니다.
    }

    // --- 플레이어 감지 로직 ---

    // 외부(자식 Trigger)에서 호출될 함수들
    public void OnPlayerEnterTrackingRange()
    {
        isPlayerInTrackingRange = true;
        Debug.Log("플레이어 추적 시작!");
    }

    public void OnPlayerExitTrackingRange()
    {
        isPlayerInTrackingRange = false;
        // 공격 범위에서도 벗어난 것으로 간주합니다.
        isPlayerInAttackRange = false;
        Debug.Log("플레이어가 추적 범위를 벗어남!");
    }

    public void OnPlayerEnterAttackRange()
    {
        isPlayerInAttackRange = true;
        Debug.Log("<color=yellow>플레이어 공격 범위 진입!</color>");
    }

    public void OnPlayerExitAttackRange()
    {
        isPlayerInAttackRange = false;
        Debug.Log("플레이어가 공격 범위를 벗어남.");
    }
}