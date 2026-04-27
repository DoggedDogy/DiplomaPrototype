using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class NPCDecisionSystem : MonoBehaviour
{
    public NPCPersonality personality;
    public NPCEmotionalState emotions;
    public NPCRelationship relationship;
    public NPCMemory memory;
    public NPCIntent CurrentIntent { get; private set; }

    private void Start()
    {
        personality = GetComponent<NPCPersonality>();
        emotions = GetComponent<NPCEmotionalState>();
        relationship = GetComponent<NPCRelationship>();
        memory = GetComponent<NPCMemory>();
    }

    public void RecalculateIntent()
    {
        Dictionary<NPCIntent, float> weights = new();

        // Cooperate: more likely if trust is high, stress/fear low, helped/persuaded positively
        weights[NPCIntent.Cooperate] =
            10f +
            relationship.trust * personality.trustfulness * 0.8f -
            emotions.stress * 0.3f -
            emotions.fear * 0.3f +
            memory.GetMemoryInfluence(PlayerAction.Calm) * 1.2f +
            memory.GetMemoryInfluence(PlayerAction.Persuade) * 0.8f;

        // Avoid: more likely if fear/stress high, low trust, repeated annoying actions, or suspicious behavior
        weights[NPCIntent.Avoid] =
            emotions.fear * 0.7f +
            emotions.stress * 0.4f -
            relationship.trust * personality.trustfulness +
            memory.GetMemoryInfluence(PlayerAction.Threaten) * 1.2f +
            memory.GetMemoryInfluence(PlayerAction.Observe) * 0.5f +
            memory.GetMemoryInfluence(PlayerAction.Ask) * 0.3f; // repeated questions can annoy NPC

        // Threaten: Aggression + stress + hostile actions
        weights[NPCIntent.Threaten] =
            personality.aggression * (100 - relationship.respect) * 0.8f +
            emotions.stress * 0.5f +
            memory.GetMemoryInfluence(PlayerAction.Threaten) * 1.2f +
            memory.GetMemoryInfluence(PlayerAction.Lie) * 0.8f +
            memory.GetMemoryInfluence(PlayerAction.Observe) * 0.2f; // suspicious observation can trigger hostility

        // Deceive: Low trust, high greed, lied before
        weights[NPCIntent.Deceive] =
            (1 - personality.trustfulness) * 50f +
            personality.greed * 20f +
            memory.GetMemoryInfluence(PlayerAction.Lie) * 1.5f;

        // Neutral: fallback/default; slightly affected by all factors
        weights[NPCIntent.Neutral] =
            5f +
            (100 - emotions.stress) * 0.1f +
            relationship.trust * 0.05f -
            emotions.fear * 0.05f +
            memory.GetMemoryInfluence(PlayerAction.Ask) * 0.1f; // if player keeps asking neutral questions

        // Select intent with highest weight
        CurrentIntent = GetMaxWeightIntent(weights);
    }

    private NPCIntent GetMaxWeightIntent(Dictionary<NPCIntent, float> weight)
    {
        return weight.Aggregate((a, b) => a.Value > b.Value ? a : b).Key;
    }
}
