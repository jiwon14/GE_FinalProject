using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// T_Boss의 공격 히트박스에 부착되어 플레이어와의 충돌을 감지하고 데미지를 처리하는 스크립트.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class T_BossHitbox : MonoBehaviour
{
    [Tooltip("이 히트박스가 가하는 기본 데미지")]
    [SerializeField] private int damage = 1;
    [Tooltip("이 히트박스를 패링했을 때 보스가 데미지와 스턴을 입는지 설정합니다.")]
    public bool causesDamageOnParry = true;

    private T_BossController bossController;
    private HashSet<Collider2D> hitColliders; // 한 번의 공격 동안 이미 맞은 대상을 기록

    void Awake()
    {
        // 부모 오브젝트에서 BossController 참조를 가져옵니다.
        bossController = GetComponentInParent<T_BossController>();
        if (bossController == null)
        {
            Debug.LogError("T_BossHitbox가 부모에서 T_BossController를 찾을 수 없습니다!");
        }
        hitColliders = new HashSet<Collider2D>();
    }

    // 히트박스가 활성화될 때마다 호출됩니다. (애니메이션 이벤트로 활성화 시)
    void OnEnable()
    {
        // 이전에 충돌했던 기록을 모두 초기화하여 새로운 공격 판정이 가능하게 합니다.
        hitColliders.Clear();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // 플레이어 태그를 가지고 있고, 이번 공격에서 아직 맞지 않았다면
        if (other.CompareTag("Player") && !hitColliders.Contains(other))
        {
            // 충돌한 대상을 기록하여 중복 데미지를 방지합니다.
            hitColliders.Add(other);

            // 플레이어 컨트롤러를 찾아 HandleAttack 함수를 호출합니다.
            other.GetComponent<F_PlayerController>()?.HandleAttack(damage, bossController.gameObject, causesDamageOnParry);
        }
    }
}