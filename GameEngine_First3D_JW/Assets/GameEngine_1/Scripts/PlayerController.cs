using UnityEngine;
using System.Collections; // 코루틴 사용을 위해 추가

public class PlayerController : MonoBehaviour
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


    SpriteRenderer sp;
    private Rigidbody2D rb;
    private bool isGrounded = false;

    private Vector3 startPosition;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        // 게임 시작 시 위치를 저장 - 새로 추가!
        startPosition = transform.position;
        Debug.Log("시작 위치 저장: " + startPosition);

        sp = GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        // 좌우 이동
        float moveX = 0f;
        if (Input.GetKey(KeyCode.A))
        {
            sp.flipX = true;
            moveX = -1f;
        }
        if (Input.GetKey(KeyCode.D))
        {
            sp.flipX = false;
            moveX = 1f;
        }

        rb.linearVelocity = new Vector2(moveX * moveSpeed, rb.linearVelocity.y);

        // 점프 (지난 시간에 배운 내용)
        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
        }

        // 패링 입력 ('E' 키)
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

        // 보스와의 충돌을 최우선으로 처리합니다.
        if (collision.gameObject.CompareTag("Boss"))
        {
            // 보스와 충돌 시 HandleAttack을 호출하도록 수정합니다.
            // 패링 여부는 HandleAttack 내부에서 판단합니다.
            // 이 코드는 이전 버전과의 호환성을 위해 남겨두지만,
            // 새로운 시스템에서는 BossController가 직접 HandleAttack을 호출하는 것이 더 정확합니다.
            Debug.Log("👹 보스와 몸체 충돌!");
            // 보스와 충돌 시에는 장애물 로직을 타지 않도록 여기서 함수를 종료합니다.
            return; 
        }

        // 일반 장애물과 충돌했을 때 (보스가 아닐 경우에만 실행됩니다)
        if (collision.gameObject.CompareTag("Obstacle"))
        {
            Debug.Log("⚠️ 장애물 충돌! 생명 -1");
            HandleAttack(1, null); // 장애물은 특별한 상호작용이 없으므로 attacker를 null로 전달
            rb.linearVelocity = Vector2.zero;
        }
    }

    void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
        {
            isGrounded = false;
        }
    }


void OnTriggerEnter2D(Collider2D other)
{
	// 코인 수집 (기존)
	if (other.CompareTag("Coin"))
	{
		GameManager gameManager = FindFirstObjectByType<GameManager>();
		if (gameManager != null)
		{
			gameManager.AddScore(10);
		}
		Destroy(other.gameObject);
	}
	// 골 도달 - 새로 추가!
	if (other.CompareTag("Goal"))
	{
		Debug.Log("🎉 Goal Reached!");
		GameManager gameManager = FindFirstObjectByType<GameManager>();
		if (gameManager != null)
		{
			gameManager.GameClear();  // 게임 클리어 함수 호출
		}
	}

	// 보스의 찌르기 공격(트리거)에 맞았을 때
	if (other.CompareTag("BossAttack"))
	{
		Debug.Log("🔪 보스의 찌르기 공격 감지!");
        HandleAttack(1, other.gameObject); // 찌르기 데미지 1로 처리
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

            // 공격자가 보스이거나, 보스의 공격 판정(BossAttack)일 경우
            if (attacker != null && (attacker.CompareTag("Boss") || attacker.CompareTag("BossAttack")))
            {
                // 보스 컨트롤러를 가져와 스턴 함수를 호출합니다.
                BossController boss = attacker.GetComponentInParent<BossController>();
                if (boss != null)
                {
                    // 패링 성공 시 반격 데미지
                    int parryDamage = 10;
                    boss.TakeDamage(parryDamage);

                    boss.GetStunned();
                    Debug.Log("보스를 스턴시킵니다!");
                }
            }
            // 플레이어는 데미지를 입지 않음
        }
        // 2. 패링 실패 (늦었거나 안 눌렀을 때)
        else
        {
            Debug.Log("패링 실패 - 플레이어 피격");
            GameManager gameManager = FindFirstObjectByType<GameManager>();
            if (gameManager != null)
            {
                gameManager.TakeDamage(damage); // 플레이어 데미지

                // 데미지 깜빡임 효과 시작
                StartCoroutine(DamageFlashRoutine());
            }
        }
    }

}
