using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InteractionUI : MonoBehaviour
{
    public static InteractionUI Instance;

    [Header("UI Panels")]
    public GameObject panel; // main interaction panel
    public TMP_Text npcText; // npc dialogue
    public TMP_Text npcNameText; // full name
    public Transform playerOptionsParent; // container for option buttons
    public Button optionButtonPrefab; // prefab for options

    private NPCInteractionReceiver currentNPC;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);

        panel.SetActive(false);
    }

    /// <summary>
    /// Opens the interaction UI with the NPC and starts the dialogue loop.
    /// </summary>
    public async Task OpenAsync(NPCInteractionReceiver npc, PlayerAction initialAction)
    {
        currentNPC = npc;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        panel.SetActive(true);
        ClearButtons();
        npcText.text = "*awaiting reply*";
        npcNameText.text = npc.fullName;
        await ProcessPlayerAction(initialAction, "initial", "greet the NPC"); // empty fullText at start
    }

    private void ClearButtons()
    {
        foreach (Transform t in playerOptionsParent)
            Destroy(t.gameObject);
    }

    /// <summary>
    /// Processes a player action, requests NPC dialogue, and populates follow-up buttons.
    /// </summary>
    private async Task ProcessPlayerAction(PlayerAction action, string contextTag, string fullText)
    {
        if (currentNPC == null) return;

        // Ask NPC for dialogue + followups
        var response = await currentNPC.ReceivePlayerActionAsync(action, contextTag, fullText);

        // Display NPC reply
        npcText.text = response.npcReply;

        // Clear existing buttons
        ClearButtons();

        // Create buttons for follow-ups
        foreach (var followUp in response.followUps)
        {
            if (!followUp.applicable) continue;

            Button btn = Instantiate(optionButtonPrefab, playerOptionsParent);
            btn.GetComponentInChildren<TMP_Text>().text = followUp.actionText;

            // Capture local variable for lambda
            var nextAction = followUp.action;
            var nextTag = string.IsNullOrEmpty(followUp.suggestedMemoryTag) ? followUp.action.ToString() : followUp.suggestedMemoryTag;
            var nextFullText = followUp.actionText;

            btn.onClick.AddListener(async () =>
            {
                await ProcessPlayerAction(nextAction, nextTag, nextFullText);
            });
        }

        // Always allow leaving
        Button leaveBtn = Instantiate(optionButtonPrefab, playerOptionsParent);
        leaveBtn.GetComponentInChildren<TMP_Text>().text = "Leave";
        leaveBtn.onClick.AddListener(() =>
        {
            panel.SetActive(false);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            ClearButtons();
        });
    }
}
