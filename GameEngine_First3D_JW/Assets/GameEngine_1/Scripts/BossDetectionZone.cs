using UnityEngine;

public class BossDetectionZone : MonoBehaviour
{
    public enum ZoneType
    {
        Tracking,
        Attack,
        Backstep
    }

    [Tooltip("이 존의 역할을 선택하세요.")]
    public ZoneType zoneType;

    private F_BossController bossController;

    void Awake()
    {
        // 부모 오브젝트에서 F_BossController를 찾아옵니다.
        bossController = GetComponentInParent<F_BossController>();
        if (bossController == null)
        {
            Debug.LogError("부모 오브젝트에서 F_BossController를 찾을 수 없습니다!", gameObject);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            switch (zoneType)
            {
                case ZoneType.Tracking:
                    bossController.OnPlayerEnterTrackingRange();
                    break;
                case ZoneType.Attack:
                    bossController.OnPlayerEnterAttackRange();
                    break;
                case ZoneType.Backstep:
                    bossController.OnPlayerEnterBackstepRange();
                    break;
            }
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            switch (zoneType)
            {
                case ZoneType.Tracking:
                    bossController.OnPlayerExitTrackingRange();
                    break;
                case ZoneType.Attack:
                    bossController.OnPlayerExitAttackRange();
                    break;
                case ZoneType.Backstep:
                    bossController.OnPlayerExitBackstepRange();
                    break;
            }
        }
    }
}
