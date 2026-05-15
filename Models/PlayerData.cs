namespace Keys.Models;

public class PlayerData
{
  public ulong SteamId { get; set; }
  public string CharacterName { get; set; }
  public int GuidHash { get; set; }
  public int PrefabGuidHash { get; set; } = 0;
  public int LastControlledPlayerGuidHash { get; set; } = 0;
  public List<ClanKeyData> ClanKeys { get; set; } = new List<ClanKeyData>();
}

public class ClanKeyData
{
  public string ClanName { get; set; }
  public string ClanGuid { get; set; }
  public bool IsOwnerKey { get; set; }
  public bool CanIgnoreClanLimit { get; set; }
  public DateTime IssuedTime { get; set; }
}
