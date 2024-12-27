using Mirror;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MTPSKIT.Gameplay
{
    public class Granade : Item
    {
        public GameObject ThrowablePrefab;
        public float RigidBodyForce = 5000;
        protected override void Use()
        {
            if (_myOwner.CharacterItemManager.GranadeSupply <= 0) return;

            base.Use();

            if (isServer)
                SpawnThrowable();
            else
                CmdSpawnThrowable();

            UpdateAmmoInHud(_myOwner.CharacterItemManager.GranadeSupply.ToString());
        }
        [Command]
        void CmdSpawnThrowable() 
        {
            SpawnThrowable();
        }
        void SpawnThrowable() 
        {
            if (_myOwner.CharacterItemManager.ServerGranadeSupply <= 0) return;
            _myOwner.CharacterItemManager.ServerGranadeSupply--;

            SnapFirePoint();

            GameObject throwable = Instantiate(ThrowablePrefab, _myOwner.characterMind.transform.position, _myOwner.transform.rotation);

            Vector3 force = _myOwner.characterMind.transform.forward * RigidBodyForce;

            throwable.GetComponent<Throwable>().Activate(_myOwner, force);
            NetworkServer.Spawn(throwable);
        }
        public override void Take()
        {
            UpdateAmmoInHud(_myOwner.CharacterItemManager.GranadeSupply.ToString());
            base.Take();
        }

        protected override void OnOwnerPickedupAmmo()
        {
            UpdateAmmoInHud(_myOwner.CharacterItemManager.GranadeSupply.ToString());
            ShowItemModel(true);
        }

        protected override void SingleUse()
        {
            _myOwner.CharacterItemManager.GranadeSupply--;

            //if we are out of granades, hide granade model from player hand
            if (_myOwner.CharacterItemManager.GranadeSupply == 0)
            {
                ShowItemModel(false);
            }

            _myOwner.CharacterItemManager.StartUsingItem(); //will disable ability tu run for 0.5 seconds
            _myOwner.CharacterAnimator.Play("Attack1");
        }

        public override bool CanBeUsed()
        {
            return _myOwner.CharacterItemManager.GranadeSupply > 0;
        }
    }
}