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
        Piercing    // 찌르기 공격 중
    }

    [Header("참조")]
    public Transform playerTransform; // 플레이어의 Transform (Inspector에서 할당)

    [Header("능력치")]
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
    // 플레이어가 보스의 공격 상태를 확인할 수 있도록 public으로 선언합니다.
    public bool IsAttacking { get; private set; } = false;

    private Vector2 moveDirection = Vector2.zero; // FixedUpdate에서 사용할 이동 방향
    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        originalColor = spriteRenderer.color;

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
        // 특정 공격(일반, 돌진, 찌르기) 중에는 FixedUpdate가 속도를 제어하지 않도록 합니다.
        if (currentState == BossState.Attacking || currentState == BossState.StrongAttacking || currentState == BossState.Piercing)
        {
            return;
        }

        rb.linearVelocity = new Vector2(moveDirection.x * moveSpeed, rb.linearVelocity.y);
    }

    // 상태를 변경하고 로그를 출력하는 함수
    void SetState(BossState newState)
    {
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
                StartCoroutine(PierceRoutine());
                break;
            case 2: // 강한 공격
                StartCoroutine(StrongAttackRoutine());
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
        IsAttacking = true; // 공격 시작!

        spriteRenderer.color = Color.red; // 색을 빨갛게 변경
        yield return new WaitForSeconds(0.8f); // 0.8초 동안 공격 모션 (대기)
        spriteRenderer.color = originalColor; // 원래 색으로 복구

        IsAttacking = false; // 공격 끝!

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
        IsAttacking = true; // 공격 시작!

        // 3-1. 공격 예고 (파란색으로 변경)
        spriteRenderer.color = Color.blue;
        yield return new WaitForSeconds(0.7f); // 0.7초 예고 동작

        // 3-2. 뒤로 짧게 이동
        // float backdashDistance = 2f; // 사용되지 않으므로 주석 처리 또는 삭제
        float directionToPlayer = (playerTransform.position.x > transform.position.x) ? 1f : -1f;
        // FixedUpdate 대신 즉각적인 반응을 위해 AddForce 사용 또는 velocity 직접 제어
        rb.linearVelocity = new Vector2(-directionToPlayer * moveSpeed, 0); 
        yield return new WaitForSeconds(0.4f); // 0.4초 동안 뒤로 이동

        // 3-3. 앞으로 돌진
        rb.linearVelocity = new Vector2(directionToPlayer * chargeSpeed, 0); 
        yield return new WaitForSeconds(0.8f); // 0.8초 동안 돌진

        // 3-4. 원상 복구
        rb.linearVelocity = Vector2.zero;
        spriteRenderer.color = originalColor;

        IsAttacking = false; // 공격 끝!

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
        IsAttacking = true; // 공격 시작! (플레이어와의 충돌 시 순간이동 방지)

        spriteRenderer.color = Color.black; // 색을 검은색으로 변경
        yield return new WaitForSeconds(0.5f); // 0.5초 공격 준비

        // 공격 판정 활성화
        if (pierceAttackHitbox != null) pierceAttackHitbox.SetActive(true);

        yield return new WaitForSeconds(0.3f); // 0.3초 동안 공격 판정 유지

        // 공격 판정 비활성화 및 원상 복구
        if (pierceAttackHitbox != null) pierceAttackHitbox.SetActive(false);
        spriteRenderer.color = originalColor;
        IsAttacking = false; // 공격 끝!

        // 상태가 전환된 후 다음 Update가 실행되기 전까지 멈추는 현상을 방지하기 위해
        // 코루틴 종료 시점에 직접 다음 이동 방향을 설정해줍니다.
        if (playerTransform != null)
        {
            moveDirection.x = (playerTransform.position.x > transform.position.x) ? 1f : -1f;
        }
        SetState(BossState.Deciding); // 다시 결정 상태로
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
