using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
using MTPSKIT.Gameplay.Gamemodes;
using MTPSKIT.UI;

namespace MTPSKIT.Gameplay
{
    public class PlayerInstance : NetworkBehaviour
    {
        public string playerName;
        public int Team { private set; get; } = -1; //-1 for no team

        public int Kills;
        public int Deaths;
        public int Assists;

        int[] _lodout;

        public bool SpawnCooldown = true;

        public CharacterInstance MyCharacter { private set; get; }

        public bool BOT { private set; get; } = false;


        public delegate void OnReceivedTeamResponse(int team, int permissionCode);
        public OnReceivedTeamResponse PlayerEvent_OnReceivedTeamResponse { set; get; }


        //counting for respawn
        Coroutine _spawnCooldownCoroutine;

        bool _registeredPlayerInstance = false;
        bool _subbedToNetworkManager;
        bool _serverInstance;

        [Space]
        [Header("Ping")]
        [SerializeField] float _pingUpdateInterval = 1f;
        float _pingUpdateTimer;
        public ushort ClientPing; //ping info sent to clients

        uint _netID;
        int _connID = -1;

        private void Start()
        {
            _netID = netId;

            DNNetworkManager.Instance.OnNewPlayerConnected += UpdateDataForLatePlayer;
            _subbedToNetworkManager = true;

            RegisterPlayerInstance();

            if (isServer)
            {
                ServerRegisterPlayerInstance();

                if (connectionToClient != null)
                {
                    GameManager.Gamemode.Relay_NewClientJoined(connectionToClient, this.netIdentity);

                    _connID = connectionToClient.connectionId;
                    GameManager.PlayersByConnectionID.Add(_connID, this);
                }
            }

            if (BOT && isServer)
                //tell everyone on chat that bot has joined
                GameManager.Gamemode.Server_WriteToChat($"<color=#{ColorUtility.ToHtmlStringRGBA(Color.cyan)}>" + playerName + " joined the game </color>");

            if (isOwned)
            {
                GameManager.myPlayerInstance = this;
                CmdReceivePlayerData(UserSettings.UserNickname);
            }
        }

        private void Update()
        {
            if (isOwned)
            {
                _pingUpdateTimer += Time.deltaTime;
                if (_pingUpdateInterval < _pingUpdateTimer)
                {
                    float rttms = (float)NetworkTime.rtt * 1000f;
                    CmdClientSendPing((ushort)rttms);
                    ClientPing = (ushort)rttms;
                    _pingUpdateTimer = 0f;
                }
            }

            if (!MyCharacter || (MyCharacter && MyCharacter.CurrentHealth <= 0))
            {
                if (isOwned)
                {
                    if (Input.GetKeyDown(KeyCode.Space))
                    {
                        CmdProcessSpawnRequest();
                    }
                }

                if (SpawnCooldown && BOT && isServer)
                    Server_SpawnRequest();
            }
        }

        public void RegisterPlayerInstance() 
        {
            if (!_registeredPlayerInstance)
            {
                GameManager.AddPlayerInstance(this);
                _registeredPlayerInstance = true;
            }
        }
        public void ServerRegisterPlayerInstance()
        {
            GameManager.Gamemode.Server_OnPlayerInstanceAdded(this);

            _serverInstance = true;
        }


        public void SetAsBot(bool bot = true) 
        {
            BOT = bot;
            RpcSetAsBot(bot);
        }
        void RpcSetAsBot(bool bot) { 
            if (isServer) return;
            BOT = bot; }

        [Command]
        void CmdProcessSpawnRequest()
        {
            Server_SpawnRequest();
        }
        public void Server_SpawnRequest()
        {
            if(Team != -1 && SpawnCooldown && GameManager.Gamemode.LetPlayersSpawnOnTheirOwn)
                GameManager.Gamemode.PlayerSpawnCharacterRequest(this);
        }

