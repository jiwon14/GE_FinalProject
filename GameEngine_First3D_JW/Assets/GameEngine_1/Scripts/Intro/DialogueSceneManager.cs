using UnityEngine;
using System.Collections; // 코루틴 사용을 위해 추가
using TMPro; // TextMeshPro를 사용하기 위해 꼭 필요합니다.
using UnityEngine.SceneManagement; // 씬 이동을 위해 필수

public class DialogueSceneManager : MonoBehaviour
{
    [Header("UI 컴포넌트")]
    public TextMeshProUGUI textDisplay; // 화면에 보여줄 텍스트 오브젝트
    public GameObject nextIcon; // 다음 대화가 있음을 알리는 아이콘 (화살표 등)

    [Header("씬 이동 설정")]
    public string nextSceneName; // 대화가 끝나면 이동할 씬의 이름

    [Header("대화 내용")]
    [TextArea(3, 10)] // 인스펙터에서 입력창을 넓게 보여줍니다.
    public string[] sentences; // 대화 문장들을 저장할 배열

    [Header("설정")]
    public float typingSpeed = 0.01f; // 글자가 나타나는 속도

    [Header("오디오 설정")]
    public AudioSource audioSource; // 오디오 소스 컴포넌트
    public AudioClip bgmClip; // 재생할 배경음악

    private int index; // 현재 몇 번째 문장을 보여주고 있는지 체크
    private bool isTyping = false; // 현재 타이핑 효과가 진행 중인지 확인
    private Coroutine blinkCoroutine; // 커서 깜빡임 코루틴을 제어하기 위한 변수

    void Start()
    {
        // 게임 시작 시 초기화
        index = 0;
        
        // 혹시 인스펙터에서 AudioSource를 연결하지 않았다면, 코드로 자동으로 찾거나 추가합니다.
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }

        // BGM 재생
        if (bgmClip != null)
        {
            audioSource.clip = bgmClip;
            audioSource.loop = true; // 반복 재생
            audioSource.spatialBlend = 0f; // 2D 사운드로 강제 설정 (거리에 따라 소리가 작아지는 문제 방지)
            audioSource.Play();
        }
        
        // 대화 내용이 있다면 첫 문장을 타자기 효과로 보여줌
        if(sentences.Length > 0)
        {
            StartCoroutine(Type());
        }
    }

    void Update()
    {
        // 스페이스바를 눌렀을 때
        if (Input.GetKeyDown(KeyCode.Space))
        {
            // 아직 타이핑 중이라면 즉시 완성
            if (isTyping)
            {
                StopAllCoroutines();
                textDisplay.text = sentences[index];
                isTyping = false;
                if (nextIcon != null) 
                {
                    nextIcon.SetActive(true); // 스킵 시 아이콘 표시
                    if (blinkCoroutine != null) StopCoroutine(blinkCoroutine);
                    blinkCoroutine = StartCoroutine(BlinkIcon()); // 깜빡임 시작
                }
            }
            else
            {
                // 타이핑이 끝났다면 다음 문장으로 넘어감
                NextSentence();
            }
        }
    }

    void NextSentence()
    {
        // 다음 문장이 아직 남아있다면
        if (index < sentences.Length - 1)
        {
            index++; // 순서를 하나 올리고
            StartCoroutine(Type()); // 타자기 효과 시작
        }
        else
        {
            // 더 이상 문장이 없을 때 (대화 종료)
            EndDialogue();
        }
    }

    // 한 글자씩 출력하는 코루틴
    IEnumerator Type()
    {
        isTyping = true;
        if (blinkCoroutine != null) StopCoroutine(blinkCoroutine); // 기존 깜빡임 중지
        if (nextIcon != null) nextIcon.SetActive(false); // 아이콘 숨김
        
        textDisplay.text = ""; // 텍스트 초기화
        foreach (char letter in sentences[index].ToCharArray())
        {
            textDisplay.text += letter;
            yield return new WaitForSeconds(typingSpeed);
        }
        isTyping = false;
        if (nextIcon != null) 
        {
            nextIcon.SetActive(true); // 아이콘 표시
            if (blinkCoroutine != null) StopCoroutine(blinkCoroutine);
            blinkCoroutine = StartCoroutine(BlinkIcon()); // 깜빡임 시작
        }
    }

    void EndDialogue()
    {
        // 대화가 끝났을 때의 처리
        textDisplay.text = ""; // 텍스트를 비우거나
        Debug.Log("대화가 종료되었습니다.");
        
        // 대화 종료 시 BGM 정지
        if (audioSource != null)
        {
            audioSource.Stop();
        }

        if (blinkCoroutine != null) StopCoroutine(blinkCoroutine); // 깜빡임 중지
        if (nextIcon != null) nextIcon.SetActive(false); // 대화 종료 시 아이콘 숨김
        
        // [추가된 기능] 씬 이동 로직
        if (!string.IsNullOrEmpty(nextSceneName))
        {
            if (nextSceneName == "Exit")
            {
                Application.Quit();
                #if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
                #endif
            }
            else
            {
                SceneManager.LoadScene(nextSceneName);
            }
        }
        else
        {
            Debug.LogWarning("이동할 씬 이름(Next Scene Name)이 설정되지 않았습니다!");
        }
    }

    // 커서(화살표)를 깜빡이게 하는 코루틴
    IEnumerator BlinkIcon()
    {
        while (true)
        {
            yield return new WaitForSeconds(0.5f); // 0.5초 대기
            if (nextIcon != null) nextIcon.SetActive(!nextIcon.activeSelf); // 껐다 켰다 반복
        }
    }
}