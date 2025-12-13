using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(BossHealth))]
public class T_BossController : MonoBehaviour
{
    private enum BossState
    {
        Idle,        // 대기 (플레이어가 인식 범위 밖에 있음)
        Deciding,    // 행동 결정 (핵심 AI 로직)
        Teleporting, // 텔레포트 중
        Attacking,   // 공격 중
        Stunned,     // 기절
        Dead         // 사망
    }

    // 텔레포트 목적지 정의
    private enum TeleportDestination { ShortZone, LongZone, BehindPlayer }

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
    [Tooltip("짧은 공격 Zone의 Collider 2D를 할당해야 합니다. 텔레포트 위치 계산에 사용됩니다.")]
    public Collider2D shortAttackZoneCollider;
    [Tooltip("긴 공격 Zone의 Collider 2D를 할당해야 합니다.")]
    public Collider2D longAttackZoneCollider;

    [Header("기본 설정")]
    [Tooltip("스프라이트가 기본적으로 왼쪽을 보는지 설정합니다.")]
    public bool spriteFacesLeft = true;

    [Header("능력치")]
    public float moveSpeed = 2f;
    [Tooltip("다음 행동까지의 최소 대기 시간입니다.")]
    public float actionCooldown = 1.0f;
    [Tooltip("이동을 멈출 거리입니다. Short_Attack_Zone 경계보다 약간 멀게 설정하는 것을 권장합니다.")]
    [SerializeField] private float stoppingDistance = 7.0f;
    [Tooltip("플레이어가 근접했을 때, 공격 대신 텔레포트로 후퇴할 확률입니다.")]
    [Range(0f, 1f)] public float retreatTeleportChance = 0.4f;

    [Header("공격 패턴")]
    [Tooltip("공격 애니메이션이 응답하지 않을 경우, 강제로 상태를 종료하기까지 대기하는 시간(초)입니다.")]
    public float attackForceFinishTime = 4.0f;
    [Tooltip("3번 공격(대쉬)의 속도입니다.")]
    public float dashAttackSpeed = 15f;
    [Tooltip("3번 공격(대쉬)의 지속 시간입니다.")]
    public float dashAttackDuration = 0.5f;

    [Header("사운드")]
    public AudioClip attackSound;
    public AudioClip dieSound;
    public AudioClip bossBgm; // BGM 추가
    [Tooltip("인스펙터에서 사운드 이름과 클립을 등록하세요.")]
    public List<SoundEffect> soundEffects;

    [Header("텔레포트")]
    [Tooltip("텔레포트 애니메이션이 사라지는 데 걸리는 시간입니다.")]
    public float teleportAnimOutDuration = 0.3f;
    [Tooltip("텔레포트 애니메이션이 나타나는 데 걸리는 시간입니다.")]
    public float teleportAnimInDuration = 0.3f;
    [Tooltip("텔레포트 후 행동을 시작하기까지의 딜레이입니다.")]
    public float postTeleportDelay = 0.3f;
    [Tooltip("전투 중 플레이어가 원거리에 있을 때, 공격 대신 텔레포트로 접근할 확률입니다.")]
    [Range(0f, 1f)]
    public float combatTeleportChance = 0.3f;
    [Tooltip("전투 중 텔레포트 시, 플레이어 뒤로 갈 확률입니다.")]
    [Range(0f, 1f)]
    public float teleportBehindChance = 0.5f;
    [Tooltip("플레이어 뒤로 텔레포트할 때의 거리 오프셋입니다.")]
    public float teleportBehindPlayerOffset = 3.0f;
    [Tooltip("텔레포트 시 플레이어와 유지할 최소 거리입니다.")]
    public float minTeleportPlayerDistance = 4.0f;

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
    private int lastAttackTriggerID = -1; // 디버깅용: 마지막으로 실행된 공격의 해시 ID
    private Vector2 moveDirection = Vector2.zero;
    private bool isDashMoving = false; // 대쉬 공격 중 실제 이동 플래그
    private Vector2 dashDirection = Vector2.zero; // 대쉬 방향
    private bool isDirectionLocked = false; // 공격 중 방향 전환 방지 플래그

    // --- Zone 감지 플래그 ---
    private bool isPlayerInTrackingZone = false;
    private bool isPlayerInLongAttackZone = false;
    private bool isPlayerInShortAttackZone = false;