        public void SpawnCharacter(Transform spawnPoint)
        {
            if (Team == -1) return; //dont spawn character if we are not assigned to a team

            SpawnCooldown = false;

            if (_spawnCooldownCoroutine != null)
            {
                StopCoroutine(_spawnCooldownCoroutine);
                _spawnCooldownCoroutine = null;
            }

            //despawn previous character if it exist
            DespawnCharacterIfExist();

            //if we have more players than spawnpoint and we spawn them all at once, then adding position randomness around spawnpoint
            //will eliminate situations where players are spawned on exactly same positions, causing them to stuck
            float radiusFromSpawnpoint = 0.4f;
            Vector3 spawnPositionRandomness;
            spawnPositionRandomness = Quaternion.Euler(0, Random.Range(0, 360), 0) * (new Vector3(0, 0, radiusFromSpawnpoint));

            MyCharacter = Instantiate(NetworkManager.singleton.spawnPrefabs[0], spawnPoint.position + spawnPositionRandomness, Quaternion.identity).GetComponent<CharacterInstance>();
            MyCharacter.lookInput.y = spawnPoint.eulerAngles.y;

            #region assign items
            //Assign player lodout, so appropriate items will be spawned
            CharacterItemManager characterItemManager = MyCharacter.GetComponent<CharacterItemManager>();

            //if this player intance is bot, then randomize his lodout every time he spawns, just for gameplay variety
            if (BOT && (_lodout == null || _lodout.Length <= 0))
            {
                List<int> randomItems = new List<int>();

                for (int i = 0; i < ItemManager.Instance.SlotsLodout.Length; i++)
                {
                    randomItems.Add(Random.Range(0, ItemManager.Instance.SlotsLodout[i].availableItemsForSlot.Length));
                }
                _lodout = randomItems.ToArray();
            }

            //if no special equipment is required just spawn character with items that player wants
            if(_lodout != null && _lodout.Length > 0)
                for (int i = 0; i < _lodout.Length; i++)
                {
                    if (_lodout[i] < 0) continue;

                    if (i >= characterItemManager.Slots.Count) break;

                    if (i >= ItemManager.Instance.SlotsLodout[i].availableItemsForSlot.Length) continue;

                    characterItemManager.Slots[i].ItemOnSpawn = ItemManager.Instance.SlotsLodout[i].availableItemsForSlot[_lodout[i]].gameObject;
                }
            #endregion

            NetworkServer.Spawn(MyCharacter.gameObject, connectionToClient);

            MyCharacter.Team = Team;
            MyCharacter.Server_OnHealthDepleted += OnCharacterHealthDepleted;
            MyCharacter.Server_KilledCharacter += PlayerKilled;
            MyCharacter.Server_OnEarnedAssist += OnEarnedAssist;

            if (BOT)
                MyCharacter.SetAsBOT(BOT);

            WritePlayerData(playerName, Team, MyCharacter.netIdentity, BOT);
            RpcReceivePlayerData(playerName, MyCharacter.netIdentity, Team, BOT);
        }

        public void DespawnCharacterIfExist()
        {
            if (MyCharacter == null) return;
            //despawn previous character

            MyCharacter.CharacterItemManager.OnDespawnCharacter(); //when despawning character despawn also its equipment
            MyCharacter.Server_OnHealthDepleted -= OnCharacterHealthDepleted;
            MyCharacter.Server_KilledCharacter -= PlayerKilled;
            MyCharacter.Server_OnEarnedAssist -= OnEarnedAssist;

            NetworkServer.Destroy(MyCharacter.gameObject);
        }

        private void OnCharacterHealthDepleted(CharacterPart characterPart, AttackType attackType, Health killer, int attackForce)
        {
            //count death for scoreboard
            Deaths++;
            Server_UpdateStatsForAllClients();
            CountCooldown();
        }

        private void OnEarnedAssist(Health victimID)
        {
            Assists++;
            RpcUpdateStats(Kills, Deaths, Assists);
        }

        void PlayerKilled(Health health)
        {
            Kills++;
            Server_UpdateStatsForAllClients();

            if (BOT)
                GetComponent<ChatBehaviour>().RpcHandleChatClientMessage($"I killed {health.CharacterName}!");
        }


        /// <summary>
        /// wait to be able to respawn
        /// </summary>
        public void CountCooldown() 
        {
            SpawnCooldown = false;

            if (_spawnCooldownCoroutine != null) 
            {
                StopCoroutine(_spawnCooldownCoroutine);
                _spawnCooldownCoroutine = null;
            }

            _spawnCooldownCoroutine = StartCoroutine(CountSpawnCooldown());
            IEnumerator CountSpawnCooldown()
            {
                yield return new WaitForSeconds(RoomSetup.Properties.P_RespawnCooldown);
                SpawnCooldown = true;

                if (GameManager.Gamemode.LetPlayersSpawnOnTheirOwn) 
                    Server_SpawnRequest();
            }
        }

