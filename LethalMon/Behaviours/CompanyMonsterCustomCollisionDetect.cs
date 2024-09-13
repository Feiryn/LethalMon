using GameNetcodeStuff;
using System.Collections;
using UnityEngine;

namespace LethalMon.Behaviours
{
    internal class CompanyMonsterCustomCollisionDetect : MonoBehaviour
    {
        internal CompanyMonsterAI? _companyMonster = null;
        private bool collided = false;

        void Start()
        {
            _companyMonster = GetComponentInParent<CompanyMonsterAI>();
            if (_companyMonster == null)
            {
                LethalMon.Log("Unable to find company mosnter ai for tentacle collision detector.", LethalMon.LogType.Error);
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
            }
            else if (!other.CompareTag("Untagged"))
                LethalMon.Log("Tentacle collided with tag: " + other.tag + ". Name: " + other.name);
        }

        IEnumerator OnColliderWithPlayer(PlayerControllerB player)
        {
            collided = true;
            Animator monsterAnimator = GetComponentInParent<Animator>();
            Transform monsterAnimatorGrabTarget = gameObject.transform;

            if (monsterAnimator == null)
            {
                LethalMon.Log("No tentacle animator found.");
                yield break;
            }

            monsterAnimator.SetBool("grabbingPlayer", value: true);
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
            monsterAnimator.SetBool("grabbingPlayer", value: false);
            yield return new WaitWhile(() => _companyMonster!.isAttacking);
            if (player.deadBody != null)
            {
                player.deadBody.attachedTo = null;
                player.deadBody.attachedLimb = null;
                player.deadBody.matchPositionExactly = false;
                player.deadBody.gameObject.SetActive(value: false);
            }
            collided = false;
        }
    }
}
