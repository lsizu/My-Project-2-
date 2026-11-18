using UnityEngine;

[CreateAssetMenu(fileName = "NewEnemyFormation", menuName = "Game/Enemy Formation Data")]
public class EnemyFormationData : ScriptableObject
{
    [System.Serializable]
    public class FormationSlot
    {
        public EnemyData enemy;
        [Min(0)] public int gap;
        [Min(0)] public int cooldown;
    }

    public string formationId;
    public FormationSlot[] slots = new FormationSlot[6];
}
