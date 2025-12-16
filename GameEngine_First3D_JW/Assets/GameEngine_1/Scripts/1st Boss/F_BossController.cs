using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(AudioSource))] 
public class F_BossController : MonoBehaviour 
{
    private enum BossState
    {
        Idle, Deciding, Attacking, Piercing, Stunned, Dead, Backstepping, Dashing
    }

    [Header("참조")]
    public Transform playerTransform; 
    public BossHealthUI bossHealthUI; // 보스 체력바 UI 참조

    [Header("설정")]
    public bool spriteFacesLeft = true; 
    [Tooltip("보스 사망 시 이동할 씬의 이름")]
    public string nextSceneName;

    [Header("능력치")]
    private BossHealth bossHealth; // BossHealth 참조로 변경
    public float moveSpeed = 2f;
    public float chargeSpeed = 8f;
    public float actionCooldown = 1.5f;
    [SerializeField] private float stoppingDistance = 2.0f;

    [Header("백스텝")]
    public float backstepCooldown = 6.0f; // 백스텝 쿨타임
    public float backstepSpeed = 20.0f; // 백스텝 속도 (moveSpeed의 3배)
    public AnimationClip backstepAnimation; // 백스텝 애니메이션 클립 참조
    
    [Header("공격 판정")]
    public GameObject normalAttackHitbox; 
    public GameObject pierceAttackHitbox; 

    [Header("사운드")]
    public AudioClip normalAttackSound; 
    public AudioClip strongAttackSound;
    public AudioClip pierceAttackSound; 
    public AudioClip dashSound; 

    [Header("찌르기 공격 (Attack 2) 설정")]
    public float pierceBackwardDistance = 1.0f; 
    public float pierceBackwardSpeed = 3.0f;    
    public float pierceForwardDistance = 3.0f;  
    public float pierceForwardSpeed = 10.0f;    
    public float pierceBackwardPause = 0.2f;    
    
    [Header("돌진 패턴")]
    public float dashTriggerTime = 1.5f; // 이 시간 이상 공격 범위 밖에 있으면 돌진
    public float dashCooldown = 7.0f;    // 돌진 쿨타임
    public float dashSpeed = 55f;        // 돌진 속도
    public float dashDuration = 0.7f;    // 돌진 지속 시간
    public float postDashDelay = 0.5f;   // 돌진 후 다른 행동까지의 딜레이

    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private AudioSource audioSource;
    private BossState currentState = BossState.Idle;

    private bool isPlayerInTrackingRange = false; 
    private bool isPlayerInAttackRange = false;
    private bool isPlayerInBackstepRange = false; // 백스텝 존 감지 플래그
    private float lastActionTime = 0f;
    private float lastBackstepTime = -99f; // 마지막 백스텝 시간    
    private float lastDashTime = -99f; // 마지막 돌진 시간
    private float timeOutsideAttackRange = 0f; // 플레이어가 공격 범위 밖에 머무른 시간
    
    private CapsuleCollider2D capsuleCollider;    
    private Vector2 dashDirection = Vector2.zero;
    private bool isDashMoving = false; // 돌진 중 실제 이동 플래그
    private Vector2 moveDirection = Vector2.zero; 
    private Animator animator;
    
    private float timeToSwitchDirection = 0.2f; 
    private float directionSwitchTimer = 0f;
    private bool isDirectionLocked = false; // 방향 전환 잠금 플래그

    private readonly int AttackAnimID = Animator.StringToHash("AttackAnimID");
    // Integer 대신 Trigger를 사용하도록 변경
    private readonly int Attack1TriggerID = Animator.StringToHash("Attack1"); // Attack1 Trigger
    private readonly int Attack3TriggerID = Animator.StringToHash("Attack3");
    private readonly int Attack4TriggerID = Animator.StringToHash("Attack4"); // Attack4 Trigger 추가
    private readonly int IsWalkingAnimID = Animator.StringToHash("isWalking");
    private readonly int DoDashTriggerID = Animator.StringToHash("doDash"); // 돌진용 Trigger
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

        // 플레이어가 추적 범위 안에 있지만 공격 범위 밖에 있는 시간 계산
        if (isPlayerInTrackingRange && !isPlayerInAttackRange)
        {
            timeOutsideAttackRange += Time.deltaTime;
        }
        else
        {
            timeOutsideAttackRange = 0f;
        }

        // [최우선 순위] 다른 행동 중이 아닐 때, 플레이어가 너무 가까우면 백스텝 시도
        if (currentState != BossState.Attacking && currentState != BossState.Piercing && currentState != BossState.Backstepping && currentState != BossState.Dashing &&
            isPlayerInBackstepRange &&
            Time.time >= lastBackstepTime + backstepCooldown)
        {
            SetState(BossState.Backstepping);
            // 백스텝이 발동되면, 이번 프레임의 다른 모든 Update 로직(공격 결정 등)을 건너뜁니다.
            return;
        }

