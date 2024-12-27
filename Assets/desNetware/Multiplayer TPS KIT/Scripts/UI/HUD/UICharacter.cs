using Mirror;
using MTPSKIT.Gameplay;
using UnityEngine;
using UnityEngine.UI;
using MTPSKIT.Gameplay.Gamemodes;
using System;
using System.Collections;

namespace MTPSKIT.UI.HUD
{
    public class UICharacter : CharacterHud
    {
        public GameObject Overlay;
        public static UICharacter _instance { get; private set; }

        [SerializeField] Text _healthText;
        [SerializeField] Image _healthBar;

        public GameObject UIMarkerPrefab;

        //borders for 3D UI icons, in this project we use it only for player nametags 
        public Canvas WorldIconBorders;

        [SerializeField] HUDTakeDamageMarker _takeDamageMarker;


        [Header("Gamemodes UI")]
        /// <summary>
        /// gamemodes UI prefabs have to be placed in this array in the same order as gamemodes in enum "Gamemodes"
        /// </summary>
        [SerializeField] GameObject[] gamemodesUI;
        [SerializeField] Text _ammo;

        [Header("Markers")]
        [SerializeField] HitMarker _hitMarker;
        [SerializeField] HitMarker _killMarker;


        [SerializeField] Image _damageIndicatorImage; //UI that will flash with red on damage
        Color _damageIndicatorColor;
        Coroutine _damageIndicatorAnimation;
        private float _damageIndicatorVanishTime = 3f;

        protected override void Awake()   
        {
            base.Awake();
            _instance = this;

            ShowCharacterHUD(false);
            _ammo.text = string.Empty;

            _damageIndicatorColor = _damageIndicatorImage.color;
            _damageIndicatorImage.color = Color.clear;

            ClientFrontend.ClientEvent_OnJoinedToGame += InstantiateUIforGivenGamemode;
        }
        
        protected override void OnDestroy()
        {
            base.OnDestroy();
            ClientFrontend.ClientEvent_OnJoinedToGame -= InstantiateUIforGivenGamemode;
        }

        void InstantiateUIforGivenGamemode(Gamemode gamemode, NetworkIdentity player) 
        {
            GameObject gamemodeUI = gamemodesUI[(int)gamemode.Indicator];

            if (gamemodeUI)
                Instantiate(gamemodeUI).GetComponent<UIGamemode>().SetupUI(gamemode, player);
        }

        public void OnAmmoStateChanged(string ammo) 
        {
            _ammo.text = ammo;
        }

        protected override void AssignCharacterForUI(CharacterInstance _characterInstanceToAssignForUI)
        {
            base.AssignCharacterForUI(_characterInstanceToAssignForUI);
            _myObservedCharacter = _characterInstanceToAssignForUI;
            _myObservedCharacter.Client_OnHealthStateChanged += OnHealthStateChanged;
            _myObservedCharacter.Client_OnHealthAdded += OnHealthAdded;
            _myObservedCharacter.Client_OnDamageDealt += OnDamageDealt;
            _myObservedCharacter.Client_KillConfirmation += OnKillConfiration;
            ShowCharacterHUD(true);
            UpdateHealthHUD();

            _takeDamageMarker.Initialize(_characterInstanceToAssignForUI);
        }

     

        protected override void DeassignCurrentCharacterFromUI(CharacterInstance _characterToDeassign)
        {
            base.DeassignCurrentCharacterFromUI(_characterToDeassign);
            _myObservedCharacter.Client_OnHealthStateChanged -= OnHealthStateChanged;
            _myObservedCharacter.Client_OnHealthAdded -= OnHealthAdded;
            _myObservedCharacter.Client_OnDamageDealt -= OnDamageDealt;
            _myObservedCharacter.Client_KillConfirmation -= OnKillConfiration;
        }

        private void OnHealthAdded(int currentHealth, int addedHealth, uint healerID)
        {
            UpdateHealthHUD();
        }

        private void OnHealthStateChanged(int currentHealth, CharacterPart damagedPart, AttackType attackType, Health attackerID)
        {
            UpdateHealthHUD();

            if (_damageIndicatorAnimation != null)
            {
                StopCoroutine(_damageIndicatorAnimation);
                _damageIndicatorAnimation = null;
            }

            _damageIndicatorAnimation = StartCoroutine(DamageIndicatorAnimation());

            IEnumerator DamageIndicatorAnimation()
            {
                _damageIndicatorImage.color = _damageIndicatorColor;

                Color startColor = _damageIndicatorImage.color;
                float progress = 0;

                while (progress < 1f)
                {
                    progress += Time.deltaTime * _damageIndicatorVanishTime;
                    _damageIndicatorImage.color = Color.Lerp(startColor, Color.clear, progress);
                    yield return null;
                }

                _damageIndicatorImage.color = Color.clear;
            }
        }

        private void OnDamageDealt(int currentHealth, int takenDamage, CharacterPart damagedPart, AttackType attackType, Health victim)
        {
            _hitMarker.PlayAnimation(damagedPart);
        }
        private void OnKillConfiration(CharacterPart damagedPart, Health attacker)
        {
            _killMarker.PlayAnimation(CharacterPart.head);
        }

        //update health state in HUD
        void UpdateHealthHUD() 
        {
            _healthText.text = _myObservedCharacter.CurrentHealth.ToString();
            _healthBar.fillAmount = (float)_myObservedCharacter.CurrentHealth / _myObservedCharacter.MaxHealth;
        }

        public void ShowCharacterHUD(bool _show)
        {
            Overlay.SetActive(_show);
        }


    }
}
