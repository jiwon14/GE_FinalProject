using System.Collections;
using UnityEngine;

[RequireComponent(typeof(AudioSource))] 
public class F_BossController : MonoBehaviour 
{
    private enum BossState
    {
        Idle, Deciding, Attacking, Piercing, Stunned, Dead
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
    private float lastActionTime = 0f;            
    
    private CapsuleCollider2D capsuleCollider;    
    private Vector2 moveDirection = Vector2.zero; 
    private Animator animator;
    
    private float timeToSwitchDirection = 0.2f; 
    private float directionSwitchTimer = 0f;

    private readonly int AttackAnimID = Animator.StringToHash("AttackAnimID");
    private readonly int IsWalkingAnimID = Animator.StringToHash("isWalking");
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
        }
        
        if (currentState == BossState.Attacking || currentState == BossState.Piercing) 
        {
            moveDirection = Vector2.zero;
        }
    }

    void FixedUpdate()
    {
        if (currentState != BossState.Stunned && currentState != BossState.Dead)
            rb.linearVelocity = new Vector2(moveDirection.x * moveSpeed, rb.linearVelocity.y);
    }

    void SetState(BossState newState)
    {
        if (currentState == newState) return;
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

        SetState(BossState.Attacking);
        moveDirection = Vector2.zero; 
        animator.SetBool(IsWalkingAnimID, false); 

        int actionChoice = Random.Range(1, 4); 
        animator.SetInteger(AttackAnimID, actionChoice);

        if (actionChoice == 2) StartCoroutine(PierceAttackMovementRoutine());
        
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
        animator.SetInteger(AttackAnimID, 0);
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
        animator.SetInteger(AttackAnimID, 0); 
        animator.SetBool(IsWalkingAnimID, false); 
        lastActionTime = Time.time; 
        SetState(BossState.Deciding);
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
        animator.SetInteger(AttackAnimID, 0);
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
    private void PlaySound(AudioClip clip) { if (audioSource != null && clip != null) audioSource.PlayOneShot(clip); }
}