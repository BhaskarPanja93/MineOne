using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Random = UnityEngine.Random;


// Behavior for the Game UI
public class Game: NetworkBehaviour
{
    [SerializeField] private GameObject cellPrefab;
    [SerializeField] private GameObject cellList;
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private GameObject playerList;
    [SerializeField] private Sprite bombSprite;
    [SerializeField] private Sprite revealedSprite;
    [SerializeField] private TMP_Text notificationText;
    private int _remainingCoveredCells;
    private int _remainingPlayablePlayers;
    private Task _generatingCells;
    private bool _generatedCells;


    // Generate all cells (logical only)
    private Task CreateCellValues(int firstClickIndex)
    {
        
        
        _remainingCoveredCells = 0;
        var size = SessionManager.GridSize.Value;
        var totalCells = size * size;
        var firstClickRow = firstClickIndex / size;
        var firstClickColumn = firstClickIndex % size;

        
        // Generate locally initially
        var cells = new CellData[totalCells];
        for (var i = 0; i < totalCells; i++)
        {
            cells[i] = new CellData
            {
                IsBomb = false,
                Index = i,
                RevealedBy = 0,
                Points = 0
            };
        }

        
        // Place bombs and increment their neighbors' points
        for (var i = 0; i < totalCells; i++)
        {
            var row = i / size;
            var column = i % size;

            
            var isFirstClickArea =
                Mathf.Abs(row - firstClickRow) <= 1 &&
                Mathf.Abs(column - firstClickColumn) <= 1;

            
            // One out of every 5 cells will be a bomb (randomized)
            if (!isFirstClickArea && Random.Range(0, 5) == 0)
            {
                cells[i].IsBomb = true;

                
                // Increment surrounding cells' points
                for (var dy = -1; dy <= 1; dy++)
                {
                    var neighborRow = row + dy;
                    if (neighborRow < 0 || neighborRow >= size)
                        continue;
                    for (var dx = -1; dx <= 1; dx++)
                    {
                        var neighborColumn = column + dx;
                        if (neighborColumn < 0 || neighborColumn >= size)
                            continue;
                        if (dx == 0 && dy == 0)
                            continue;
                        var neighborIndex = neighborRow * size + neighborColumn;
                        cells[neighborIndex].Points++;
                    }
                }
            }
            else
            {
                _remainingCoveredCells++;
            }
        }

        
        // Modify the NetworkList
        SessionManager.Cells.Clear();
        for (var i = 0; i < totalCells; i++)
            SessionManager.Cells.Add(cells[i]);

        
        _generatedCells = true;
        return Task.CompletedTask;
    }


    // Send all players to EndScreen
    private async void AllDisqualified()
    {
        await Awaitable.EndOfFrameAsync();
        ChangeWarningTextRpc("All players disqualified!!");
        await Task.Delay(3000);
        NetworkManager.Singleton.SceneManager.LoadScene("EndScreen", LoadSceneMode.Single);
    }
    
    
    // Send all players to Lobby
    private async void AllCellsUncovered()
    {
        await Awaitable.EndOfFrameAsync();
        ChangeWarningTextRpc("All cells uncovered!!");
        await Task.Delay(3000);
        NetworkManager.Singleton.SceneManager.LoadScene("Lobby", LoadSceneMode.Single);
    }
    