    // --- 애니메이터 파라미터 ID (성능 최적화) ---
    private readonly int IsWalkingAnimID = Animator.StringToHash("isWalking");
    private readonly int StunAnimID = Animator.StringToHash("Stun");
    private readonly int DieAnimID = Animator.StringToHash("Die");
    private readonly int TeleportOutTriggerID = Animator.StringToHash("TP");
    private readonly int TeleportInTriggerID = Animator.StringToHash("TP_END");
    private readonly int Attack3TriggerID = Animator.StringToHash("Attack3");
    private List<int> shortAttackTriggerIDs;
    private List<int> longAttackTriggerIDs;


    void Awake()
    {
        // 컴포넌트 초기화
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        audioSource = GetComponent<AudioSource>();
        animator = GetComponent<Animator>();
        bossHealth = GetComponent<BossHealth>();
        capsuleCollider = GetComponent<CapsuleCollider2D>();

        shortAttackTriggerIDs = new List<int>();
        longAttackTriggerIDs = new List<int>();

        attackIdToNameMap = new Dictionary<int, string>();

        // 근거리 공격 패턴 등록
        int attack1Id = Animator.StringToHash("Attack1");
        shortAttackTriggerIDs.Add(attack1Id);
        attackIdToNameMap[attack1Id] = "Attack1";

        int attack2Id = Animator.StringToHash("Attack2");
        shortAttackTriggerIDs.Add(attack2Id);
        attackIdToNameMap[attack2Id] = "Attack2";

        shortAttackTriggerIDs.Add(Attack3TriggerID);
        attackIdToNameMap[Attack3TriggerID] = "Attack3";

        // 원거리 공격 패턴 등록
        int attack4Id = Animator.StringToHash("Attack4");
        longAttackTriggerIDs.Add(attack4Id);
        attackIdToNameMap[attack4Id] = "Attack4";

        int attack5Id = Animator.StringToHash("Attack5");
        longAttackTriggerIDs.Add(attack5Id);
        attackIdToNameMap[attack5Id] = "Attack5";

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
        if (playerTransform == null) Debug.LogError("<b>[T_BossController]</b> 플레이어 Transform이 할당되지 않았습니다! Inspector를 확인해주세요.", this);
        if (bossHealthUI == null) Debug.LogError("<b>[T_BossController]</b> BossHealthUI가 할당되지 않았습니다! Inspector를 확인해주세요.", this);
        if (shortAttackZoneCollider == null) Debug.LogError("<b>[T_BossController]</b> Short Attack Zone Collider가 할당되지 않았습니다! Inspector를 확인해주세요.", this);
        if (longAttackZoneCollider == null) Debug.LogError("<b>[T_BossController]</b> Long Attack Zone Collider가 할당되지 않았습니다! Inspector를 확인해주세요.", this);
    }

    void Update()
    {
        // 특정 상태에서는 AI 로직을 실행하지 않음
        if (currentState == BossState.Dead || currentState == BossState.Stunned || currentState == BossState.Attacking || currentState == BossState.Teleporting)
        {
            return;
        }

        switch (currentState)
        {
            case BossState.Idle:
                if (isPlayerInTrackingZone)
                {
                    SetState(BossState.Deciding);
                }
                break;

            case BossState.Deciding:
                DecideNextAction();
                break;
        }
    }

    void FixedUpdate()
    {
        // 물리 이동은 상태에 따라 다르게 처리
        if (currentState == BossState.Deciding)
        {
            rb.linearVelocity = new Vector2(moveDirection.x * moveSpeed, rb.linearVelocity.y);
        }
        else if (currentState == BossState.Attacking && isDashMoving) // 대쉬 공격 중일 때
        {
            rb.linearVelocity = new Vector2(dashDirection.x * dashAttackSpeed, rb.linearVelocity.y);
        }
        else
        {
            rb.linearVelocity = Vector2.zero; // 다른 상태에서는 확실히 멈춤
        }
    }

    void SetState(BossState newState)
    {
        if (currentState == newState) return;

        if (enableDebugLogs) Debug.Log($"<color=cyan>상태 변경: {currentState} -> {newState}</color>");

        // --- 이전 상태 종료 로직 ---
        if (currentState == BossState.Attacking)
        {
            isDashMoving = false; // 공격 상태를 벗어날 때 돌진 플래그를 확실히 끕니다.
            isDirectionLocked = false; // 공격이 끝났으므로 방향 전환 잠금을 해제합니다.
        }

        // 상태 변경 직전, 이전 상태의 움직임 초기화
        rb.linearVelocity = Vector2.zero;
        moveDirection = Vector2.zero;
        animator.SetBool(IsWalkingAnimID, false);

        currentState = newState;

        // 새로운 상태 진입 시 초기화 로직
        switch (currentState)
        {
            case BossState.Attacking:
                FacePlayer();
                isDirectionLocked = true; // 공격이 시작되면 방향을 고정합니다.
                break;
        }
    }

