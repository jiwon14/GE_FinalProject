using System.Collections;
using UnityEngine;

[RequireComponent(typeof(AudioSource))] 
public class F_BossController : MonoBehaviour 
{
    private enum BossState
    {
        Idle, Deciding, Attacking, Piercing, Stunned, Dead, Backstepping
    }

    [Header("참조")]
    public Transform playerTransform; 
    public BossHealthUI bossHealthUI; // 보스 체력바 UI 참조

    [Header("설정")]
    public bool spriteFacesLeft = true; 

    [Header("능력치")]
    private BossHealth bossHealth; // BossHealth 참조로 변경
    public float moveSpeed = 2f;         
    public float chargeSpeed = 8f;       
    public float actionCooldown = 1.5f;    
    [SerializeField] private float stoppingDistance = 2.0f;

    [Header("백스텝")]
    public float backstepCooldown = 3.0f; // 백스텝 쿨타임
    public float backstepDistance = 2.0f; // 백스텝으로 이동할 거리
    
    [Header("공격 판정")]
    public GameObject normalAttackHitbox; 
    public GameObject pierceAttackHitbox; 

    [Header("사운드")]
    public AudioClip normalAttackSound; 
    public AudioClip strongAttackSound; 
    public AudioClip pierceAttackSound; 

    [Header("찌르기 공격 (Attack 2) 설정")]
    public float pierceBackwardDistance = 1.0f; 
    public float pierceBackwardSpeed = 3.0f;    
    public float pierceForwardDistance = 3.0f;  
    public float pierceForwardSpeed = 10.0f;    
    public float pierceBackwardPause = 0.2f;    
    
    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private AudioSource audioSource;
    private BossState currentState = BossState.Idle;

    private bool isPlayerInTrackingRange = false; 
    private bool isPlayerInAttackRange = false;
    private bool isPlayerInBackstepRange = false; // 백스텝 존 감지 플래그
    private float lastActionTime = 0f;
    private float lastBackstepTime = -99f; // 마지막 백스텝 시간
    
    private CapsuleCollider2D capsuleCollider;    
    private Vector2 moveDirection = Vector2.zero; 
    private Animator animator;
    
    private float timeToSwitchDirection = 0.2f; 
    private float directionSwitchTimer = 0f;

    private readonly int AttackAnimID = Animator.StringToHash("AttackAnimID");
    // Integer 대신 Trigger를 사용하도록 변경
    private readonly int Attack1TriggerID = Animator.StringToHash("Attack1"); // Attack1 Trigger
    private readonly int Attack3TriggerID = Animator.StringToHash("Attack3");
    private readonly int Attack4TriggerID = Animator.StringToHash("Attack4"); // Attack4 Trigger 추가
    private readonly int IsWalkingAnimID = Animator.StringToHash("isWalking");
    private readonly int DoBackstepTriggerID = Animator.StringToHash("doBackstep"); // 백스텝용 Trigger
    private readonly int StunAnimID = Animator.StringToHash("Stun");
    private readonly int DieAnimID = Animator.StringToHash("Die");

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        audioSource = GetComponent<AudioSource>();
        animator = GetComponent<Animator>();
        bossHealth = GetComponent<BossHealth>(); // BossHealth 컴포넌트 가져오기
        capsuleCollider = GetComponent<CapsuleCollider2D>();

        if (playerTransform == null) Debug.LogError("플레이어 Transform 미할당!");
        if (bossHealthUI == null) Debug.LogError("BossHealthUI가 F_BossController에 할당되지 않았습니다!");

