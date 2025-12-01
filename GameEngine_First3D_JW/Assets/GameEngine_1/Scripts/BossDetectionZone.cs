using UnityEngine;
using UnityEngine.Events; // UnityEvent를 사용하기 위해 추가

public class BossDetectionZone : MonoBehaviour
{
    // Inspector 창에서 설정할 이벤트
    [Tooltip("플레이어가 이 구역에 들어왔을 때 호출될 이벤트")]
    public UnityEvent OnPlayerEnter;

    [Tooltip("플레이어가 이 구역에서 나갔을 때 호출될 이벤트")]
    public UnityEvent OnPlayerExit;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            // 설정된 Enter 이벤트를 호출합니다.
            OnPlayerEnter.Invoke();
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            // 설정된 Exit 이벤트를 호출합니다.
            OnPlayerExit.Invoke();
        }
    }
}