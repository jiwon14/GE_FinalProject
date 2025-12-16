using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class S_BossZoneTrigger : MonoBehaviour
{
    // S_BossController가 사용하는 Zone 타입들을 정의합니다.
    public enum ZoneType { Tracking, Backdash, JumpAttack, Attack }
    [Tooltip("이 Zone의 역할을 선택하세요: Tracking(추적), Backdash(백대쉬), JumpAttack(점프공격), Attack(일반공격)")]
    public ZoneType zoneType;

    private S_BossController bossController;

    void Start()
    {
        // 씬에서 S_BossController를 자동으로 찾아 연결합니다.
        bossController = FindFirstObjectByType<S_BossController>();
        if (bossController == null)
        {
            // S_BossController가 씬에 없을 경우를 대비한 경고입니다. 2페이즈 보스가 활성화되면 정상적으로 찾아 연결됩니다.
            Debug.LogWarning("씬에서 S_BossController를 찾을 수 없습니다. 보스가 활성화되면 자동으로 연결됩니다.");
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // 보스가 아직 활성화되지 않았을 수 있으므로, 다시 한번 찾아봅니다.
        if (bossController == null)
        {
            bossController = FindFirstObjectByType<S_BossController>();
        }

        if (other.CompareTag("Player") && bossController != null)
        {
            bossController.OnPlayerEnterZone(zoneType.ToString());
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        // [수정] 보스가 비활성화 상태에서 활성화될 때 참조가 누락되는 경우를 방지하기 위해, 다시 한번 찾아봅니다.
        if (bossController == null)
        {
            bossController = FindFirstObjectByType<S_BossController>();
        }

        if (other.CompareTag("Player") && bossController != null)
        {
            bossController.OnPlayerExitZone(zoneType.ToString());
        }
    }
}