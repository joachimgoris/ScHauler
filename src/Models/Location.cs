namespace ScHauler.Models;

public sealed class Location
{
    private Location(LocationId id, string name, LocationKind kind, LocationId? parentId)
    {
        Id = id;
        Name = name;
        Kind = kind;
        ParentId = parentId;
    }

    public LocationId Id { get; }

    public string Name { get; }

    public LocationKind Kind { get; }

    /// <summary>
    /// Site -> Body -> System, strictly three levels (a moon is a Body under its system). Null = unknown parent; route distance treats it as far.
    /// </summary>
    public LocationId? ParentId { get; }

    public static Location Create(string name, LocationKind kind, Location? parent)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return kind switch
        {
            LocationKind.System when parent is not null => throw new ArgumentException("A system has no parent.", nameof(parent)),
            LocationKind.Body when parent is null || parent.Kind != LocationKind.System => throw new ArgumentException("A body's parent must be a system.", nameof(parent)),
            LocationKind.Site when parent is null || parent.Kind == LocationKind.Site => throw new ArgumentException("A site cannot parent another site.", nameof(parent)),
            _ => new Location(LocationId.New(), name.Trim(), kind, parent?.Id),
        };
    }
}