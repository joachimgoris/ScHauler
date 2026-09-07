namespace ScHauler.Services;

public static class Commodities
{
    public static IReadOnlyList<string> All { get; } =
     [
        "Agricultural Supplies", "Aluminum", "Carbon", "Construction Materials",
        "Copper", "Corundum", "Hydrogen Fuel", "Iron", "Medical Supplies",
        "Pressurized Ice", "Processed Food", "Quantum Fuel", "Quartz",
        "Recycled Material Composite", "Scrap", "Ship Ammunition", "Silicon",
        "Stims", "Tin", "Titanium", "Tungsten", "Waste",
    ];
}