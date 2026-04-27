using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NPCBehaviorController : MonoBehaviour
{
    public NPCDecisionSystem decision;
    public Transform player;

    private void Start()
    {
        decision = GetComponent<NPCDecisionSystem>();
        player = GetComponent<Transform>();
    }

    void Update()
    {
        //switch (decision.CurrentIntent)
        //{
        //    case NPCIntent.Cooperate:
        //        transform.LookAt(player);
        //        break;

        //    case NPCIntent.Avoid:
        //        transform.Translate(Vector3.back * Time.deltaTime);
        //        break;
        //}
    }
}
