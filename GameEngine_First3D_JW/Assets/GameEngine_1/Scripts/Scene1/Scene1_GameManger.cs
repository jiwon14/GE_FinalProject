using UnityEngine;
using UnityEngine.SceneManagement;

public class PortalController : MonoBehaviour
{
    [Header("이동할 씬 이름")]
    public string nextSceneName = "Scene2"; // 이동할 씬 이름

    // 2D에서는 이 함수를 씁니다 (뒤에 2D가 붙어요!)
    private void OnTriggerEnter2D(Collider2D other)
    {
        // 닿은 물체가 'Player' 태그인지 확인
        if (other.CompareTag("Player"))
        {
            Debug.Log("2D 포탈 작동! 이동합니다.");
            SceneManager.LoadScene(nextSceneName);
        }
    }
}