        #region update stats
        public void Server_UpdateStatsForAllClients()
        {
            RpcUpdateStats(Kills, Deaths, Assists);
        }
        [ClientRpc]
        void RpcUpdateStats(int kills, int deaths, int assists)
        {
            if (isServer) return;
            Kills = kills;
            Deaths = deaths;
            Assists = assists;
        }
        #endregion

        #region SendPlayerData

        [Command]
        void CmdReceivePlayerData(string _username)
        {
            playerName = _username;
            RpcReceivePlayerData(_username, (MyCharacter? MyCharacter.netIdentity: null), Team, BOT);

            //tell everyone on chat that player joined
            GameManager.Gamemode.Server_WriteToChat($"<color=#{ColorUtility.ToHtmlStringRGBA(Color.cyan)}>" + playerName + " joined the game </color>");
        }

        [ClientRpc]
        void RpcReceivePlayerData(string _username, NetworkIdentity _charNetIdentity, int team, bool isBot)
        {
            WritePlayerData(_username, team, _charNetIdentity, isBot);

            if (_charNetIdentity && isOwned) 
            {
                PlayerGameplayInput.Instance.AssignCharacterToBeControlledByPlayer(_charNetIdentity.GetComponent<CharacterInstance>());
            }
        }

        //update for late players
        void UpdateDataForLatePlayer(NetworkConnection conn)
        {
            TargetRpcReceiveDataForLatePlayer(conn, playerName, MyCharacter ? MyCharacter.netIdentity : null, Kills, Deaths, Team, BOT);
        }

        [TargetRpc]
        void TargetRpcReceiveDataForLatePlayer(NetworkConnection conn, string username, NetworkIdentity charNetIdentity, int kills, int deaths, int team, bool isBot)
        {
            Team = team;
            Kills = kills;
            Deaths = deaths;

            WritePlayerData(username, team, charNetIdentity, isBot);
        }

        void WritePlayerData(string username, int team, NetworkIdentity charNetIdentity, bool isBot)
        {
            BOT = isBot;

            playerName = username;
            Team = team;

            //for inspector so we can better tell who is who
            gameObject.name = "Player: " + username;

            if (charNetIdentity != null)
            {
                MyCharacter = charNetIdentity.GetComponent<CharacterInstance>();
                MyCharacter.CharacterName = username;
                MyCharacter.Team = team;
                //for inspector so we can better whose character is whose
                MyCharacter.gameObject.name = "Character: " + username;
            }

            if (isOwned)
                ClientFrontend.ThisClientTeam = team;

        }
        #endregion

        //launched by UI
        public void ClientRequestJoiningTeam(int team) 
        {
            ProcessClientRequestToJoinTeam(team);
        }

        //here we can check for example if team that client wants to join to is not full 
        [Command]
        void ProcessClientRequestToJoinTeam(int team) 
        {
            ProcessRequestToJoinTeam(team);
        }
        public void ProcessRequestToJoinTeam(int team)
        {
            GameManager.Gamemode.PlayerRequestToJoinTeam(this, team);
        }

        [ClientRpc]
        public void RpcTeamJoiningResponse(int team, int permissionCode) {
            //for UI to subscribe to
            PlayerEvent_OnReceivedTeamResponse?.Invoke(team, permissionCode);
        }
        public void ServerSetTeam(int team) 
        {
            Team=team;
            RpcSetTeam(team);
        }
        [ClientRpc]
        void RpcSetTeam(int team) 
        {
            Team = team;
        }


        #region lodout
        [Command]
        public void CmdSendNewLodoutInfo(int[] loadoutInfo)
        {
            _lodout = loadoutInfo;
        }
        #endregion

        #region ping
        [Command]
        void CmdClientSendPing(ushort ping)
        {
            ClientPing = (byte)ping;
            RpcSendPingInfoToPlayer(ClientPing);
        }
        [ClientRpc(includeOwner = false)]
        void RpcSendPingInfoToPlayer(ushort ping)
        {
            ClientPing = ping;
        }
        #endregion

        private void OnDestroy()
        {
            if(_subbedToNetworkManager)
                DNNetworkManager.Instance.OnNewPlayerConnected -= UpdateDataForLatePlayer;

            if (_registeredPlayerInstance)
            {
                GameManager.RemovePlayerInstance(this, _netID);
            }

            if(_serverInstance)
                GameManager.Gamemode.Server_OnPlayerInstanceRemoved(this);

            if (_connID != -1) 
            {
                GameManager.PlayersByConnectionID.Remove(_connID);
                GameManager.Gamemode.Relay_ClientDisconnected(connectionToClient, this.netIdentity);
            }
        }
    }

}