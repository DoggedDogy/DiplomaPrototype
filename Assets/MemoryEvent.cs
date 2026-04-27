using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class MemoryEvent
{
    public PlayerAction action;

    // Short label describing situation ("interrogation", "crime_scene", "trade", etc.)
    public string contextTag;

    // Full text of what was said / shown in the UI (for exact reference)
    public string fullText;

    // How the NPC perceived it (-1 hostile… 0 neutral… +1 positive)
    public float influence;

    // Timestamp for decay
    public float time;
}
