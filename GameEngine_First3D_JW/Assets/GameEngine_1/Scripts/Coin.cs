using UnityEngine;

public class Coin : MonoBehaviour
{
    public int coinValue = 10;  // 코인 점수 (10점)
    
    void OnTriggerEnter2D(Collider2D other)
    {
        // Player와 충돌했는지 확인
        if (other.CompareTag("Player"))
        {
            // GameManager 찾기
            GameManager gameManager = FindObjectOfType<GameManager>();
            
            if (gameManager != null)
            {
                gameManager.AddScore(coinValue);  // 점수 증가
            }
            
            Destroy(gameObject);  // 코인 제거
        }
    }
}