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
    [SerializeField] private float rollSpeed = 8.0f;      
    [SerializeField] private float rollDuration = 0.5f;   
    [SerializeField] private float invincibilityDuration = 0.3f; 
    [SerializeField] private float rollCooldown = 0.8f;   
    
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

    // --- [수정] 파란색 무적 효과 추가 ---
    IEnumerator RollRoutine(float direction)
    {
        canRoll = false;   
        isRolling = true;  

        // 혹시 피격 중이었다면 깜빡임 중지하고 파란색이 우선되도록 정리
        if (flashRoutine != null) StopCoroutine(flashRoutine);
        
        anim.SetTrigger("doRoll");
        
        // 1. 무적 시작 & 파란색 변신
        isInvincible = true;
        sp.color = new Color(0.3f, 0.3f, 1f, 1f); // 너무 어두운 파랑 대신 밝은 파랑 적용
        
        rb.linearVelocity = new Vector2(direction * rollSpeed, rb.linearVelocity.y);

        // 2. 무적 시간 대기
        yield return new WaitForSeconds(invincibilityDuration);
        
        // 3. 무적 종료 & 색깔 복구
        isInvincible = false;
        sp.color = defaultColor; 

        float remainingTime = rollDuration - invincibilityDuration;
        if (remainingTime > 0) yield return new WaitForSeconds(remainingTime);

        isRolling = false;
        rb.linearVelocity = Vector2.zero; 

        yield return new WaitForSeconds(rollCooldown - rollDuration);
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

    public void HandleAttack(int damage, GameObject attacker)
    {
        // [1] 무적 (파란색 상태)
        if (isInvincible)
        {
            Debug.Log("<b>[회피!]</b>");
            return;
        }

        // [2] 패링
        if (isParryActive) 
        {
            Debug.Log("<b>[패링 성공!]</b>");
            PlaySound(parrySuccessSound);

            // F_BossController 또는 T_BossController를 처리
            var f_boss = attacker.GetComponent<F_BossController>();
            if (f_boss != null)
            {
                int parryDamage = 10;
                f_boss.TakeDamage(parryDamage);
                f_boss.GetStunned();
                return; // 처리가 끝났으므로 함수 종료
            }

            var t_boss = attacker.GetComponent<T_BossController>();
            if (t_boss != null)
            {
                int parryDamage = 10;
                t_boss.TakeDamage(parryDamage);
                t_boss.GetStunned();
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