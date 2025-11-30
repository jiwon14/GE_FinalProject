using System.Collections;
using UnityEngine;

public class BossController : MonoBehaviour
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
    public float actionCooldown = 2f;    // 행동과 행동 사이의 대기 시간
    public GameObject pierceAttackHitbox; // 전방 찌르기 공격 판정 (Inspector에서 할당)
    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private BossState currentState = BossState.Idle;

    private bool isPlayerInTrackingRange = false; // 플레이어 감지 여부
    private float lastActionTime = 0f;            // 마지막 행동 시간
    private Color originalColor;                  // 원래 스프라이트 색상

    private Vector3 pierceHitboxInitialLocalPos;  // 찌르기 히트박스의 초기 로컬 위치
    private CapsuleCollider2D capsuleCollider;    // 보스의 메인 콜라이더
    private Vector2 moveDirection = Vector2.zero; // FixedUpdate에서 사용할 이동 방향
    private Animator animator;
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
                    SetState(BossState.Idle);
                    break;
                }

                // 플레이어를 향해 계속 이동합니다.
                if (playerTransform != null)
                {
                    float directionToPlayer = (playerTransform.position.x > transform.position.x) ? 1f : -1f;
                    moveDirection.x = directionToPlayer;
                }

                // 마지막 행동 후 일정 시간이 지났다면 다음 행동을 결정합니다.
                if (Time.time >= lastActionTime + actionCooldown)
                {
                    DecideNextAction();
                }
                break;
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
        // 'Deciding' 또는 'Moving' 상태일 때만 이동 로직을 실행합니다.
        if (currentState == BossState.Deciding || currentState == BossState.Moving)
        {
            rb.linearVelocity = new Vector2(moveDirection.x * moveSpeed, rb.linearVelocity.y);
        }
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
        // 플레이어가 보스의 왼쪽에 있을 때 (원본 스프라이트가 왼쪽을 본다고 가정)
        if (playerTransform.position.x < transform.position.x)
        {
            spriteRenderer.flipX = false; // 스프라이트를 뒤집지 않는다 (왼쪽)
            if (pierceAttackHitbox != null)
            {
                // 히트박스를 왼쪽으로 (초기 x 위치의 절댓값에 -를 붙여 항상 왼쪽을 보장)
                pierceAttackHitbox.transform.localPosition = new Vector3(
                    -Mathf.Abs(pierceHitboxInitialLocalPos.x),
                    pierceHitboxInitialLocalPos.y,
                    pierceHitboxInitialLocalPos.z);
            }
        }
        else // 플레이어가 보스의 오른쪽에 있을 때
        {
            spriteRenderer.flipX = true; // 스프라이트를 뒤집는다 (오른쪽)
            if (pierceAttackHitbox != null)
            {
                // 히트박스를 오른쪽으로 (초기 x 위치의 절댓값을 사용해 항상 오른쪽을 보장)
                pierceAttackHitbox.transform.localPosition = new Vector3(
                    Mathf.Abs(pierceHitboxInitialLocalPos.x),
                    pierceHitboxInitialLocalPos.y,
                    pierceHitboxInitialLocalPos.z);
            }
        }
    }
    // 다음 행동을 무작위로 결정하는 함수
    void DecideNextAction()
    {
        moveDirection = Vector2.zero; // 공격 전 잠시 멈춤
        int actionChoice = Random.Range(0, 3); // 0: 일반 공격, 1: 강한 공격, 2: 찌르기 공격

        switch (actionChoice)
        {
            case 0:
                StartCoroutine(AttackRoutine());
                break;
            case 1:
                StartCoroutine(StrongAttackRoutine());
                break;
            case 2:
                StartCoroutine(PierceRoutine());
                break;
        }
        lastActionTime = Time.time; // 마지막 행동 시간 기록
    }

    // --- 행동 패턴 구현 (코루틴) ---

    // 1. 좌우로 움직이는 행동
    private IEnumerator MoveRoutine()
    {
        SetState(BossState.Moving);

        float moveDuration = 1.5f; // 1.5초 동안 움직임
        float timer = 0f;
        // 플레이어의 반대 방향으로 움직일지, 같은 방향으로 움직일지 랜덤 결정
        moveDirection.x = (Random.value > 0.5f) ? 1f : -1f;

        while (timer < moveDuration)
        {
            timer += Time.deltaTime;
            yield return null; // 다음 프레임까지 대기
        }

        moveDirection = Vector2.zero; // 이동 방향 초기화
        rb.linearVelocity = Vector2.zero;   // 즉시 정지
        SetState(BossState.Deciding); // 다시 결정 상태로
    }

    // 2. 일반 공격 행동
    private IEnumerator AttackRoutine()
    {
        SetState(BossState.Attacking);

        // 공격 예고 (빨간색으로 변경)
        spriteRenderer.color = Color.red;

        yield return new WaitForSeconds(0.4f); // 예고 동작 대기
        PerformMeleeAttack(1, 2.0f); // 범위 2.0f의 근접 공격 실행

        yield return new WaitForSeconds(0.4f); // 공격 후 대기
        spriteRenderer.color = originalColor; // 원래 색으로 복구

        // 코루틴 종료 시점에 직접 다음 이동 방향을 설정해줍니다.
        if (playerTransform != null)
        {
            moveDirection.x = (playerTransform.position.x > transform.position.x) ? 1f : -1f;
        }

        SetState(BossState.Deciding); // 다시 결정 상태로
    }

    // 3. 강한 공격 행동
    private IEnumerator StrongAttackRoutine()
    {
        SetState(BossState.StrongAttacking);

        // 3-1. 공격 예고 (애니메이션 재생)
        animator.SetTrigger("StrongAttack"); // "StrongAttack" 애니메이션 트리거
        yield return new WaitForSeconds(0.7f); // 0.7초 예고 동작

        // 3-2. 뒤로 짧게 물러나기
        float directionToPlayer = (playerTransform.position.x > transform.position.x) ? 1f : -1f;
        rb.linearVelocity = new Vector2(-directionToPlayer * moveSpeed, 0);
        yield return new WaitForSeconds(0.4f); // 0.4초 동안 뒤로 이동

        // 3-3. 플레이어 방향으로 돌진
        rb.linearVelocity = new Vector2(directionToPlayer * chargeSpeed, 0); 

        // 3-4. 돌진하는 동안 매 프레임 충돌을 감지
        float chargeDuration = 0.8f;
        float chargeTimer = 0f;
        bool hasHit = false; // 돌진 중 한 번만 피격되도록 처리

        while (chargeTimer < chargeDuration)
        {
            if (!hasHit) // 아직 공격이 적중하지 않았을 때만 판정
            {
                // 보스의 캡슐 콜라이더와 겹치는 플레이어를 찾습니다.
                ContactFilter2D filter = new ContactFilter2D().NoFilter();
                filter.SetLayerMask(LayerMask.GetMask("Player")); // 플레이어 레이어만 감지하도록 필터 설정
                Collider2D[] hitPlayers = new Collider2D[1];
                int hitCount = capsuleCollider.Overlap(filter, hitPlayers);

                if (hitCount > 0)
                {
                    Collider2D playerCollider = hitPlayers[0];
                    Debug.Log("돌진 공격이 플레이어에게 적중!");
                    playerCollider.GetComponent<PlayerController>()?.HandleAttack(2, gameObject);
                    hasHit = true; // 공격이 적중했음을 표시
                }
            }
            chargeTimer += Time.deltaTime;
            yield return null; // 다음 프레임까지 대기
        }

        // 3-5. 돌진 후 정지 및 원상 복구
        rb.linearVelocity = Vector2.zero;
        // 애니메이션이 끝나면 자동으로 원래 상태로 돌아가므로 색상 복구 코드는 필요 없습니다.
        // spriteRenderer.color = originalColor;

        // 코루틴 종료 시점에 직접 다음 이동 방향을 설정해줍니다.
        if (playerTransform != null)
        {
            moveDirection.x = (playerTransform.position.x > transform.position.x) ? 1f : -1f;
        }

        SetState(BossState.Deciding); // 다시 결정 상태로
    }

    // 4. 전방 찌르기 행동
    private IEnumerator PierceRoutine()
    {
        SetState(BossState.Piercing);

        spriteRenderer.color = Color.black; // 색을 검은색으로 변경
        yield return new WaitForSeconds(0.5f); // 0.5초 공격 준비

        // 공격 판정 활성화
        if (pierceAttackHitbox != null) pierceAttackHitbox.SetActive(true);

        // 찌르기 공격은 PlayerController의 OnTriggerEnter2D에서 감지하여 HandleAttack을 호출합니다.
        // 따라서 여기서는 별도로 TryDamagePlayer를 호출하지 않습니다.
        yield return new WaitForSeconds(0.3f); // 0.3초 동안 공격 판정 유지

        // 공격 판정 비활성화 및 원상 복구
        if (pierceAttackHitbox != null) pierceAttackHitbox.SetActive(false);
        spriteRenderer.color = originalColor;

        // 코루틴 종료 시점에 직접 다음 이동 방향을 설정해줍니다.
        if (playerTransform != null)
        {
            moveDirection.x = (playerTransform.position.x > transform.position.x) ? 1f : -1f;
        }

        SetState(BossState.Deciding); // 다시 결정 상태로
    }

    // --- 패링 및 스턴 로직 ---

    // 근접 공격을 수행하고 플레이어에게 데미지를 시도하는 함수
    void PerformMeleeAttack(int damage, float attackRange)
    {
        // 공격 순간에 스턴 상태면 데미지를 주지 않습니다.
        if (currentState == BossState.Stunned) return;

        // 지정된 범위 내의 모든 'Player' 레이어를 가진 콜라이더를 찾습니다.
        Collider2D[] hitPlayers = Physics2D.OverlapCircleAll(transform.position, attackRange, LayerMask.GetMask("Player"));

        foreach (Collider2D playerCollider in hitPlayers)
        {
            Debug.Log("근접 공격이 플레이어에게 적중!");
            PlayerController pc = playerCollider.GetComponent<PlayerController>();
            if(pc != null)
            {
                // 플레이어의 HandleAttack 함수를 호출하여 패링 여부 판단을 위임합니다.
                pc.HandleAttack(damage, gameObject);
            }
        }
    }

    // 데미지를 받는 함수 (외부에서 호출 가능)
    public void TakeDamage(int damage)
    {
        // 스턴 상태가 아닐 때는 데미지를 받지 않거나, 특정 로직을 추가할 수 있습니다.
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

    // 외부(PlayerController)에서 호출될 스턴 함수
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

    // --- 플레이어 감지 로직 ---

    // 자식 오브젝트의 Trigger에 플레이어가 들어왔을 때
    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInTrackingRange = true;
            Debug.Log("플레이어 감지!");
        }
    }

    // 자식 오브젝트의 Trigger에서 플레이어가 나갔을 때
    void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInTrackingRange = false;
            Debug.Log("플레이어가 감지 범위를 벗어남!");
        }
    }

}
