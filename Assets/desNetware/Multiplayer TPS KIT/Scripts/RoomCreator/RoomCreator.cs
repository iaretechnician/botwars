using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MTPSKIT.Gameplay.Gamemodes;
using MTPSKIT;
namespace MTPSKIT
{
    /// <summary>
    /// user interface class for user to be able to specify game parameters like map, gamemode
    /// max players and game duration
    /// </summary>
    public class RoomCreator : MonoBehaviour
    {
        public Dropdown MapselectionDropdown;
        public Dropdown GamemodeSelectionDropdown;
        public Dropdown GameDurationDropdown;
        public Dropdown PlayerNumberDropdown;

        public Button StartGameButton;
        public Button ServerOnlyButton;
        public Toggle BotsToggle;

        DNNetworkManager _networkManager;

        //user input
        int _selectedMapID;
        int _selectedTimeDurationID;
        int _selectedPlayerNumberOptionID;
        Gamemodes _selectedGamemode;

        [Header("Options for player to choose from")]
        public MapRepresenter[] Maps;
        public int[] TimeOptionsInMinutes = { 2, 5, 10 };
        public int[] PlayerNumberOptions = { 2, 4, 6, 8 };

        void Start()
        {
            _networkManager = DNNetworkManager.Instance;

            List<string> mapOptions = new List<string>();

            for (int i = 0; i < Maps.Length; i++)
            {
                mapOptions.Add(Maps[i].Name);
            }

            MapselectionDropdown.ClearOptions();
            MapselectionDropdown.AddOptions(mapOptions);

            MapselectionDropdown.onValueChanged.AddListener(OnMapselected);
            GamemodeSelectionDropdown.onValueChanged.AddListener(OnGamemodeSelected);
            GameDurationDropdown.onValueChanged.AddListener(OnGameDurationSelected);
            PlayerNumberDropdown.onValueChanged.AddListener(OnPlayerNumberOption);

            //game duration options
            List<string> durationOptions = new List<string>();

            for (int i = 0; i < TimeOptionsInMinutes.Length; i++)
            {
                durationOptions.Add(TimeOptionsInMinutes[i].ToString() + " minutes");
            }

            GameDurationDropdown.AddOptions(durationOptions);

            List<string> playerNumberOptions = new List<string>();
            //player number options
            for (int i = 0; i < PlayerNumberOptions.Length; i++)
            {
                playerNumberOptions.Add(PlayerNumberOptions[i].ToString() + " players");
            }
            PlayerNumberDropdown.ClearOptions();
            PlayerNumberDropdown.AddOptions(playerNumberOptions);

            StartGameButton.onClick.AddListener(StartGame);
            ServerOnlyButton.onClick.AddListener(StartServer);

            OnMapselected(0);
        }
        void OnMapselected(int mapID)
        {
            _selectedMapID = mapID;
            OnGamemodeSelected(0);

            //fill gamemodes dropdown with options avaible for given map
            Gamemodes[] avaibleGamemodesForThisMap = Maps[mapID].AvailableGamemodes;

            List<string> gamemodeOptions = new List<string>();

            for (int i = 0; i < avaibleGamemodesForThisMap.Length; i++)
            {
                gamemodeOptions.Add(avaibleGamemodesForThisMap[i].ToString());
            }

            GamemodeSelectionDropdown.ClearOptions();
            GamemodeSelectionDropdown.AddOptions(gamemodeOptions);
        }

        /// <summary>
        /// trigged by selecting gamemode in UI room creator, tells game which gamemode to setup
        /// </summary>
        /// <param name="gamemodeID"></param>
        void OnGamemodeSelected(int gamemodeID) //gamemode ID is relevant to gamemodes order in their enum
        {
            _selectedGamemode = Maps[_selectedMapID].AvailableGamemodes != null && Maps[_selectedMapID].AvailableGamemodes.Length > 0 ? Maps[_selectedMapID].AvailableGamemodes[gamemodeID] : Gamemodes.None;
        }
        void OnGameDurationSelected(int timeOptionID)
        {
            _selectedTimeDurationID = timeOptionID;
        }
        void OnPlayerNumberOption(int playerOptionID)
        {
            _selectedPlayerNumberOptionID = playerOptionID;
        }

        //write parameters and start game as host
        void StartGame()
        {
            _networkManager.onlineScene = Maps[_selectedMapID].Scene;

            RoomSetup.Properties.P_Gamemode = _selectedGamemode;
            RoomSetup.Properties.P_FillEmptySlotsWithBots = BotsToggle.isOn;
            RoomSetup.Properties.P_GameDuration = TimeOptionsInMinutes[_selectedTimeDurationID] * 60;
            RoomSetup.Properties.P_RespawnCooldown = 4f;

            //player count
            int maxPlayers = PlayerNumberOptions[_selectedPlayerNumberOptionID];
            RoomSetup.Properties.P_MaxPlayers = maxPlayers;
            _networkManager.maxConnections = maxPlayers;

            _networkManager.StartHost();
        }

        void StartServer() 
        {
            _networkManager.onlineScene = Maps[_selectedMapID].Scene;

            RoomSetup.Properties.P_Gamemode = _selectedGamemode;
            RoomSetup.Properties.P_FillEmptySlotsWithBots = BotsToggle.isOn;
            RoomSetup.Properties.P_GameDuration = TimeOptionsInMinutes[_selectedTimeDurationID] * 60;
            RoomSetup.Properties.P_RespawnCooldown = 4f;

            //player count
            int maxPlayers = PlayerNumberOptions[_selectedPlayerNumberOptionID];
            RoomSetup.Properties.P_MaxPlayers = maxPlayers;
            _networkManager.maxConnections = maxPlayers;

            _networkManager.StartServer();
        }
    }
}