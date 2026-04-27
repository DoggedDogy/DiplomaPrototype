using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class SceneManager : MonoBehaviour
{
    [Header("Scene Settings")]
    public ScenePremise currentScenePremise;

    private void Awake()
    {
        if (currentScenePremise == null)
        {
            Debug.LogError("ScenePremise not assigned in SceneManager!");
            return;
        }

        AssignPremiseToNPCs();
    }

    /// <summary>
    /// Finds all NPCInteractionReceiver in the scene and assigns the current ScenePremise.
    /// </summary>
    private void AssignPremiseToNPCs()
    {
        NPCInteractionReceiver[] npcs = FindObjectsOfType<NPCInteractionReceiver>();

        foreach (var npc in npcs)
        {
            npc.scenePremise = currentScenePremise;

            npc.fullName = currentScenePremise.people.Where(x => x.id.Trim() == npc.npcId.Trim()).Select(x => x.displayName).FirstOrDefault();
        }

        Debug.Log($"Assigned ScenePremise '{currentScenePremise.name}' to {npcs.Length} NPC(s).");
    }
}
