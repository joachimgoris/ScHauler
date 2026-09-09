using ScHauler.Models;

namespace ScHauler.Services;

/// <summary>
/// Pure hop-distance over the location hierarchy: same location 0, same body 1, same system 2, otherwise (or unknown) 3. Built once from the flat location list; no navigation required.
/// </summary>
public sealed class RouteNet
{
    private readonly Dictionary<LocationId, LocationId?> _parents;
    private RouteNet(Dictionary<LocationId, LocationId?> parents) => _parents = parents;

    public static RouteNet From(IEnumerable<Location> locations)
    {
        ArgumentNullException.ThrowIfNull(locations);
        return new RouteNet(locations.ToDictionary(l => l.Id, l => l.ParentId));
    }

    public int Hops(LocationId from, LocationId to)
    {
        if (from == to)
        {
            return 0;
        }

        var fromParent = Parent(from);
        var toParent = Parent(to);
        if (fromParent is { } fp && fp == toParent)
        {
            return 1;
        }

        var fromRoot = Root(from);
        var toRoot = Root(to);
        return fromRoot is { } fr && fr == toRoot ? 2 : 3;
    }

    private LocationId? Parent(LocationId id) => _parents.GetValueOrDefault(id);

    private LocationId? Root(LocationId id)
    {
        var current = id;
        for (int depth = 0; depth < 3; depth++)
        {
            var parent = Parent(current);
            if (parent is null)
            {
                return current;
            }

            current = parent.Value;
        }

        return current;
    }
}