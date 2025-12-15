using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro; // TextMeshPro 사용을 위해 필수

public class BossCutsceneManager : MonoBehaviour
{
    // 1. 화자 이름과 대사를 묶는 구조체 (인스펙터에서 리스트로 입력 가능)
    [System.Serializable]
    public struct DialogueData
    {
        public string speakerName; // 화자 이름
        [TextArea(3, 5)]           // 인스펙터에서 여러 줄 입력이 편하도록 설정
        public string sentence;    // 대사 내용
    }

    [Header("컷신 설정")]
    [Tooltip("대화가 모두 끝나면 이동할 보스전 씬의 이름입니다.")]
    public string nextSceneName;

    [Header("대화 데이터")]
    [Tooltip("순서대로 출력될 대사 리스트입니다.")]
    public List<DialogueData> dialogueList; 

    [Header("UI 연결")]
    public TextMeshProUGUI nameText;     // 화자 이름 표시용 TextMeshPro
    public TextMeshProUGUI sentenceText; // 대사 내용 표시용 TextMeshPro

    private int currentIndex = 0; // 현재 대사 인덱스

    void Start()
    {
        // 게임 시작 시 첫 번째 대사 출력
        if (dialogueList != null && dialogueList.Count > 0)
        {
            UpdateDialogueUI();
        }
        else
        {
            Debug.LogWarning("대화 데이터가 비어있습니다. 바로 씬을 이동합니다.");
            EndCutscene();
        }
    }

    void Update()
    {
        // 스페이스바를 누르면 다음 대사로 진행
        if (Input.GetKeyDown(KeyCode.Space))
        {
            NextDialogue();
        }
    }

    // 다음 대사로 넘어가는 로직
    private void NextDialogue()
    {
        currentIndex++;

        // 아직 보여줄 대사가 남아있다면 UI 업데이트
        if (currentIndex < dialogueList.Count)
        {
            UpdateDialogueUI();
        }
        // 모든 대사가 끝났다면 씬 이동
        else
        {
            EndCutscene();
        }
    }

    // 현재 인덱스에 맞는 대사를 UI에 표시
    private void UpdateDialogueUI()
    {
        if (currentIndex < dialogueList.Count)
        {
            DialogueData currentData = dialogueList[currentIndex];

            // UI 텍스트 갱신 (null 체크 포함)
            if (nameText != null) 
                nameText.text = currentData.speakerName;
            
            if (sentenceText != null) 
                sentenceText.text = currentData.sentence;
        }
    }

    // 컷신 종료 및 씬 이동 처리
    private void EndCutscene()
    {
        if (!string.IsNullOrEmpty(nextSceneName))
        {
            SceneManager.LoadScene(nextSceneName);
        }
        else
        {
            Debug.LogError("이동할 씬 이름(Next Scene Name)이 인스펙터에 설정되지 않았습니다!");
        }
    }
}
