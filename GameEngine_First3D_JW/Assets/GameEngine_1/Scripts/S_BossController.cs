using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(BossHealth))]
public class S_BossController : MonoBehaviour
{
    // 광폭화 보스 상태
    private enum BossState
    {
        Idle,        // 대기 (플레이어가 인식 범위 밖에 있음)
        Chasing,     // 플레이어 추적 및 행동 결정
        Dashing,     // 대쉬 중
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

    [Header("디버그")]
    [Tooltip("활성화하면 보스의 현재 상태와 결정 등 상세 로그를 콘솔에 출력합니다.")]
    public bool enableDebugLogs = true;

    [Header("참조")]
    [Tooltip("플레이어 오브젝트의 Transform을 할당해야 합니다.")]
    public Transform playerTransform;
    [Tooltip("보스 체력 UI 스크립트를 할당해야 합니다.")]
    public BossHealthUI bossHealthUI;
    
    [Header("Zone 콜라이더 참조")]
    [Tooltip("플레이어와의 교전을 시작하는 중간 범위의 Zone 콜라이더입니다.")]
    public Collider2D engagementZoneCollider;
    [Tooltip("보스가 공격을 시작할 수 있는 근접 범위의 Zone 콜라이더입니다.")]
    public Collider2D attackZoneCollider;

    [Header("기본 설정")]
    [Tooltip("스프라이트가 기본적으로 왼쪽을 보는지 설정합니다.")]
    public bool spriteFacesLeft = true;

    [Header("능력치")]
    public float moveSpeed = 4f; // 광폭화 컨셉에 맞게 속도 상향
    [Tooltip("다음 행동까지의 최소 대기 시간입니다.")]
    public float actionCooldown = 0.5f;
    [Tooltip("추적을 멈출 거리입니다. Attack_Zone 경계와 비슷하게 설정하는 것을 권장합니다.")]
    [SerializeField] private float stoppingDistance = 3.0f;

    [Header("대쉬 (이동용)")]
    [Tooltip("플레이어를 향한 전방 대쉬 속도입니다.")]
    public float dashSpeed = 20f;
    [Tooltip("전방 대쉬 지속 시간입니다.")]
    public float dashDuration = 0.4f;
    [Tooltip("백대쉬를 사용할 플레이어와의 최소 거리입니다.")]
    public float backDashDistance = 2.0f;
    [Tooltip("백대쉬 속도입니다.")]
    public float backDashSpeed = 15f;
    [Tooltip("백대쉬 지속 시간입니다.")]
    public float backDashDuration = 0.2f;
    [Tooltip("대쉬 (전방/후방) 이후 다음 대쉬까지의 쿨타임입니다.")]
    public float dashCooldown = 3.0f;
    private float lastDashTime = -10f; // 대쉬 쿨다운 계산용

    [Header("공격 패턴")]
    [Tooltip("공격 애니메이션이 응답하지 않을 경우, 강제로 상태를 종료하기까지 대기하는 시간(초)입니다.")]
    public float attackForceFinishTime = 4.0f;
    
    [Header("사운드")]
    public AudioClip attackSound;
    public AudioClip dieSound;
    public AudioClip bossBgm;
    [Tooltip("인스펙터에서 사운드 이름과 클립을 등록하세요.")]
    public List<SoundEffect> soundEffects;

    // --- 내부 컴포넌트 및 상태 변수 ---
    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private AudioSource audioSource;
    private Animator animator;
    private BossHealth bossHealth;
    private CapsuleCollider2D capsuleCollider;
    private Dictionary<int, string> attackIdToNameMap;
    private Dictionary<string, AudioClip> soundDictionary;

    private BossState currentState = BossState.Idle;
    private float lastActionTime = 0f;
    private int lastAttackTriggerID = -1;
    private Vector2 moveDirection = Vector2.zero;
    private bool isDirectionLocked = false;

