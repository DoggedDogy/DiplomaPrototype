using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NPCEmotionalState : MonoBehaviour
{
    [Range(0, 100)] public float stress;
    [Range(0, 100)] public float fear;

    public void ModifyStress(float value)
    {
        stress = Mathf.Clamp(stress + value, 0, 100);
    }

    public void ModifyFear(float value)
    {
        fear = Mathf.Clamp(fear + value, 0, 100);
    }
}