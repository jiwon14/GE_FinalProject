using UnityEngine;

public class BossController : MonoBehaviour
{
    // 보스 행동 상태 정의
    private enum BossState
    {
        Idle,       // 정찰 대기
        Patrol,     // 정찰 이동
        Attack,     // 공격 실행 중
        Recover,    // 공격 후 쿨타임 대기 (3초)
        Chase       // 플레이어 추적
    }

    [Header("설정값")]
    public float patrolSpeed = 1.5f;       // 정찰 및 추적 속도
    public float detectionRange = 10f;     // 플레이어 감지 거리
    public float attackCooldown = 3.0f;    // 공격 간 쿨타임 (회복 시간)
    public Transform playerTarget;         // Inspector에서 Player Transform 할당

    private BossState currentState = BossState.Idle;
    private Rigidbody2D rb;
    private float nextActionTime = 0f;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (playerTarget == null)
        {
            Debug.LogError("Player Target이 BossController에 할당되지 않았습니다!");
        }
    }

    void Update()
    {
        HandleFSM();
    }
    
    // FSM 메인 로직 처리 함수
    void HandleFSM()
    {
        // 플레이어 감지 거리 계산
        float distanceToPlayer = Vector2.Distance(transform.position, playerTarget.position);

        switch (currentState)
        {
            case BossState.Idle:
            case BossState.Patrol:
                // 1. 감지 범위 밖: 정찰 (Patrol)
                if (distanceToPlayer > detectionRange)
                {
                    // 정찰 로직 (생략: 간단히 Idle 유지)
                    SetState(BossState.Idle); 
                }
                // 2. 감지 범위 안: 추적/공격 (Chase)
                else
                {
                    SetState(BossState.Chase);
                }
                break;

            case BossState.Chase:
                // 플레이어 추적 이동 (X축)
                ChasePlayer();
                
                // 공격 쿨타임 확인 및 공격 시도
                if (Time.time >= nextActionTime)
                {
                    ExecuteRandomAttack();
                    SetState(BossState.Recover); // 공격 후 회복 상태로 전환
                }
                break;

            case BossState.Attack:
                // (공격 모션 애니메이션이 완료될 때까지 대기)
                // 현재는 공격 함수 내에서 처리 후 바로 Recover로 넘어갑니다.
                break;

            case BossState.Recover:
                // 쿨타임 시간 계산
                if (Time.time >= nextActionTime)
                {
                    SetState(BossState.Chase); // 쿨타임이 끝나면 다시 추적/공격 시작
                }
                break;
        }
    }

    void SetState(BossState newState)
    {
        currentState = newState;

        // 상태 전환 시 초기화 작업
        if (newState == BossState.Recover)
        {
            nextActionTime = Time.time + attackCooldown; // 3초 쿨타임 설정
            Debug.Log($"보스 상태: {newState}. 다음 행동까지 {attackCooldown}초 대기.");
        }
        else
        {
            Debug.Log($"보스 상태: {newState}");
        }
    }

    // 플레이어 추적 및 이동 로직
    void ChasePlayer()
    {
        float direction = playerTarget.position.x > transform.position.x ? 1f : -1f;
        
        // Rigidbody2D.MovePosition을 사용하여 안정적인 물리 이동
        Vector2 targetPosition = rb.position;
        targetPosition.x += direction * patrolSpeed * Time.deltaTime;
        rb.MovePosition(targetPosition);

        // 방향 전환 (SpriteRenderer flipX 로직을 여기에 추가)
        // SpriteRenderer sp = GetComponent<SpriteRenderer>(); 
        // if (sp != null) { sp.flipX = direction < 0; }
    }

    // 공격 패턴 랜덤 선택 및 실행
    void ExecuteRandomAttack()
    {
        // 1부터 4까지 랜덤 선택 (4가지 공격)
        int attackChoice = Random.Range(1, 5); 
        
        switch (attackChoice)
        {
            case 1: Attack_Pierce(); break;
            case 2: Attack_Slam(); break;
            case 3: Attack_DashBackAndCharge(); break;
            case 4: Attack_VulnerableStand(); break;
        }
        SetState(BossState.Attack);
    }
    
    // ===============================================
    // 4가지 공격 패턴 구현
    // ===============================================
    
    void Attack_Pierce() // 1. 찌르기
    {
        Debug.Log("공격 1: 찌르기 (짧은 거리 공격)");
        // TODO: 찌르기 애니메이션 실행 및 짧은 거리 공격 판정 콜라이더 활성화
    }

    void Attack_Slam() // 2. 내려찍기
    {
        Debug.Log("공격 2: 내려찍기 (점프 후 착지 지점 공격)");
        // TODO: 위로 점프 후 플레이어 위치 예측하여 내려찍는 로직 구현
    }

    void Attack_DashBackAndCharge() // 3. 뒤로 한번 이동 후 앞으로 돌진
    {
        Debug.Log("공격 3: 뒤로 이동 후 돌진 (위험한 공격)");
        // TODO: 현재 위치 기반으로 뒤로 짧게 이동 후, 플레이어 방향으로 고속 돌진 로직 구현
    }

    void Attack_VulnerableStand() // 4. 가만히 있기 (딜링 기회)
    {
        Debug.Log("공격 4: 가만히 서있기 (플레이어에게 딜링 기회 제공)");
        // TODO: 일정 시간(예: 1.5초) 동안 속도 0으로 멈추고 피격 약점 콜라이더 활성화
        
        // **중요:** 이 공격은 짧은 시간 동안 딜레이 후 Recover 상태로 넘어가야 합니다.
        nextActionTime = Time.time + 1.5f; // 공격 모션 시간 (1.5초)을 쿨타임으로 사용
    }
    
    // 충돌 로직 (플레이어 공격 시 피격 판정 로직은 여기에 추가될 수 있음)
}