    // --- Zone 감지 플래그 ---
    private bool isPlayerInTrackingZone = false;
    private bool isPlayerInEngagementZone = false;
    private bool isPlayerInAttackZone = false;

    // --- 애니메이터 파라미터 ID (성능 최적화) ---
    private readonly int IsWalkingAnimID = Animator.StringToHash("isWalking");
    private readonly int StunAnimID = Animator.StringToHash("Stun");
    private readonly int DieAnimID = Animator.StringToHash("Die");
    private List<int> attackTriggerIDs;

    void Awake()
    {
        // 컴포넌트 초기화
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        audioSource = GetComponent<AudioSource>();
        animator = GetComponent<Animator>();
        bossHealth = GetComponent<BossHealth>();
        capsuleCollider = GetComponent<CapsuleCollider2D>();

        attackTriggerIDs = new List<int>();
        attackIdToNameMap = new Dictionary<int, string>();

        // 공격 패턴 등록 (1,2,3,4: 근접, 5: 근접/원거리)
        // S_BossController에서는 모든 공격을 하나의 풀로 관리합니다.
        int[] attackIDs = {
            Animator.StringToHash("Attack1"),
            Animator.StringToHash("Attack2"),
            Animator.StringToHash("Attack3"),
            Animator.StringToHash("Attack4"),
            Animator.StringToHash("Attack5")
        };
        
        for(int i = 0; i < attackIDs.Length; i++)
        {
            attackTriggerIDs.Add(attackIDs[i]);
            attackIdToNameMap[attackIDs[i]] = $"Attack{i+1}";
        }

        // 사운드 딕셔너리 초기화
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

    void Start()
    {
        // 참조 변수들이 제대로 할당되었는지 확인
        if (playerTransform == null)
        {
            var player = FindFirstObjectByType<F_PlayerController>();
            if (player != null) playerTransform = player.transform;
            else Debug.LogError("<b>[S_BossController]</b> 플레이어 Transform을 찾을 수 없습니다! Inspector를 확인해주세요.", this);
        }
        if (bossHealthUI == null) Debug.LogError("<b>[S_BossController]</b> BossHealthUI가 할당되지 않았습니다! Inspector를 확인해주세요.", this);
        if (engagementZoneCollider == null) Debug.LogError("<b>[S_BossController]</b> Engagement Zone Collider가 할당되지 않았습니다! Inspector를 확인해주세요.", this);
        if (attackZoneCollider == null) Debug.LogError("<b>[S_BossController]</b> Attack Zone Collider가 할당되지 않았습니다! Inspector를 확인해주세요.", this);
    }

    void Update()
    {
        if (currentState == BossState.Dead || currentState == BossState.Stunned || currentState == BossState.Attacking || currentState == BossState.Dashing)
        {
            return;
        }

        switch (currentState)
        {
            case BossState.Idle:
                if (isPlayerInTrackingZone)
                {
                    SetState(BossState.Chasing);
                }
                break;

            case BossState.Chasing:
                DecideNextAction();
                break;
        }
    }

    void FixedUpdate()
    {
        if (currentState == BossState.Chasing)
        {
            rb.linearVelocity = new Vector2(moveDirection.x * moveSpeed, rb.linearVelocity.y);
        }
        // Dashing 상태의 이동은 코루틴에서 직접 제어합니다.
        else if (currentState != BossState.Dashing)
        {
            rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
        }
    }

    void SetState(BossState newState)
    {
        if (currentState == newState) return;

        if (enableDebugLogs) Debug.Log($"<color=yellow>상태 변경: {currentState} -> {newState}</color>");

        // --- 이전 상태 종료 로직 ---
        if (currentState == BossState.Attacking)
        {
            isDirectionLocked = false; // 공격이 끝났으므로 방향 전환 잠금을 해제합니다.
        }

        // 상태 변경 직전, 이전 상태의 움직임 초기화
        rb.linearVelocity = Vector2.zero;
        moveDirection = Vector2.zero;
        animator.SetBool(IsWalkingAnimID, false);

        currentState = newState;

        // --- 새로운 상태 진입 시 초기화 로직 ---
        switch (currentState)
        {
            case BossState.Attacking:
                FacePlayer();
                isDirectionLocked = true; // 공격이 시작되면 방향을 고정합니다.
                break;
            case BossState.Chasing:
                // 추적 상태로 돌아올 때마다 플레이어를 바라봅니다.
                FacePlayer();
                break;
        }
    }

    void DecideNextAction()
    {
        if (!isPlayerInTrackingZone)
        {
            SetState(BossState.Idle);
            return;
        }

        bool canPerformAction = Time.time >= lastActionTime + actionCooldown;
        bool canDash = Time.time >= lastDashTime + dashCooldown;
        float distanceToPlayerX = Mathf.Abs(playerTransform.position.x - transform.position.x);

        // --- AI 행동 결정 로직 (우선순위 기반) ---

        // 우선순위 1: 플레이어가 교전 범위(Engagement Zone) 밖에 있고, 대쉬가 가능하면 무조건 대쉬로 접근
        if (!isPlayerInEngagementZone && canDash)
        {
            if (enableDebugLogs) Debug.Log("AI 결정: 플레이어가 멀리 있어 대쉬로 접근합니다.");
            StartCoroutine(DashRoutine(true)); // true: 전방 대쉬
            return;
        }

        // 우선순위 2: 플레이어가 공격 범위(Attack Zone) 안에 있고, 행동이 가능하면 공격 또는 백대쉬
        if (isPlayerInAttackZone && canPerformAction)
        {
            // 2-1: 플레이어가 너무 가까우면(backDashDistance) 백대쉬 시도
            if (distanceToPlayerX < backDashDistance && canDash)
            {
                if (enableDebugLogs) Debug.Log("AI 결정: 플레이어가 너무 가까워 백대쉬로 거리를 둡니다.");
                StartCoroutine(DashRoutine(false)); // false: 후방 대쉬
                return;
            }
            // 2-2: 공격
            else
            {
                if (enableDebugLogs) Debug.Log("AI 결정: 근접 공격 실행");
                PerformAttack();
                return;
            }
        }

        // 우선순위 3: 아무 행동도 하지 않을 경우, 플레이어를 향해 이동
        HandleMovementAndFacing();
    }

    void HandleMovementAndFacing()
    {
        if (playerTransform == null) return;

        float distanceToPlayerX = Mathf.Abs(playerTransform.position.x - transform.position.x);

        // 플레이어와의 거리가 멈춤 거리(stoppingDistance)보다 멀면 추적합니다.
        if (distanceToPlayerX > stoppingDistance)
        {
            moveDirection.x = Mathf.Sign(playerTransform.position.x - transform.position.x);
            animator.SetBool(IsWalkingAnimID, true);
        }
        else // 거리가 충분히 가까우면 멈춤
        {
            moveDirection.x = 0;
            animator.SetBool(IsWalkingAnimID, false);
        }

        FacePlayer();
    }

    void FacePlayer()
    {
        if (playerTransform == null || isDirectionLocked) return;

        if (animator != null && animator.GetCurrentAnimatorStateInfo(0).IsTag("Attack"))
        {
            return;
        }

        bool isPlayerRight = (playerTransform.position.x - transform.position.x) > 0;
        ApplyScale(isPlayerRight);
    }

    void ApplyScale(bool lookRight)
    {
        // [수정] 보스 오브젝트의 기본 스케일 크기(1 이상)를 유지하면서 방향만 바꾸도록 로직을 수정합니다.
        // 이전 로직은 스케일 값을 1 또는 -1로 강제하여, 크기가 조절된 보스에게 예기치 않은 문제를 일으킬 수 있었습니다.

        // 목표로 해야 할 스케일의 부호 결정 (+1 또는 -1)
        float targetSign;
        if (spriteFacesLeft)
        {
            // 기본 스프라이트가 왼쪽을 향할 때: 오른쪽을 보려면 부호가 -1이어야 함
            targetSign = lookRight ? -1f : 1f;
        }
        else
        {
            // 기본 스프라이트가 오른쪽을 향할 때: 왼쪽을 보려면 부호가 -1이어야 함
            targetSign = lookRight ? 1f : -1f;
        }

        // 현재 스케일의 부호와 목표 부호가 다를 경우에만 스케일을 반전시킵니다.
        if (Mathf.Sign(transform.localScale.x) != targetSign)
        {
            transform.localScale = new Vector3(-transform.localScale.x, transform.localScale.y, transform.localScale.z);
        }
    }

    void PerformAttack()
    {
        if (attackTriggerIDs == null || attackTriggerIDs.Count == 0)
        {
            Debug.LogError("실행할 공격이 없습니다! Animator Trigger를 확인하세요.");
            SetState(BossState.Chasing);
            return;
        }
        SetState(BossState.Attacking);
        int randomIndex = Random.Range(0, attackTriggerIDs.Count);
        int chosenAttackTrigger = attackTriggerIDs[randomIndex];
        lastAttackTriggerID = chosenAttackTrigger;
        animator.SetTrigger(lastAttackTriggerID);

        StartCoroutine(ForceFinishAttackRoutine(attackForceFinishTime));
    }

    // --- 외부 호출 함수 (데미지, 사망, 스턴 등) ---
    
    public void TakeDamage(int damage)
    {
        if (currentState == BossState.Dead) return;
        bossHealth.TakeDamage(damage);
    }

    public void Die()
    {
        StopAllCoroutines();
        SetState(BossState.Dead);
        DisableAllAttackHitboxes();
        if (audioSource != null) audioSource.Stop();
        rb.bodyType = RigidbodyType2D.Kinematic;
        if (capsuleCollider != null) capsuleCollider.enabled = false;
        animator.SetTrigger(DieAnimID);
        PlaySound(dieSound);
        StartCoroutine(DelayedDestroyRoutine(2.0f));
    }

    public void GetStunned()
    {
        if (currentState == BossState.Dead) return;
        StopAllCoroutines();
        StartCoroutine(StunRoutine());
    }

    // --- 애니메이션 이벤트 함수 ---

    public void AnimationEvent_AttackFinished()
    {
        if (currentState == BossState.Dead) return;
        StopCoroutine("ForceFinishAttackRoutine");
        lastAttackTriggerID = -1;
        lastActionTime = Time.time;
        SetState(BossState.Chasing);
    }

    public void PlaySoundEffect(string soundName)
    {
        if (soundDictionary != null && soundDictionary.TryGetValue(soundName, out AudioClip clip))
        {
            if (clip != null) audioSource.PlayOneShot(clip);
        }
        else
        {
            Debug.LogError($"[S_BossController] 사운드 '{soundName}'를 찾을 수 없습니다.");
        }
    }

    // --- 코루틴 ---

    private IEnumerator DashRoutine(bool isForwardDash)
    {
        SetState(BossState.Dashing);
        lastDashTime = Time.time; // 쿨다운 타이머 시작
        lastActionTime = Time.time; // 다른 행동 쿨다운도 같이 돌림

        float moveDirection;
        float speed;
        float duration;

        if (isForwardDash)
        {
            // 전방 대쉬: 플레이어를 향해
            moveDirection = Mathf.Sign(playerTransform.position.x - transform.position.x);
            speed = dashSpeed;
            duration = dashDuration;
            // TODO: 전방 대쉬 애니메이션 트리거
            // animator.SetTrigger("ForwardDash");
        }
        else
        {
            // 후방 대쉬: 플레이어의 반대 방향으로
            moveDirection = -Mathf.Sign(playerTransform.position.x - transform.position.x);
            speed = backDashSpeed;
            duration = backDashDuration;
            // TODO: 후방 대쉬 애니메이션 트리거
            // animator.SetTrigger("BackDash");
        }
        
        // [수정] 방향 전환: 대쉬 종류와 관계없이 항상 플레이어를 바라보도록 수정합니다.
        bool playerIsOnTheRight = (playerTransform.position.x - transform.position.x) > 0;
        ApplyScale(playerIsOnTheRight);

        // 대쉬 시작
        rb.linearVelocity = new Vector2(moveDirection * speed, 0);
        
        yield return new WaitForSeconds(duration);

        // 대쉬 종료
        rb.linearVelocity = Vector2.zero;
        
        // 스턴 등으로 상태가 바뀌지 않았다면 추적 상태로 전환
        if (currentState == BossState.Dashing)
        {
            SetState(BossState.Chasing);
        }
    }

    private IEnumerator DelayedDestroyRoutine(float delay)
    {
        yield return new WaitForSeconds(delay);
        #if UNITY_EDITOR
        UnityEditor.Selection.activeObject = null;
        #endif
        Destroy(gameObject);
    }

    private IEnumerator ForceFinishAttackRoutine(float limitTime)
    {
        yield return new WaitForSeconds(limitTime);
        if (currentState == BossState.Attacking)
        {
            string attackName = "알 수 없는 공격";
            if (attackIdToNameMap.TryGetValue(lastAttackTriggerID, out string foundName))
            {
                attackName = foundName;
            }
            Debug.LogWarning($"공격 상태 강제 종료! '{attackName}' 애니메이션에 AnimationEvent_AttackFinished() 이벤트가 누락되었을 수 있습니다.");
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
            SetState(BossState.Chasing);
        }
    }

    // --- Zone 트리거 감지 함수 ---
    // BossZoneTrigger 스크립트에서 이 함수들을 호출하도록 설정해야 합니다.
    public void OnPlayerEnterTrackingZone() 
    { 
        isPlayerInTrackingZone = true; 
        if (enableDebugLogs) Debug.Log("플레이어 진입: TrackingZone"); 
        if (bossHealthUI != null) bossHealthUI.Show();

        if (audioSource != null && bossBgm != null && (audioSource.clip != bossBgm || !audioSource.isPlaying))
        {
            audioSource.clip = bossBgm;
            audioSource.loop = true;
            audioSource.Play();
        }
    }
    public void OnPlayerExitTrackingZone() 
    { 
        isPlayerInTrackingZone = false; 
        isPlayerInEngagementZone = false;
        isPlayerInAttackZone = false; 
        SetState(BossState.Idle);
        if (enableDebugLogs) Debug.Log("플레이어 이탈: TrackingZone");
        if (audioSource != null && audioSource.clip == bossBgm) audioSource.Stop();
    }
    public void OnPlayerEnterEngagementZone() { isPlayerInEngagementZone = true; if (enableDebugLogs) Debug.Log("플레이어 진입: EngagementZone"); }
    public void OnPlayerExitEngagementZone() { isPlayerInEngagementZone = false; if (enableDebugLogs) Debug.Log("플레이어 이탈: EngagementZone"); }
    public void OnPlayerEnterAttackZone() { isPlayerInAttackZone = true; if (enableDebugLogs) Debug.Log("플레이어 진입: AttackZone"); }
    public void OnPlayerExitAttackZone() { isPlayerInAttackZone = false; if (enableDebugLogs) Debug.Log("플레이어 이탈: AttackZone"); }

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
        T_BossHitbox[] hitboxes = GetComponentsInChildren<T_BossHitbox>();
        foreach (var hitbox in hitboxes)
        {
            if (hitbox.gameObject != gameObject)
            {
                hitbox.gameObject.SetActive(false);
            }
            else
            {
                hitbox.enabled = false;
                Collider2D col = hitbox.GetComponent<Collider2D>();
                if (col != null) col.enabled = false;
            }
        }
    }
}