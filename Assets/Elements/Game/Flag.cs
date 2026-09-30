using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;


// Controls flagging of cells
public class Flag : MonoBehaviour, IPointerClickHandler
{
    public int cellIndex;
    private bool _isFlagged;
    [SerializeField] private Sprite untouchedSprite;
    [SerializeField] private Sprite flaggedSprite;
    

    // Changes the icon based on player's playable state and cell's revealed state
    private void ToggleFlag()
    {
        if (SessionManager.Singleton.cells.Count > cellIndex && SessionManager.Singleton.cells[cellIndex].RevealedBy > 0) return;
        if (SessionManager.Players[SessionManager.LocalPlayer.ClientId].PlayableState != PlayableStates.Playable) return;
        _isFlagged = !_isFlagged;
        transform.GetComponent<Image>().sprite = _isFlagged ? flaggedSprite : untouchedSprite;
    }
    
    
    // Callback for when a button was clicked
    public void OnPointerClick(PointerEventData eventData)
    {
        
        
        // Toggle flag state
        if (eventData.button == PointerEventData.InputButton.Right)
            ToggleFlag();
        
    }
    
    
    // Run as soon as the script is created
    private void Awake()
    {
        _isFlagged = false;
    }
}
