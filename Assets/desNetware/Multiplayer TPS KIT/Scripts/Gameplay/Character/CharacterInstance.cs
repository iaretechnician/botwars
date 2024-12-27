using System.Collections.Generic;
using UnityEngine;
using Mirror;
using MTPSKIT.UI.HUD;
using MTPSKIT.Gameplay.Gamemodes;
using MTPSKIT.UI;
using System;

namespace MTPSKIT.Gameplay {

    /// <summary>
    /// This component is responsible for managing and animating character
    /// </summary>
    [RequireComponent(typeof(CharacterAnimator))]
    public class CharacterInstance : Health
    {
        private GameObject _myUIMarker;
        public Transform CharacterMarkerPosition;

        [HideInInspector] public Transform FPPCameraTarget; //Transform that player's camera will stick to

        /// <summary>
        /// hand to place item in
        /// </summary>
        public Transform ItemTarget;

        /// <summary>
        /// items can stick to character model in wrong direction, so adjusting this varable will correct that
        /// </summary>
        public Vector3 itemRotationCorrector;


        [SerializeField] GameObject CharacterModel;


        public delegate void CharacterEvent_SetAsBOT(bool _set);
        public CharacterEvent_SetAsBOT Server_SetAsBOT;

        public Transform characterMind; //this objects indicated direction character is looking at
        public Transform characterFirePoint; //this object is child of characterMind, will be used for recoil of weapons


        /// <summary>
        /// character controller
        /// </summary>
        private CharacterMotor _motor;

        /// <summary>
        /// hitbox prefab to assign to player model
        /// </summary>
        [SerializeField] GameObject _hitBoxContainerPrefab;
        HitboxSetup _hitboxes;

        [HideInInspector] public PlayerRecoil PlayerRecoil;

        public bool IsUsingItem = false;

        public bool IsRunning { get; set; }


        public bool Block { private set; get; } = false; //determines if character can move and shoot or not, block if it is end of round

        /// <summary>
        /// only true for character that is controlled by client, so only for player controller
        /// </summary>
        public bool IsObserved { set; get; }

        /// <summary>
        /// indicates if character is controlled by server or client
        /// </summary>
        public bool BOT = false;

        Health _killer;

        [HideInInspector]public CharacterItemManager CharacterItemManager;

        [Header("Player/Bot input")]
        #region input to synchronize
        public Vector2 lookInput;
        public Vector2 movementInput { get; private set; }
        byte _actionCode;

        #endregion
        //this flag is needed to determine if we need to animate upper part of character to show reload animation while running
        public bool IsReloading = false;
        public bool IsAiming;

        public delegate void CharacterEvent_OnPickedupObject(string message);
        public CharacterEvent_OnPickedupObject Client_OnPickedupObject { get; set; }


        public delegate void CharacterEvent(float time);
        public CharacterEvent CharacterEvent_OnItemUsed { get; set; }

        public CharacterAnimator CharacterAnimator { private set; get; }
        public bool IsCrouching { get; internal set; }
        public bool IsGrounded { get; set; }