    // Wait for all cell creation and then execute
    private async void ProcessClickAfterGeneration(PlayerData playerData, int cellIndex)
    {

        
        // If generation is not completed yet
        if (!_generatedCells)
        {
            
            
            // If generation has not started yet, start the generation
            if (_generatingCells == null)
                _generatingCells = CreateCellValues(cellIndex);
           
            
            // Wait for just created or previously created task
            await _generatingCells;
        }

        // Finally fetch the cell data
        var cellData = SessionManager.Cells[cellIndex];
        
        
        // If bomb clicked
        if (cellData.IsBomb)
        {
            
            
            // Disqualify player
            playerData.PlayableState = PlayableStates.Disqualified;
            for (var i = 0; i < SessionManager.PlayersList.Count; i++)
            {
                if (playerData.ClientId == SessionManager.PlayersList[i].ClientId)
                    SessionManager.PlayersList[i] = playerData;
            }
            
            
            // Send the bomb data only to the clicker
            // This way other players are still unaware of where the bombs are
            var param = new RpcParams
            {
                Send = new RpcSendParams
                {
                    Target = RpcTarget.Single(playerData.ClientId-1, RpcTargetUse.Temp)
                }
            };
            CallbackBombRevealRpc(cellData.Index, param);
            
            
            // Reduce count of remaining players
            _remainingPlayablePlayers -= 1;
            
            
            // If no players remain, move to end screen
            if (_remainingPlayablePlayers <= 0)
                AllDisqualified();
            return;
        }

        
        // DFS based flood-fill logic
        var stack = new Stack<int>();
        
        
        // Reveal a cell 
        void RevealCell(int index)
        {
            if (index < 0 || index >= SessionManager.Cells.Count)
                return;
            
            
            var cell = SessionManager.Cells[index];
            if (cell.IsBomb || cell.RevealedBy != 0)
                return;

            cell.RevealedBy = playerData.ClientId;
            SessionManager.Cells[index] = cell;
            _remainingCoveredCells -= 1;
            if (_remainingCoveredCells <= 0)
            {
                AllCellsUncovered();
                return;
            }
            stack.Push(index);
        }

        
        // Start with revealing the clicked cell
        RevealCell(cellIndex);

        
        var size = SessionManager.GridSize.Value;

        
        // Continue while there's cells in the stack
        while (stack.Count > 0)
        {
            var index = stack.Pop();
            var cell = SessionManager.Cells[index];

            playerData.Score += cell.Points;

            if (cell.Points != 0)
                continue;

            var x = index % size;
            var y = index / size;

            for (var dy = -1; dy <= 1; dy++)
            for (var dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0)
                    continue;

                var nx = x + dx;
                var ny = y + dy;

                if (nx < 0 || nx >= size || ny < 0 || ny >= size)
                    continue;

                var neighborIndex = ny * size + nx;
                RevealCell(neighborIndex);
            }
        }
        
        
        // Update player data (score)
        for (var i = 0; i < SessionManager.PlayersList.Count; i++)
        {
            if (playerData.ClientId == SessionManager.PlayersList[i].ClientId)
                SessionManager.PlayersList[i] = playerData;
        }
    }
    

    // Callback for when any cell data changes
    private void CallbackCellsChanged(NetworkListEvent<CellData> change)
    {
        switch (change.Type)
        {
            
            
            // Cell Added or modified
            case NetworkListEvent<CellData>.EventType.Add:
            case NetworkListEvent<CellData>.EventType.Insert:
            case NetworkListEvent<CellData>.EventType.Value:
                var cellData = change.Value;
                
                
                // if cell states its revealed, change its appearance accordingly
                if (cellData.RevealedBy > 0)
                {
                    var cellUI = cellList.transform.GetChild(cellData.Index);
                    var textUI = cellUI.Find("Text");
                    textUI.GetComponent<TMP_Text>().text = cellData.Points > 0 ? cellData.Points.ToString() : "";
                    cellUI.GetComponent<Image>().sprite = cellData.IsBomb ? bombSprite : revealedSprite;
                    textUI.GetComponent<TMP_Text>().color = SessionManager.Players[cellData.RevealedBy].Color;
                    textUI.gameObject.SetActive(true);
                    Destroy(cellUI.GetComponent<Button>());
                }
                break;
        } 
    }

    
    // Callback for when any player data changes
    private void CallbackPlayerChanged(NetworkListEvent<PlayerData> change)
    {
        switch (change.Type)
        {
            
            
            // Player Added
            // Ignore new player added as new players will be added as disqualified
            case NetworkListEvent<PlayerData>.EventType.Add:
            case NetworkListEvent<PlayerData>.EventType.Insert:
                break;
            
            
            // Player modified
            case NetworkListEvent<PlayerData>.EventType.Value:
                var playerData = change.Value;
                var playerUI = playerList.transform.Find(playerData.ClientId.ToString());
                if (!playerUI) return;
                playerUI.Find("Score").GetComponent<TMP_Text>().text = playerData.Score.ToString();
                if (playerData.PlayableState == PlayableStates.Disqualified)
                {
                    playerUI.GetComponent<Image>().color = new Color(104 / 255f, 104 / 255f, 104 / 255f);
                    notificationText.text = (playerData.ClientId == SessionManager.LocalPlayer.ClientId ? "You" : playerData.Name.Value) + " disqualified!";
                }
                break;
            
            
            // Player Removed
            case NetworkListEvent<PlayerData>.EventType.Remove:
            case NetworkListEvent<PlayerData>.EventType.RemoveAt:
                playerData = change.Value;
                playerUI = playerList.transform.Find(playerData.ClientId.ToString());
                if (!playerUI) return;
                Destroy(playerUI.gameObject);
                break;
        }
    }

    
    // Everyone -> Server RPC
    // Client tells server which cell it clicked
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RegisterNewClickRpc(int cellIndex, RpcParams rpcParams = default)
    {
        
        Debug.Log("RegisterNewClickRpc");
        
        
        // Fetch the caller's ClientId
        var sender = rpcParams.Receive.SenderClientId + 1;
        
        
        // Ignore if data not found
        if (!SessionManager.Players.TryGetValue(sender, out var playerData)) return;
    
        
        // Ignore if player not playable
        if (!playerData.PlayableState.Equals(PlayableStates.Playable)) return;
        
        
        // Process click only after cells have been generated
        ProcessClickAfterGeneration(playerData, cellIndex);
    }


    // Server -> Everyone RPC
    // Send a random string to show as notification
    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void ChangeWarningTextRpc(string newText, RpcParams _ = default)
    {
        notificationText.text = newText;
    }
    
    
    // Server -> Everyone RPC
    // Reveal a random bomb spot
    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server, AllowTargetOverride =  true)]
    private void CallbackBombRevealRpc(int cellIndex, RpcParams _ = default)
    {
        cellList.transform.GetChild(cellIndex).GetComponent<Image>().sprite = bombSprite;
    }


    // Run when script enables (regardless of network events)
    private async void Start()
    {

        
        // Wait some time to allow NetworkManager to load
        while (SessionManager.Singleton == null || NetworkManager == null || NetworkManager.Singleton == null)
            await Awaitable.EndOfFrameAsync();
       
        
        // Clear cells data if player is server
        if (IsServer) SessionManager.Cells.Clear();
        _remainingPlayablePlayers = 0;
        
        
        // Attach callbacks
        SessionManager.Cells.OnListChanged += CallbackCellsChanged;
        SessionManager.PlayersList.OnListChanged += CallbackPlayerChanged;
        
        
        // Create UI for each player (playable)
        foreach (var playerData in SessionManager.PlayersList)
        {
            if (playerData.PlayableState != PlayableStates.Playable) continue;
            _remainingPlayablePlayers++;
            var playerUI = Instantiate(playerPrefab, playerList.transform);
            playerUI.name = playerData.ClientId.ToString();
            playerUI.transform.Find("Name").GetComponent<TMP_Text>().text = playerData.Name.ToString();
            playerUI.transform.Find("Score").GetComponent<TMP_Text>().text = playerData.Score.ToString();
            playerUI.transform.Find("Color").GetComponent<Image>().color = playerData.Color;
        }

        
        // Set cell grid size and properties
        var grid = cellList.GetComponent<GridLayoutGroup>();
        var cellSize = cellList.GetComponent<RectTransform>().rect.width / SessionManager.GridSize.Value;
        grid.constraintCount = SessionManager.GridSize.Value;
        grid.cellSize = new Vector2(cellSize, cellSize);

        
        // Create all the cell buttons
        for (var i = 0; i < SessionManager.GridSize.Value * SessionManager.GridSize.Value; i++)
        {
            var index = i;
            var cellUI = Instantiate(cellPrefab, cellList.transform);
            cellUI.transform.GetChild(0).GetComponent<TMP_Text>().fontSize = cellSize * 0.8f;
            cellUI.GetComponent<Button>().onClick.AddListener(() =>
            {
                
                
                // Small local-side check to ignore click if a cell was already revealed
                if (SessionManager.Cells.Count > index && SessionManager.Cells[index].RevealedBy > 0) return;
                RegisterNewClickRpc(index);
            });
        }
    }
    

    // Run when network session ends
    public override void OnNetworkDespawn()
    {
        SessionManager.Cells.OnListChanged -= CallbackCellsChanged;
        SessionManager.PlayersList.OnListChanged -= CallbackPlayerChanged;
    }
}