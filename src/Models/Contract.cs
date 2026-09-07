namespace ScHauler.Models;

public sealed class Contract
{
  private readonly List<CargoLine> _lines = [];

  private Contract(ContractId id, string name, int rewardAuec, DateTimeOffset acceptedAt)
  {
    Id = id;
    Name = name;
    RewardAuec = rewardAuec;
    AcceptedAt = acceptedAt;
  }

  public ContractId Id { get; }

  public string Name { get; }

  public int RewardAuec { get; }

  public DateTimeOffset AcceptedAt { get; }

  public IReadOnlyCollection<CargoLine> Lines => _lines.AsReadOnly();

  public bool IsCompleted => Lines.Count > 0 && Lines.All(l => l.Status == CargoLineStatus.Delivered);

  public static Contract Create(string name, int rewardAuec, DateTimeOffset acceptedAt)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(name);
    ArgumentOutOfRangeException.ThrowIfNegative(rewardAuec);

    return new Contract(ContractId.New(), name.Trim(), rewardAuec, acceptedAt);
  }

  public CargoLine AddLine(string commodity, Scu scu, Location pickup, Location dropOff)
  {
    var line = CargoLine.Create(this, commodity, scu, pickup, dropOff);
    _lines.Add(line);
    return line;
  }
}
