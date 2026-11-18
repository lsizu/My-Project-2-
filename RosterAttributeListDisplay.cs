using UnityEngine;

/// <summary>
/// Displays Attributes represented by at least two characters on the roster board.
/// </summary>
public class RosterAttributeListDisplay : RosterSlotTagListDisplay
{
    protected override bool IsUnitList => false;
    protected override string TemplateObjectName => "RosterAttributeButton";
    protected override string NameTextObjectName => "RosterAttributeNameText";
    protected override string CountTextObjectName => "RosterAttributeCountText";
}
