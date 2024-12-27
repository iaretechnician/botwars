using System;
using UnityEngine;

namespace MTPSKIT.Gameplay
{
    /// <summary>
    /// script responsible for hi
    /// </summary>
    public class HumanoidDeath : MonoBehaviour
    {
        [Tooltip("Ragdoll prefab that will be spawned on character death")]
        [SerializeField] GameObject ragDoll_Prefab;

        [Tooltip("Clip that will be player when character receives headshot")]
        [SerializeField] AudioClip _headShot;

        AudioSource _audioSource;

        RagDoll _spawnedRagdoll;

        CharacterInstance _characterInstance;
        RagDollSyncer _ragDollSyncer;

        [Header("CharacterBones")]
        [SerializeField] Transform _head;
        [SerializeField] Transform _chest;

        [Header("Pools")]
        [SerializeField] GameObject _headBloodEffectPrefab;
        private ObjectPool _headBloodEffectPool;
        [SerializeField] GameObject _bloodEffectPrefab;
        private ObjectPool _bloodEffectPool;
        private void Awake()
        {
            _characterInstance = GetComponent<CharacterInstance>();
            _audioSource = GetComponent<AudioSource>();
            _ragDollSyncer = GetComponent<RagDollSyncer>();
        }
        private void Start()
        {
            _headBloodEffectPool = ObjectPooler.Instance.GetPoolByName(_headBloodEffectPrefab.name);
            _bloodEffectPool = ObjectPooler.Instance.GetPoolByName(_bloodEffectPrefab.name);

            Health h = GetComponent<Health>();
            h.Server_OnHealthDepleted += ServerHealthDepleted;
            h.Client_OnHealthStateChanged += ClientOnHealthStateChanged;
            h.Client_OnHealthDepleted += ClientOnHealthDepleted;
            h.Client_Resurrect += OnResurrect;
            h.Server_Resurrect += OnResurrect;
        }

        private void ClientOnHealthDepleted(CharacterPart damagedPart, Health attacker)
        {
            _characterInstance.CharacterAnimator.enabled = false;
            _characterInstance.CharacterAnimator.ShowModel(false);
            _spawnedRagdoll = SpawnRagdoll();
            _spawnedRagdoll.ActivateRagdoll(damagedPart);

            GetComponent<RagDollSyncer>().AssignRagdoll(_spawnedRagdoll);

            if (damagedPart == CharacterPart.head)
            {
                PooledObject headBlood = _headBloodEffectPool.ReturnObject(_spawnedRagdoll._head.transform.position, _spawnedRagdoll._head.transform.rotation);
                headBlood.transform.LookAt(attacker.GetPositionToAttack());
            }
        }

        private void OnResurrect(int health)
        {
            _characterInstance.CharacterAnimator.ShowModel(true);
        }

        private void ClientOnHealthStateChanged(int currentHealth, CharacterPart damagedPart, AttackType attackType, Health attackerID)
        {

            //play headshot clip when hitted in head, plays always when character receives damage, not only for death
            if (damagedPart == CharacterPart.head)
                _audioSource.PlayOneShot(_headShot);

            //executes only on death, hides player model and spawns ragdoll
            if (currentHealth > 0)
            { 
                PooledObject blood = _bloodEffectPool.ReturnObject(_chest.transform.position, _chest.transform.rotation);

                if (damagedPart == CharacterPart.head)
                    blood.transform.SetPositionAndRotation(_head.position, _head.rotation);
            }
        }

        private void ServerHealthDepleted(CharacterPart characterPart, AttackType attackType, Health killer, int attackForce)
        {
            CharacterInstance characterInstance = GetComponent<CharacterInstance>();

            _spawnedRagdoll = SpawnRagdoll();

            Vector3 movementDirection = transform.rotation * new Vector3(characterInstance.movementInput.x, 0, characterInstance.movementInput.y);

            _spawnedRagdoll.ServerActivateRagdoll(
                _characterInstance.GetPositionToAttack(),
                killer.GetPositionToAttack(),
                movementDirection * (characterInstance.ReadActionKeyCode(ActionCodes.Sprint) ? 2f : 1f),
                characterPart,
                (short)attackForce
                );
            _ragDollSyncer.ServerStartSynchronizingRagdoll(_spawnedRagdoll.GetComponent<RagDoll>());
        }


        RagDoll SpawnRagdoll() 
        {
            if (!_spawnedRagdoll)
            {
                _spawnedRagdoll = Instantiate(ragDoll_Prefab, transform.position, transform.rotation).GetComponent<RagDoll>();
                _spawnedRagdoll.transform.SetParent(transform);
                return _spawnedRagdoll;
            }
            else 
            {
                return _spawnedRagdoll;
            }
        }
    }
}