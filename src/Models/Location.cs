namespace ScHauler.Models;

public sealed class Location
{
    private Location(LocationId id, string name)
    {
        Id = id;
        Name = name;
    }

    public LocationId Id { get; }

    public string Name { get; }

    public static Location Create(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new Location(LocationId.New(), name.Trim());
    }
}