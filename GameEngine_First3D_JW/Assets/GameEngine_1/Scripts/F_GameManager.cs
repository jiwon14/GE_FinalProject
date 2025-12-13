using UnityEngine;
using UnityEngine.SceneManagement; // 씬 관리를 위해 추가
using UnityEngine.UI; // UI 관련 클래스 사용을 위해 추가

public class F_GameManager : MonoBehaviour
{
    [Header("플레이어 설정")]
    public int playerMaxHealth = 5;
    public int playerCurrentHealth;
    
    [Header("참조")]
    public F_PlayerController playerController; // 플레이어 컨트롤러 참조

    [Header("UI 설정")]
    public Image[] hearts; // 하트 이미지들을 담을 배열

    private bool isGameOver = false; // 게임 오버 상태인지 확인

    // 게임 시작 시 한 번만 실행
    void Start()
    {
        if (playerController == null)
        {
            playerController = FindFirstObjectByType<F_PlayerController>();
        }

        // 현재 체력을 최대 체력으로 초기화
        playerCurrentHealth = playerMaxHealth;
        UpdateHealthUI();
        Debug.Log($"게임 시작! 플레이어 체력: {playerCurrentHealth}/{playerMaxHealth}");
    }

    // 플레이어가 데미지를 입었을 때 호출될 함수
    public void TakeDamage(int damage)
    {
        // 게임이 이미 끝났거나 플레이어가 죽었다면 데미지를 받지 않음
        if (isGameOver) return;

        // 현재 체력에서 데미지만큼 감소
        playerCurrentHealth -= damage;

        UpdateHealthUI();

        Debug.Log($"플레이어 피격! 현재 체력: {playerCurrentHealth}");

        // 체력이 0 이하로 떨어졌는지 확인
        if (playerCurrentHealth <= 0)
        {
            playerCurrentHealth = 0; // 체력이 마이너스가 되지 않도록 보정
            GameOver();
        }
    }

    // 체력 UI 업데이트
    void UpdateHealthUI()
    {
        for (int i = 0; i < hearts.Length; i++)
        {
            // 현재 체력보다 i가 크면(즉, 체력이 깎이면) 하트를 비활성화
            // 그렇지 않으면 활성화
            if (i < playerCurrentHealth)
                hearts[i].enabled = true;
            else
                hearts[i].enabled = false;
        }
    }

    // 게임 오버 처리
    private void GameOver()
    {
        if (isGameOver) return; // 게임 오버가 중복 호출되는 것을 방지

        isGameOver = true;
        Debug.LogWarning("게임 오버!");

        // 플레이어 컨트롤러에게 사망 처리를 지시합니다.
        if (playerController != null)
        {
            playerController.Die();
        }
    }
}
