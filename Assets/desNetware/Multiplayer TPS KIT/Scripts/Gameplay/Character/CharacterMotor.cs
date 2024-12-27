using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

namespace MTPSKIT.Gameplay
{
    /// <summary>
    /// Script responsible for character movement
    /// </summary>
    [RequireComponent(typeof(CharacterInstance))]
    public class CharacterMotor : NetworkBehaviour
    {
        CharacterInstance _charInstance;
        CharacterController _controller;

        public float WalkSpeed = 5f;
        public float RunSpeed = 10f;
        public float JumpHeight;
        public float FallingSpeed = 10f;
        float _speed;

        Vector3 force;
        bool _jumped;

#if UNITY_EDITOR
        [Header("Noclip")]
        bool _noclip = false;
        [SerializeField] float noclipSpeed = 10;
        [SerializeField] float noclipRunSpeed = 30;
#endif

        Vector3 defaultAttackPos;
        Vector3 crouchAttackPos = new Vector3(0, 0.7f, 0);

        

        void Start()
        {
            _charInstance = GetComponent<CharacterInstance>();
            _controller = GetComponent<CharacterController>();
        }
        void Update()
        {
            //rotate character based on player mouse input/bot input
            if(_charInstance.CurrentHealth>0)
                transform.rotation = Quaternion.Euler(0, _charInstance.lookInput.y, 0);

            if(isOwned || (_charInstance.BOT&&isServer))
                MovementTick();

        }

        private void FixedUpdate()
        {
            _charInstance.IsGrounded = Physics.CheckSphere(transform.position + new Vector3(0, 0.3f, 0), 0.5f, GameManager.environmentLayer);

            _charInstance.IsCrouching = _charInstance.ReadActionKeyCode(ActionCodes.Crouch) && _charInstance.IsGrounded;

            _charInstance.IsRunning =
                    _charInstance.ReadActionKeyCode(ActionCodes.Sprint) && _charInstance.IsGrounded &&
                    !_charInstance.IsUsingItem && _charInstance.movementInput.y > 0 &&
                    (_charInstance.CharacterItemManager.CurrentlyUsedItem == null || _charInstance.CharacterItemManager.CurrentlyUsedItem &&
                    !_charInstance.CharacterItemManager.CurrentlyUsedItem.DisableRunningAbility) &&
                    !_charInstance.IsCrouching;


            if (!_charInstance.IsCrouching)
            {
                if (_charInstance.ReadActionKeyCode(ActionCodes.Crouch) && _charInstance.IsGrounded && !CheckSphere())
                {
                    Crouch();
                    _charInstance.IsCrouching = true;
                }
            }
            else
            {
                if (!_charInstance.ReadActionKeyCode(ActionCodes.Crouch) && !CheckSphere())
                {
                    Stand();
                    _charInstance.IsCrouching = false;
                }
            }
        }

        public void MovementTick() 
        {

#if UNITY_EDITOR
            if (Input.GetKeyDown(KeyCode.J))
                _noclip = !_noclip;

            if (_noclip && isOwned)
            {
                Vector3 noclipInput = new Vector3(_charInstance.movementInput.x, (Input.GetKey(KeyCode.LeftControl) ? -1 : Input.GetKey(KeyCode.Space) ? 1f : 0), _charInstance.movementInput.y);
                noclipInput = _charInstance.FPPCameraTarget.rotation * noclipInput;
                float speed = _charInstance.ReadActionKeyCode(ActionCodes.Sprint) ? noclipRunSpeed : noclipSpeed;
                noclipInput = noclipInput * speed;
                transform.position += noclipInput * Time.deltaTime;
                return;
            }
#endif

            if (_charInstance.CurrentHealth <= 0) return;

            //decide character speed
            if (_controller.isGrounded)
            {
                //sprint/walk speed

                _speed = _charInstance.IsRunning ? RunSpeed : WalkSpeed;
            }
            else
            {
                //dont let character be as fast when falling, as if it was running
                _speed = Mathf.Clamp(_speed, 0f, RunSpeed * 0.8f);
            }

            //get input and, make vector from that and, multiply it by speed and give it appropriate direction based on character rotation
            Vector3 playerInput = new Vector3(_charInstance.movementInput.x, 0, _charInstance.movementInput.y);
            playerInput = playerInput * _speed;
            playerInput = transform.rotation * playerInput; //give movement direction dependent on camera

            if (_controller.isGrounded)
            {
                //if character jumped dont treat it as if it was grounded
                if (!_jumped)
                    force.y = -6f;
            }
            else
            {
                //when not grounded make it fall
                force.y -= FallingSpeed * Time.deltaTime;
            }

            //finally move character
            _controller.Move((playerInput + force) * Time.deltaTime);
            _jumped = false;
        }

        public void Jump() 
        {
            if (_controller.isGrounded)
            {
                force.y = JumpHeight;
                _jumped = true;
            }
        }
        /// <summary>
        /// Dont let character move when is dead
        /// </summary>
        public void Die(CharacterPart hittedPartID, Health _attacker)
        {
            enabled = false;
        }

        void Crouch()
        {
            _controller.center = new Vector3(0, 0.5f, 0);
            _controller.height = 1f;

            _charInstance.centerPosition = crouchAttackPos;

            //LerpCamera(1f);
        }
        void Stand()
        {
            _controller.center = new Vector3(0, 1, 0);
            _controller.height = 2f;

            _charInstance.centerPosition = defaultAttackPos;

            //LerpCamera(_charInstance.CameraHeight);
        }

        bool CheckSphere()
        {
            Collider[] col = Physics.OverlapSphere(transform.position + new Vector3(0, 1.5f, 0), 0.4f, GameManager.environmentLayer);

            return col.Length > 0;
        }
    }
}
