using System.Collections;
using UnityEngine;
using UnityEngine.UI; // UI를 다루기 위해 필요

public class SceneFader : MonoBehaviour
{
    public Image fadePanel;  // 검은색 패널을 넣을 변수
    public float fadeTime = 2f; // 밝아지는 데 걸리는 시간 (초)

    void Start()
    {
        // 게임 시작 시 코루틴(시간 흐름 제어) 실행
        StartCoroutine(FadeIn());
    }

    IEnumerator FadeIn()
    {
        float currentTime = 0f;
        Color panelColor = fadePanel.color;

        while (currentTime < fadeTime)
        {
            currentTime += Time.deltaTime;
            // 알파값(투명도)을 1(불투명)에서 0(투명)으로 서서히 변경
            float alpha = Mathf.Lerp(1f, 0f, currentTime / fadeTime);
            
            panelColor.a = alpha;
            fadePanel.color = panelColor;

            yield return null; // 한 프레임 대기
        }

        // 확실하게 투명하게 만들고 오브젝트 끄기 (성능 최적화)
        fadePanel.gameObject.SetActive(false); 
    }
}