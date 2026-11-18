using UnityEngine;

[CreateAssetMenu(fileName = "NewTargetData", menuName = "Game/Target Data")]
public class TargetData : ScriptableObject
{
    public string targetText;
    public string targetId;
    public string candidateFilter;
    public string selectionMethod;

    [HideInInspector] public int selectSlot;
    [HideInInspector] public int noTarget;
    [HideInInspector] public int selectCharacter;
}
