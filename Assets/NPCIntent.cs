using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum NPCIntent
{
    Cooperate,
    Deceive,
    Threaten,
    Avoid,
    Neutral
}

[System.Serializable]
public class IntentWeight
{
    public NPCIntent intent;
    public float baseWeight;
}
