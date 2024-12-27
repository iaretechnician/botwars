
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace MTPSKIT.Gameplay
{
    public class CharacterAnimator : MonoBehaviour
    {
        [SerializeField] OverrideTransform _chestIK;
        [SerializeField] OverrideTransform _headIK;

        public RuntimeAnimatorController BaseAnimatorController;
        CharacterInstance _characterInstance;

        /// <summary>
        /// player model animator
        /// </summary>
        [SerializeField] Animator _animator;
        private float _upperBodyAnimateFactor;

        float _targetRecoilUp;
        float _currentRecoilUp;

        float _currentBodyRotation;
        [SerializeField] float _rotationAngleThreshold = 80;
        [SerializeField] float _rotationAngleThresholdLeftSide = 20;
        bool _adjustingRotation = false;
        [SerializeField] float _adjustingRotationLerpSpeed = 180;

        float _takingDamageFactor;
        float _takingDamageHeadshotFactor;

        [SerializeField] float _damageAnimationRecoverSpeed = 30;

        float _lerpedMovementInputX;
        float _lerpedMovementInputY;

        [Header("Audio")]
        [SerializeField] AudioSource _movementAudioSource;
        [SerializeField] AudioClip _movementClip;

        void Awake()
        {
            _characterInstance = GetComponent<CharacterInstance>();
            GetComponent<Health>().Client_OnHealthStateChanged += OnDamaged;
        }

        public void ShowModel(bool show) => _animator.gameObject.SetActive(show);

        private void OnDamaged(int currentHealth, CharacterPart damagedPart, AttackType attackType, Health attackerID)
        {
            if (damagedPart == CharacterPart.head)
            {
                _takingDamageHeadshotFactor = 30;
                _takingDamageFactor = 14;
            }
            else
                _takingDamageFactor = 12;

            if (currentHealth <= 0)
                _movementAudioSource.enabled = false;
        }

        void Update()
        {
            //animate character
            _animator.SetFloat("look", -_characterInstance.lookInput.x);

            _chestIK.weight = _upperBodyAnimateFactor;
            Vector3 chestRot = new Vector3(_characterInstance.lookInput.x * 0.15f - _currentRecoilUp, _characterInstance.lookInput.y, -_takingDamageFactor);
            _chestIK.transform.localEulerAngles = chestRot;

            _headIK.weight = _upperBodyAnimateFactor;
            Vector3 headRot = new Vector3(_characterInstance.lookInput.x * 0.8f, _characterInstance.lookInput.y, -_takingDamageHeadshotFactor);
            _headIK.transform.localEulerAngles = headRot;

            _animator.SetLayerWeight(1, _upperBodyAnimateFactor);

            if (_currentRecoilUp < _targetRecoilUp)
            {
                _currentRecoilUp += 350 * Time.deltaTime;

                if (_currentRecoilUp > _targetRecoilUp) 
                    _targetRecoilUp = 0;
            }

            if (_targetRecoilUp == 0 && _currentRecoilUp > 0)
                _currentRecoilUp -= 45 * Time.deltaTime;

            _currentRecoilUp = Mathf.Clamp(_currentRecoilUp, 0, 30);


            _takingDamageFactor = Mathf.MoveTowards(_takingDamageFactor, 0, _damageAnimationRecoverSpeed * Time.deltaTime);
            _takingDamageHeadshotFactor = Mathf.MoveTowards(_takingDamageHeadshotFactor, 0, _damageAnimationRecoverSpeed * Time.deltaTime);

            float difference = -Mathf.DeltaAngle(_characterInstance.lookInput.y, _currentBodyRotation);
            if (difference > 0 && Mathf.Abs(difference) > _rotationAngleThreshold ||
                difference < 0 && Mathf.Abs(difference) > _rotationAngleThresholdLeftSide)
            {
                if (!_adjustingRotation)
                {
                    _animator.CrossFade("turn", 0.05f);
                    _adjustingRotation = true;
                }
            }
            else if (Mathf.Abs(difference) < 1)
                _adjustingRotation = false;

            if (_characterInstance.movementInput != Vector2.zero || !_characterInstance.IsGrounded)
                _currentBodyRotation = _characterInstance.lookInput.y;

            if(_adjustingRotation)
                _currentBodyRotation = ClampRotation(
                    Mathf.Lerp(_currentBodyRotation, _characterInstance.lookInput.y, _adjustingRotationLerpSpeed * Time.deltaTime),
                    _characterInstance.lookInput.y,_rotationAngleThreshold);
            
            _animator.transform.eulerAngles = new Vector3(0, _currentBodyRotation, 0);
        }

        private void FixedUpdate()
        {
            //apply animation properties
            _lerpedMovementInputX = Mathf.MoveTowards(_lerpedMovementInputX, _characterInstance.movementInput.x, Time.fixedDeltaTime*10);
            _lerpedMovementInputY = Mathf.MoveTowards(_lerpedMovementInputY, _characterInstance.movementInput.y, Time.fixedDeltaTime*10);

            _animator.SetFloat("X", Mathf.Clamp(_lerpedMovementInputX, -1, 1));
            _animator.SetFloat("Y", Mathf.Clamp(_lerpedMovementInputY, -1, 1));
            _animator.SetFloat("speed", _characterInstance.IsRunning ? 2f: (_characterInstance.movementInput != Vector2.zero? 1: 0));
            _animator.SetFloat("isCrouchingFloat", _characterInstance.IsCrouching ? 1f:0f);
            _animator.SetBool("isCrouching", _characterInstance.IsCrouching);
            _animator.SetBool("isGrounded", _characterInstance.IsGrounded);

            _upperBodyAnimateFactor = Mathf.Lerp(_animator.GetLayerWeight(1), System.Convert.ToInt32(!_characterInstance.IsRunning || _characterInstance.IsReloading || _characterInstance.IsCrouching), 20f * Time.deltaTime);

            _movementAudioSource.enabled = _characterInstance.IsRunning;
        }

        public void SetRuntimeAnimatorController(RuntimeAnimatorController controller)
        {
            if (!controller) 
            {
                _animator.runtimeAnimatorController = BaseAnimatorController;
                return;
            }

            _animator.runtimeAnimatorController = controller;
        }

        public void DoRecoil(float recoilUp) 
        {
            _targetRecoilUp = recoilUp;
            _currentRecoilUp = 0;
        }

        public void Play(string animName)
        {
            _animator.Play(animName);
        }

        float ClampRotation(float currentRot, float targetRot, float threshold)
        {
            float difference = Mathf.DeltaAngle(currentRot, targetRot);

            if (difference > threshold || difference < -threshold)
            {
                float angle = Mathf.Clamp(currentRot, targetRot - threshold, targetRot + threshold);
                return angle;
            }
            else
                return currentRot;
        }
    }
}