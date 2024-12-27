using UnityEngine;
using Mirror;

using MTPSKIT.UI.HUD;
using MTPSKIT;

namespace MTPSKIT.Gameplay
{

    
    public class Health : NetworkBehaviour
    {
        
        [Header("Health")]
        /// <summary>
        /// character name, used for example for killfeed
        /// </summary>
        public string CharacterName = "DEFAULT";
        public int CurrentHealth = 100;
        public int MaxHealth = 100;

        public int Team = -1;

        [Header("Element 1 -Body, 2 -Head, another ones can be custom")]
        [SerializeField] float[] damageMultipliers = { 1f, 2, 0.5f };


        //server callbacks
        public delegate void SHealthDepleted(CharacterPart characterPart, AttackType attackType, Health killer, int attackForce);
        public SHealthDepleted Server_OnHealthDepleted { set; get; } //when this object is killed on server

        public KilledCharacter Server_KilledCharacter;

        #region assists
        public delegate void KilledCharacter(Health victimID);
        public KilledCharacter Server_OnEarnedAssist;
        #endregion

        public delegate void SOnDamaged(int currentHealth, CharacterPart damagedPart, AttackType attackType, Health attacker, int attackForce);
        public SOnDamaged Server_OnDamaged { set; get; } //when this object is killed on server



        //client callbacks
        public delegate void ClientHealthStateChanged(int currentHealth, CharacterPart damagedPart, AttackType attackType, Health attackerID);
        public ClientHealthStateChanged Client_OnHealthStateChanged { set; get; }



        public delegate void ClientHealthAdded(int currentHealth, int addedHealth, uint healerID);
        public ClientHealthAdded Client_OnHealthAdded { set; get; }



        public delegate void HealthDepleted(CharacterPart damagedPart, Health attacker);
        public HealthDepleted Client_OnHealthDepleted { set; get; } //when this object is killed on client



        public delegate void KillConfirmation(CharacterPart damagedPart, Health victim);
        public HealthDepleted Client_KillConfirmation { set; get; }
        public KilledCharacter Client_OnEarnedAssist { set; get; } 


        public delegate void ClientDamageDealt(int currentHealth, int takenDamage, CharacterPart damagedPart, AttackType attackType, Health victim);
        public ClientDamageDealt Client_OnDamageDealt { set; get; }


        public delegate void ResurrectEvent(int health);
        public ResurrectEvent Server_Resurrect { set; get; }
        public ResurrectEvent Client_Resurrect { set; get; }


        bool _dead = false;
        bool _clientDead = false;

        public Vector3 centerPosition = new Vector3(0, 1.5f, 0);

        #region register/deregister health object

        Transform _targetPositionTracker;

        public DamageMemory[] _damageMemories;
        int _lastDamageMemory = 0;

        uint _netID;
        protected virtual void Awake() 
        {
            _targetPositionTracker = new GameObject("TargetTracker").transform;
            _targetPositionTracker.transform.SetParent(transform);
            _targetPositionTracker.transform.localPosition = centerPosition;
            _targetPositionTracker.transform.localRotation = Quaternion.identity;
        }

        protected virtual void Start()
        {
            _netID = netId;

            _damageMemories = new DamageMemory[6];

            gameObject.layer = (int)GameLayers.character;

            CustomSceneManager.RegisterCharacter(this); //we have to register spawned characters in order to let bot "see" them, and select nearest enemies from that register
            GameManager.AddHealthInstance(this);
        }
        protected virtual void OnDestroy()
        {
            CustomSceneManager.DeRegisterCharacter(this);
            GameManager.RemoveHealthInstance(_netID);
        }
        #endregion


        //process taking damage server side
        public void Server_ChangeHealthState(int damage, CharacterPart damagedPart, AttackType attackType, Health attacker, int attackForce)
        {
            int finalDamage = Mathf.FloorToInt(damage * damageMultipliers[(int)damagedPart]);

            if (!(!GameManager.Gamemode.FriendyFire && attacker != this && attacker.Team == Team)) //avoid friendly fire
                CurrentHealth -= finalDamage;

            RememberDamage(attacker, (short)finalDamage);

            CurrentHealth = Mathf.Clamp(CurrentHealth, 0, 100000);
            RpcHealthStateChanged(CurrentHealth, finalDamage, damagedPart, attackType, attacker);

            Server_OnDamaged?.Invoke(CurrentHealth, damagedPart, attackType, attacker, attackForce);

            if (CurrentHealth <= 0 && !_dead)
            {
                Server_OnHealthDepleted?.Invoke(damagedPart, attackType, attacker, attackForce);
                _dead = true;

                RpcNotifyGamemode(damagedPart, attackType, attacker, attackForce, EvaluateAssist(attacker));
            }
        }

        //process taking damage server side
        public void Server_ChangeHealthStateRaw(int damage, CharacterPart damagedPart, AttackType attackType, Health attacker, int attackForce)
        {
            if (!(!GameManager.Gamemode.FriendyFire && attacker != this && attacker.Team == Team)) //avoid friendly fir
                CurrentHealth -= damage;

            CurrentHealth = Mathf.Clamp(CurrentHealth, 0, 100000);

            RememberDamage(attacker, (short)damage);

            if (CurrentHealth <= 0 && !_dead)
            {
                Server_OnHealthDepleted?.Invoke(damagedPart, attackType, attacker, attackForce);
                _dead = true;

                RpcNotifyGamemode(damagedPart, attackType, attacker, attackForce, EvaluateAssist(attacker));
            }

            RpcHealthStateChanged(CurrentHealth, damage, damagedPart, attackType, attacker);
        }

