using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement; // 씬 관리를 위해 추가
using UnityEngine.UI; // UI 관련 클래스 사용을 위해 추가
using Unity.Cinemachine; // Unity 6 (Cinemachine 3.0) 대응

[RequireComponent(typeof(CinemachineImpulseSource))] // Impulse Source 컴포넌트 필수
public class F_GameManager : MonoBehaviour
{
    [Header("플레이어 설정")]
    public int playerMaxHealth = 10;
    public int playerCurrentHealth;
    
    [Header("참조")]
    public F_PlayerController playerController; // 플레이어 컨트롤러 참조

    [Header("UI 설정")]
    public Image[] heartImages;      // 하트 이미지 배열 (5개)
    public Sprite fullHeartSprite;   // 완전한 하트 스프라이트
    public Sprite brokenHeartSprite; // 깨진 하트 스프라이트

    [Header("카메라 흔들림 (Impulse)")]
    public float parryImpulseForce = 0.8f; // 패링 시 발생시킬 충격의 강도 계수
    private CinemachineImpulseSource impulseSource;

    private bool isGameOver = false; // 게임 오버 상태인지 확인

    // 게임 시작 시 한 번만 실행
    void Start()
    {
        if (playerController == null)
        {
            playerController = FindFirstObjectByType<F_PlayerController>();
        }

        impulseSource = GetComponent<CinemachineImpulseSource>();

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

    // 패링 성공 시 호출할 함수
    public void TriggerParryShake()
    {
        if (impulseSource != null)
        {
            // Impulse 발생 (설정된 Raw Signal 패턴대로 흔들림)
            impulseSource.GenerateImpulse(parryImpulseForce);
            Debug.Log($"Impulse 충격 발생! 강도: {parryImpulseForce}");
        }
    }

    // 체력 UI 업데이트
    void UpdateHealthUI()
    {
        // UI 변수가 연결되지 않았다면(null이라면) 실행하지 않음
        if (heartImages == null) return;

        for (int i = 0; i < heartImages.Length; i++)
        {
            // 배열 내부의 요소가 null인 경우 건너뜀
            if (heartImages[i] == null) continue;

            if (playerCurrentHealth >= (i * 2) + 2)
            {
                heartImages[i].sprite = fullHeartSprite;
                heartImages[i].enabled = true;
            }
            else if (playerCurrentHealth >= (i * 2) + 1)
            {
                heartImages[i].sprite = brokenHeartSprite;
                heartImages[i].enabled = true;
            }
            else
            {
                heartImages[i].enabled = false;
            }
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
