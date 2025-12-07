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

    [Header("패링 설정")]
    [SerializeField] private float parryStartupTime = 0.25f; // 선딜레이
    [SerializeField] private float parryWindow = 0.3f;       // 패링 유지 시간
    [SerializeField] private float parryCooldown = 1.0f;
    
    private bool isParrying = false;    // 동작 중 (이동 잠금용)
    private bool isParryActive = false; // 판정 중 (방어 성공용)
    private bool canParry = true;

    [Header("사운드 설정")]
    public AudioClip parrySuccessSound; 

    private SpriteRenderer sp;
    private Rigidbody2D rb;
    private AudioSource audioSource;
    private Animator anim; 
    private bool isGrounded = false;

    // [수정] 피격 효과 중복 방지용 변수 추가
    private Coroutine flashRoutine; 
    private Color defaultColor;     

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        sp = GetComponent<SpriteRenderer>();
        audioSource = GetComponent<AudioSource>();
        anim = GetComponent<Animator>();

        // [수정] 게임 시작 시점의 진짜 원래 색을 저장해둡니다.
        // 이제 맞을 때마다 sp.color를 가져오지 않고 이 변수를 사용합니다.
        defaultColor = sp.color;
    }

    void Update()
    {
        // 1. 땅 체크 상태 업데이트
        anim.SetBool("isGrounded", isGrounded);

        // --- 패링 중 이동/행동 잠금 ---
        if (isParrying)
        {
            rb.linearVelocity = Vector2.zero;
            anim.SetBool("isRunning", false); 
            return; 
        }
        // -----------------------

        // 2. 좌우 이동 (패링 중이 아닐 때만)
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

        // 3. 점프
        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            anim.SetTrigger("doJump");
            isGrounded = false;
            anim.SetBool("isGrounded", false);
        }

        // 4. 패링 입력
        if (Input.GetKeyDown(KeyCode.D) && canParry)
        {
            StartCoroutine(ParryRoutine());
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
        {
            isGrounded = true;
        }
    }

    void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
        {
            isGrounded = false;
        }
    }

    IEnumerator ParryRoutine()
    {
        canParry = false;
        isParrying = true; 
        
        rb.linearVelocity = Vector2.zero; 
        rb.constraints = RigidbodyConstraints2D.FreezePositionX | RigidbodyConstraints2D.FreezeRotation;
        
        anim.SetBool("isParrying", true); 

        // 선딜레이
        yield return new WaitForSeconds(parryStartupTime);

        // 판정 시작
        isParryActive = true;     
        
        // (시각적 피드백) - 여기는 일시적인 효과라 sp.color를 직접 써도 무방하지만,
        // 안전하게 defaultColor를 기준으로 변경해도 됩니다.
        sp.color = Color.yellow;  

        yield return new WaitForSeconds(parryWindow); 

        // 판정 종료
        isParryActive = false;    
        sp.color = defaultColor; // [수정] 색 복구 시 defaultColor 사용

        isParrying = false;       
        anim.SetBool("isParrying", false);

        rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        yield return new WaitForSeconds(parryCooldown);
        canParry = true;
    }

    // [수정] 데미지 플래시 코루틴 개선
    private IEnumerator DamageFlashRoutine()
    {
        int blinkCount = 2; 
        float blinkDuration = 0.1f; 

        for (int i = 0; i < blinkCount; i++)
        {
            sp.color = new Color(1f, 0.5f, 0.5f, 0.8f); // 빨간색
            yield return new WaitForSeconds(blinkDuration);
            
            sp.color = defaultColor; // [수정] 무조건 저장해둔 원래 색으로 복구
            yield return new WaitForSeconds(blinkDuration);
        }
        
        // 코루틴 종료 시 변수 비움
        flashRoutine = null;
    }

    public void HandleAttack(int damage, GameObject attacker)
    {
        if (isParryActive) 
        {
            Debug.Log("<b>[패링 성공! - 완벽한 방어]</b>");
            PlaySound(parrySuccessSound);

            F_BossController boss = attacker.GetComponent<F_BossController>();
            if (boss != null)
            {
                int parryDamage = 10;
                boss.TakeDamage(parryDamage);
                boss.GetStunned();
            }
        }
        else
        {
            Debug.Log("패링 실패 - 플레이어 피격");
            F_GameManager gameManager = FindFirstObjectByType<F_GameManager>();
            if (gameManager != null)
            {
                gameManager.TakeDamage(damage);

                // --- [수정] 중복 실행 방지 로직 ---
                
                // 1. 이미 깜빡이는 중이면 멈춤
                if (flashRoutine != null)
                {
                    StopCoroutine(flashRoutine);
                }

                // 2. 색깔 초기화 (붉은색 상태에서 다시 시작되는 것 방지)
                sp.color = defaultColor;

                // 3. 새로 시작하고 변수에 저장
                flashRoutine = StartCoroutine(DamageFlashRoutine());
            }
        }
    }

    private void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }
}