        protected override void Awake()
        {
            base.Awake();
            CharacterAnimator = GetComponent<CharacterAnimator>();
            CharacterItemManager = GetComponent<CharacterItemManager>();
            PlayerRecoil = GetComponent<PlayerRecoil>();
            _motor = GetComponent<CharacterMotor>();

            CustomSceneManager.RegisterCharacter(this);//we have to register spawned characters in order to let bot "see" them, and select nearest enemies from that register

            Server_OnHealthDepleted += ServerDeath;
            Client_OnHealthStateChanged += ClientOnHealthStateChanged;

            _hitboxes = Instantiate(_hitBoxContainerPrefab, transform.position, transform.rotation).GetComponent<HitboxSetup>();
            _hitboxes.SetHiboxes(CharacterModel, this);
            _hitboxes.transform.SetParent(transform);
        }
        protected override void Start()
        {
            base.Start();

            RoomManager.RoomTick += CharacterInstance_Tick;

            lookInput.y = transform.eulerAngles.y; //assigning start look rotation to spawnpoint rotation

            ObserveCharacter(isOwned);

            if (isOwned)
            {
                ClientFrontend.SetObservedCharacter(this);
                IsObserved = true;
            }

            gameObject.layer = 6; //setting apppropriate layer for character collisions
            GameManager.SetLayerRecursively(CharacterModel, 8);

            //spawning teammate marker
            if (!_myUIMarker && !isOwned && Team == ClientFrontend.ThisClientTeam && !GameManager.Gamemode.FFA)
            {
                _myUIMarker = Instantiate(UICharacter._instance.UIMarkerPrefab);
                _myUIMarker.transform.SetParent(UICharacter._instance.WorldIconBorders.transform);
                _myUIMarker.GetComponent<PlayerNametag>().SetupNameplate(this);
            }

        }

        void Update()
        {
            lookInput.x = Mathf.Clamp(lookInput.x, -90f, 90f);

            if (Block)
                movementInput = Vector2.zero;

            if (_killer && isOwned)
                ClientInterfaceManager.Instance.TPPCameraAssignCharacterToLookAt(_killer);
        }

        #region input networking
        void CharacterInstance_Tick()
        {
            if (isOwned)
            {
                NetworkClient.Send(ClientPrepareInputMessage(), Channels.Unreliable);
            }
            else if (BOT)
            {
                RpcReceiveInputFromServer(movementInput, lookInput, _actionCode);
            }
        }

        /// <summary>
        /// send player input to every client except client who sent it, so we can rotate character correcly and 
        /// play appropriate animations
        /// </summary>
        [ClientRpc(includeOwner = false)]
        void RpcReceiveInputFromServer(Vector2 movement, Vector2 look, byte inputCode)
        {
            if (BOT && isServer) return;

            movementInput = movement;
            lookInput = look;
            _actionCode = inputCode;
        }
        #endregion


        private void ClientOnHealthStateChanged(int currentHealth, CharacterPart damagedPart, AttackType attackType, Health attacker)
        {
            if (currentHealth > 0) return;

            //Set camera to follow killer
            if (IsObserved && attacker) {
                ClientInterfaceManager.Instance.TPPCameraAssignCharacterToLookAt(attacker);
            }

            _hitboxes.DisableHitboxes();
            _motor.Die(damagedPart, attacker); //disable movement for dead character

            GetComponent<CharacterController>().enabled = false;

            GameManager.SetLayerRecursively(CharacterModel, 9); //set ragdoll layer

            DespawnCharacterMarker();
        }

        private void ServerDeath(CharacterPart characterPart, AttackType attackType, Health killer, int attackForce)
        {
            if (killer) {
                CharacterInstance killerChar = killer.GetComponent<CharacterInstance>();

                if (killerChar)
                    killerChar.Server_KilledCharacter?.Invoke(this);
            }

            GameManager.Gamemode.Server_OnPlayerKilled(this, killer);

            GetComponent<CharacterController>().enabled = false;
        }

        public void SetAsBOT(bool _set) 
        {
            BOT = _set;
            Server_SetAsBOT?.Invoke(_set);
        }

        public void ObserveCharacter(bool _observe)
        {
            if (_observe)
                ClientInterfaceManager.Instance.AssignTPPCamera(this);
        }

        private void FixedUpdate()
        {
            //character animations
            if (Block)
                movementInput = Vector2.zero;

            IsGrounded = Physics.CheckSphere(transform.position + new Vector3(0, 0.3f, 0), 0.5f, GameManager.environmentLayer);
        }

        public void SetMovementInput(Vector2 input) 
        {
            if (Block) return;

            movementInput = input;
        }
        public void SetMovementInput(float x, float y) 
        {
            if (Block) return;

            SetMovementInput(new Vector2(x, y));
        }

