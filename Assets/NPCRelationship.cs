using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NPCRelationship : MonoBehaviour
{
    [Range(-100, 100)] public float trust;
    [Range(-100, 100)] public float respect;

    public void ModifyTrust(float value)
    {
        trust = Mathf.Clamp(trust + value, -100, 100);
    }
    public void ModifyRespect(float value)
    {
        respect = Mathf.Clamp(respect + value, -100, 100);
    }
}