        [ClientRpc]
        void RpcHealthStateChanged(int currentHealth, int takenDamage, CharacterPart damagedPart, AttackType attackType, Health attacker)
        {
            if (!isServer)
                CurrentHealth = currentHealth;

            //Health attacker = GameSync.Singleton.Healths.GetObj(attackerID);

            if (!attacker) return;

            if (!_clientDead)
            {
                Client_OnHealthStateChanged?.Invoke(currentHealth, damagedPart, attackType, attacker);

                attacker.Client_OnDamageDealt?.Invoke(currentHealth, takenDamage, damagedPart, attackType, this);

                if (CurrentHealth <= 0)
                {
                    Client_OnHealthDepleted?.Invoke(damagedPart, attacker);


                    if (attacker)
                        attacker.Client_KillConfirmation?.Invoke(damagedPart, this);

                    _clientDead = true;
                }
            }
        }

        #region assists
        void RememberDamage(Health attackerToRemember, short receivedDamage)
        {
            int currentID = DoWeRememberHim(attackerToRemember);

            if (currentID > -1)
            {
                _damageMemories[currentID].DamageDealt += (ushort)receivedDamage;
                _lastDamageMemory = currentID + 1;
            }
            else
            {
                if (_lastDamageMemory >= _damageMemories.Length)
                    _lastDamageMemory = 0;

                _damageMemories[_lastDamageMemory].DamageDealt = (ushort)receivedDamage;
                _damageMemories[_lastDamageMemory].PIID = attackerToRemember;

                _lastDamageMemory++;
            }

            int DoWeRememberHim(Health attacker)
            {
                for (int i = 0; i < _damageMemories.Length; i++)
                {
                    DamageMemory mem = _damageMemories[i];
                    if (mem.DamageDealt > 0 && mem.PIID == attacker) return i;
                }
                return -1;
            }
        }
        protected Health EvaluateAssist(Health killer)
        {
            int highestDmg = 0;
            Health id = null;

            DamageMemory mem;

            for (byte i = 0; i < _damageMemories.Length; i++)
            {
                mem = _damageMemories[i];
                if (mem.PIID != killer && mem.DamageDealt > highestDmg)
                {
                    highestDmg = mem.DamageDealt;
                    id = mem.PIID;
                }
            }

            if (id && highestDmg > Mathf.FloorToInt(MaxHealth * 0.5f))
            {
                id.Server_OnEarnedAssist?.Invoke(this);
                id.RpcOnAssistEarned(this);
                return id;
            }
            
            return null;
        }
        #endregion
        [ClientRpc]
        void RpcOnAssistEarned(Health victim) 
        {
            Client_OnEarnedAssist?.Invoke(victim);
        }


        [ClientRpc]
        void RpcNotifyGamemode(CharacterPart hittedPart, AttackType attackType, Health attacker, int attackForce, Health assist)
        {
            GameManager.Gamemode.Client_PlayerKilledByPlayer?.Invoke(this, hittedPart, attackType, attacker, assist); //killfeed listens to this event
        }


        [ClientRpc]
        void RpcHealthAdded(int currentHealth, int addedHealth, uint healerID)
        {
            if (!isServer)
                CurrentHealth = currentHealth;

            Client_OnHealthAdded?.Invoke(currentHealth, addedHealth, healerID);
        }

        public int CountDamage(byte hittedPart, int damage)
        {
            return Mathf.FloorToInt(damage * damageMultipliers[hittedPart]);
        }

        //for AI to know where to aim at
        public Vector3 GetPositionToAttack()
        {
            return _targetPositionTracker.position;
        }

        //execute only on server, then server will update health state for every client
        public int ServerHeal(int healthToAdd, uint healerID)
        {
            if (CurrentHealth == MaxHealth) return 0;

            int neededHealth = MaxHealth - CurrentHealth;

            if (neededHealth > healthToAdd)
                neededHealth = healthToAdd;

            CurrentHealth += neededHealth;

            RpcHealthAdded(CurrentHealth, neededHealth, healerID);

            return neededHealth;
        }

        public void ServerResurrect()
        {
            //prepare client for resurrection
            RpcClientResurrect();

            //resurrect
            _dead = false;
            CurrentHealth = MaxHealth;
            Server_Resurrect?.Invoke(MaxHealth);
        }
        [ClientRpc]
        public void RpcClientResurrect()
        {
            _clientDead = false;
            CurrentHealth = MaxHealth;
            Client_Resurrect?.Invoke(CurrentHealth);
        }
    }

    [System.Serializable]
    public struct DamageMemory
    {
        public Health PIID;
        public int DamageDealt;
    }

    public enum CharacterPart : byte
    {
        body,
        head,
        legs,
    }

    public enum AttackType : byte
    {
        hitscan,
        melee,
        explosion,
        falldamage,
    }
}