    void DecideNextAction()
    {
        // 플레이어가 추적 존을 벗어나면 다시 대기 상태로
        if (!isPlayerInTrackingZone)
        {
            SetState(BossState.Idle);
            return;
        }

        // 행동 쿨다운이 지났는지 확인합니다. 텔레포트와 공격이 이 쿨다운을 공유합니다.
        bool canPerformAction = Time.time >= lastActionTime + actionCooldown;

        // --- AI 행동 결정 로직 (우선순위 기반) ---

        // --- 행동 결정 (쿨다운이 준비되었을 때만) ---
        if (canPerformAction)
        {
            // 우선순위 1: 플레이어가 너무 가까움 -> 공격 또는 후퇴를 확률적으로 결정
            if (isPlayerInShortAttackZone)
            {
                if (Random.value < retreatTeleportChance)
                {
                    if (enableDebugLogs) Debug.Log("AI 결정: 근접 상태에서 텔레포트로 후퇴 (확률적)");
                    StartCoroutine(TeleportRoutine(TeleportDestination.LongZone));
                }
                else
                {
                    if (enableDebugLogs) Debug.Log("AI 결정: 근접 상태에서 공격 (확률적)");
                    if (shortAttackTriggerIDs.Count > 0)
                    {
                        PerformAttack(shortAttackTriggerIDs);
                    }
                    else
                    {
                        Debug.LogWarning("근접 공격을 시도했지만, 설정된 근접 공격 패턴이 없습니다!");
                    }
                }
                return; // 행동 결정 완료
            }

            // 우선순위 2: 플레이어가 원거리 공격 존에 있음 (근접 존에는 없음) -> 원거리 공격
            if (isPlayerInLongAttackZone)
            {
                if (Random.value < combatTeleportChance)
                {
                    if (enableDebugLogs) Debug.Log("AI 결정: 원거리에서 텔레포트로 접근 (확률적)");

                    // [수정] 설정된 확률에 따라 플레이어 뒤 또는 ShortZone으로 텔레포트
                    if (Random.value < teleportBehindChance)
                    {
                        if (enableDebugLogs) Debug.Log("... 목표: 플레이어 뒤");
                        StartCoroutine(TeleportRoutine(TeleportDestination.BehindPlayer));
                    }
                    else
                    {
                        if (enableDebugLogs) Debug.Log("... 목표: 근접 공격 존");
                        StartCoroutine(TeleportRoutine(TeleportDestination.ShortZone));
                    }
                }
                else
                {
                    if (enableDebugLogs) Debug.Log("AI 결정: 원거리 공격");
                    if (longAttackTriggerIDs.Count > 0)
                    {
                        PerformAttack(longAttackTriggerIDs);
                    }
                    else
                    {
                        Debug.LogWarning("원거리 공격을 시도했지만, 설정된 원거리 공격 패턴이 없습니다!");
                    }
                }
                return; // 행동 결정 완료
            }

            // 우선순위 3: 플레이어가 일반 공격 사거리(stoppingDistance) 내에 있음 -> 근거리 공격 (Zone 트리거의 보조 역할)
            float distanceToPlayerX = Mathf.Abs(playerTransform.position.x - transform.position.x);
            if (distanceToPlayerX <= stoppingDistance)
            {
                if (enableDebugLogs) Debug.Log("AI 결정: 근거리 공격 (Stopping Distance)");
                if (shortAttackTriggerIDs.Count > 0)
                {
                    PerformAttack(shortAttackTriggerIDs);
                }
                else
                {
                     Debug.LogWarning("근접 공격을 시도했지만, 설정된 근접 공격 패턴이 없습니다!");
                }
                return; // 행동 결정 완료
            }
        }

        // --- 이동 결정 (행동을 하지 않았을 경우) ---
        if (isPlayerInShortAttackZone) // 쿨다운 중인데 플레이어가 가까이 있다면, 이동을 멈추고 대기
        {
            moveDirection.x = 0;
            animator.SetBool(IsWalkingAnimID, false);
            FacePlayer();
        }
        else // 그 외의 경우, 플레이어를 추적하거나 멈춤
        {
            HandleMovementAndFacing();
        }
    }
    
