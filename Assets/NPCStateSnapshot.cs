using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class NPCStateSnapshot2
{
    public string name;
    public float aggression;
    public float trustfulness;
    public float greed;
    public float stress;
    public float fear;
    public Dictionary<PlayerAction, float> memoryInfluence; // simplified
}
