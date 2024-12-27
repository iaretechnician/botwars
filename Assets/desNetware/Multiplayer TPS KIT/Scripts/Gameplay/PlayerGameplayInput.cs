using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MTPSKIT.UI;
namespace MTPSKIT.Gameplay
{
    public class PlayerGameplayInput : MonoBehaviour
    {
        public static PlayerGameplayInput Instance;
        private CharacterInstance _myCharIntance;
        private CharacterMotor _motor;

        public Vector2 LookInput;
        public Vector2 MovementInput;

        public float CharacterRotationSpeed = 5f;

        float _targetCharacterRot;

        public float IdleLookAngle = -20f;

        public float LerpLookSpeed = 10f;

        float _itemUseTimer;

        public bool AlwaysSnapCharacterToCamera;


        private void Awake()
        {
            if (Instance)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }
        void Update() {
            if (!_myCharIntance) return;

            //game managament related input
            /*if (Input.GetKeyDown(KeyCode.L))
            {
                ClientFrontend.GamemodeUI.Btn_ShowTeamSelector();
            }*/

            if (ClientFrontend.GamePlayInput())
            {
                //character related input
                if (Input.GetKeyDown(KeyCode.Space)) _motor.Jump();

                if (Input.GetKeyDown(KeyCode.E)) _myCharIntance.CharacterItemManager.TryGrabItem();
                if (Input.GetKeyDown(KeyCode.G)) _myCharIntance.CharacterItemManager.TryDropItem();

                if (Input.GetKeyDown(KeyCode.Alpha1)) _myCharIntance.CharacterItemManager.ClientTakeItem(0);
                if (Input.GetKeyDown(KeyCode.Alpha2)) _myCharIntance.CharacterItemManager.ClientTakeItem(1);
                if (Input.GetKeyDown(KeyCode.Alpha3)) _myCharIntance.CharacterItemManager.ClientTakeItem(2);
                if (Input.GetKeyDown(KeyCode.Alpha4)) _myCharIntance.CharacterItemManager.ClientTakeItem(3);

                if (Input.GetKeyDown(KeyCode.R)) _myCharIntance.CharacterItemManager.Reload();


                _myCharIntance.CharacterItemManager.UsePrimaryInput = Input.GetMouseButton(0);
                _myCharIntance.CharacterItemManager.UseSecondaryInput = Input.GetMouseButton(1);

                _myCharIntance.IsAiming = Input.GetMouseButton(1);

                float scopeMultiplier = _myCharIntance.IsAiming && _myCharIntance.CharacterItemManager.CurrentlyUsedItem ? _myCharIntance.CharacterItemManager.CurrentlyUsedItem.AimSensitivityMultiplier : 1f;


                LookInput.y += Input.GetAxis("Mouse X") * UserSettings.MouseSensitivity * scopeMultiplier;
                LookInput.x -= Input.GetAxis("Mouse Y") * UserSettings.MouseSensitivity * scopeMultiplier;
                LookInput.x = Mathf.Clamp(LookInput.x, -90f, 90f);
                
                MovementInput.x = Input.GetAxis("Horizontal");
                MovementInput.y = Input.GetAxis("Vertical");

                _myCharIntance.SetActionKeyCode(ActionCodes.Sprint, Input.GetKey(KeyCode.LeftShift) && !Input.GetMouseButton(0) && !Input.GetMouseButton(1));
                _myCharIntance.SetActionKeyCode(ActionCodes.Crouch, Input.GetKey(KeyCode.C));

                if (AlwaysSnapCharacterToCamera) 
                {
                    _myCharIntance.SetMovementInput(MovementInput);
                    _myCharIntance.lookInput = LookInput;
                    return;
                }

                //if player wishes to use item or item is in use snap character to camera view
                if (_itemUseTimer >= Time.time || Input.GetMouseButton(0) || Input.GetMouseButton(1))
                {
                    _targetCharacterRot = LookInput.y;

                    _myCharIntance.lookInput.x = Mathf.MoveTowards(_myCharIntance.lookInput.x, LookInput.x, LerpLookSpeed * Time.deltaTime);
                }
                else
                {
                    Vector3 playerInput = new Vector3(MovementInput.x, 0, MovementInput.y);
                    Vector3 direction = Quaternion.Euler(0, LookInput.y, 0) * playerInput;

                    if (MovementInput != Vector2.zero)
                        _targetCharacterRot = Quaternion.LookRotation(direction, Vector3.up).eulerAngles.y;

                    _myCharIntance.lookInput.x = Mathf.MoveTowards(_myCharIntance.lookInput.x, -IdleLookAngle, LerpLookSpeed*Time.deltaTime);    
                }


                //rotate player
                _myCharIntance.lookInput.y = Mathf.MoveTowardsAngle(_myCharIntance.lookInput.y, _targetCharacterRot, CharacterRotationSpeed * Time.deltaTime);


                Quaternion camRot = Quaternion.Euler(0, LookInput.y - _myCharIntance.lookInput.y, 0);
                Vector3 m = camRot * new Vector3(MovementInput.x, 0, MovementInput.y);

                _myCharIntance.SetMovementInput(m.x, m.z);
            }
            else
            {
                _myCharIntance.SetMovementInput(Vector2.zero);
                _myCharIntance.SetActionKeyCode(ActionCodes.Sprint, false);
            }
        }

        public void AssignCharacterToBeControlledByPlayer(CharacterInstance character)
        {
            _myCharIntance = character;
            _motor = character.GetComponent<CharacterMotor>();

            _myCharIntance.CharacterEvent_OnItemUsed += OnItemUsed;
        }

        void OnItemUsed(float time)
        {
            _itemUseTimer = Time.time + time;
        }
        private void OnDestroy()
        {
            if(_myCharIntance)
                _myCharIntance.CharacterEvent_OnItemUsed -= OnItemUsed;
        }
    }
}