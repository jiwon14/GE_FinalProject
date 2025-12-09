using UnityEngine;

/// <summary>
/// 두 번째 보스(T_Boss)의 다양한 감지 범위를 관리하는 스크립트.
/// </summary>
public class T_BossDetectionZone : MonoBehaviour
{
    public enum ZoneType
    {
        Tracking,     // 플레이어 추적 시작/종료
        MeleeAttack,  // 근접 공격이 가능한 범위
        TooClose      // 너무 가까워서 회피 텔레포트를 사용해야 하는 범위
        // 향후 원거리 공격 등 새로운 패턴을 위한 존 타입을 여기에 추가할 수 있습니다.
    }

    [Tooltip("이 존의 역할을 선택하세요.")]
    public ZoneType zoneType;

    private T_BossController bossController;

    void Awake()
    {
        // 부모 오브젝트에서 T_BossController를 찾아옵니다.
        bossController = GetComponentInParent<T_BossController>();
        if (bossController == null)
        {
            Debug.LogError("부모 오브젝트에서 T_BossController를 찾을 수 없습니다!", gameObject);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            switch (zoneType)
            {
                case ZoneType.Tracking:     bossController.OnPlayerEnterTrackingRange(); break;
                case ZoneType.MeleeAttack:  bossController.OnPlayerEnterMeleeRange();    break;
                case ZoneType.TooClose:     bossController.OnPlayerEnterTooCloseRange(); break;
            }
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            switch (zoneType)
            {
                case ZoneType.Tracking:     bossController.OnPlayerExitTrackingRange(); break;
                case ZoneType.MeleeAttack:  bossController.OnPlayerExitMeleeRange();    break;
                case ZoneType.TooClose:     bossController.OnPlayerExitTooCloseRange(); break;
            }
        }
    }
}