using System.Collections;
using UnityEngine;

public class F_BossController : MonoBehaviour
{
    // 보스의 행동 상태를 정의합니다.
    private enum BossState
    {
        Idle,       // 행동 전 대기 상태
        Deciding,   // 다음 행동을 결정하는 상태
        Moving,     // 좌우로 움직이는 중
        Attacking,  // 일반 공격 중
        StrongAttacking, // 강한 공격 중
        Piercing,    // 찌르기 공격 중
        Stunned     // 패링 당해서 기절한 상태
    }

    [Header("참조")]
    public Transform playerTransform; // 플레이어의 Transform (Inspector에서 할당)

    [Header("능력치")]
    [SerializeField] private int health = 50;  // 보스 체력
    public float moveSpeed = 2f;         // 이동 속도
    public float chargeSpeed = 8f;       // 돌진 속도
    public float actionCooldown = 1.5f;    // 행동과 행동 사이의 대기 시간
    public GameObject normalAttackHitbox; // 일반 공격 판정 (Inspector에서 할당)
    public GameObject pierceAttackHitbox; // 전방 찌르기 공격 판정 (Inspector에서 할당)
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

    // 애니메이터 파라미터 ID (성능 최적화)
    private readonly int AttackAnimID = Animator.StringToHash("AttackAnimID");
    private readonly int IsWalkingAnimID = Animator.StringToHash("isWalking");
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
                    // 이동 방향을 계산하고 걷기 애니메이션을 재생합니다.
                    float directionToPlayer = (playerTransform.position.x > transform.position.x) ? 1f : -1f;
                    moveDirection.x = directionToPlayer;
                    animator.SetBool(IsWalkingAnimID, true);
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
        if (currentState == BossState.Attacking)
        {
            moveDirection = Vector2.zero;
        }

        // 플레이어를 바라보도록 방향 전환 (Idle 상태가 아닐 때만)
        if (currentState != BossState.Idle && playerTransform != null)
        {
            FacePlayer();
        }
    }

    // 물리 계산은 FixedUpdate에서 처리합니다.
    void FixedUpdate()
    {
        // 스턴 상태가 아닐 때, 계산된 이동 방향(moveDirection)에 따라 물리적 속도를 적용합니다.
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
        // 떨림 현상을 방지하기 위한 데드존(Dead Zone) 설정
        float deadZone = 0.2f;

        // 플레이어가 보스의 왼쪽에 있을 때 (원본 스프라이트가 왼쪽을 본다고 가정)
        if (playerTransform.position.x < transform.position.x - deadZone)
        {
            spriteRenderer.flipX = true; // 왼쪽을 보도록 스프라이트 뒤집기
            if (pierceAttackHitbox != null)
            {
                // 히트박스를 왼쪽으로 (초기 x 위치의 절댓값에 -를 붙여 항상 왼쪽을 보장)
                pierceAttackHitbox.transform.localPosition = new Vector3(
                    -Mathf.Abs(pierceHitboxInitialLocalPos.x),
                    pierceHitboxInitialLocalPos.y,
                    pierceHitboxInitialLocalPos.z);
            }
        }
        else if (playerTransform.position.x > transform.position.x + deadZone) // 플레이어가 보스의 오른쪽에 있을 때
        {
            spriteRenderer.flipX = false; // 오른쪽을 보도록 스프라이트 원상복구
            if (pierceAttackHitbox != null)
            {
                // 히트박스를 오른쪽으로 (초기 x 위치의 절댓값을 사용해 항상 오른쪽을 보장)
                pierceAttackHitbox.transform.localPosition = new Vector3(
                    Mathf.Abs(pierceHitboxInitialLocalPos.x),
                    pierceHitboxInitialLocalPos.y,
                    pierceHitboxInitialLocalPos.z);
            }
        }
        // 플레이어가 deadZone 안에 있을 경우, 방향을 바꾸지 않고 현재 상태를 유지합니다.
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
        // 이미 스턴 상태이거나, 패링이 불가능한 찌르기 공격 중에는 스턴에 걸리지 않습니다.
        if (currentState == BossState.Stunned || currentState == BossState.Piercing) return;

        StopAllCoroutines(); // 진행 중인 모든 공격 행동을 즉시 중단합니다.
        StartCoroutine(StunRoutine());
    }

    private IEnumerator StunRoutine()
    {
        SetState(BossState.Stunned);
        rb.linearVelocity = Vector2.zero; // 물리적 움직임을 멈춥니다.
        Debug.Log("보스: 크윽... (스턴 상태)");
        spriteRenderer.color = Color.cyan; // 스턴 상태를 시안 색으로 표시

        yield return new WaitForSeconds(1.5f); // 1.5초 동안 스턴

        spriteRenderer.color = originalColor; // 원래 색으로 복구
        lastActionTime = Time.time; // 스턴이 풀린 직후 바로 공격하지 않도록 쿨타임을 초기화합니다.
        SetState(BossState.Deciding); // 다시 행동 결정 상태로 돌아갑니다.
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
        if (currentState != BossState.Stunned)
        {
            lastActionTime = Time.time; // 공격이 끝난 시점부터 쿨타임 계산 시작
            SetState(BossState.Deciding);
        }
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
