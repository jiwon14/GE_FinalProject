using UnityEngine;
using System.Collections; 
using UnityEngine.SceneManagement;

[RequireComponent(typeof(AudioSource))]
[RequireComponent(typeof(Animator))] 
public class F_PlayerController : MonoBehaviour 
{
    [Header("이동 설정")]
    public float moveSpeed = 5.0f;

    [Header("점프 설정")]
    public float jumpForce = 10.0f;

    [Header("구르기 설정 (Shift)")]
    [SerializeField] private float rollSpeed = 12.0f;      
    [SerializeField] private float rollDuration = 0.5f;   // 구르는 시간 (이동 시간)
    
    [Tooltip("구르기 시작 직후 무적 유지 시간")]
    [SerializeField] private float invincibilityDuration = 0.3f; 
    
    // 후딜레이 변수 삭제됨
    
    [SerializeField] private float rollCooldown = 0.5f; // 구르기 끝난 후 다음 구르기까지 대기 시간
    
    private bool isRolling = false;     
    private bool isInvincible = false;  
    private bool canRoll = true;        

    [Header("패링 설정")]
    [SerializeField] private float parryStartupTime = 0.25f; 
    [SerializeField] private float parryWindow = 0.3f;       
    [SerializeField] private float parryCooldown = 1.0f;
    
    private bool isParrying = false;    
    private bool isParryActive = false; 
    private bool canParry = true;

    [Header("사운드 설정")]
    public AudioClip parrySuccessSound; 
    // public AudioClip rollSound; 

    [Header("사망 설정")]
    public string deathSceneName; // 사망 시 이동할 씬 이름

    private SpriteRenderer sp;
    private Rigidbody2D rb;
    private AudioSource audioSource;
    private Animator anim; 
    private bool isDead = false; // 플레이어 사망 상태
    private bool isGrounded = false;

    private Coroutine flashRoutine; 
    private Color defaultColor;     
    private bool isFlashing = false; // 피격 효과 중인지 확인하는 플래그

    // --- 애니메이터 파라미터 ID ---
    private readonly int DoDieTriggerID = Animator.StringToHash("doDie");

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        sp = GetComponent<SpriteRenderer>();
        audioSource = GetComponent<AudioSource>();
        anim = GetComponent<Animator>();

