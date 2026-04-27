using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public class NPCInteractionReceiver : MonoBehaviour
{
    [Header("Components")]
    public NPCEmotionalState emotions;
    public NPCRelationship relationship;
    public NPCMemory memory;
    public NPCDecisionSystem decisionSystem;

    [Header("Story")]
    public ScenePremise scenePremise;
    public string npcId;
    public string fullName;
    private bool hasInteracted = false;

    private void Awake()
    {
        // Automatically gather components if not manually assigned
        if (emotions == null) emotions = GetComponent<NPCEmotionalState>();
        if (relationship == null) relationship = GetComponent<NPCRelationship>();
        if (memory == null) memory = GetComponent<NPCMemory>();
        if (decisionSystem == null) decisionSystem = GetComponent<NPCDecisionSystem>();

        // Ensure NPCDecisionSystem has references
        if (decisionSystem != null)
        {
            decisionSystem.emotions = emotions;
            decisionSystem.relationship = relationship;
            decisionSystem.memory = memory;
            decisionSystem.personality = GetComponent<NPCPersonality>();
        }
    }

    public async Task<DialogueGenerator.DialogueResponse> ReceivePlayerActionAsync(PlayerAction action, string contextTag, string fullText)
    {
        // Apply mechanical effects
        ApplyActionEffects(action, contextTag, fullText);

        decisionSystem.RecalculateIntent();

        // Determine if first interaction
        bool firstInteraction = !hasInteracted;
        if (firstInteraction)
            hasInteracted = true;

        // Call AI for dialogue
        var response = await DialogueGenerator.GenerateNPCDialogue(
            this,
            action,
            scenePremise,
            recentMemoryCount: 6,
            npcId: this.npcId,
            isFirstInteraction: firstInteraction // NEW: tells API this is first interaction
        );

        if (response.suggestAddMemory)
            memory.AddMemory(action, contextTag, fullText);

        return response;
    }

    private void ApplyActionEffects(PlayerAction action, string contextTag, string fullText)
    {
        switch (action)
        {
            case PlayerAction.Calm:
                relationship.ModifyTrust(+20);
                emotions.ModifyStress(-10);
                memory.AddMemory(action, contextTag, fullText, 1f);
                break;

            case PlayerAction.Threaten:
                relationship.ModifyTrust(-30);
                emotions.ModifyStress(+20);
                emotions.ModifyFear(10f);
                memory.AddMemory(action, contextTag, fullText, -1f);
                break;

            case PlayerAction.Lie:
                relationship.ModifyTrust(-15);
                emotions.ModifyStress(+5);
                memory.AddMemory(action, contextTag, fullText, -0.5f);
                break;

            case PlayerAction.Persuade:
                relationship.ModifyTrust(+10);
                emotions.ModifyStress(-5);
                memory.AddMemory(action, contextTag, fullText, 0.5f);
                break;

            case PlayerAction.Ask:
                memory.AddMemory(action, contextTag, fullText, 0f);
                break;

            case PlayerAction.Observe:
                emotions.ModifyStress(+2);
                memory.AddMemory(action, contextTag, fullText, 0f);
                break;
        }
    }

    //// Synchronous wrapper when you only need current available actions
    //public List<PlayerAction> GetAvailableActions()
    //{
    //    List<PlayerAction> actions = new();

    //    // Always available
    //    actions.Add(PlayerAction.Talk);
    //    actions.Add(PlayerAction.Observe);

    //    // Trust-based actions
    //    if (relationship.trust > 30)
    //        actions.Add(PlayerAction.Ask);

    //    if (relationship.trust < 20)
    //        actions.Add(PlayerAction.Lie); // lying only meaningful if trust is low

    //    // Stress/aggression check for Threaten
    //    if (emotions.stress < 80)
    //        actions.Add(PlayerAction.Threaten);

    //    // Persuade: only if NPC is not fully cooperative
    //    if (decisionSystem.CurrentIntent != NPCIntent.Cooperate)
    //        actions.Add(PlayerAction.Persuade);

    //    // Always allow leaving
    //    actions.Add(PlayerAction.Leave);

    //    return actions;
    //}

    //public string GetCurrentDialogue()
    //{
    //    switch (decisionSystem.CurrentIntent)
    //    {
    //        case NPCIntent.Cooperate:
    //            return "Hello, friend! How can I help you?";
    //        case NPCIntent.Avoid:
    //            return "I don't want trouble...";
    //        case NPCIntent.Threaten:
    //            return "Back off!";
    //        default:
    //            return "Hmm?";
    //    }
    //}
}