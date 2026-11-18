using UnityEngine;

[CreateAssetMenu(fileName = "NewCharacter", menuName = "Game/Character Data")]
public class CharacterData : ScriptableObject
{
    [Header("Character")]
    public string characterName;
    public Sprite icon;

    [Header("Cost")]
    public int cost = 1;

    [Header("Attributes")]
    public string[] attributes;

    [Header("Units")]
    public string[] units;
}