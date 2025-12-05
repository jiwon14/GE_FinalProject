using UnityEngine;
using UnityEngine.SceneManagement; // 씬 관리를 위해 추가

public class F_GameManager : MonoBehaviour
{
    [Header("플레이어 설정")]
    public int playerMaxHealth = 5;
    public int playerCurrentHealth;

    // 게임 시작 시 한 번만 실행
    void Start()
    {
        // 현재 체력을 최대 체력으로 초기화
        playerCurrentHealth = playerMaxHealth;
        Debug.Log($"게임 시작! 플레이어 체력: {playerCurrentHealth}/{playerMaxHealth}");
    }

    // 플레이어가 데미지를 입었을 때 호출될 함수
    public void TakeDamage(int damage)
    {
        // 현재 체력에서 데미지만큼 감소
        playerCurrentHealth -= damage;

        Debug.Log($"플레이어 피격! 현재 체력: {playerCurrentHealth}");

        // 체력이 0 이하로 떨어졌는지 확인
        if (playerCurrentHealth <= 0)
        {
            playerCurrentHealth = 0; // 체력이 마이너스가 되지 않도록 보정
            GameOver();
        }
    }

    // 게임 오버 처리
    private void GameOver()
    {
        Debug.LogWarning("게임 오버!");
        // 예: 현재 씬을 다시 시작
        // SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}

