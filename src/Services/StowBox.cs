using ScHauler.Models;

namespace ScHauler.Services;

/// <summary>
/// A box to plan, by id and size only - what the planner needs, without a tracked entity, so a fit check can be a dry run.
/// </summary>
public readonly record struct StowBox(CargoContainerId Id, ContainerSize Size);
