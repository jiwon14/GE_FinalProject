using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class S_BossHitbox : MonoBehaviour
{
    [Header("공격 설정")]
    [Tooltip("이 히트박스에 닿았을 때 플레이어가 입을 데미지입니다.")]
    public int damage = 10;

    [Tooltip("피격 시 넉백을 적용할지 여부입니다.")]
    public bool enableKnockback = true;

    [Tooltip("이 히트박스를 패링했을 때 보스가 데미지와 스턴을 입는지 설정합니다.")]
    public bool causesDamageOnParry = true;

    private S_BossController bossController;
    private HashSet<Collider2D> hitColliders; // 한 번의 공격 동안 이미 맞은 대상을 기록

    void Awake()
    {
        bossController = GetComponentInParent<S_BossController>();
        hitColliders = new HashSet<Collider2D>();
    }

    void OnEnable()
    {
        hitColliders.Clear();
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // 플레이어 태그 확인
        if (collision.CompareTag("Player") && !hitColliders.Contains(collision))
        {
            hitColliders.Add(collision);

            F_PlayerController player = collision.GetComponent<F_PlayerController>();
            if (player != null)
            {
                // 보스 컨트롤러가 있으면 그것을, 없으면(예외) 히트박스 자신을 전달
                GameObject attacker = bossController != null ? bossController.gameObject : this.gameObject;
                player.HandleAttack(damage, attacker, causesDamageOnParry, enableKnockback);
            }
        }
    }
}