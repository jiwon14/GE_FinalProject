using UnityEngine;
using System.Collections; // 코루틴 사용을 위해 추가

[RequireComponent(typeof(AudioSource))]
[RequireComponent(typeof(Animator))] // Animator 컴포넌트 필수 지정<<<추가
public class F_PlayerController : MonoBehaviour 
{
    [Header("이동 설정")]
    public float moveSpeed = 5.0f;

    [Header("점프 설정")]
    public float jumpForce = 10.0f;

    [Header("패링 설정")]
    [SerializeField] private float parryWindow = 0.3f;
    [SerializeField] private float parryCooldown = 1.0f;
    private bool isParrying = false;
    private bool canParry = true;

    [Header("사운드 설정")]
    public AudioClip parrySuccessSound; // 패링 성공 사운드

    private SpriteRenderer sp;
    private Rigidbody2D rb;
    private AudioSource audioSource;
    private Animator anim; // [애니메이션] Animator 변수 추가<<<추가
    private bool isGrounded = false;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        sp = GetComponent<SpriteRenderer>();
        audioSource = GetComponent<AudioSource>();
        anim = GetComponent<Animator>(); // [애니메이션] 컴포넌트 가져오기<<<<추가
    }

    void Update()
    {
        // 1. 땅 체크 상태 업데이트
        anim.SetBool("isGrounded", isGrounded);

        // --- [문제 해결의 핵심] ---
        // 패링 중이라면?
        if (isParrying)
        {
            // 1. 물리 속도를 매 프레임 0으로 강제 고정 (절대 못 움직임)
            rb.linearVelocity = Vector2.zero;
            
            // 2. 혹시라도 달리기 애니메이션이 켜져있다면 끔
            anim.SetBool("isRunning", false); 

            // 3. 아래 있는 이동 코드를 실행하지 않고 여기서 함수 종료
            return; 
        }
        // -----------------------

        // 2. 좌우 이동 (패링 중이 아닐 때만 실행됨)
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

    // 바닥 충돌 감지 (Collision)
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
        // 1. 패링 상태 시작
        canParry = false;
        isParrying = true;
        
        // 2. [가장 중요] 이동 기능 물리적 삭제 (X축 잠금)
        // 현재 속도를 0으로 없애고 + X축으로 아예 못 움직이게 못 박아버립니다.
        rb.linearVelocity = Vector2.zero; 
        rb.constraints = RigidbodyConstraints2D.FreezePositionX | RigidbodyConstraints2D.FreezeRotation;

        // 3. 패링 애니메이션 시작
        anim.SetBool("isParrying", true); 

        // 4. 패링 판정 시간 대기
        yield return new WaitForSeconds(parryWindow);

        // 5. 패링 상태 종료
        isParrying = false;
        anim.SetBool("isParrying", false);

        // 6. [중요] 이동 기능 복구 (X축 잠금 해제)
        // 다시 움직일 수 있게 회전만 잠그고 나머지는 풉니다.
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        // 7. 쿨타임 대기
        yield return new WaitForSeconds(parryCooldown);
        canParry = true;
    }

    // 데미지를 입었을 때 깜빡이는 효과
    private IEnumerator DamageFlashRoutine()
    {
        int blinkCount = 2; // 깜빡일 횟수
        float blinkDuration = 0.1f; // 각 깜빡임 지속 시간

        Color originalColor = sp.color;

        for (int i = 0; i < blinkCount; i++)
        {
            sp.color = new Color(1f, 0.5f, 0.5f, 0.8f); // 연한 빨강
            yield return new WaitForSeconds(blinkDuration);
            sp.color = originalColor;
            yield return new WaitForSeconds(blinkDuration);
        }
    }

    // --- [중요] 공격 판정 처리 ---
    public void HandleAttack(int damage, GameObject attacker)
    {
        // 1. 패링 성공 (isParrying이 true일 때만 실행)
        if (isParrying)
        {
            Debug.Log("<b>[패링 성공! - 완벽한 방어]</b>");
            PlaySound(parrySuccessSound); // 패링 성공 사운드 재생

            // 공격자로부터 보스 컨트롤러를 가져옵니다.
            F_BossController boss = attacker.GetComponent<F_BossController>();
            if (boss != null)
            {
                // 패링 성공 시 반격 데미지
                int parryDamage = 10;
                boss.TakeDamage(parryDamage);

                boss.GetStunned();
                Debug.Log("보스를 스턴시킵니다!");
            }
        }
        // 2. 패링 실패 (늦었거나 안 눌렀을 때)
        else
        {
            Debug.Log("패링 실패 - 플레이어 피격");
            F_GameManager gameManager = FindFirstObjectByType<F_GameManager>();
            if (gameManager != null)
            {
                gameManager.TakeDamage(damage); // 플레이어 데미지
                StartCoroutine(DamageFlashRoutine());
            }
        }
    }

    // 사운드 재생을 위한 헬퍼 함수
    private void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }
}
