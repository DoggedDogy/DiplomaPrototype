using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ScenePremise", menuName = "Detective/ScenePremise")]
public class ScenePremise : ScriptableObject
{
    [TextArea(3, 8)]
    public string sceneSummary; // short paragraph: what happened before entering scene

    public string locationName;

    [System.Serializable]
    public class PersonEntry
    {
        public string id;              // unique id used by AI (e.g., "alex_street_vendor")
        public string displayName;     // user-facing name
        public string role;            // "culprit", "witness", "victim", "bystander"
        [TextArea(1, 4)]
        public string shortDescription; // 1-2 lines describing appearance/attitude
    }

    public List<PersonEntry> people = new List<PersonEntry>();
}
