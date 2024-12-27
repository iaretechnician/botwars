using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
using MTPSKIT.UI.HUD;

namespace MTPSKIT.Gameplay
{
    public class Gun : Item
    {
        [SerializeField] protected ParticleSystem _particleSystem;
        [SerializeField] GameObject decals;
        [SerializeField] protected AudioClip fireClip;

        protected Transform _firePoint;
        public float ReloadTime = 1.5f;
        protected bool _isReloading;

        Coroutine c_reload;

        [Header("Recoil stats")]
        [SerializeField] public float _recoil_minAngle = 0.01f;
        [SerializeField] public float _recoil_maxAngle = 3;
        [SerializeField] public float _recoil_scopeMultiplier = 0.5f; //set 0 for no recoil, useful for sniper rifle
        [SerializeField] protected float _recoil_angleAddedOnShot = 0.4f;
        [SerializeField] protected float _recoil_stabilizationSpeed = 6f; //how much angle devation from barrel to recover in one second
        public float Recoil_walkMultiplier = 1.5f; //how much multiply recoil when player using item is walking        
        [HideInInspector] public float CurrentRecoil;
        [SerializeField] protected float _characterModelRecoil = 5f;

        [Header("Crosshair")]
        public int Crosshair_minSize = 64;
        public int Crosshair_maxSize = 128;
        public bool HideCrosshairWhenAiming;

        [Header("ReloadAnimation")]
        [SerializeField] protected GameObject _magazinePrefab;
        protected Rigidbody _spawnedMagazine;
        [SerializeField] protected float _magazineDropTime; //at which point in reload animation should magazine be spawned
        [SerializeField] AudioClip _reloadAudioClip;

        [Header("Pooled prefabs")]
        [SerializeField] GameObject _bulletPrefab;
        ObjectPool _bulletPrefabPool;

        [SerializeField] GameObject _decalPrefab;
        ObjectPool _decalPrefabPool;

        protected override void Awake()
        {
            base.Awake();

            if (_magazinePrefab)
            {
                _spawnedMagazine = Instantiate(_magazinePrefab).GetComponent<Rigidbody>();
                _spawnedMagazine.gameObject.SetActive(false);
                _magazinePrefab.SetActive(false);
            }

            _bulletPrefabPool = ObjectPooler.Instance.GetPoolByName(_bulletPrefab.name);
            _decalPrefabPool = ObjectPooler.Instance.GetPoolByName(_decalPrefab.name);
        }

        protected override void Update()
        {
            if (!_myOwner) return;

            float recoilMinAngle = _myOwner.CharacterItemManager.UseSecondaryInput ? _recoil_minAngle * _recoil_scopeMultiplier : _recoil_minAngle;

            if (CurrentRecoil > recoilMinAngle)
                CurrentRecoil -= _recoil_stabilizationSpeed * Time.deltaTime;
            else
                CurrentRecoil = recoilMinAngle;
        }

        protected override void Use()
        {
            if (CurrentAmmo <= 0 || _isReloading) return;

            CurrentAmmo--;
            UpdateAmmoInHud();

            byte hittedPart = 0;
            Vector3 hitPoint;
            RaycastHit hit;

            SnapFirePoint();

            Transform shootPoint = _myOwner.characterFirePoint;
            Quaternion hitRotation = Quaternion.identity;

            shootPoint.localEulerAngles = new Vector3(
                Random.Range(-CurrentRecoil, CurrentRecoil),
                Random.Range(-CurrentRecoil, CurrentRecoil),
                Random.Range(-CurrentRecoil, CurrentRecoil));

            RaycastHit[] hitScan = GameTools.HitScan(shootPoint, _myOwner.transform, GameManager.fireLayer, 350f);

            if (hitScan.Length > 0) { 

                hit = hitScan[0];
                GameObject go = hit.collider.gameObject;
                hitPoint = hit.point;

                hitRotation = Quaternion.FromToRotation(Vector3.forward, hit.normal);

                HitBox hb = go.GetComponent<HitBox>();

                if (hb)
                {
                    hittedPart = (byte)hb.part;
                    if (!_myOwner.BOT)
                    {
                        CmdDamage(hb._health, hb.part);
                    }
                    else
                    {
                        ServerDamage(hb._health, hb.part);
                    }
                }
            }
            else 
            {
                hitPoint = shootPoint.forward * 99999f;
            }

            if(isOwned)
                Shoot(hitPoint, hitRotation, hittedPart);

            if (_myOwner.BOT)
            {
                RpcShoot(hitPoint, hitRotation, hittedPart);
            }
            else 
            {
               if(isOwned)
                    CmdShoot(hitPoint, hitRotation, hittedPart);
            }

            if (CurrentAmmo <= 0)
                RequestReload();

            base.Use();
        }

       

        protected override void UpdateAmmoInHud()
        {
            if (_myOwner && !_myOwner.IsObserved) return;

            UICharacter._instance.OnAmmoStateChanged($"{CurrentAmmo}/{MagazineCapacity}");
        }

