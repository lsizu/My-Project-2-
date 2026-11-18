using UnityEngine;

[CreateAssetMenu(fileName = "NewEnemy", menuName = "Game/Enemy Data")]
public class EnemyData : ScriptableObject
{
    [Header("Enemy")]
    public string enemyId;
    public string enemyName;
    public Sprite icon;

    [Header("Stats")]
    public int attack;
    public int defense;
    public int health;
    [Header("Enemy Effects")]
    [Tooltip("EffectData.effectId values. Multiple IDs can be comma-separated.")]
    public string cip;
    [Tooltip("EffectData.effectId values. Multiple IDs can be comma-separated.")]
    public string ae;
    [Tooltip("EffectData.effectId values. Multiple IDs can be comma-separated.")]
    public string pig;
    public string target;

    [Header("Appears On Stages")]
    public int[] stages;

    [Header("Categories")]
    public string[] categories;
}