        // [우선 순위 2] 플레이어가 공격 범위 밖에 오래 머무르면 돌진
        if (currentState == BossState.Deciding &&
            timeOutsideAttackRange >= dashTriggerTime &&
            Time.time >= lastDashTime + dashCooldown)
        {
            SetState(BossState.Dashing);
            return; // 돌진이 발동되면 다른 로직을 건너뜁니다.
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

            case BossState.Dashing:
                // 돌진 중에는 DashRoutine 코루틴이 모든 것을 제어합니다.
                break;
        }
    }

    void FixedUpdate()
    {
        // [수정] 각 상태에 맞는 이동 로직만 명확하게 실행합니다.
        if (currentState == BossState.Deciding)
        {
            rb.linearVelocity = new Vector2(moveDirection.x * moveSpeed, rb.linearVelocity.y);
        }
        else if (currentState == BossState.Backstepping)
        {
            // 백스텝 상태일 때만 뒤로 물러나는 움직임을 처리합니다.
            rb.linearVelocity = new Vector2(moveDirection.x * backstepSpeed, rb.linearVelocity.y);
        }
        else if (currentState == BossState.Dashing && isDashMoving) // isDashMoving 플래그가 true일 때만 이동
        {
            // 돌진 상태이고, 실제 이동 신호를 받았을 때만 앞으로 돌진합니다.
            rb.linearVelocity = new Vector2(dashDirection.x * dashSpeed, rb.linearVelocity.y);
        }
        // 다른 상태(Idle, Attacking 등)에서는 FixedUpdate에서 속도를 제어하지 않습니다.
        // SetState에서 속도를 0으로 만듭니다.
    }

    void SetState(BossState newState)
    {
        if (currentState == newState) return;

        // [핵심 수정] 상태 변경 직전에 모든 움직임을 확실하게 초기화합니다.
        rb.linearVelocity = Vector2.zero;
        moveDirection = Vector2.zero;
        animator.SetBool(IsWalkingAnimID, false);

        // 새로운 상태로 전환
        currentState = newState;

        // [핵심 수정] 새로운 상태에 진입할 때 필요한 초기화 로직을 여기서 실행합니다.
        // "Enter" 로직 통합
        switch (currentState)
        {
            case BossState.Idle:
                // Idle 상태에서는 아무것도 하지 않습니다.
                break;

            case BossState.Deciding:
                // Deciding 상태에 진입하면 다시 행동을 결정하기 시작합니다.
                break;

            case BossState.Attacking:
                // 공격 상태 진입 시, 즉시 플레이어를 바라보게 합니다.
                if (playerTransform != null)
                {
                    bool isPlayerRight = (playerTransform.position.x - transform.position.x) > 0;
                    ApplyScale(isPlayerRight);
                }
                break;

            case BossState.Backstepping:
                // 백스텝을 시작할 때, 이동 방향과 관계없이 항상 플레이어를 바라보도록 즉시 방향을 전환합니다.
                if (playerTransform != null)
                {
                    bool isPlayerRight = (playerTransform.position.x - transform.position.x) > 0;
                    ApplyScale(isPlayerRight);
                }

                float directionAwayFromPlayer = (playerTransform != null) ? -Mathf.Sign(playerTransform.position.x - transform.position.x) : -1f;
                moveDirection = new Vector2(directionAwayFromPlayer, 0); // 백스텝 방향 설정
                animator.SetTrigger(DoBackstepTriggerID);
                StartCoroutine(BackstepDurationRoutine());
                break;

            case BossState.Dashing:
                // 돌진 시작 시, 즉시 플레이어를 바라보게 합니다.
                if (playerTransform != null)
                {
                    bool isPlayerRight = (playerTransform.position.x - transform.position.x) > 0;
                    ApplyScale(isPlayerRight);
                    // 돌진 방향 설정
                    dashDirection = new Vector2(isPlayerRight ? 1f : -1f, 0);
                }
                
                timeOutsideAttackRange = 0f; // 돌진을 시작했으므로 타이머 초기화
                isDashMoving = false; // 실제 이동 플래그 초기화
                isDirectionLocked = true; // 돌진 시작 시 방향 고정
                animator.SetTrigger(DoDashTriggerID);
                StartCoroutine(DashRoutine());
                break;
        }
    }

    void FacePlayer()
    {
        if (playerTransform == null || isDirectionLocked) return; // 방향이 고정되어 있으면 실행하지 않음
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

        // [추가] 공격 범위 내에서도 10% 확률로 돌진을 시도합니다.
        // 단, 돌진 쿨타임이 지났을 때만 가능합니다.
        if (Time.time >= lastDashTime + dashCooldown && Random.value < 0.2f) // 20% 확률
        {
            SetState(BossState.Dashing);
            return; // 행동을 결정했으므로 함수 종료
        }

        // [수정] 80% 확률로 일반 공격을 수행합니다.
        // 상태를 먼저 Attacking으로 확실하게 변경합니다.
        SetState(BossState.Attacking);
        
        // Attack1, Attack3, Attack4(일반 공격) 중에서 랜덤으로 하나를 선택합니다.
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

        // 1. 모든 행동/코루틴 즉시 중단
        StopAllCoroutines(); 
        
        SetState(BossState.Dead);
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

        if (!string.IsNullOrEmpty(nextSceneName))
        {
            SceneManager.LoadScene(nextSceneName);
        }
        else
        {
            #if UNITY_EDITOR
            UnityEditor.Selection.activeObject = null;
            #endif
            Destroy(gameObject); 
        }
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
        // 이 함수의 모든 로직을 BackstepDurationRoutine으로 이전했습니다.
        // 이중 호출을 막기 위해 비워둡니다.
    }

    /// <summary>
    /// 애니메이션 이벤트에서 호출되어 실제 돌진 이동을 시작합니다.
    /// </summary>
    public void AnimationEvent_StartDashMovement()
    {
        if (currentState == BossState.Dashing)
        {
            PlaySound(dashSound);
            isDashMoving = true;
        }
    }

    private IEnumerator BackstepDurationRoutine()
    {
        // 1. [핵심 수정] 애니메이션 클립의 실제 길이를 가져와서 그 시간만큼만 이동 및 대기합니다.
        // 이렇게 하면 이동 시간과 애니메이션 재생 시간이 완벽하게 동기화됩니다.
        float animationDuration = 1.0f; // 애니메이션 클립이 할당되지 않았을 경우의 기본값
        if (backstepAnimation != null)
        {
            animationDuration = backstepAnimation.length;
        }
        yield return new WaitForSeconds(animationDuration);

        // 2. 애니메이션이 끝나면 물리적인 이동을 즉시 멈춥니다.
        moveDirection = Vector2.zero;
        rb.linearVelocity = Vector2.zero;

        // 3. 백스텝 후딜레이 시간만큼 추가로 대기합니다.
        // 이 시간 동안에도 currentState는 'Backstepping'이므로, Update()에서 다른 행동을 할 수 없습니다.
        yield return new WaitForSeconds(0.3f);

        // 4. 모든 과정(애니메이션 + 후딜레이)이 끝났는지 최종 확인합니다.
        // (스턴 등으로 중간에 코루틴이 중단되지 않았는지 확인하는 안전장치)
        if (currentState == BossState.Backstepping)
        {
            // [핵심 수정] 백스텝의 모든 과정(애니메이션 + 후딜레이)이 끝난 이 시점에 쿨타임을 기록합니다.
            // 이렇게 하면 백스텝이 연쇄적으로 발동되는 것을 완벽하게 막을 수 있습니다.
            lastBackstepTime = Time.time;
            // [추가] 백스텝 또한 하나의 행동으로 간주하여, 다음 공격까지의 딜레이를 적용합니다.
            // 이렇게 하면 백스텝 직후 바로 공격하는 어색한 움직임을 방지할 수 있습니다.
            lastActionTime = Time.time;
            SetState(BossState.Deciding);
        }
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
        if (currentState == BossState.Dead || currentState == BossState.Dashing) return; // 죽었거나 돌진 중이면 스턴(패링)에 걸리지 않음

        StopAllCoroutines(); 
        StartCoroutine(StunRoutine());
    }

    private IEnumerator DashRoutine()
    {
        // 1. 애니메이션 이벤트가 isDashMoving을 true로 바꿀 때까지 대기합니다.
        //    (혹은 스턴 등으로 상태가 바뀌면 즉시 종료)
        yield return new WaitUntil(() => isDashMoving || currentState != BossState.Dashing);

        // 2. 상태가 Dashing이 아니게 되었다면(예: 스턴), 코루틴을 즉시 종료합니다.
        if (currentState != BossState.Dashing) yield break;

        // 3. 실제 돌진 이동이 시작되었으므로, dashDuration 만큼 대기합니다.
        yield return new WaitForSeconds(dashDuration);

        // 4. 돌진 시간이 끝나면, 이동을 멈춥니다.
        isDashMoving = false;
        rb.linearVelocity = Vector2.zero;

        // 5. 돌진 후딜레이 시간만큼 추가로 대기합니다.
        yield return new WaitForSeconds(postDashDelay);

        // 6. 모든 과정이 끝났는지 최종 확인합니다.
        if (currentState == BossState.Dashing)
        {
            lastDashTime = Time.time;
            lastActionTime = Time.time - actionCooldown; // 다음 행동 쿨타임 초기화
            isDirectionLocked = false; // 방향 고정 해제
            SetState(BossState.Deciding);
        }
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