        defaultColor = sp.color;
    }

    void Update()
    {
        // 사망 시 모든 입력을 무시합니다.
        if (isDead) return;

        anim.SetBool("isGrounded", isGrounded);

        if (isParrying || isRolling)
        {
            if (isParrying) rb.linearVelocity = Vector2.zero;
            anim.SetBool("isRunning", false); 
            return; 
        }

        // 1. 좌우 이동
        if (isGrounded)
        {
            float moveX = 0f;
            if (Input.GetKey(KeyCode.LeftArrow))
            {
                moveX = -1f;
                sp.flipX = true;
            }
            else if (Input.GetKey(KeyCode.RightArrow))
            {
                moveX = 1f;
                sp.flipX = false;
            }

            rb.linearVelocity = new Vector2(moveX * moveSpeed, rb.linearVelocity.y);
            anim.SetBool("isRunning", moveX != 0);
        }

        // 2. 점프
        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            anim.SetTrigger("doJump");
            isGrounded = false;
            anim.SetBool("isGrounded", false);
        }

        // 3. 패링
        if (Input.GetKeyDown(KeyCode.D) && canParry && isGrounded)
        {
            StartCoroutine(ParryRoutine());
        }

        // 4. 구르기
        if (Input.GetKeyDown(KeyCode.LeftShift) && canRoll && isGrounded)
        {
            float rollDir = 0f;
            if (Mathf.Abs(rb.linearVelocity.x) > 0.1f)
            {
                rollDir = Mathf.Sign(rb.linearVelocity.x);
            }
            else
            {
                rollDir = sp.flipX ? -1f : 1f;
            }

            StartCoroutine(RollRoutine(rollDir));
        }
    }

    void LateUpdate()
    {
        // 애니메이터가 매 프레임 색상을 덮어쓰는 문제를 방지하기 위해 LateUpdate에서 색상을 강제로 지정합니다.
        if (isFlashing && sp != null)
        {
            sp.color = new Color(1f, 0.5f, 0.5f, 0.8f); // 요청하신 연한 붉은색
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Ground")) isGrounded = true;
    }

    void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Ground")) isGrounded = false;
    }

    // --- [수정] 후딜레이 삭제 버전 ---
    IEnumerator RollRoutine(float direction)
    {
        canRoll = false;   
        isRolling = true;  // 조작 잠금

        // [수정] 피격 효과 중 구르면 빨간색이 유지되는 버그 수정
        if (flashRoutine != null) 
        {
            StopCoroutine(flashRoutine);
            isFlashing = false; // LateUpdate의 색상 덮어쓰기 해제
            sp.color = defaultColor; // 색상 원상복구
        }
        
        anim.SetTrigger("doRoll");
        
        // 1. [즉시 발동] 무적 & 이동 & 파란색 변신
        isInvincible = true;
        sp.color = new Color(0.3f, 0.3f, 1f, 1f); 
        rb.linearVelocity = new Vector2(direction * rollSpeed, rb.linearVelocity.y);

        // 2. 무적 시간 대기 (0.3초)
        yield return new WaitForSeconds(invincibilityDuration);
        
        // 3. 무적 종료 & 색깔 복구
        isInvincible = false;
        sp.color = defaultColor; 

        // 4. 남은 이동 시간 대기
        float remainingMoveTime = rollDuration - invincibilityDuration;
        if (remainingMoveTime > 0)
        {
            yield return new WaitForSeconds(remainingMoveTime);
        }

        // 5. 이동 정지 & 조작 잠금 즉시 해제 (후딜 없음)
        rb.linearVelocity = Vector2.zero; 
        isRolling = false; // [핵심] 멈추자마자 바로 조작 가능
        
        // 6. 다음 구르기 쿨타임
        if (rollCooldown > 0)
        {
            yield return new WaitForSeconds(rollCooldown);
        }
        
        canRoll = true;
    }

    IEnumerator ParryRoutine()
    {
        canParry = false;
        isParrying = true; 
        
        rb.linearVelocity = Vector2.zero; 
        rb.constraints = RigidbodyConstraints2D.FreezePositionX | RigidbodyConstraints2D.FreezeRotation;
        
        anim.SetBool("isParrying", true); 
        yield return new WaitForSeconds(parryStartupTime);

        isParryActive = true;     
        sp.color = Color.yellow;  
        yield return new WaitForSeconds(parryWindow); 

        isParryActive = false;    
        sp.color = defaultColor; 

        isParrying = false;       
        anim.SetBool("isParrying", false);
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        yield return new WaitForSeconds(parryCooldown);
        canParry = true;
    }

    private IEnumerator DamageFlashRoutine()
    {
        int blinkCount = 3; 
        float blinkDuration = 0.1f; 

        for (int i = 0; i < blinkCount; i++)
        {
            isFlashing = true; // LateUpdate에서 색상 적용 활성화
            yield return new WaitForSeconds(blinkDuration);
            isFlashing = false; // 색상 적용 비활성화
            if (sp != null) sp.color = defaultColor; // 원래 색으로 복구
            yield return new WaitForSeconds(blinkDuration);
        }
        flashRoutine = null;
    }

    // 기존 시스템(F_Boss 등)과의 호환성을 위한 오버로드입니다.
    public void HandleAttack(int damage, GameObject attacker)
    {
        // 이 메서드를 호출하는 공격은 항상 패링 시 데미지를 입히는 것으로 간주합니다.
        HandleAttack(damage, attacker, true);
    }

    /// <summary>
    /// 플레이어의 사망 처리를 담당합니다.
    /// </summary>
    public void Die()
    {
        if (isDead) return; // 중복 실행 방지

        isDead = true;
        Debug.Log("플레이어 사망!");

        // 물리 효과 및 충돌 비활성화
        rb.linearVelocity = Vector2.zero;
        rb.bodyType = RigidbodyType2D.Kinematic;
        GetComponent<Collider2D>().enabled = false;

        // 사망 애니메이션 재생
        anim.SetTrigger(DoDieTriggerID);

        // 게임 종료 코루틴 시작
        StartCoroutine(EndGameRoutine());
    }

    private IEnumerator EndGameRoutine()
    {
        // 죽는 애니메이션이 끝날 때까지 대기 (예: 2초)
        yield return new WaitForSeconds(2.0f);

        if (!string.IsNullOrEmpty(deathSceneName))
        {
            SceneManager.LoadScene(deathSceneName);
        }
        else
        {
            Debug.LogWarning("이동할 씬 이름(Death Scene Name)이 설정되지 않았습니다! 게임을 종료합니다.");
            #if UNITY_EDITOR
            UnityEditor.Selection.activeObject = null;
            UnityEditor.EditorApplication.isPlaying = false;
            #else
            Application.Quit();
            #endif
        }
    }

    public void HandleAttack(int damage, GameObject attacker, bool isParryDamageable)
    {
        // [1] 무적 (회피 성공)
        if (isInvincible)
        {
            Debug.Log("<b>[회피!]</b>");
            return;
        }

        // [2] 패링 성공
        if (isParryActive) 
        {
            Debug.Log("<b>[패링 성공!]</b>");
            PlaySound(parrySuccessSound);

            // 패링 성공 시 화면 흔들림 효과 호출
            F_GameManager gameManager = FindFirstObjectByType<F_GameManager>();
            if (gameManager != null)
            {
                gameManager.TriggerParryShake();
            }

            // 패링 시 데미지를 입히는 공격인지 확인합니다.
            if (isParryDamageable)
            {
                var f_boss = attacker.GetComponent<F_BossController>();
                if (f_boss != null)
                {
                    int parryDamage = 10;
                    f_boss.TakeDamage(parryDamage);
                    f_boss.GetStunned();
                    return;
                }

                var t_boss = attacker.GetComponent<T_BossController>();
                if (t_boss != null)
                {
                    Debug.Log("근접/특수 공격 패링! 보스에게 데미지와 스턴 적용.");
                    int parryDamage = 10;
                    t_boss.TakeDamage(parryDamage);
                    t_boss.GetStunned();
                    return;
                }
            }
            else // isParryDamageable == false
            {
                Debug.Log("원거리/일반 공격 패링! 공격만 막아냅니다.");
                return; // 데미지 없이 공격만 막고 함수 종료
            }
        }
        else // [3] 피격
        {
            Debug.Log("플레이어 피격");
            F_GameManager gameManager = FindFirstObjectByType<F_GameManager>();
            if (gameManager != null)
            {
                gameManager.TakeDamage(damage);
            }

            // 데미지 시각 효과를 게임 매니저 존재 여부와 상관없이 실행합니다.
            if (flashRoutine != null) 
            {
                StopCoroutine(flashRoutine);
                isFlashing = false; // 기존 플래그 초기화
            }
            
            sp.color = defaultColor; // 깜빡임이 중첩될 경우를 대비해 기본 색상으로 초기화
            flashRoutine = StartCoroutine(DamageFlashRoutine());
        }
    }

    private void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null) audioSource.PlayOneShot(clip);
    }
}