        if (normalAttackHitbox != null) normalAttackHitbox.SetActive(false);
        if (pierceAttackHitbox != null) pierceAttackHitbox.SetActive(false);
    }

    void Update()
    {
        // [철벽 방어 1] 죽었으면 Update 로직 아예 실행 X
        if (currentState == BossState.Dead || currentState == BossState.Stunned) return;

        // [최우선 순위] 다른 행동 중이 아닐 때, 플레이어가 너무 가까우면 백스텝 시도
        if (currentState != BossState.Attacking && currentState != BossState.Piercing && currentState != BossState.Backstepping && currentState != BossState.Stunned &&
            isPlayerInBackstepRange &&
            Time.time >= lastBackstepTime + backstepCooldown)
        {
            SetState(BossState.Backstepping);
            // 백스텝이 발동되면, 이번 프레임의 다른 모든 Update 로직(공격 결정 등)을 건너뜁니다.
            return;
        }

        switch (currentState)
        {
            case BossState.Idle:
                if (isPlayerInTrackingRange) SetState(BossState.Deciding);
                break;

            case BossState.Deciding:
                if (!isPlayerInTrackingRange)
                {
                    moveDirection = Vector2.zero; 
                    animator.SetBool(IsWalkingAnimID, false);
                    SetState(BossState.Idle);
                    break;
                }

                if (playerTransform != null)
                {
                    float distanceToPlayerX = playerTransform.position.x - transform.position.x;
                    
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
                    
                    FacePlayer(); 
                }

                if (isPlayerInAttackRange && Time.time >= lastActionTime + actionCooldown)
                {
                    DecideNextAction();
                }
                break;

            case BossState.Backstepping:
                // 이 상태에서는 애니메이션이 끝날 때까지 아무것도 하지 않고 기다립니다.
                // 모든 이동과 상태 변경은 애니메이션 이벤트가 처리합니다.
                break;
        }
    }

    void FixedUpdate()
    {
        // [수정] 백스텝 중에는 코루틴이 직접 위치를 제어하므로, FixedUpdate에서는 이동을 처리하지 않습니다.
        if (currentState != BossState.Stunned && currentState != BossState.Dead && currentState != BossState.Backstepping)
            rb.linearVelocity = new Vector2(moveDirection.x * moveSpeed, rb.linearVelocity.y);
    }

    void SetState(BossState newState)
    {
        if (currentState == newState) return;

        // 새 상태에 대한 초기화
        if (newState == BossState.Attacking || newState == BossState.Piercing) moveDirection = Vector2.zero;

        if (newState == BossState.Backstepping)
        {
            lastBackstepTime = Time.time; // 백스텝 시작 시 쿨타임 기록
            moveDirection = Vector2.zero; // 이동 코루틴이 제어하므로 일단 정지
            animator.SetTrigger(DoBackstepTriggerID); // 애니메이션 Trigger 발동
            StartCoroutine(BackstepMovementRoutine()); // 백스텝 이동 코루틴 시작
        }

        currentState = newState;
    }

    void FacePlayer()
    {
        if (playerTransform == null) return;
        float directionToPlayer = playerTransform.position.x - transform.position.x;
        bool isPlayerRight = directionToPlayer > 0;
        bool isFacingRight;
        if (spriteFacesLeft) isFacingRight = transform.localScale.x < 0; 
        else isFacingRight = transform.localScale.x > 0;

        if (isFacingRight != isPlayerRight)
        {
            bool isMoving = (currentState == BossState.Deciding && moveDirection.x != 0);
            if (isMoving)
            {
                ApplyScale(isPlayerRight);
                directionSwitchTimer = 0f;
            }
            else
            {
                directionSwitchTimer += Time.deltaTime;
                if (directionSwitchTimer >= timeToSwitchDirection)
                {
                    ApplyScale(isPlayerRight);
                    directionSwitchTimer = 0f;
                }
            }
        }
        else directionSwitchTimer = 0f;
    }

    void ApplyScale(bool lookRight)
    {
        float targetScaleX = 1f;
        if (spriteFacesLeft) targetScaleX = lookRight ? -1f : 1f;
        else targetScaleX = lookRight ? 1f : -1f;
        transform.localScale = new Vector3(targetScaleX, transform.localScale.y, transform.localScale.z);
    }

    void DecideNextAction()
    {
        if(playerTransform != null)
        {
            bool isPlayerRight = (playerTransform.position.x - transform.position.x) > 0;
            ApplyScale(isPlayerRight);
        }

        // [수정] 상태를 먼저 Attacking으로 확실하게 변경합니다.
        SetState(BossState.Attacking);
        moveDirection = Vector2.zero; 
        animator.SetBool(IsWalkingAnimID, false); 
        
        // Attack1, Attack3, Attack4 중에서 랜덤으로 하나를 선택합니다.
        int[] attackChoices = { 1, 3, 4 };
        int choiceIndex = Random.Range(0, attackChoices.Length);
        int chosenAttack = attackChoices[choiceIndex];

        // 선택된 공격에 따라 적절한 Trigger를 발동시킵니다.
        switch (chosenAttack)
        {
            case 1: // Attack1
                animator.SetTrigger(Attack1TriggerID);
                break;
            case 3: // Attack3
                animator.SetTrigger(Attack3TriggerID);
                break;
            case 4: // Attack4
                animator.SetTrigger(Attack4TriggerID);
                break;
        }

        // 안전장치: 4초 뒤에도 공격 상태면 강제 종료 (죽었을 땐 실행 안 됨)
        StartCoroutine(ForceFinishAttackRoutine(4.0f));
    }

    // 사망 처리
    public void Die() // BossHealth에서 호출할 수 있도록 public으로 변경
    {
        Debug.Log("보스가 쓰러졌습니다!");
        SetState(BossState.Dead); 

        // 1. 모든 행동/코루틴 즉시 중단
        StopAllCoroutines(); 
        
        // 2. 물리/이동 정지
        rb.linearVelocity = Vector2.zero; 
        moveDirection = Vector2.zero;
        rb.bodyType = RigidbodyType2D.Kinematic; // 중력 끄기

        // 3. 충돌 끄기
        if (capsuleCollider != null) capsuleCollider.enabled = false;

        // 4. 공격 판정 끄기
        AnimationEvent_DeactivateAllHitboxes();

        // 5. 애니메이션 정리 (걷기 끄기, 공격 끄기, 죽음 켜기)
        animator.SetBool(IsWalkingAnimID, false);
        animator.SetTrigger(DieAnimID);

        // 6. 삭제 대기
        StartCoroutine(DeathRoutine());
    }

    private IEnumerator DeathRoutine()
    {
        // 2초 동안 시체 보여주기
        yield return new WaitForSeconds(2.0f); 
        Destroy(gameObject); 
    }

    // --- 이벤트 함수들 (철벽 방어 적용) ---

    public void AnimationEvent_PerformNormalAttack()
    {
        if (currentState == BossState.Dead) return; // [철벽]
        PlaySound(normalAttackSound);
        if (normalAttackHitbox != null) normalAttackHitbox.SetActive(true);
    }

    public void AnimationEvent_StartCharge()
    {
        if (currentState == BossState.Dead) return; // [철벽]
        PlaySound(strongAttackSound);
        StartCoroutine(ChargeRoutine());
    }

    public void AnimationEvent_SetPierceHitbox(int active)
    {
        if (currentState == BossState.Dead) return; // [철벽]
        if (active == 1) PlaySound(pierceAttackSound);
        if (pierceAttackHitbox != null) pierceAttackHitbox.SetActive(active == 1);
    }

    // [가장 중요] 공격 종료 이벤트
    public void AnimationEvent_AttackFinished()
    {
        // 죽었으면 절대로 상태를 Deciding으로 바꾸지 마라!
        if (currentState == BossState.Dead) return; 

        StopCoroutine("ForceFinishAttackRoutine");
        AnimationEvent_DeactivateAllHitboxes(); 
        animator.SetBool(IsWalkingAnimID, false); 
        lastActionTime = Time.time; 
        SetState(BossState.Deciding);
    }

    // [가장 중요] 백스텝 애니메이션 종료 이벤트
    public void AnimationEvent_BackstepFinished()
    {
        // 죽었으면 상태를 바꾸지 않음
        if (currentState == BossState.Dead) return;

        // [중요] 이전에 사용된 백스텝 트리거가 남아있지 않도록 확실하게 리셋합니다.
        animator.ResetTrigger(DoBackstepTriggerID);

        // [수정] 백스텝이 끝난 시점을 기준으로 쿨다운을 다시 계산하여 연속 발동을 방지합니다.
        lastBackstepTime = Time.time;

        // 백스텝이 끝났으므로 다음 행동 결정 상태로 전환
        SetState(BossState.Deciding);
    }

    private IEnumerator BackstepMovementRoutine()
    {
        // 1. 목표 지점 계산
        float directionAwayFromPlayer = (playerTransform != null) ? -Mathf.Sign(playerTransform.position.x - transform.position.x) : -1f;
        Vector2 startPosition = transform.position;
        Vector2 targetPosition = startPosition + new Vector2(directionAwayFromPlayer * backstepDistance, 0);

        // 백스텝 시작 시 플레이어를 즉시 바라보게 함
        FacePlayer();

        // 2. 목표 지점으로 이동
        while (Vector2.Distance(transform.position, targetPosition) > 0.01f)
        {
            // 죽거나 스턴 상태가 되면 즉시 중단
            if (currentState == BossState.Dead || currentState == BossState.Stunned) yield break;

            // MoveTowards를 사용하여 프레임마다 목표 위치로 이동
            transform.position = Vector2.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);

            yield return null;
        }

        // 3. 이동 완료 후 정확한 위치 보정 및 속도 초기화
        transform.position = targetPosition;
        rb.linearVelocity = Vector2.zero;
    }

    // --- 나머지 로직 ---

    public void TakeDamage(int damage)
    {
        if (currentState == BossState.Dead) return;

        // BossHealth 컴포넌트에 데미지 처리를 위임합니다.
        bossHealth.TakeDamage(damage);
        Debug.Log($"<color=red>보스 피격! 데미지: {damage}</color>");
    }
    
    public void GetStunned()
    {
        if (currentState == BossState.Dead) return; // 죽었으면 스턴 X

        StopAllCoroutines(); 
        StartCoroutine(StunRoutine());
    }

    private IEnumerator StunRoutine()
    {
        SetState(BossState.Stunned);
        animator.SetTrigger(StunAnimID); 
        rb.linearVelocity = Vector2.zero; 
        yield return new WaitForSeconds(1.5f); 
        AnimationEvent_AttackFinished();
    }
    
    private IEnumerator ForceFinishAttackRoutine(float limitTime)
    {
        yield return new WaitForSeconds(limitTime);
        if (currentState == BossState.Attacking || currentState == BossState.Piercing)
        {
             // 죽었으면 강제 종료도 하지 마라
            if (currentState != BossState.Dead) AnimationEvent_AttackFinished();
        }
    }

    public void AnimationEvent_DeactivateAllHitboxes()
    {
        if (normalAttackHitbox != null && normalAttackHitbox.activeSelf)
            normalAttackHitbox.SetActive(false);
        if (pierceAttackHitbox != null && pierceAttackHitbox.activeSelf)
            pierceAttackHitbox.SetActive(false);
    }

    private IEnumerator ChargeRoutine()
    {
        float facingDir = Mathf.Sign(transform.localScale.x);
        if (spriteFacesLeft) facingDir *= -1;
        rb.linearVelocity = new Vector2(facingDir * chargeSpeed, 0);
        
        float duration = 0.8f; float timer = 0f; bool hasHit = false;
        while (timer < duration)
        {
            if (currentState == BossState.Dead) yield break; // 죽었으면 돌진 중단

            if (!hasHit) {
                ContactFilter2D filter = new ContactFilter2D().NoFilter();
                filter.SetLayerMask(LayerMask.GetMask("Player"));
                Collider2D[] hits = new Collider2D[1];
                if (capsuleCollider.Overlap(filter, hits) > 0) {
                    hits[0].GetComponent<F_PlayerController>()?.HandleAttack(2, gameObject); hasHit = true;
                }
            }
            timer += Time.deltaTime; yield return null;
        }
        rb.linearVelocity = Vector2.zero;
    }

    private IEnumerator PierceAttackMovementRoutine()
    {
        SetState(BossState.Piercing); rb.linearVelocity = Vector2.zero; 
        float facingDir = Mathf.Sign(transform.localScale.x);
        if (spriteFacesLeft) facingDir *= -1;

        float duration = pierceBackwardDistance / pierceBackwardSpeed; float timer = 0f;
        while (timer < duration) {
            if (currentState == BossState.Dead) yield break; // 죽었으면 중단
            rb.linearVelocity = new Vector2(-facingDir * pierceBackwardSpeed, rb.linearVelocity.y);
            timer += Time.deltaTime; yield return null;
        }
        rb.linearVelocity = Vector2.zero; 
        yield return new WaitForSeconds(pierceBackwardPause);

        duration = pierceForwardDistance / pierceForwardSpeed; timer = 0f;
        while (timer < duration) {
            if (currentState == BossState.Dead) yield break; // 죽었으면 중단
            rb.linearVelocity = new Vector2(facingDir * pierceForwardSpeed, rb.linearVelocity.y);
            timer += Time.deltaTime; yield return null;
        }
        rb.linearVelocity = Vector2.zero; 
    }

    public void OnPlayerEnterTrackingRange() 
    { 
        isPlayerInTrackingRange = true;
        // 플레이어가 추적 범위에 들어오면 체력바를 표시합니다.
        if (bossHealthUI != null) bossHealthUI.Show();
    }

    public void OnPlayerExitTrackingRange() { isPlayerInTrackingRange = false; isPlayerInAttackRange = false; }
    public void OnPlayerEnterAttackRange() { isPlayerInAttackRange = true; }
    public void OnPlayerExitAttackRange() { isPlayerInAttackRange = false; }
    public void OnPlayerEnterBackstepRange() { isPlayerInBackstepRange = true; }
    public void OnPlayerExitBackstepRange() { isPlayerInBackstepRange = false; }
    private void PlaySound(AudioClip clip) { if (audioSource != null && clip != null) audioSource.PlayOneShot(clip); }
}