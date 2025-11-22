using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;  // Scene 관리용!
public class GameManager : MonoBehaviour
{
	[Header("UI 참조")]
	public GameObject titleScreenPanel;
	public GameObject hudPanel;
    public GameObject gameOverPanel;  // Game Over 패널 추가!
    public GameObject gameClearPanel;  // Game Clear 패널!
	public TextMeshProUGUI scoreText;
	public TextMeshProUGUI timeText;
	public TextMeshProUGUI healthText;
    public TextMeshProUGUI finalScoreText;  // 최종 점수 텍스트!
    
    public TextMeshProUGUI clearScoreText;  // 클리어 점수!
    public TextMeshProUGUI clearTimeText;  // 클리어 시간!
    
	[Header("게임 상태")]
	private int score = 0;
	private float playTime = 0f;
	private bool isPlaying = false;
	private int health = 3;
	void Start()
	{
		ShowTitleScreen();
		UpdateScoreUI();
		UpdateTimeUI();
		UpdateHealthUI();
	}
	
	void Update()
	{
		if (isPlaying)
		{
			playTime += Time.deltaTime;
			UpdateTimeUI();
		}
	}
	
	void ShowTitleScreen()
	{
		titleScreenPanel.SetActive(true);
		hudPanel.SetActive(false);
		gameOverPanel.SetActive(false);
		gameClearPanel.SetActive(false);  // Game Clear 숨기기!
		Time.timeScale = 0f;
		isPlaying = false;
	}
	
	public void StartGame()
	{
		titleScreenPanel.SetActive(false);
		hudPanel.SetActive(true);
		gameOverPanel.SetActive(false);
		gameClearPanel.SetActive(false);  // Game Clear 숨기기!
		Time.timeScale = 1f;
		score = 0;
		playTime = 0f;
		health = 3;
		isPlaying = true;
		UpdateScoreUI();
		UpdateTimeUI();
		UpdateHealthUI();
	}
	
	public void AddScore(int amount)
	{
		score += amount;
		UpdateScoreUI();
	}
	
	public void TakeDamage(int damage)
	{
		health -= damage;
		UpdateHealthUI();
		if (health <= 0)
		{
			GameOver();
		}
	}
	
	void UpdateScoreUI()
	{
		if (scoreText != null)
		{
			scoreText.text = "Score: " + score;
		}
	}
	
	void UpdateTimeUI()
	{
		if (timeText != null)
		{
			int minutes = Mathf.FloorToInt(playTime / 60f);
			int seconds = Mathf.FloorToInt(playTime % 60f);
			timeText.text = string.Format("Time: {0:00}:{1:00}", minutes, seconds);
		}
	}
	void UpdateHealthUI()
	{
		if (healthText != null)
		{
			healthText.text = "Health: " + health;
		}
	}
	// Game Over 함수 수정!
	void GameOver()
	{
		Debug.Log("💀 Game Over!");
		isPlaying = false;
		Time.timeScale = 0f;
		// Game Over 화면 표시
		hudPanel.SetActive(false);  // HUD 숨기기
		gameOverPanel.SetActive(true);  // Game Over 패널 표시
		// 최종 점수 표시
		if (finalScoreText != null)
		{
			finalScoreText.text = "Final Score: " + score;
		}
	}
	
	// Retry 버튼 함수 - 새로 추가!
	public void RetryGame()
	{
		Time.timeScale = 1f;  // 시간 재개
		SceneManager.LoadScene(SceneManager.GetActiveScene().name);  // 현재 씬 재시작
	}

    // Quit 버튼 함수 - 새로 추가!
    public void QuitGame()
    {
        Debug.Log("게임 종료");
        Application.Quit();  // 빌드된 게임 종료
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;  // 에디터에서 종료
#endif
    }

    public void GameClear()
	{
		Debug.Log("🎉🎉🎉 Game Clear! 🎉🎉🎉");
		isPlaying = false;
		Time.timeScale = 0f;
		// Game Clear 화면 표시
		hudPanel.SetActive(false);
		gameClearPanel.SetActive(true);
		// 최종 점수 및 시간 표시
		if (clearScoreText != null)
		{
			clearScoreText.text = "Score: " + score;
		}
		if (clearTimeText != null)
		{
			int minutes = Mathf.FloorToInt(playTime / 60f);
			int seconds = Mathf.FloorToInt(playTime % 60f);
			clearTimeText.text = string.Format("Time: {0:00}:{1:00}", minutes, seconds);
		}
	}
}
    
    
