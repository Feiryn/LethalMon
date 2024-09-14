using GameNetcodeStuff;
using System.Collections;
using UnityEngine;

namespace LethalMon.Behaviours
{
    internal class CompanyMonsterCustomCollisionDetect : MonoBehaviour
    {
        internal CompanyMonsterAI? _companyMonster = null;
        private Animator? _monsterAnimator = null;
        private bool collided = false;

        void Start()
        {
            _companyMonster = GetComponentInParent<CompanyMonsterAI>();
            if (_companyMonster == null)
            {
                LethalMon.Log("Unable to find company mosnter ai for tentacle collision detector.", LethalMon.LogType.Error);
                Destroy(this);
            }

            _monsterAnimator = GetComponentInParent<Animator>();
            if (_monsterAnimator == null)
            {
                LethalMon.Log("No tentacle animator found.");
                Destroy(this);
            }
        }

        void OnTriggerEnter(Collider other)
        {
            if (collided || _companyMonster == null || !_companyMonster.isAttacking) return;

            if (other.CompareTag("Player"))
            {
                LethalMon.Log("Tentacle collided with player.");
                StartCoroutine(OnColliderWithPlayer(other.GetComponent<PlayerControllerB>()));
            }
            else if (other.CompareTag("Enemy"))
            {
                LethalMon.Log("Tentacle collided with enemy.");
                StartCoroutine(OnColliderWithEnemy(other.GetComponent<EnemyAI>()));
            }
            else if (other.CompareTag("Props")) // wip
            {
                LethalMon.Log("Tentacle collided with item.");
                StartCoroutine(OnColliderWithItem(other.GetComponent<GrabbableObject>()));
            }
            else if (!other.CompareTag("Untagged"))
                LethalMon.Log("Tentacle collided with tag: " + other.tag + ". Name: " + other.name);
        }

        IEnumerator OnColliderWithPlayer(PlayerControllerB player)
        {
            if (_monsterAnimator == null) yield break;

            collided = true;
            Transform monsterAnimatorGrabTarget = gameObject.transform;

            _monsterAnimator.SetBool("grabbingPlayer", value: true);
            monsterAnimatorGrabTarget.position = player.transform.position;
            yield return new WaitForSeconds(0.05f);
            if (player.IsOwner)
            {
                player.KillPlayer(Vector3.zero);
            }
            float startTime = Time.realtimeSinceStartup;
            yield return new WaitUntil(() => player.deadBody != null || Time.realtimeSinceStartup - startTime > 4f);
            if (player.deadBody != null)
            {
                player.deadBody.attachedTo = monsterAnimatorGrabTarget; // GrabPoint initially
                player.deadBody.attachedLimb = player.deadBody.bodyParts[6];
                player.deadBody.matchPositionExactly = true;
            }
            else
            {
                Debug.Log("Player body was not spawned in time for animation.");
            }
            yield return new WaitWhile(() => _companyMonster!.isAttacking);
            _monsterAnimator.SetBool("grabbingPlayer", value: false);
            if (player.deadBody != null)
            {
                player.deadBody.attachedTo = null;
                player.deadBody.attachedLimb = null;
                player.deadBody.matchPositionExactly = false;
                player.deadBody.gameObject.SetActive(value: false);
            }
            collided = false;
        }

        IEnumerator OnColliderWithEnemy(EnemyAI enemy)
        {
            if (_monsterAnimator == null || _companyMonster == null) yield break;
            collided = true;

            _monsterAnimator.SetBool("grabbingPlayer", value: true);
            yield return new WaitForSeconds(0.05f);

            enemy.enabled = false;
            enemy.transform.SetParent(gameObject.transform, true);

            yield return new WaitWhile(() => _companyMonster!.isAttacking);
            _monsterAnimator.SetBool("grabbingPlayer", value: false);

            _companyMonster.CaughtEnemy(enemy);

            if(enemy.dieSFX != null)
                Utils.PlaySoundAtPosition(enemy.transform.position, enemy.dieSFX);

            LethalMon.Log("ENEMY DIED FROM COMPANY MONSTER!", LethalMon.LogType.Warning);
            RoundManager.Instance.DespawnEnemyOnServer(enemy.NetworkObject);

            collided = false;
        }

        IEnumerator OnColliderWithItem(GrabbableObject item)
        {
            if (_monsterAnimator == null || _companyMonster == null) yield break;
            collided = true;

            _monsterAnimator.SetBool("grabbingPlayer", value: true);
            yield return new WaitForSeconds(0.05f);
            item.transform.SetParent(gameObject.transform, true);

            yield return new WaitWhile(() => _companyMonster!.isAttacking);
            _monsterAnimator.SetBool("grabbingPlayer", value: false);

            _companyMonster.CaughtItem(item);

            collided = false;
        }
    }
}
