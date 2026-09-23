using TMPro;
using Unity.Netcode;
using UnityEngine;


// Behavior for the Menu UI
public class Menu: NetworkBehaviour
{
    [SerializeField] private TMP_InputField joinCodeInput;
    [SerializeField] private TMP_Text notificationText;
    [SerializeField] private TMP_InputField nameInput;
    private bool _uiDisabled;

    
    // Checks and returns a boolean for correctness of name
    // Also shows warning in case of invalid name
    private bool NameIsOk()
    {
        
        
        // Disallow blank names
        if (SessionManager.LocalPlayer.Name == "")
        {
            
            
            // Display warning in case of invalid name
            notificationText.text = "Please enter a name.";
            notificationText.gameObject.SetActive(true);
            return false;
        }

        
        // Hide the warning if name is fine
        notificationText.gameObject.SetActive(false);
        return true;
    }


    // Callback for when the nameInput changes
    public void OnNameChange()
    {
        
        
        // If ui was disabled, reject and revert the change 
        if (_uiDisabled)
        {
            nameInput.text = SessionManager.LocalPlayer.Name.Value;
            return;
        }
        
        
        // Set the new name into LocalPlayer and PlayerPrefs
        SessionManager.LocalPlayer.Name = nameInput.text;
        PlayerPrefs.SetString("Name", nameInput.text);
        
        
        // Dry-run name checker
        NameIsOk();
    }

    
    // Callback for the create lobby button
    public async void HostLobby()
    {
        
        
        // Reject if invalid name
        if (!NameIsOk()) return;
        
        
        // Reject if ui was disabled
        if (_uiDisabled) return;
        
        
        // Disable UI
        _uiDisabled = true;
        notificationText.text = "Creating lobby...";
        notificationText.gameObject.SetActive(true);
        
        
        // Start the Create lobby process
        // Show the task response as warning text
        notificationText.text = await SessionManager.Singleton.CreateLobby();
        
        
        // Enable UI
        _uiDisabled = false;
    }

    
    // Callback for the join lobby button
    public async void JoinLobby()
    {
        
        // Reject if invalid name
        if (!NameIsOk()) return;


        // Reject if join code empty
        if (joinCodeInput.text == "") return;
        
        
        // Reject if ui was disabled
        if (_uiDisabled) return;
        
        
        // Disable UI
        _uiDisabled = true;
        notificationText.text = "Joining lobby...";
        notificationText.gameObject.SetActive(true);

        
        // Start the join process
        // Show the task response as warning text
        notificationText.text = await SessionManager.Singleton.ConnectCode(joinCodeInput.text);
        
        
        // Enable UI
        _uiDisabled = false;
    }
    
    
    // Run when script loads (regardless of network events)
    private void Awake()
    {
        
        
        // Fetch and display last saved name
        SessionManager.LocalPlayer.Name = PlayerPrefs.GetString("Name");
        nameInput.text = SessionManager.LocalPlayer.Name.ToString();
    }
}