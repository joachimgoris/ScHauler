namespace ScHauler.Models;

/// <summary>
/// Where a box sits: a bay, the cell of its ramp-side/port-side corner (X along the bay length, Y across, Z = stack level, all 0-based), and whether it is rotated 90 degrees (length and width swap; height never rotates).
/// </summary>
public readonly record struct Placement(ShipBayId BayId, int X, int Y, int Z, bool Rotated);