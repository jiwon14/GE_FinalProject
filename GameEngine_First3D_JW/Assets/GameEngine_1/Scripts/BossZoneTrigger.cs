using UnityEngine;

/// <summary>
/// 보스의 각 Zone에 부착하여 플레이어의 진입/이탈을 T_BossController에 알려주는 스크립트입니다.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class BossZoneTrigger : MonoBehaviour
{
    public enum ZoneType { Tracking, LongAttack, ShortAttack, Close }
    [Tooltip("이 Zone의 역할을 선택하세요.")]
    public ZoneType zoneType;

    private T_BossController bossController;

    void Start()
    {
        // 씬에서 보스 컨트롤러를 자동으로 찾아 연결합니다.
        bossController = FindFirstObjectByType<T_BossController>();
        if (bossController == null)
        {
            Debug.LogError("씬에 T_BossController가 없습니다! 또는 이 스크립트가 보스보다 먼저 생성되었습니다.");
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            if (bossController == null) return;
            switch (zoneType)
            {
                case ZoneType.Tracking: bossController.OnPlayerEnterTrackingZone(); break;
                case ZoneType.LongAttack: bossController.OnPlayerEnterLongAttackZone(); break;
                case ZoneType.ShortAttack: bossController.OnPlayerEnterShortAttackZone(); break;
                case ZoneType.Close: bossController.OnPlayerEnterCloseZone(); break;
            }
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            if (bossController == null) return;
            switch (zoneType)
            {
                case ZoneType.Tracking: bossController.OnPlayerExitTrackingZone(); break;
                case ZoneType.LongAttack: bossController.OnPlayerExitLongAttackZone(); break;
                case ZoneType.ShortAttack: bossController.OnPlayerExitShortAttackZone(); break;
                case ZoneType.Close: bossController.OnPlayerExitCloseZone(); break;
            }
        }
    }
}