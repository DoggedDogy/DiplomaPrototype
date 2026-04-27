using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class NPCMemory : MonoBehaviour
{
    public List<MemoryEvent> events = new();

    // AddMemory overload that accepts fullText and a context tag
    public void AddMemory(PlayerAction action, string contextTag, string fullText, float value = 1f)
    {
        MemoryEvent e = new MemoryEvent
        {
            action = action,
            contextTag = contextTag,
            fullText = fullText,
            influence = value,
            time = Time.time
        };

        events.Add(e);
    }

    // existing helper, optional contextTag
    public float GetMemoryInfluence(PlayerAction action, string contextTag = null)
    {
        float result = 0f;

        foreach (var e in events)
        {
            // decay over time (older = weaker) — simple linear decay; tune later
            float age = Time.time - e.time;
            float decayedInfluence = e.influence * Mathf.Max(0f, 1f - age / 600f); // full decay after 10 minutes, tweakable

            if (e.action == action)
            {
                result += decayedInfluence;

                if (!string.IsNullOrEmpty(contextTag) && e.contextTag == contextTag)
                    result += 0.5f;  // stronger if same situation
            }
        }

        return result;
    }

    // Return last n memory events (most recent first)
    public List<MemoryEvent> GetRecentEvents(int n = 5)
    {
        return events
            .OrderByDescending(e => e.time)
            .Take(n)
            .ToList();
    }
}
