using UnityEngine;

/// <summary>
/// Displays complete Units represented by at least two characters on the roster board.
/// </summary>
public class RosterUnitListDisplay : RosterSlotTagListDisplay
{
    protected override bool IsUnitList => true;
    protected override string TemplateObjectName => "RosterUnitButton";
    protected override string NameTextObjectName => "RosterUnitNameText";
    protected override string CountTextObjectName => "RosterUnitCountText";
}
