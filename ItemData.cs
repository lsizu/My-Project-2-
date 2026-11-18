using UnityEngine;
[CreateAssetMenu(fileName = "NewItemData", menuName = "Game/Item Data")]
public class ItemData : ScriptableObject
{
    public string itemId;
    public string itemName;
    [TextArea] public string description;
    public Sprite icon;
    [Min(0)] public int purchasePrice;
    public string[] effects;
    [Tooltip("各効果の数値です。Star効果の場合は変更先の★ (1～3) を指定します。")]
    public int[] effectValues;
    public string target;
    public string duration;
}