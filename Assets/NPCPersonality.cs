using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NPCPersonality : MonoBehaviour
{
    [Range(0, 1)] public float aggression;
    [Range(0, 1)] public float trustfulness;
    [Range(0, 1)] public float greed;
}
