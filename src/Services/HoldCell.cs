using ScHauler.Models;

namespace ScHauler.Services;

/// <summary>
/// One 1-SCU cell of the hold, addressed like a <see cref="Placement"/> corner.
/// </summary>
public readonly record struct HoldCell(ShipBayId BayId, int X, int Y, int Z);
