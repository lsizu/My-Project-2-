using UnityEngine;

[CreateAssetMenu(fileName = "NewEncounterPool", menuName = "Game/Encounter Pool Data")]
public class EncounterPoolData : ScriptableObject
{
    public string encounterId;
    public int[] roundIndices;
    public string category;
    public EnemyFormationData[] formations;

    public bool ContainsRound(int roundIndex)
    {
        if (roundIndices == null)
            return false;

        foreach (int configuredRound in roundIndices)
        {
            if (configuredRound == roundIndex)
                return true;
        }

        return false;
    }
}
