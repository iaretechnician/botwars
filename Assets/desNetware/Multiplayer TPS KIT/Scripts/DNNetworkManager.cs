using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
using Mirror.SimpleWeb;
using System.Text.RegularExpressions;
using UnityEngine.SceneManagement;
using MTPSKIT.Gameplay;
using System;

namespace MTPSKIT
{

    public class DNNetworkManager : NetworkManager
    {
        public static DNNetworkManager Instance;

        //this event exist to send late players data about gamemode and equipment of other players.
        public delegate void NewPlayerJoinedTheGame(NetworkConnectionToClient conn);
        public NewPlayerJoinedTheGame OnNewPlayerConnected { get; set; }

        public delegate void PlayerDisconnected(NetworkConnectionToClient conn);
        public PlayerDisconnected OnPlayerDisconnected { get; set; }
        public Action<ushort> Callback_OnNetworkTransportPortSet { get; internal set; }

        public override void Awake()
        {
            if (Instance)
            {
                Debug.LogError("Fatal error, two instances of Custom Network Manager");
                Destroy(Instance.gameObject);
            }

            base.Awake();

            Instance = this;
            autoCreatePlayer = true;

            offlineScene = SceneManager.GetActiveScene().path;
        }

        public void SetTransportPort(ushort port) 
        {
            Callback_OnNetworkTransportPortSet?.Invoke(port);
        }


        public override void OnServerAddPlayer(NetworkConnectionToClient conn)
        {
            base.OnServerAddPlayer(conn);
            OnNewPlayerConnected?.Invoke(conn);
        }

        public override void OnServerDisconnect(NetworkConnectionToClient conn)
        {
            OnPlayerDisconnected?.Invoke(conn);
            base.OnServerDisconnect(conn);
        }

        void OnReceivedPlayerInputMessage(NetworkConnection conn, ClientSendInputMessage msg)
        {
            try
            {
                if (GameManager.PlayersByConnectionID.TryGetValue(conn.connectionId, out PlayerInstance pi))
                {
                    if (pi.MyCharacter)
                        pi.MyCharacter.ReadAndApplyInputFromClient(msg);
                }
            }
            catch (Exception ex){ Debug.Log($"{ex.Message}"); Debug.Log($"{ex}"); }
        }

        public void SetAddressAndPort(string address, ushort port)
        {
            if (string.IsNullOrEmpty(address)) return;

            SetTransportPort((ushort)System.Convert.ToInt32(port));
            networkAddress = address;
        }

        public override void OnStartServer()
        {
            NetworkServer.RegisterHandler<ClientSendInputMessage>(OnReceivedPlayerInputMessage);
        }
        public override void OnStopServer()
        {
            NetworkServer.UnregisterHandler<ClientSendInputMessage>();
        }
    }

    public struct ClientSendInputMessage : NetworkMessage
    {
        public byte Movement;
        public sbyte LookX;
        public short LookY;
        public byte ActionCodes; //sprint, fire1, fire2, and 5 free inputs to utilize   
    }
}