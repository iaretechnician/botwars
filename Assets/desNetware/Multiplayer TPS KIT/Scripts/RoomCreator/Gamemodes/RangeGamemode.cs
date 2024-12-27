using MTPSKIT.Gameplay;

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MTPSKIT.Gameplay.Gamemodes
{
    public class RangeGamemode : Gamemode
    {
        [SerializeField] SpawnpointsContainer _playersSpawnPoints;
        [SerializeField] SpawnpointsContainer _botsSpawnPoints;

        protected override void Awake()
        {
            base.Awake();
            PeacufulBots = true;
        }

        public override void Server_OnPlayerInstanceAdded(PlayerInstance player)
        {
            if (player.BOT)
            {
                AssignPlayerToTeam(player, 1);
                player.SpawnCharacter(_botsSpawnPoints.GetNextSpawnPoint());
            }
            else
            {
                AssignPlayerToTeam(player, 0);
                player.SpawnCharacter(_playersSpawnPoints.GetNextSpawnPoint());
            }
        }

        public override void PlayerSpawnCharacterRequest(PlayerInstance playerInstance)
        {
            playerInstance.SpawnCharacter(playerInstance.BOT ? _botsSpawnPoints.GetNextSpawnPoint() : _playersSpawnPoints.GetNextSpawnPoint());
        }
    }
}