    /// <summary>
    /// 플레이어를 향해 이동하거나, 사정거리 내에 있으면 멈춥니다.
    /// </summary>
    void HandleMovementAndFacing()
    {
        if (playerTransform == null) return;

        float distanceToPlayerX = Mathf.Abs(playerTransform.position.x - transform.position.x);

        // 플레이어와의 거리가 공격 사거리(stoppingDistance)보다 멀면 추적합니다.
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
        if (playerTransform == null || isDirectionLocked) return; // 방향이 고정되어 있으면 실행하지 않음
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

    void PerformAttack(List<int> attackPool)
    {
        if (attackPool == null || attackPool.Count == 0)
        {
            Debug.LogError("실행할 공격이 없습니다! Animator Trigger를 확인하세요.");
            SetState(BossState.Deciding); // 공격 실패 시 다시 결정 상태로 돌아갑니다.
            return;
        }
        SetState(BossState.Attacking);
        int randomIndex = Random.Range(0, attackPool.Count);
        int chosenAttackTrigger = attackPool[randomIndex];
        lastAttackTriggerID = chosenAttackTrigger; // 마지막 공격 ID 저장
        animator.SetTrigger(lastAttackTriggerID);

        // 3번 공격일 경우, 돌진 코루틴을 실행합니다.
        if (chosenAttackTrigger == Attack3TriggerID)
        {
            StartCoroutine(DashAttackRoutine());
        }
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
        rb.bodyType = RigidbodyType2D.Kinematic;
        if (capsuleCollider != null) capsuleCollider.enabled = false;
        animator.SetTrigger(DieAnimID);
        if (audioSource != null) audioSource.Stop(); // 사망 시 BGM 정지
        PlaySound(dieSound);
        Destroy(gameObject, 2.0f);
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

        // 공격이 정상적으로 끝났으므로, 강제 종료 코루틴을 중지시킵니다.
        StopCoroutine("ForceFinishAttackRoutine");
        lastAttackTriggerID = -1; // 공격이 끝났으므로 ID 초기화
        lastActionTime = Time.time;
        SetState(BossState.Deciding);
    }

    /// 애니메이션 이벤트에서 호출되어 실제 돌진 이동을 시작합니다.
    /// </summary>
    public void AnimationEvent_StartDashMovement()
    {
        if (currentState == BossState.Attacking) isDashMoving = true;
    }

    public void PlaySoundEffect(string soundName)
    {
        // 1. 호출 확인 로그
        Debug.Log($"[T_BossController] PlaySoundEffect 호출됨: {soundName}");

        // 3. 예외 처리: AudioSource 확인
        if (audioSource == null)
        {
            Debug.LogError("[T_BossController] AudioSource 컴포넌트가 없습니다!");
            return;
        }

        // 2. 사운드 찾기 및 재생
        if (soundDictionary != null && soundDictionary.TryGetValue(soundName, out AudioClip clip))
        {
            if (clip != null) audioSource.PlayOneShot(clip);
            else Debug.LogError($"[T_BossController] '{soundName}'에 해당하는 AudioClip이 비어있습니다.");
        }
        else
        {
            // 3. 예외 처리: 이름 없음
            Debug.LogError($"[T_BossController] 사운드 '{soundName}'를 찾을 수 없습니다. Inspector의 Sound Effects 리스트를 확인하세요.");
        }
    }

    // --- 코루틴 ---

    private IEnumerator DashAttackRoutine()
    {
        // 1. 돌진 준비
        isDashMoving = false; // 이동 플래그 초기화

        // 보스가 바라보는 방향으로 돌진 방향을 결정합니다.
        float facingDir = Mathf.Sign(transform.localScale.x);
        if (spriteFacesLeft)
        {
            facingDir *= -1;
        }
        dashDirection = new Vector2(facingDir, 0);

        // 2. 애니메이션 이벤트가 isDashMoving을 true로 바꿀 때까지 대기
        // (또는 공격 상태가 아니게 되면 즉시 종료)
        yield return new WaitUntil(() => isDashMoving || currentState != BossState.Attacking);

        // 3. 상태가 바뀌었다면(예: 스턴) 코루틴 중단
        if (currentState != BossState.Attacking)
        {
            yield break;
        }

        // 4. 실제 돌진 이동 시간만큼 대기
        yield return new WaitForSeconds(dashAttackDuration);

        // 5. 돌진 종료
        if (currentState == BossState.Attacking)
        {
            isDashMoving = false;
            // FixedUpdate가 속도를 0으로 만들어 줄 것입니다.
        }
    }

    private Vector2? GetRandomTeleportPositionInZone(Collider2D zoneCollider, string zoneName, float minDistance, int maxRetries)
    {
        if (zoneCollider == null)
        {
            Debug.LogError($"{zoneName} 콜라이더가 없어 텔레포트할 수 없습니다!");
            return null;
        }

        for (int i = 0; i < maxRetries; i++)
        {
            Bounds zoneBounds = zoneCollider.bounds;
            float randomX = Random.Range(zoneBounds.min.x, zoneBounds.max.x);
            Vector2 potentialPosition = new Vector2(randomX, transform.position.y);

            // 플레이어와의 최소 거리 체크
            if (playerTransform != null && Vector2.Distance(potentialPosition, playerTransform.position) < minDistance)
            {
                // 마지막 시도라면 그냥 이 위치를 사용
                if (i == maxRetries - 1)
                {
                    if (enableDebugLogs) Debug.LogWarning($"텔레포트 위치 재시도({zoneName}) 실패, 마지막 위치 사용.");
                    return potentialPosition;
                }
                continue; // 너무 가까우면 다시 시도
            }
            return potentialPosition; // 적절한 위치를 찾았으면 반환
        }
        return null; // Should not be reached
    }

    private IEnumerator TeleportRoutine(TeleportDestination destination)
    {
        SetState(BossState.Teleporting);

        // 1. 텔레포트 시작 (사라지기)
        animator.SetTrigger(TeleportOutTriggerID);
        yield return new WaitForSeconds(teleportAnimOutDuration);

        // 2. 목표 위치 계산
        Vector2? targetPosition = null;
        const int maxRetries = 10;

        if (destination == TeleportDestination.BehindPlayer)
        {
            if (playerTransform != null && playerTransform.GetComponent<SpriteRenderer>() != null)
            {
                SpriteRenderer playerSprite = playerTransform.GetComponent<SpriteRenderer>();
                float behindDirection = playerSprite.flipX ? 1f : -1f;
                targetPosition = new Vector2(playerTransform.position.x + (behindDirection * teleportBehindPlayerOffset), transform.position.y);
            }
            else
            {
                Debug.LogWarning("플레이어 뒤로 텔레포트 실패. ShortZone으로 대체합니다.");
                destination = TeleportDestination.ShortZone;
            }
        }

        // BehindPlayer가 아니었거나, 실패했을 경우 Zone으로 텔레포트
        if (destination == TeleportDestination.ShortZone)
        {
            targetPosition = GetRandomTeleportPositionInZone(shortAttackZoneCollider, "ShortAttackZone", minTeleportPlayerDistance, maxRetries);
        }
        else if (destination == TeleportDestination.LongZone)
        {
            targetPosition = GetRandomTeleportPositionInZone(longAttackZoneCollider, "LongAttackZone", minTeleportPlayerDistance, maxRetries);
        }

        if (targetPosition == null)
        {
            Debug.LogError("텔레포트 위치를 결정할 수 없어 취소합니다.");
            SetState(BossState.Deciding);
            yield break;
        }

        // 2.5. 위치 이동
        transform.position = targetPosition.Value;

        // 3. 텔레포트 종료 (나타나기)
        FacePlayer();
        animator.SetTrigger(TeleportInTriggerID);
        yield return new WaitForSeconds(teleportAnimInDuration);

        // 4. 텔레포트 후 딜레이
        yield return new WaitForSeconds(postTeleportDelay);

        // 5. 텔레포트 후 즉시 공격
        if (enableDebugLogs) Debug.Log("텔레포트 후 즉시 공격 실행.");

        // 텔레포트 목적지에 따라 다른 공격 수행
        if (destination == TeleportDestination.LongZone)
        {
            if (longAttackTriggerIDs.Count > 0)
            {
                PerformAttack(longAttackTriggerIDs);
            }
            else
            {
                Debug.LogWarning("텔레포트 후 원거리 공격을 시도했지만, 설정된 원거리 공격 패턴이 없습니다!");
                SetState(BossState.Deciding);
            }
        }
        else // ShortZone or BehindPlayer
        {
            if (shortAttackTriggerIDs.Count > 0)
            {
                PerformAttack(shortAttackTriggerIDs);
            }
            else
            {
                Debug.LogWarning("텔레포트 후 근접 공격을 시도했지만, 설정된 근접 공격 패턴이 없습니다!");
                SetState(BossState.Deciding);
            }
        }
    }

    private IEnumerator ForceFinishAttackRoutine(float limitTime)
    {
        yield return new WaitForSeconds(limitTime);
        if (currentState == BossState.Attacking)
        {
            string attackName = "알 수 없는 공격"; // 기본값
            if (attackIdToNameMap.TryGetValue(lastAttackTriggerID, out string foundName))
            {
                attackName = foundName;
            }
            
            Debug.LogWarning($"공격 상태 강제 종료! '{attackName}' 애니메이션에 AnimationEvent_AttackFinished() 이벤트가 누락되었을 수 있습니다. 애니메이션 클립을 확인해주세요.");
            AnimationEvent_AttackFinished();
        }
    }

    private IEnumerator StunRoutine()
    {
        SetState(BossState.Stunned);
        animator.SetTrigger(StunAnimID);
        yield return new WaitForSeconds(1.5f); // 스턴 지속 시간
        if (currentState == BossState.Stunned)
        {
            SetState(BossState.Deciding);
        }
    }

    // --- Zone 트리거 감지 함수 ---
    public void OnPlayerEnterTrackingZone() 
    { 
        isPlayerInTrackingZone = true; 
        if (enableDebugLogs) Debug.Log("플레이어 진입: TrackingZone"); 
        if (bossHealthUI != null) bossHealthUI.Show();

        // BGM 재생 로직
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
        isPlayerInLongAttackZone = false;
        isPlayerInShortAttackZone = false; 

        if (enableDebugLogs) Debug.Log("플레이어 이탈: TrackingZone");

        // 범위 이탈 시 BGM 정지
        if (audioSource != null && audioSource.clip == bossBgm) audioSource.Stop();

        // 로직 3: 플레이어가 추적 존을 벗어나면 근거리 공격 존으로 텔레포트합니다.
        // 단, 다른 행동(공격, 스턴 등) 중이 아닐 때만 실행합니다.
        if (currentState != BossState.Dead && currentState != BossState.Stunned && currentState != BossState.Attacking)
        {
            // 텔레포트도 다른 행동과 쿨다운을 공유합니다.
            if (Time.time >= lastActionTime + actionCooldown)
            {
                if (enableDebugLogs) Debug.Log("AI 결정: 플레이어 이탈로 인한 리셋 텔레포트");
                StartCoroutine(TeleportRoutine(TeleportDestination.ShortZone));
            }
            else
            {
                SetState(BossState.Idle); // 쿨다운 중이면 그냥 대기 상태로 전환
            }
        }
    }
    public void OnPlayerEnterLongAttackZone() { isPlayerInLongAttackZone = true; if (enableDebugLogs) Debug.Log("플레이어 진입: LongAttackZone"); }
    public void OnPlayerExitLongAttackZone() { isPlayerInLongAttackZone = false; if (enableDebugLogs) Debug.Log("플레이어 이탈: LongAttackZone"); }
    public void OnPlayerEnterShortAttackZone() { isPlayerInShortAttackZone = true; if (enableDebugLogs) Debug.Log("플레이어 진입: ShortAttackZone"); }
    public void OnPlayerExitShortAttackZone() { isPlayerInShortAttackZone = false; if (enableDebugLogs) Debug.Log("플레이어 이탈: ShortAttackZone"); }

    // --- 유틸리티 ---
    private void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }

#if UNITY_EDITOR
    // --- 에디터 전용 검증 로직 ---
    private void OnValidate()
    {
        // 인스펙터에서 참조가 할당될 때마다 호출되어, 잘못된 참조를 검사합니다.
        CheckReference(playerTransform, "Player Transform");
        CheckReference(bossHealthUI, "Boss Health UI");
        CheckReference(shortAttackZoneCollider, "Short Attack Zone Collider");
        CheckReference(longAttackZoneCollider, "Long Attack Zone Collider");
    }

    private void CheckReference(Object obj, string fieldName)
    {
        if (obj == null) return;

        // 'DontSaveInEditor' 플래그가 설정된 오브젝트가 할당되었는지 확인합니다.
        if ((obj.hideFlags & HideFlags.DontSaveInEditor) != 0)
        {
            Debug.LogWarning($"<b>[{this.GetType().Name}]</b> '{fieldName}' 필드에 에디터에 저장할 수 없는 임시 오브젝트(<b>'{obj.name}'</b>)가 할당되었습니다. " +
                             "이는 씬 저장 시 문제를 일으킬 수 있습니다. 씬에 있는 유효한 오브젝트나 프로젝트 에셋을 할당해주세요.", this);
        }
    }
#endif
}