using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.SceneManagement;
using Random = UnityEngine.Random;


// Behavior for maintaining a session
public class SessionManager : NetworkBehaviour
{
    
    
    // Local variables
    public static PlayerData LocalPlayer;
    private static ISession _currentSession;
    public static Dictionary<ulong, PlayerData> Players;
    private float _lastProvidedHValue;
    
    
    // Network synced variables
    public static SessionManager Singleton { get; private set; }
    public static NetworkList<PlayerData> PlayersList { get; private set; }
    public static NetworkVariable<bool> GameStarted { get; private set; }
    public static NetworkVariable<int> GridSize { get; private set; }
    public static NetworkVariable<FixedString32Bytes> JoinCode { get; private set; }
    public static NetworkList<CellData> Cells { get; set; }

    
    // Client -> Server RPC
    // Client notifies server of its arrival for registration
    // Server provides a color for the client, sets its client id as seen from the server and decides its playable state based on if 
    // Instead of providing a random color, provide an equally spaced Hue value based on number of players to remove the possibility of 2 or more players getting same color
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RegisterNewPlayerRpc(PlayerData playerData, RpcParams rpcParams = default)
    {
        playerData.ClientId = rpcParams.Receive.SenderClientId + 1;
        playerData.Color = Color.HSVToRGB(_lastProvidedHValue, 1, 1);
        playerData.PlayableState = GameStarted.Value ? PlayableStates.Disqualified : PlayableStates.Playable;
        PlayersList.Add(playerData);
        _lastProvidedHValue += 1 / (float)(2 + PlayersList.Count);
    }


    // Callback for when a player leaves
    private void RemovePlayer(ulong clientId)
    {
        for (var i = 0; i < PlayersList.Count; i++)
            if (PlayersList[i].ClientId == clientId + 1)
            {
                PlayersList.RemoveAt(i);
                break;
            }
    }

    
    // Fetch a player session using Unity's AuthenticationService
    private async Task<bool> SignInAnonymously()
    {
        try
        {
            
            
            // Init UnityServices if not already
            if (UnityServices.State != ServicesInitializationState.Initialized) await UnityServices.InitializeAsync();
            
            
            // If player isn't signed in already, sign them in
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    
    // Allows creating a new session using unity's MultiplayerService
    public async Task<string> CreateLobby()
    {
        try
        {
            
            
            // Attempt login (skips automatically if already logged in)
            if (!await SignInAnonymously()) return "Failed to login";
            
            
            // Set session options including max players and server location
            var options = new SessionOptions{ MaxPlayers = 100 }.WithRelayNetwork("asia-south1"); // Remove specific server if required
            
            
            // Create the session and save it
            _currentSession = await MultiplayerService.Instance.CreateSessionAsync(options);
            JoinCode.Value = _currentSession.Code;
            
            
            // Switch to lobby scene for all players
            NetworkManager.Singleton.SceneManager.LoadScene("Lobby", LoadSceneMode.Single);
            return "Lobby created";
        }
        catch (Exception e)
        {
            return e.Message;
        }
    }

    
    // Allows joining a Pre-Existing lobby using its join code
    public async Task<string> ConnectCode(string code)
    {
        
        
        // Attempt login (skips automatically if already logged in)
        if (!await SignInAnonymously()) return "Failed to login";
        
        
        try
        {
            
            
            // Join the session and save it
            _currentSession = await MultiplayerService.Instance.JoinSessionByCodeAsync(code);
            return "Joined lobby";
        }
        catch (Exception e)
        {
            return e.Message;
        }
    }
    
    
    // Run when the script is created
    private void Awake()
    {
        
        
        // Create a singleton and tell unity to not destroy the GameObject on scene changes
        if (Singleton != null && Singleton != this)
        {
            Destroy(gameObject);
            return;
        }
        Singleton = this;
        DontDestroyOnLoad(gameObject);

        
        // Initialize the network variables
        GameStarted = new NetworkVariable<bool>();
        GridSize = new NetworkVariable<int>();
        JoinCode = new NetworkVariable<FixedString32Bytes>();
        PlayersList = new NetworkList<PlayerData>();
        Cells = new NetworkList<CellData>();
        
        
        // Initialize other variables
        Players = new Dictionary<ulong, PlayerData>();
        
        
        // Map players from list to a dictionary for faster fetching
        PlayersList.OnListChanged += change =>
        {
            switch (change.Type)
            {
            
            
                // Player Added or modified
                case NetworkListEvent<PlayerData>.EventType.Add:
                case NetworkListEvent<PlayerData>.EventType.Insert:
                case NetworkListEvent<PlayerData>.EventType.Value:
                    var playerData = change.Value;
                    Players[playerData.ClientId] = playerData;
                    break;

            
                // Player Removed
                case NetworkListEvent<PlayerData>.EventType.Remove:
                case NetworkListEvent<PlayerData>.EventType.RemoveAt:
                    playerData = change.Value;
                    Players.Remove(playerData.ClientId);
                    break;

            
                // List Reset
                case NetworkListEvent<PlayerData>.EventType.Clear:
                case NetworkListEvent<PlayerData>.EventType.Full:
                    Players.Clear();
                    break;
            }
        };
    }


    // Run when network session starts
    public override void OnNetworkSpawn()
    {
        LocalPlayer.ClientId = NetworkManager.Singleton.LocalClientId + 1;
        
        
        // If self is server, register player leave callback
        if (IsServer) NetworkManager.OnClientDisconnectCallback += RemovePlayer;
        
        
        // If self is a client, register self to server
        if (IsClient) RegisterNewPlayerRpc(LocalPlayer);


        // Set initial value of Players dictionary
        foreach (var playerData in PlayersList)
        {
            Players[playerData.ClientId] = playerData;
        }
    }

    
    // Run when network session ends
    public override void OnNetworkDespawn()
    {
        if (IsServer) NetworkManager.OnClientDisconnectCallback -= RemovePlayer;
    }
}