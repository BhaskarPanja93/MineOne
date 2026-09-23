using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;


// Behavior for the Lobby UI
public class Lobby: NetworkBehaviour
{
    [SerializeField] private GameObject startButton;
    [SerializeField] private GameObject playerList;
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private TMP_Text joinCodeText;
    [SerializeField] private TMP_Text sizeText;
    [SerializeField] private Button increaseSizeButton;
    [SerializeField] private Button decreaseSizeButton;


    // Cleanup all Player elements from UI and recreate UI from scratch
    private void RecreateUI()
    {
        
        
        // Cleanup
        foreach (Transform child in playerList.transform)
            Destroy(child.gameObject);
        
        
        // Recreate
        foreach (var player in SessionManager.PlayersList)
        {
            var go = Instantiate(playerPrefab, playerList.transform);
            go.name = player.ClientId.ToString(); // Save GO name as ClientId for a future Find() call
            go.transform.Find("Name").GetComponent<TMP_Text>().text = player.Name.ToString();
            go.transform.Find("Score").GetComponent<TMP_Text>().text = player.Score.ToString();
        }


        // Update button state
        UpdatePlayableButton();
    }


    // Update the start button text and interactivity based on number of players
    private void UpdatePlayableButton()
    {
        if (SessionManager.PlayersList.Count > 1)
        {
            startButton.transform.GetChild(0).GetComponent<TMP_Text>().text = NetworkManager.Singleton.IsServer ? "Start Game" : "Waiting for host to start";
            startButton.GetComponent<Button>().interactable = NetworkManager.Singleton.IsServer;
        }
        else
        {
            startButton.transform.GetChild(0).GetComponent<TMP_Text>().text = "Waiting for players...";
            startButton.GetComponent<Button>().interactable = false;
        }
    }
    

    // Callback for when the NetworkList PlayerList changes
    private void OnPlayersChanged(NetworkListEvent<PlayerData> change)
    {
        switch (change.Type)
        {
            
            
            // Player Added
            case NetworkListEvent<PlayerData>.EventType.Add:
            case NetworkListEvent<PlayerData>.EventType.Insert:
                var playerData = change.Value;
                var go = Instantiate(playerPrefab, playerList.transform);
                go.name = playerData.ClientId.ToString(); // Save GO name as ClientTd for a future Find() call
                go.transform.Find("Name").GetComponent<TMP_Text>().text = playerData.Name.ToString();
                go.transform.Find("Score").GetComponent<TMP_Text>().text = playerData.Score.ToString();
                UpdatePlayableButton();
                break;

            
            // Player Removed
            case NetworkListEvent<PlayerData>.EventType.Remove:
            case NetworkListEvent<PlayerData>.EventType.RemoveAt:
                playerData = change.Value;
                go = playerList.transform.Find(playerData.ClientId.ToString()).gameObject;
                if (go != null) Destroy(go.gameObject);
                UpdatePlayableButton();
                break;

            
            // List Reset
            case NetworkListEvent<PlayerData>.EventType.Clear:
            case NetworkListEvent<PlayerData>.EventType.Full:
                RecreateUI();
                break;

            
            // Items Modified
            // Ignore all modify calls, because players can't modify data once they are in lobby
            case NetworkListEvent<PlayerData>.EventType.Value:
                break;
        }
    }


    // Callback for when NetworkVariable JoinCode changes
    private void OnJoinCodeChanged(FixedString32Bytes _, FixedString32Bytes newValue)
    {
        joinCodeText.text = newValue.Value;
    }


    // Callback for when NetworkVariable GridSize changes
    private void OnGridSizeChanged(int _, int newValue)
    {
        sizeText.text = newValue.ToString();
    }

    
    // Callback for increase size button
    // Limit to upper bound of 100
    private void IncreaseSize()
    {
        if (SessionManager.GridSize.Value >= 100) return;
        SessionManager.GridSize.Value += 5;
    }

    
    // Callback for decrease size button
    // Limit to lower bound of 5
    private void DecreaseSize()
    {
        if (SessionManager.GridSize.Value <= 10) return;
        SessionManager.GridSize.Value -= 5;
    }

    
    // Callback for game start button
    private void StartGame()
    {
        SessionManager.GameStarted.Value = true;
        NetworkManager.Singleton.SceneManager.LoadScene("Game", LoadSceneMode.Single);
    }
    
    
    // Run when script enables (regardless of network events)
    private async void Start()
    {
        
        
        // Add all button callbacks and set interactability
        if (NetworkManager.Singleton.IsServer)
        {
            startButton.GetComponent<Button>().interactable = true;
            startButton.GetComponent<Button>().onClick.AddListener(StartGame);
            increaseSizeButton.interactable = true;
            increaseSizeButton.onClick.AddListener(IncreaseSize);
            decreaseSizeButton.interactable = true;
            decreaseSizeButton.onClick.AddListener(DecreaseSize);
            if (SessionManager.GridSize.Value < 10) SessionManager.GridSize.Value = 10;
        }
        else
        {
            startButton.GetComponent<Button>().interactable = false;
            increaseSizeButton.interactable = false;
            decreaseSizeButton.interactable = false;
        }
        
        
        // Wait some time to allow NetworkManager to load
        while (SessionManager.Singleton == null || NetworkManager == null || NetworkManager.Singleton == null)
            await Awaitable.EndOfFrameAsync();
        
        
        // Add NetworkVariable change callbacks
        SessionManager.PlayersList.OnListChanged += OnPlayersChanged;
        SessionManager.JoinCode.OnValueChanged += OnJoinCodeChanged;
        joinCodeText.text = SessionManager.JoinCode.Value.ToString();
        SessionManager.GridSize.OnValueChanged += OnGridSizeChanged;
        sizeText.text = SessionManager.GridSize.Value.ToString();
        
        
        // Initial UI recreation
        RecreateUI();
    }

    
    // Run when script disables (regardless of network events)
    public override void OnNetworkDespawn()
    {
        
        
        // Remove all callbacks
        increaseSizeButton.onClick.RemoveAllListeners();
        decreaseSizeButton.onClick.RemoveAllListeners();
        startButton.GetComponent<Button>().onClick.RemoveAllListeners();
        SessionManager.PlayersList.OnListChanged -= OnPlayersChanged;
        SessionManager.JoinCode.OnValueChanged -= OnJoinCodeChanged;
        SessionManager.GridSize.OnValueChanged -= OnGridSizeChanged;
    }
}