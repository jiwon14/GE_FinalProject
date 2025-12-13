// BossHealth.cs
using UnityEngine;
using System; // Action 이벤트를 사용하기 위해 추가

public class BossHealth : MonoBehaviour
{
    [SerializeField] private float maxHealth = 1000f;
    private float currentHealth;
    private bool hasDiedOnce = false; // 2페이즈 전환을 위해, 한 번 죽었는지 확인
    private bool isDead = false; // 보스가 죽었는지 확인하는 플래그

    private F_BossController f_bossController;
    private T_BossController t_bossController;

    // 체력이 변경될 때 호출될 이벤트
    // 파라미터: 현재 체력, 최대 체력
    public event Action<float, float> OnHealthChanged;

    void Awake()
    {
        f_bossController = GetComponent<F_BossController>();
        t_bossController = GetComponent<T_BossController>();
        currentHealth = maxHealth;
    }

    void Update()
    {
        // 보스가 살아있고(isDead == false), 체력이 최대치가 아닐 때만 재생
        if (!isDead && currentHealth > 0 && currentHealth < maxHealth)
        {
            // 초당 0.1의 체력을 회복합니다.
            Heal(0.2f * Time.deltaTime);
        }
    }

    // 데미지를 받는 함수
    public void TakeDamage(float damage)
    {
        currentHealth = Mathf.Max(currentHealth - damage, 0);
        OnHealthChanged?.Invoke(currentHealth, maxHealth); // 이벤트 호출

        if (currentHealth <= 0 && !isDead) // 죽지 않았을 때만 Die()를 한 번만 호출
        {
            HandleDeath();
        }
    }

    // 체력을 회복하는 함수
    public void Heal(float amount)
    {
        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
        OnHealthChanged?.Invoke(currentHealth, maxHealth); // 이벤트 호출
    }

    private void HandleDeath()
    {
        isDead = true; // 사망 상태로 변경

        // 연결된 보스 컨트롤러의 Die 함수를 호출
        if (t_bossController != null)
        {
            // 2페이즈 보스이거나, 이미 한 번 죽었다면 최종 죽음 처리
            if (t_bossController.isPhase2 || hasDiedOnce)
            {
                t_bossController.Die(); // 최종 죽음
            }
            else // 1페이즈 보스의 첫 죽음이라면 2페이즈 전환 시작
            {
                hasDiedOnce = true;
                t_bossController.StartPhase2Transition(); // 2페이즈 전환 시작
            }
        }
        else if (f_bossController != null) // T_Boss가 아닐 경우 기존 로직
        {
            f_bossController.Die();
        }
    }

    /// <summary>
    /// 현재 체력을 반환합니다.
    /// </summary>
    public float GetCurrentHealth()
    {
        return currentHealth;
    }

    /// <summary>
    /// 최대 체력을 반환합니다.
    /// </summary>
    public float GetMaxHealth()
    {
        return maxHealth;
    }
}