        void DespawnCharacterMarker()
        {
            if (_myUIMarker)
                Destroy(_myUIMarker);
        }

        public void BlockCharacter(bool block) 
        {
            Block = block;
            RpcBlockCharacter(block);
        }
        [ClientRpc]
        private void RpcBlockCharacter(bool block) 
        {
            Block = block;
        }

        #region input


        public ClientSendInputMessage ClientPrepareInputMessage()
        {
            return new ClientSendInputMessage {
                Movement = FitMovementInputToOneByte(movementInput),
                LookX = (sbyte)Mathf.FloorToInt(lookInput.x),
                LookY = (short)lookInput.y,
                ActionCodes = _actionCode
            };
        }
        //public CharacterInputMessage ServerPrepareInputMessage()
        //{
        //    InputMessage.Movement = FitMovementInputToOneByte(Input.Movement);
        //    InputMessage.LookX = (sbyte)Mathf.FloorToInt(Input.LookX);
        //    InputMessage.LookY = (short)Input.LookY;
        //    InputMessage.ActionCodes = Input.ActionCodes;

        //    return InputMessage;
        //}


        public byte FitMovementInputToOneByte(Vector2 movement)
        {
            // Two small signed numbers (values between -8 to 7)
            int mX = Mathf.FloorToInt(movement.x / 0.2f);
            int mY = Mathf.FloorToInt(movement.y / 0.2f);

            // Convert the numbers to 4-bit two's complement representation
            byte first4Bits = (byte)((mX < 0 ? 0x08 : 0x00) | (Math.Abs(mX) & 0x07)); // Check sign bit and keep last 3 bits
            byte second4Bits = (byte)((mY < 0 ? 0x08 : 0x00) | (Math.Abs(mY) & 0x07)); // Check sign bit and keep last 3 bits

            // Combine the two 4-bit representations into a single byte
            return (byte)((first4Bits << 4) | second4Bits);
        }


        public void ReadAndApplyInputFromClient(ClientSendInputMessage msg)
        {
            if (!BOT && !isOwned)
                ApplyInput(msg.Movement, msg.LookX, msg.LookY, msg.ActionCodes);

            RpcReceiveInputFromServer(movementInput, lookInput, _actionCode);
        }
        /*public void ReadAndApplyInputFromServer(CharacterInputMessage msg)
        {
            ApplyInput(msg.Movement, msg.LookX, msg.LookY, msg.ActionCodes);
        }*/

        void ApplyInput(byte packedMovementInput, float lookInputX, float lookInputY, byte actionCodes)
        {
            SetMovementInput(new Vector2(((packedMovementInput & 0x70) >> 4) * ((packedMovementInput & 0x80) == 0x80 ? -1 : 1) * 0.2f,
               (packedMovementInput & 0x07) * ((packedMovementInput & 0x08) == 0x08 ? -1 : 1) * 0.2f
            ));

            
            lookInput.x = lookInputX;
            lookInput.y = lookInputY;
            _actionCode = actionCodes;

            //_currentRotationTargetX = lookInputX;
            //_currentRotationTargetY = lookInputY;
        }


        public bool ReadActionKeyCode(ActionCodes actionCode)
        {
            return (_actionCode & (1 << (int)actionCode)) != 0;
        }
        public void SetActionKeyCode(ActionCodes actionCode, bool _set)
        {
            int a = _actionCode;
            if (_set)
            {
                a |= 1 << ((byte)actionCode);
            }
            else
            {
                a &= ~(1 << (byte)actionCode);
            }
            _actionCode = (byte)a;
        }
        
        #endregion

        protected override void OnDestroy()
        {
            base.OnDestroy();
            RoomManager.RoomTick -= CharacterInstance_Tick;

            CustomSceneManager.DeRegisterCharacter(this);

            DespawnCharacterMarker();
        }
    }
    public enum ActionCodes
    {
        Trigger1,
        Trigger2,
        Sprint,
        Crouch,
    }
}