        //game logic side of reload, this code will only run on client that owns this item or on server if bot owns this item
        public override void RequestReload()
        {
            //dont start realod procedure if magazine is full or if we are already reloading
            if (CurrentAmmo == MagazineCapacity || _isReloading) return;


            if(isOwned)
            Reload();

            if (isServer)
                ServerReload();
            else
                CmdReloadGun();
        }
        void Reload() 
        {
            StopReloadingCoroutine();

            if (!_myOwner) return;

            _isReloading = true;

            _myOwner.CharacterAnimator.Play("reload");
            PlayAnimation("reload");

            if (_reloadAudioClip)
                _audioSource.PlayOneShot(_reloadAudioClip);

            c_reload = StartCoroutine(ReloadCounter());
            IEnumerator ReloadCounter()
            {
                yield return new WaitForSeconds(Mathf.Clamp(coolDownTimer - Time.time, 0, coolDown)); //to avoid reloading immediately after shooting

                _myOwner.IsReloading = true;

                yield return new WaitForSeconds(_magazineDropTime);
                if (_spawnedMagazine)
                {
                    _spawnedMagazine.transform.SetPositionAndRotation(_magazinePrefab.transform.position, _magazinePrefab.transform.rotation);
                    _spawnedMagazine.velocity = Vector3.zero;
                    _spawnedMagazine.angularVelocity = Vector3.zero;
                    _spawnedMagazine.gameObject.layer = (int)GameLayers.ragdoll;
                    _spawnedMagazine.gameObject.SetActive(true);
                    _spawnedMagazine.AddForce(-_magazinePrefab.transform.up * 50);
                }

                yield return new WaitForSeconds(ReloadTime-_magazineDropTime);
                CurrentAmmo = MagazineCapacity;

                UpdateAmmoInHud();
                _myOwner.IsReloading = false;
                _isReloading = false;
            }
        }

        [Command]
        void CmdReloadGun()
        {
            ServerReload();
        }

        protected void ServerReload() 
        {
            Reload();
            RpcReload();
        }

        [ClientRpc(includeOwner = false)]
        void RpcReload() 
        {
            if(!isServer)
                Reload();
        }

        void StopReloadingCoroutine() 
        {
            if (c_reload != null)
            {
                StopCoroutine(c_reload);
                c_reload = null;
            }

            _isReloading = false;
        }

        [Command(channel = Channels.Unreliable)]
        void CmdShoot(Vector3 decalPos, Quaternion decalRot, byte hittedMaterialID)
        {
            RpcShoot(decalPos, decalRot, hittedMaterialID);
        }
        [ClientRpc(includeOwner = false)]
        void RpcShoot(Vector3 _decalPos, Quaternion _decalRot, byte _hittedMaterialID)
        {
            Shoot(_decalPos, _decalRot, _hittedMaterialID);
        }
        void Shoot(Vector3 decalPos, Quaternion decalRot, byte _hittedMaterialID)
        {
            if (!_myOwner) return;

            _myOwner.CharacterAnimator.DoRecoil(_characterModelRecoil);

            _audioSource.PlayOneShot(fireClip);
            _particleSystem.Play();

            PlayAnimation("fire");

            SpawnBullet(decalPos, decalRot);
        }

        protected void SpawnBullet(Vector3 decalPos, Quaternion decalRot) {
            _decalPrefabPool.ReturnObject(decalPos, decalRot);//spawn decal
            _bulletPrefabPool.ReturnObject(_particleSystem.transform.position, decalRot).StartBullet(decalPos);//spawn bullet from barrel
        }

        #region shotgun
        [Command]
        protected void CmdDamageByMultipleBullets(List<Health> hittedHealths, List<CharacterPart> hittedParts, List<int> rawDamage) 
        {
            ServerDamageByMultipleBullets(hittedHealths, hittedParts, rawDamage);
        }
        protected void ServerDamageByMultipleBullets(List<Health> hittedHealths, List<CharacterPart> hittedParts, List<int> rawDamage)
        {
            for (int i = 0; i < hittedHealths.Count; i++)
            {
                ServerDamageRaw(hittedHealths[i], hittedParts[i], rawDamage[i]);
            }
        }
        #endregion

        protected void ServerDamageRaw(Health h, CharacterPart hittedPart, int rawDamage)
        {
            h.Server_ChangeHealthStateRaw(rawDamage, hittedPart, AttackType.hitscan, _myOwner, _ragdollPushForce);
        }

        public override void Take()
        {
            base.Take();
            _firePoint = _myOwner.characterFirePoint;
            UpdateAmmoInHud();
        }

        public override void Drop()
        {
            base.Drop();
            StopReloadingCoroutine();
        }

        public override void PutDown()
        {
            base.PutDown();
            StopReloadingCoroutine();
        }

        protected override void SingleUse()
        {
            base.SingleUse();
            CurrentRecoil += _recoil_angleAddedOnShot;
            CurrentRecoil = Mathf.Clamp(CurrentRecoil, _recoil_minAngle, _recoil_maxAngle);
        }

        private void OnDestroy()
        {
            if (_spawnedMagazine)
                Destroy(_spawnedMagazine);
        }
    }
}