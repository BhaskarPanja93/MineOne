using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;


// Behavior for the EndScreen UI
public class EndScreen : NetworkBehaviour
{
    [SerializeField] private Button playAgainButton;
    [SerializeField] private List<GameObject> topperPlayers;
    [SerializeField] private GameObject playerList;
    [SerializeField] private GameObject playerPrefab;

    
    // Callback for going back to lobby
    public void PlayAgain()
    {
        
        
        // Reset state for all players if play again is pressed
        if (NetworkManager.Singleton.IsServer)
        {
            SessionManager.GameStarted.Value = false;
            for (var i = 0; i < SessionManager.PlayersList.Count; i++)
            {
                var playerData = SessionManager.PlayersList[i];
                playerData.PlayableState = PlayableStates.Playable;
                playerData.Score = 0;
                SessionManager.PlayersList[i] = playerData;
            }
            NetworkManager.Singleton.SceneManager.LoadScene("Lobby", LoadSceneMode.Single);
        }
    }
    
    
    // Run when script enables (regardless of network events)
   private async void Start()
   {
       
       
        // Allow only server to press play again
        playAgainButton.interactable = NetworkManager.Singleton.IsServer;


        // Wait some time to allow NetworkManager to load
        while (SessionManager.Singleton == null || NetworkManager == null || NetworkManager.Singleton == null)
            await Awaitable.EndOfFrameAsync();
        
        
        // Sort all players by their scores
        var playersSorted = new List<PlayerData>(SessionManager.PlayersList.AsNativeArray())
            .OrderByDescending(p => p.Score)
            .ToList();
        
        
        // Disable all player ui objects initially
        foreach (var ui in topperPlayers)
            ui.SetActive(false);
        
        
        // For each player assign one of the topper ui items, or create new ui and assign
        for (var i = 0; i < playersSorted.Count; i++)
        {
            if (i >= topperPlayers.Count)
            {
                var instance = Instantiate(playerPrefab, playerList.transform);
                instance.transform.Find("Name").GetComponent<TMP_Text>().text = playersSorted[i].Name.ToString();
                instance.transform.Find("Score").GetComponent<TMP_Text>().text = playersSorted[i].Score.ToString();
            }

            topperPlayers[i].transform.Find("Name").GetComponent<TMP_Text>().text = playersSorted[i].Name.ToString();
            topperPlayers[i].transform.Find("Score").GetComponent<TMP_Text>().text = playersSorted[i].Score.ToString();
            topperPlayers[i].SetActive(true);
        }
    }
}