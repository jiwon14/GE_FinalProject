// BossHealthUI.cs
using UnityEngine;
using UnityEngine.UI;

public class BossHealthUI : MonoBehaviour
{
    [SerializeField] private BossHealth bossHealth; // 보스 체력 스크립트 참조
    [SerializeField] private Image healthBarLeft;   // 왼쪽 체력바 이미지
    [SerializeField] private Image healthBarRight;  // 오른쪽 체력바 이미지

    private void OnEnable()
    {
        // BossHealth의 OnHealthChanged 이벤트에 UpdateHealthBar 함수를 구독
        // 게임이 실행 중일 때만 이벤트를 구독합니다.
        if (Application.isPlaying && bossHealth != null)
        {
            bossHealth.OnHealthChanged += UpdateHealthBar;
        }
    }

    private void OnDisable()
    {
        // 오브젝트가 비활성화될 때 이벤트 구독 해제
        // 게임이 실행 중일 때만 이벤트 구독을 해제합니다.
        if (Application.isPlaying && bossHealth != null)
        {
            bossHealth.OnHealthChanged -= UpdateHealthBar;
        }
    }

    private void Start()
    {
        // UI를 비활성화 상태로 시작 (보스가 나타날 때 활성화)
        gameObject.SetActive(false);

        // 초기 상태를 최대 체력으로 설정해둡니다.
        if (bossHealth != null)
        {
            UpdateHealthBar(bossHealth.GetMaxHealth(), bossHealth.GetMaxHealth());
        }
    }

    // 체력바 UI를 업데이트하는 함수
    private void UpdateHealthBar(float currentHealth, float maxHealth)
    {
        float fillAmount = currentHealth / maxHealth;
        healthBarLeft.fillAmount = fillAmount;
        healthBarRight.fillAmount = fillAmount;
    }
    
    // 보스가 나타날 때 호출할 함수
    public void Show()
    {
        gameObject.SetActive(true);
        // UI가 활성화될 때, 보스의 현재 체력 상태로 UI를 즉시 업데이트합니다.
        if (bossHealth != null)
        {
            UpdateHealthBar(bossHealth.GetCurrentHealth(), bossHealth.GetMaxHealth());
        }
    }

    // 보스가 사라질 때 호출할 함수
    public void Hide()
    {
        gameObject.SetActive(false);
    }
}
