using MTPSKIT.Gameplay;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MTPSKIT.Gameplay
{
    public class MeleeWeapon : Item
    {
        protected override void Use()
        {
            base.Use();

            Collider[] collider = Physics.OverlapSphere(_myOwner.transform.position + _myOwner.transform.up * 1f + _myOwner.transform.forward * 1f, 1f, GameManager.characterLayer);

            for (int i = 0; i < collider.Length; i++)
            {
                Collider col = collider[i];
                if (col.transform.root != _myOwner.transform.root)
                {
                    Health victim = col.GetComponent<Health>();
                    if (victim)
                    {
                        if (isServer)
                            ServerDamage(victim, (byte)CharacterPart.body);
                        else
                            CmdDamage(victim, (byte)CharacterPart.body);
                    }
                }
            }
        }
        protected override void SingleUse()
        {
            base.SingleUse();
            _myOwner.CharacterAnimator.Play("Attack1");
        }
    }
}