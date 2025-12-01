using UnityEngine;
using System.Collections; // 코루틴 사용을 위해 추가

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

    private SpriteRenderer sp;
    private Rigidbody2D rb;
    private bool isGrounded = false;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        sp = GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        // 좌우 이동
        float moveX = 0f;
        if (Input.GetKey(KeyCode.A))
        {
            sp.flipX = false;
            moveX = -1f;
        }
        if (Input.GetKey(KeyCode.D))
        {
            sp.flipX = true;
            moveX = 1f;
        }

        rb.linearVelocity = new Vector2(moveX * moveSpeed, rb.linearVelocity.y);

        // 점프
        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
        }

        // 패링 입력 ('F' 키)
        if (Input.GetKeyDown(KeyCode.F) && canParry)
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
        canParry = false;
        isParrying = true;

        Color originalColor = sp.color;
        sp.color = Color.yellow; // 패링 중일 때 노란색으로 변경

        yield return new WaitForSeconds(parryWindow);

        isParrying = false;
        sp.color = originalColor; // 원래 색으로 복구

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
            GameManager gameManager = FindFirstObjectByType<GameManager>();
            if (gameManager != null)
            {
                gameManager.TakeDamage(damage); // 플레이어 데미지
                StartCoroutine(DamageFlashRoutine());
            }
        }
    }
}
