using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    // 버튼의 OnClick 이벤트에 연결하여 사용합니다.
    // 인스펙터 입력란에 이동하고 싶은 씬 이름(예: "Stage1", "Lobby")을 적어주세요.
    public void LoadScene(string sceneName)
    {
        if (!string.IsNullOrEmpty(sceneName))
        {
            SceneManager.LoadScene(sceneName);
        }
        else
        {
            Debug.LogWarning("이동할 씬 이름이 입력되지 않았습니다!");
        }
    }

    // '재시작' 버튼용: 플레이어가 죽기 직전의 씬을 불러옵니다.
    public void RestartLastScene()
    {
        string lastScene = PlayerPrefs.GetString("LastScene");
        if (!string.IsNullOrEmpty(lastScene))
        {
            SceneManager.LoadScene(lastScene);
        }
        else
        {
            Debug.LogWarning("저장된 마지막 씬 정보가 없습니다. 로비로 이동합니다.");
            LoadScene("Lobby"); // 기본값 (씬 이름이 'Lobby'가 아니라면 수정 필요)
        }
    }

    // '종료' 버튼용
    public void QuitGame()
    {
        Debug.Log("게임 종료");
        Application.Quit();
    }
}