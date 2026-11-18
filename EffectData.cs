using UnityEngine;

[CreateAssetMenu(fileName = "NewEffectData", menuName = "Game/Effect Data")]
public class EffectData : ScriptableObject
{
    public string effectId;
    [TextArea] public string description;
    [Tooltip("Supported values: Attack, Def, Gap, Gold, Damage, Block.")]
    public string effect;
    public int effectValue;
    [Tooltip("Supported values: 攻撃者, 攻撃対象, プレイヤー.")]
    public string target;
    [Tooltip("Supported values: このバトル中, 即時効果.")]
    public string duration;
}
