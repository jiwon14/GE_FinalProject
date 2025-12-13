using UnityEngine;
using System.Collections; 

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

    private SpriteRenderer sp;
    private Rigidbody2D rb;
    private AudioSource audioSource;
    private Animator anim; 
    private bool isGrounded = false;

    private Coroutine flashRoutine; 
    private Color defaultColor;     

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

        if (flashRoutine != null) StopCoroutine(flashRoutine);
        
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
        int blinkCount = 2; 
        float blinkDuration = 0.1f; 

        for (int i = 0; i < blinkCount; i++)
        {
            sp.color = new Color(1f, 0.5f, 0.5f, 0.8f); 
            yield return new WaitForSeconds(blinkDuration);
            sp.color = defaultColor; 
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
                
                if (flashRoutine != null) StopCoroutine(flashRoutine);
                sp.color = defaultColor;
                flashRoutine = StartCoroutine(DamageFlashRoutine());
            }
        }
    }

    private void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null) audioSource.PlayOneShot(clip);
    }
}