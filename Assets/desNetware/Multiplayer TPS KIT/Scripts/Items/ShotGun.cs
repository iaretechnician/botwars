using MTPSKIT.Gameplay;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

namespace MTPSKIT.Gameplay
{
    public class ShotGun : Gun
    {
        [SerializeField] int bulletsInOneShoot;
        public float RecoilFactor = 2f;

        Vector3[] _bulletsDirections;

        protected override void Awake()
        {
            base.Awake();
            _bulletsDirections = new Vector3[bulletsInOneShoot];
        }

        protected override void Use()
        {
            if (CurrentAmmo <= 0 || _isReloading) return;

            CurrentAmmo--;
            UpdateAmmoInHud();

            SnapFirePoint();

            List<Health> hittedHealths = new List<Health>();
            List<int> damage = new List<int>();
            List<CharacterPart> hittedParts = new List<CharacterPart>();

            for (int i = 0; i < bulletsInOneShoot; i++) //single shoot
            {
                RandomRecoil();

                RaycastHit[] hittedObjects = GameTools.HitScan(_firePoint, _myOwner.transform, GameManager.fireLayer, 250f);

                if (hittedObjects.Length > 0)
                {
                    _bulletsDirections.SetValue(hittedObjects[0].point, i);

                    HitBox phb = hittedObjects[0].collider.gameObject.GetComponent<HitBox>();

                    if (phb)
                    {
                        //damage
                        if (!hittedHealths.Contains(phb._health))
                        {
                            hittedHealths.Add(phb._health);
                            hittedParts.Add(phb.part); //causes to damage only first hitted object
                            damage.Add(phb._health.CountDamage((byte)phb.part, _damage));
                        }
                        else
                        {
                            int _helthIndexToDamageAgain = hittedHealths.IndexOf(phb._health);

                            damage[_helthIndexToDamageAgain] += phb._health.CountDamage((byte)phb.part, _damage);

                            //if any bullet from shotgun hits the head, the we count whole shoot as a headshot
                            if (phb.part == CharacterPart.head) hittedParts[_helthIndexToDamageAgain] = phb.part;
                        }
                    }
                }
                else
                {
                    _bulletsDirections.SetValue(_firePoint.position + _firePoint.transform.forward * 999f, i);
                }
            }

            ShotgunShoot(_bulletsDirections);
            if (isServer)
                RpcShotgunShoot(_bulletsDirections);
            else
                CmdShotgunShoot(_bulletsDirections);

            //damaging object after summarizing damage
            if (hittedHealths.Count > 0)
            {
                if (!_myOwner.BOT)
                {
                    CmdDamageByMultipleBullets(hittedHealths, hittedParts, damage);
                }
                else
                {
                    ServerDamageByMultipleBullets(hittedHealths, hittedParts, damage);
                }
            }

            if (isOwned)
            {
                SingleUse(); //for client to for example immediately see muzzleflash when he fires his gun
                CmdSingleUse();
                if (CurrentAmmo <= 0)
                    RequestReload();
            }
            else if (isServer)
            {
                SingleUse();
                RpcSingleUse();

                if (CurrentAmmo <= 0)
                    ServerReload();
            }


        }

        [Command]
        void CmdShotgunShoot(Vector3[] directions)
        {
            RpcShotgunShoot(directions);
        }

        //dont want to launch this method for client who took a shoot because
        //he already launched it locally
        [ClientRpc(includeOwner = false)]
        void RpcShotgunShoot(Vector3[] directions)
        {
            ShotgunShoot(directions);
        }

        public void ShotgunShoot(Vector3[] directions)
        {
            if (!_myOwner) return;

            _myOwner.CharacterAnimator.DoRecoil(_characterModelRecoil);
            _audioSource.PlayOneShot(fireClip);
            _particleSystem.Play();
            PlayAnimation("fire");

            for (int i = 0; i < _bulletsDirections.Length; i++)
            {
                SpawnBullet(directions[i], Quaternion.identity);
            }
        }

        public void RandomRecoil()
        {
            _firePoint.localRotation = Quaternion.identity;
            _firePoint.localRotation = Quaternion.Euler(UnityEngine.Random.Range(-RecoilFactor, RecoilFactor), UnityEngine.Random.Range(-RecoilFactor, RecoilFactor), 0);
        }
    }
}