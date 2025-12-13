using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
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
    private enum TeleportDestination { ShortZone, LongZone }

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
    [Tooltip("긴 공격 Zone의 Collider 2D를 할당해야 합니다. 텔레포트 위치 계산에 사용됩니다.")]
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

    [Header("텔레포트")]
    [Tooltip("텔레포트 재사용 대기시간입니다.")]
    public float teleportCooldown = 6.0f;
    public float teleportAnimOutDuration = 0.3f;
    public float teleportAnimInDuration = 0.3f;
    public float postTeleportDelay = 0.3f;

    [Header("공격 패턴")]
    [Tooltip("공격 애니메이션이 응답하지 않을 경우, 강제로 상태를 종료하기까지 대기하는 시간(초)입니다.")]
    public float attackForceFinishTime = 4.0f;

    [Header("사운드")]
    public AudioClip attackSound;
    public AudioClip dieSound;
    public AudioClip bossBgm; // BGM 추가
    [Tooltip("인스펙터에서 사운드 이름과 클립을 등록하세요.")]
    public List<SoundEffect> soundEffects;

    // --- 내부 컴포넌트 및 상태 변수 ---
    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private AudioSource audioSource;
    private Animator animator;
    private BossHealth bossHealth;
    private CapsuleCollider2D capsuleCollider;
    private Dictionary<string, AudioClip> soundDictionary;

    private BossState currentState = BossState.Idle;
    private float lastActionTime = 0f;
    private float lastTeleportTime = -99f;
    private Vector2 moveDirection = Vector2.zero;

    // --- Zone 감지 플래그 ---
    private bool isPlayerInTrackingZone = false;
    private bool isPlayerInLongAttackZone = false;
    private bool isPlayerInShortAttackZone = false;
    private bool isPlayerInCloseZone = false;

    // --- 애니메이터 파라미터 ID (성능 최적화) ---
    private readonly int IsWalkingAnimID = Animator.StringToHash("isWalking");
    private readonly int StunAnimID = Animator.StringToHash("Stun");
    private readonly int DieAnimID = Animator.StringToHash("Die");
    private readonly int TeleportOutTriggerID = Animator.StringToHash("TP");
    private readonly int TeleportInTriggerID = Animator.StringToHash("TP_END");
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

        // 공격 애니메이션 ID 초기화
        // Animator에 실제 사용하는 공격 Trigger 이름을 추가하세요.
        shortAttackTriggerIDs = new List<int>
        {
            Animator.StringToHash("Attack1"),
            Animator.StringToHash("Attack2"),
            Animator.StringToHash("Attack3"),
        };
        longAttackTriggerIDs = new List<int>
        {
            Animator.StringToHash("Attack4"),
            Animator.StringToHash("Attack5"),
        };

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
        // 물리 이동은 Deciding 상태에서만 처리
        if (currentState == BossState.Deciding)
        {
            rb.linearVelocity = new Vector2(moveDirection.x * moveSpeed, rb.linearVelocity.y);
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

        // 상태 변경 직전, 이전 상태의 움직임 초기화
        rb.linearVelocity = Vector2.zero;
        moveDirection = Vector2.zero;
        animator.SetBool(IsWalkingAnimID, false);

        currentState = newState;

        // 새로운 상태 진입 시 초기화 로직
        switch (currentState)
        {
            case BossState.Attacking:
                rb.linearVelocity = Vector2.zero; // 만약을 위해 속도 재설정
                FacePlayer();
                break;
            case BossState.Idle:
            case BossState.Deciding:
            case BossState.Teleporting:
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

        bool canPerformAction = Time.time >= lastActionTime + actionCooldown;
        bool canTeleport = Time.time >= lastTeleportTime + teleportCooldown;

        // --- AI 행동 결정 로직 (우선순위 기반) ---

        // 우선순위 1: 긴급 텔레포트 (규칙 D: 플레이어가 너무 가까울 때)
        if (isPlayerInCloseZone && canTeleport)
        {
            if (enableDebugLogs) Debug.Log("AI 결정: 너무 가까워서 후방 텔레포트 (규칙 D)");
            StartCoroutine(TeleportRoutine(TeleportDestination.LongZone));
            return;
        }

        // 우선순위 2: 짧은 공격 (규칙 3: Short_Attack_Zone에 있을 때)
        if (isPlayerInShortAttackZone && canPerformAction)
        {
            if (enableDebugLogs) Debug.Log("AI 결정: 짧은 공격 (규칙 3)");
            PerformAttack(shortAttackTriggerIDs);
            return;
        }

        // 우선순위 3: 긴 공격 (규칙 4: Long_Attack_Zone에 있을 때)
        if (isPlayerInLongAttackZone && !isPlayerInShortAttackZone && canPerformAction)
        {
            if (enableDebugLogs) Debug.Log("AI 결정: 긴 공격 (규칙 4)");
            PerformAttack(longAttackTriggerIDs);
            return;
        }

        // 우선순위 4: 일반 텔레포트 (규칙 A, B, C)
        if (canTeleport)
        {
            // 규칙 C: Short Zone에 있을 때 (Close Zone 제외)
            if (isPlayerInShortAttackZone)
            {
                TeleportDestination dest = (Random.value > 0.5f) ? TeleportDestination.LongZone : TeleportDestination.ShortZone;
                if (enableDebugLogs) Debug.Log($"AI 결정: Short Zone에서 랜덤 텔레포트 -> {dest} (규칙 C)");
                StartCoroutine(TeleportRoutine(dest));
                return;
            }
            // 규칙 B: Long Zone에 있을 때
            else if (isPlayerInLongAttackZone)
            {
                if (enableDebugLogs) Debug.Log("AI 결정: Long Zone에서 Short Zone으로 텔레포트 (규칙 B)");
                StartCoroutine(TeleportRoutine(TeleportDestination.ShortZone));
                return;
            }
            // 규칙 A: 공격 Zone 밖에 있을 때
            else if (isPlayerInTrackingZone)
            {
                if (enableDebugLogs) Debug.Log("AI 결정: 공격 Zone 밖에서 Long Zone으로 텔레포트 (규칙 A)");
                StartCoroutine(TeleportRoutine(TeleportDestination.LongZone));
                return;
            }
        }

        // 우선순위 5: 이동 (규칙 2) - 위의 어떤 행동도 하지 않을 경우
        if (enableDebugLogs) Debug.Log("AI 결정: 이동 (규칙 2)");
        HandleMovementAndFacing();
    }

    void HandleMovementAndFacing()
    {
        if (playerTransform == null) return;

        float distanceToPlayerX = Mathf.Abs(playerTransform.position.x - transform.position.x);

        // 플레이어와의 거리가 stoppingDistance보다 멀면 이동
        if (Mathf.Abs(distanceToPlayerX) > stoppingDistance)
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
        if (playerTransform == null) return;
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
            return;
        }
        SetState(BossState.Attacking);
        int randomIndex = Random.Range(0, attackPool.Count);
        animator.SetTrigger(attackPool[randomIndex]);
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
        lastActionTime = Time.time;
        SetState(BossState.Deciding);
    }

    public void AnimationEvent_PlayAttackSound()
    {
        PlaySound(attackSound);
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

    private IEnumerator TeleportRoutine(TeleportDestination destination)
    {
        SetState(BossState.Teleporting);

        // 1. 텔레포트 시작 (사라지기)
        animator.SetTrigger(TeleportOutTriggerID);
        yield return new WaitForSeconds(teleportAnimOutDuration);

        // 2. 목표 위치 계산 및 이동 (Zone 내부의 랜덤 위치)
        Collider2D targetZoneCollider = (destination == TeleportDestination.ShortZone) ? shortAttackZoneCollider : longAttackZoneCollider;
        
        // 이중 확인
        if (targetZoneCollider == null)
        {
            Debug.LogError($"텔레포트 목적지 Zone 콜라이더({destination})가 설정되지 않았습니다! 텔레포트를 취소합니다.");
            SetState(BossState.Deciding);
            yield break;
        }

        Bounds zoneBounds = targetZoneCollider.bounds;
        float randomX = Random.Range(zoneBounds.min.x, zoneBounds.max.x);
        Vector2 targetPosition = new Vector2(randomX, transform.position.y);

        transform.position = targetPosition;

        // 3. 텔레포트 종료 (나타나기)
        FacePlayer();
        animator.SetTrigger(TeleportInTriggerID);
        yield return new WaitForSeconds(teleportAnimInDuration);

        // 4. 텔레포트 후 딜레이
        yield return new WaitForSeconds(postTeleportDelay);

        lastTeleportTime = Time.time;
        SetState(BossState.Deciding);
    }

    private IEnumerator ForceFinishAttackRoutine(float limitTime)
    {
        yield return new WaitForSeconds(limitTime);
        if (currentState == BossState.Attacking)
        {
            Debug.LogWarning("공격 상태 강제 종료! 애니메이션 이벤트가 호출되지 않았을 수 있습니다. 애니메이션 클립을 확인해주세요.");
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
        isPlayerInTrackingZone = false; isPlayerInLongAttackZone = false; isPlayerInShortAttackZone = false; isPlayerInCloseZone = false; 
        SetState(BossState.Idle); 
        if (enableDebugLogs) Debug.Log("플레이어 이탈: TrackingZone");
        if (audioSource != null && audioSource.clip == bossBgm) audioSource.Stop(); // 범위 이탈 시 BGM 정지
    }
    public void OnPlayerEnterLongAttackZone() { isPlayerInLongAttackZone = true; if (enableDebugLogs) Debug.Log("플레이어 진입: LongAttackZone"); }
    public void OnPlayerExitLongAttackZone() { isPlayerInLongAttackZone = false; if (enableDebugLogs) Debug.Log("플레이어 이탈: LongAttackZone"); }
    public void OnPlayerEnterShortAttackZone() { isPlayerInShortAttackZone = true; if (enableDebugLogs) Debug.Log("플레이어 진입: ShortAttackZone"); }
    public void OnPlayerExitShortAttackZone() { isPlayerInShortAttackZone = false; if (enableDebugLogs) Debug.Log("플레이어 이탈: ShortAttackZone"); }
    public void OnPlayerEnterCloseZone() { isPlayerInCloseZone = true; if (enableDebugLogs) Debug.Log("플레이어 진입: CloseZone"); }
    public void OnPlayerExitCloseZone() { isPlayerInCloseZone = false; if (enableDebugLogs) Debug.Log("플레이어 이탈: CloseZone"); }

    // --- 유틸리티 ---
    private void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }
}