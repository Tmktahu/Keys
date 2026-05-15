using ProjectM;
using ProjectM.Network;
using Unity.Entities;
using Keys.Models;
using VampireCommandFramework;
using Unity.Collections;

namespace Keys.Services;

internal static class KeyManagementService
{
  private static Dictionary<string, ClanCacheEntry> _clanCache = new();

  private class ClanCacheEntry
  {
    public string ClanGuid { get; set; }
    public string ClanName { get; set; }
    public Dictionary<int, ClanKeyData> KeysByPlayer { get; set; } = new();
  }

  private static Entity GetClanEntity(string identifier, ChatCommandContext ctx)
  {
    var clans = EntityService.GetEntitiesByComponentType<ClanTeam>();
    Entity targetClan = Entity.Null;
    List<string> matchingGuids = new();

    foreach (var clan in clans)
    {
      var clanTeam = clan.Read<ClanTeam>();
      var clanName = clanTeam.Name.ToString();
      var clanGuid = clanTeam.ClanGuid.ToString();

      if (clanGuid.Equals(identifier, StringComparison.OrdinalIgnoreCase))
      {
        targetClan = clan;
        break;
      }

      if (clanName.Equals(identifier, StringComparison.OrdinalIgnoreCase))
      {
        matchingGuids.Add(clanGuid);
      }
    }

    if (targetClan.Equals(Entity.Null) && matchingGuids.Count > 0)
    {
      var errorMsg = $"Ambiguous clan name: '{identifier}' matches {matchingGuids.Count} clans with GUIDs:\n";
      errorMsg += string.Join("\n", matchingGuids.Select((guid, i) => $"  {i + 1}. {guid}"));
      ctx.Reply(errorMsg);
      return Entity.Null;
    }

    if (targetClan.Equals(Entity.Null))
    {
      var error = $"Clan '{identifier}' not found.";
      ctx.Reply(error);
      return Entity.Null;
    }

    return targetClan;
  }

  internal static string GetClanGuidFromName(string clanName, ChatCommandContext ctx)
  {
    var clanEntity = GetClanEntity(clanName, ctx);
    if (clanEntity.Equals(Entity.Null))
    {
      return null;
    }

    var clanTeam = clanEntity.Read<ClanTeam>();
    return clanTeam.ClanGuid.ToString();
  }

  public static void Initialize()
  {
    Core.Log.LogInfo("Initializing Key Management Service...");
    BuildCache();
  }

  private static void BuildCache()
  {
    _clanCache.Clear();
    var allPlayers = PlayerDataService.GetAllPlayerData();
    int totalKeys = 0;
    int totalPlayersWithKeys = 0;
    int disposedOldKeys = 0;
    bool dataModified = false;

    foreach (var playerData in allPlayers)
    {
      int playerKeyCount = 0;
      List<ClanKeyData> keysToRemove = new();

      foreach (var key in playerData.ClanKeys)
      {
        if (string.IsNullOrWhiteSpace(key.ClanGuid) || string.IsNullOrWhiteSpace(key.ClanName))
        {
          keysToRemove.Add(key);
          disposedOldKeys++;
          continue;
        }

        if (!_clanCache.ContainsKey(key.ClanGuid))
        {
          _clanCache[key.ClanGuid] = new ClanCacheEntry
          {
            ClanGuid = key.ClanGuid,
            ClanName = key.ClanName
          };
        }

        var entry = _clanCache[key.ClanGuid];
        entry.KeysByPlayer[playerData.GuidHash] = key;
        totalKeys++;
        playerKeyCount++;
      }

      if (keysToRemove.Count > 0)
      {
        keysToRemove.ForEach(key => playerData.ClanKeys.Remove(key));
        dataModified = true;
      }

      if (playerKeyCount > 0)
        totalPlayersWithKeys++;
    }

    if (disposedOldKeys > 0)
    {
      Core.Log.LogWarning($"Disposed {disposedOldKeys} old keys without ClanGuid");
    }

    if (dataModified)
    {
      PlayerDataService.SaveData();
    }

    Core.Log.LogInfo($"Key Cache Built: {_clanCache.Count} clans registered, {totalKeys} total keys across {totalPlayersWithKeys} players");
  }

  private static void EnsureCache()
  {
    if (_clanCache.Count == 0)
    {
      Core.Log.LogInfo("Key cache empty, rebuilding...");
      BuildCache();
    }
  }

  private static void RefreshCache()
  {
    BuildCache();
  }

  public static PlayerData GetPlayerData(User user)
  {
    return PlayerDataService.GetPlayerData(user.LocalCharacter.GetEntityOnServer());
  }

  public static bool IsClanRegistered(string clanGuid)
  {
    EnsureCache();
    return _clanCache.Keys.Any(k => k.Equals(clanGuid, StringComparison.OrdinalIgnoreCase));
  }

  public static void RegisterClan(ChatCommandContext ctx)
  {
    var userEntity = ctx.Event.SenderUserEntity;
    var user = userEntity.Read<User>();

    if (user.ClanEntity.Equals(NetworkedEntity.Empty))
    {
      ctx.Reply("You are not in a clan.");
      return;
    }

    var clanEntity = user.ClanEntity.GetEntityOnServer();
    var clanTeam = clanEntity.Read<ClanTeam>();
    var clanName = clanTeam.Name.ToString();
    var clanGuid = clanTeam.ClanGuid.ToString();

    if (IsClanRegistered(clanGuid))
    {
      ctx.Reply($"Clan '{clanName}' is already registered with the keys system.");
      return;
    }

    var members = Core.EntityManager.GetBuffer<ClanMemberStatus>(clanEntity);
    var userBuffer = Core.EntityManager.GetBuffer<SyncToUserBuffer>(clanEntity);

    for (var i = 0; i < members.Length; ++i)
    {
      var member = members[i];
      if (member.ClanRole != ClanRoleEnum.Leader) continue;

      var userBufferEntry = userBuffer[i];
      if (userBufferEntry.UserEntity.Equals(userEntity))
      {
        var playerData = GetPlayerData(user);
        playerData.ClanKeys.Add(new ClanKeyData
        {
          ClanName = clanName,
          ClanGuid = clanGuid,
          IsOwnerKey = true,
          CanIgnoreClanLimit = false,
          IssuedTime = DateTime.Now
        });
        PlayerDataService.SaveData();
        RefreshCache();

        ctx.Reply($"Clan '{clanName}' registered! You are the owner. Use .keys give <player> to issue keys.");
        return;
      }
    }

    ctx.Reply("You must be a clan leader to register the clan.");
  }

  public static void TransferOwnership(ChatCommandContext ctx, Entity newUserEntity, Entity? originalOwnerEntity = null)
  {
    var isAdminOp = originalOwnerEntity.HasValue && !originalOwnerEntity.Value.Equals(Entity.Null);
    var userEntity = isAdminOp ? originalOwnerEntity.Value : ctx.Event.SenderUserEntity;
    var user = userEntity.Read<User>();
    var personalPronoun = isAdminOp ? "The original owner" : "You";
    var possessivePronoun = isAdminOp ? "the original owner's" : "your";

    if (user.ClanEntity.Equals(NetworkedEntity.Empty))
    {
      ctx.Reply($"{personalPronoun} are not in a clan.");
      return;
    }

    var clanEntity = user.ClanEntity.GetEntityOnServer();
    var clanTeam = clanEntity.Read<ClanTeam>();
    var clanName = clanTeam.Name.ToString();
    var clanGuid = clanTeam.ClanGuid.ToString();

    if (!IsClanRegistered(clanGuid))
    {
      ctx.Reply($"Clan '{clanName}' is not registered with the keys system. Use .keys register.");
      return;
    }

    var currentPlayerData = GetPlayerData(user);
    if (!currentPlayerData.ClanKeys.Any(k => k.ClanGuid.Equals(clanGuid, StringComparison.OrdinalIgnoreCase) && k.IsOwnerKey))
    {
      ctx.Reply($"{personalPronoun} are not the clan owner.");
      return;
    }

    if (newUserEntity.Equals(Entity.Null))
    {
      ctx.Reply("Target player not found.");
      return;
    }

    var newOwnerUser = newUserEntity.Read<User>();
    var newOwnerCharacterName = newOwnerUser.CharacterName.ToString();

    if (newOwnerUser.ClanEntity.Equals(NetworkedEntity.Empty))
    {
      ctx.Reply($"{newOwnerCharacterName} is not in a clan.");
      return;
    }

    var newOwnerClanEntity = newOwnerUser.ClanEntity.GetEntityOnServer();
    if (!newOwnerClanEntity.Equals(clanEntity))
    {
      ctx.Reply($"{newOwnerCharacterName} is not in {possessivePronoun} clan.");
      return;
    }

    var newOwnerPlayerData = GetPlayerData(newOwnerUser);

    var existingOwnerKey = currentPlayerData.ClanKeys.FirstOrDefault(k => k.ClanGuid.Equals(clanGuid, StringComparison.OrdinalIgnoreCase) && k.IsOwnerKey);
    if (existingOwnerKey != null)
    {
      currentPlayerData.ClanKeys.Remove(existingOwnerKey);
    }

    currentPlayerData.ClanKeys.Add(new ClanKeyData
    {
      ClanName = clanName,
      ClanGuid = clanGuid,
      IsOwnerKey = false,
      CanIgnoreClanLimit = false,
      IssuedTime = DateTime.Now
    });

    var newOwnerKey = newOwnerPlayerData.ClanKeys.FirstOrDefault(k => k.ClanGuid.Equals(clanGuid, StringComparison.OrdinalIgnoreCase));
    if (newOwnerKey != null)
    {
      newOwnerKey.IsOwnerKey = true;
    }
    else
    {
      newOwnerPlayerData.ClanKeys.Add(new ClanKeyData
      {
        ClanName = clanName,
        ClanGuid = clanGuid,
        IsOwnerKey = true,
        CanIgnoreClanLimit = false,
        IssuedTime = DateTime.Now
      });
    }

    PlayerDataService.SaveData();
    RefreshCache();

    ctx.Reply($"Ownership of '{clanName}' transferred to {newOwnerCharacterName}.");
  }

  public static bool CanIssueKeys(Entity userEntity, string clanGuid)
  {
    var user = userEntity.Read<User>();
    var playerData = GetPlayerData(user);
    return playerData.ClanKeys.Any(k => k.ClanGuid.Equals(clanGuid, StringComparison.OrdinalIgnoreCase) && k.IsOwnerKey);
  }

  public static void IssueKey(ChatCommandContext ctx, Entity senderUserEntity, Entity targetUserEntity, string clanName = null, bool canIgnoreLimit = false, bool isAdmin = false, bool forceOwnershipValidation = true)
  {
    string clanGuid = null;
    string effectiveClanName = clanName;

    if (string.IsNullOrEmpty(clanName))
    {
      var senderUser = senderUserEntity.Read<User>();

      if (senderUser.ClanEntity.Equals(NetworkedEntity.Empty))
      {
        ctx.Reply("You are not in a clan.");
        return;
      }

      var clanEntity = senderUser.ClanEntity.GetEntityOnServer();
      var clanTeam = clanEntity.Read<ClanTeam>();
      effectiveClanName = clanTeam.Name.ToString();
      clanGuid = clanTeam.ClanGuid.ToString();
    }
    else
    {
      var clanEntity = GetClanEntity(clanName, ctx);
      if (clanEntity.Equals(Entity.Null))
      {
        return;
      }

      var clanTeam = clanEntity.Read<ClanTeam>();
      effectiveClanName = clanTeam.Name.ToString();
      clanGuid = clanTeam.ClanGuid.ToString();
    }

    if (!IsClanRegistered(clanGuid))
    {
      ctx.Reply($"Clan '{effectiveClanName}' is not registered with the keys system. Use .keys register first.");
      return;
    }

    if (!isAdmin && forceOwnershipValidation)
    {
      if (!CanIssueKeys(senderUserEntity, clanGuid))
      {
        ctx.Reply("Only the clan owner can issue keys.");
        return;
      }
    }

    if (targetUserEntity.Equals(Entity.Null))
    {
      ctx.Reply("Target player not found.");
      return;
    }

    var targetUser = targetUserEntity.Read<User>();
    var targetPlayerData = GetPlayerData(targetUser);

    var existingKey = targetPlayerData.ClanKeys.FirstOrDefault(k => k.ClanGuid.Equals(clanGuid, StringComparison.OrdinalIgnoreCase));
    if (existingKey != null)
    {
      existingKey.CanIgnoreClanLimit = existingKey.CanIgnoreClanLimit || canIgnoreLimit;
    }
    else
    {
      targetPlayerData.ClanKeys.Add(new ClanKeyData
      {
        ClanName = effectiveClanName,
        ClanGuid = clanGuid,
        IsOwnerKey = false,
        CanIgnoreClanLimit = canIgnoreLimit,
        IssuedTime = DateTime.Now
      });
    }

    PlayerDataService.SaveData();
    RefreshCache();

    FixedString512Bytes notification;
    string reply;

    if (canIgnoreLimit)
    {
      notification = new FixedString512Bytes($"You have been given a key for clan '{effectiveClanName}' that bypasses clan limits!\n  Use <color=green>.keys use \"{effectiveClanName}\"</color> to join.");
      reply = $"Bypass key issued to {targetUser.CharacterName} for {effectiveClanName}.";
    }
    else
    {
      notification = new FixedString512Bytes($"You have been given a key for clan '{effectiveClanName}'!\n  Use <color=green>.keys use \"{effectiveClanName}\"</color> to join.");
      reply = $"Key issued to {targetUser.CharacterName} for {effectiveClanName}.";
    }

    ServerChatUtils.SendSystemMessageToClient(Core.EntityManager, targetUser, ref notification);
    ctx.Reply(reply);
  }

  public static void RevokeKey(ChatCommandContext ctx, Entity targetUserEntity, bool force = false)
  {
    var userEntity = ctx.Event.SenderUserEntity;
    var user = userEntity.Read<User>();

    if (user.ClanEntity.Equals(NetworkedEntity.Empty))
    {
      ctx.Reply("You are not in a clan.");
      return;
    }

    var clanEntity = user.ClanEntity.GetEntityOnServer();
    var clanTeam = clanEntity.Read<ClanTeam>();
    var clanName = clanTeam.Name.ToString();
    var clanGuid = clanTeam.ClanGuid.ToString();

    if (!force && !IsClanRegistered(clanGuid))
    {
      ctx.Reply($"Clan '{clanName}' is not registered with the keys system.");
      return;
    }

    if (!force && !CanIssueKeys(userEntity, clanGuid))
    {
      ctx.Reply("Only the clan owner can revoke keys.");
      return;
    }

    if (targetUserEntity.Equals(Entity.Null))
    {
      ctx.Reply("Target player not found.");
      return;
    }

    var targetUser = targetUserEntity.Read<User>();
    var targetPlayerData = GetPlayerData(targetUser);

    var keyToRemove = targetPlayerData.ClanKeys.FirstOrDefault(k => k.ClanGuid.Equals(clanGuid, StringComparison.OrdinalIgnoreCase));
    if (keyToRemove == null)
    {
      ctx.Reply($"{targetUser.CharacterName} does not have a key for your clan.");
      return;
    }

    targetPlayerData.ClanKeys.Remove(keyToRemove);
    PlayerDataService.SaveData();
    RefreshCache();

    var notification = new FixedString512Bytes($"Your key for clan '{clanName}' has been revoked.");
    ServerChatUtils.SendSystemMessageToClient(Core.EntityManager, targetUser, ref notification);

    ctx.Reply($"Key revoked from {targetUser.CharacterName} for {clanName}.");
  }

  public static List<(PlayerData PlayerData, ClanKeyData Key)> GetKeysForClan(string clanGuid)
  {
    EnsureCache();
    var results = new List<(PlayerData PlayerData, ClanKeyData Key)>();

    var allPlayers = PlayerDataService.GetAllPlayerData();

    foreach (var playerData in allPlayers)
    {
      foreach (var key in playerData.ClanKeys.Where(k => k.ClanGuid.Equals(clanGuid, StringComparison.OrdinalIgnoreCase)))
      {
        results.Add((playerData, key));
      }
    }

    return results;
  }

  public static Dictionary<string, int> GetAllClansKeyCounts()
  {
    EnsureCache();
    var clanCounts = new Dictionary<string, int>();

    var allPlayers = PlayerDataService.GetAllPlayerData();

    foreach (var playerData in allPlayers)
    {
      foreach (var key in playerData.ClanKeys)
      {
        if (!string.IsNullOrEmpty(key.ClanGuid))
        {
          if (clanCounts.ContainsKey(key.ClanGuid))
          {
            clanCounts[key.ClanGuid]++;
          }
          else
          {
            clanCounts[key.ClanGuid] = 1;
          }
        }
      }
    }

    return clanCounts;
  }

  public static List<string> GetClansWithKeysForPlayer(Entity playerEntity)
  {
    var user = playerEntity.Read<User>();
    var playerData = GetPlayerData(user);

    return playerData.ClanKeys
      .Where(k => !string.IsNullOrEmpty(k.ClanGuid))
      .Select(k => k.ClanName)
      .ToList();
  }

  public static void JoinClan(ChatCommandContext ctx, string clanName)
  {
    if (string.IsNullOrEmpty(clanName))
    {
      ctx.Reply("Please specify a clan name.");
      return;
    }

    var userEntity = ctx.Event.SenderUserEntity;
    var user = userEntity.Read<User>();

    if (!user.ClanEntity.Equals(NetworkedEntity.Empty))
    {
      var currentClanEntity = user.ClanEntity.GetEntityOnServer();
      var currentClanTeam = currentClanEntity.Read<ClanTeam>();
      var currentClanName = currentClanTeam.Name.ToString();
      var currentClanGuid = currentClanTeam.ClanGuid.ToString();
      ctx.Reply($"You are already in a clan: '{currentClanName}' ({currentClanGuid}). Leave it first.");
      return;
    }

    var playerData = GetPlayerData(user);
    var matchingKeys = playerData.ClanKeys
      .Where(k => k.ClanName.Equals(clanName, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(k.ClanGuid))
      .ToList();

    if (matchingKeys.Count == 0)
    {
      var clansWithKeys = GetClansWithKeysForPlayer(userEntity);
      ctx.Reply($"You don't have a key for clan '{clanName}'. You have keys for: {string.Join(", ", clansWithKeys)}");
      return;
    }

    if (matchingKeys.Count > 1)
    {
      var keyList = string.Join("\n", matchingKeys.Select((k, i) => $"  {i + 1}. {k.ClanGuid} - {k.ClanName}"));
      ctx.Reply($"Ambiguous clan name '{clanName}'. You have keys for multiple clans:\n{keyList}");
      return;
    }

    var targetKey = matchingKeys[0];
    var targetClanGuid = targetKey.ClanGuid;
    var targetClanName = targetKey.ClanName;

    var clans = EntityService.GetEntitiesByComponentType<ClanTeam>();
    Entity targetClan = Entity.Null;

    foreach (var clan in clans)
    {
      var clanTeam = clan.Read<ClanTeam>();
      if (clanTeam.ClanGuid.ToString().Equals(targetClanGuid, StringComparison.OrdinalIgnoreCase))
      {
        targetClan = clan;
        break;
      }
    }

    if (targetClan.Equals(Entity.Null))
    {
      ctx.Reply($"Clan '{targetClanName}' not found.");
      return;
    }

    var actualClanTeam = targetClan.Read<ClanTeam>();
    targetClanName = actualClanTeam.Name.ToString();

    bool isAtCapacity = IsClanAtCapacity(targetClan, false);
    bool canIgnoreLimit = targetKey.CanIgnoreClanLimit;

    if (isAtCapacity && !canIgnoreLimit)
    {
      ctx.Reply($"Clan '{targetClanName}' is at maximum capacity.");
      return;
    }

    var limitType = CastleHeartLimitType.User;
    TeamUtility.AddUserToClan(Core.EntityManager, targetClan, userEntity, ref user, limitType);
    userEntity.Write<User>(user);

    var clanRole = userEntity.Read<ClanRole>();
    clanRole.Value = targetKey.IsOwnerKey ? ClanRoleEnum.Leader : ClanRoleEnum.Member;
    userEntity.Write<ClanRole>(clanRole);

    ctx.Reply($"Joined clan '{targetClanName}'!");

    _ = DiscordWebhookService.SendGameEventMessageToWebhook(DiscordLogType.ClanKeyJoin, userEntity, targetClan);
  }

  public static bool IsClanAtCapacity(Entity clanEntity, bool ignoreLimit)
  {
    if (ignoreLimit)
      return false;

    var members = Core.EntityManager.GetBuffer<ClanMemberStatus>(clanEntity);
    int currentCount = members.Length;

    return currentCount >= ConfigService.ClanMemberLimit;
  }

  public static void RevokeAllKeysForClan(ChatCommandContext ctx, string clanName, bool deRegister = false)
  {
    if (string.IsNullOrEmpty(clanName))
    {
      ctx.Reply("Please specify a clan name.");
      return;
    }

    var clanEntity = GetClanEntity(clanName, ctx);
    if (clanEntity.Equals(Entity.Null))
    {
      return;
    }

    var clanTeam = clanEntity.Read<ClanTeam>();
    var clanGuid = clanTeam.ClanGuid.ToString();

    var keysToRevoke = GetKeysForClan(clanGuid);
    int revokeCount = 0;

    foreach (var (playerData, key) in keysToRevoke)
    {
      if (playerData != null)
      {
        if (!deRegister && key.IsOwnerKey)
          continue;

        playerData.ClanKeys.Remove(key);
        revokeCount++;
      }
    }

    if (revokeCount > 0)
    {
      PlayerDataService.SaveData();
      RefreshCache();
    }

    ctx.Reply($"Revoked {revokeCount} keys for clan '{clanName}'.");
  }

  public static void DeregisterClan(ChatCommandContext ctx, string clanName)
  {
    if (string.IsNullOrEmpty(clanName))
    {
      ctx.Reply("Please specify a clan name.");
      return;
    }

    var clanEntity = GetClanEntity(clanName, ctx);
    if (clanEntity.Equals(Entity.Null))
    {
      return;
    }

    var clanTeam = clanEntity.Read<ClanTeam>();
    var clanGuid = clanTeam.ClanGuid.ToString();

    if (!IsClanRegistered(clanGuid))
    {
      ctx.Reply($"Clan '{clanName}' is not registered.");
      return;
    }

    RevokeAllKeysForClan(ctx, clanName, true);

    RefreshCache();

    ctx.Reply($"Clan '{clanName}' deregistered and all keys revoked.");
  }

  public static void IssueKeysToAll(ChatCommandContext ctx, Entity senderUserEntity, string clanName, bool canBypass)
  {
    if (string.IsNullOrEmpty(clanName))
    {
      ctx.Reply("Please specify a clan name.");
      return;
    }

    var targetClan = GetClanEntity(clanName, ctx);
    if (targetClan.Equals(Entity.Null))
    {
      return;
    }

    var clanTeam = targetClan.Read<ClanTeam>();
    var effectiveClanName = clanTeam.Name.ToString();

    var members = Core.EntityManager.GetBuffer<ClanMemberStatus>(targetClan);
    var userBuffer = Core.EntityManager.GetBuffer<SyncToUserBuffer>(targetClan);

    int issueCount = 0;

    for (var i = 0; i < members.Length; ++i)
    {
      var member = members[i];
      var userBufferEntry = userBuffer[i];
      var memberUserEntity = userBufferEntry.UserEntity;

      if (memberUserEntity.Equals(senderUserEntity))
        continue;

      IssueKey(ctx, senderUserEntity, memberUserEntity, clanName, canBypass, false, false);
      issueCount++;
    }

    ctx.Reply($"Issued keys to {issueCount} members of clan '{effectiveClanName}'.");
  }

  public static void IssueKeysInRadius(ChatCommandContext ctx, Entity senderUserEntity, string clanName, float radius, bool canBypass)
  {
    if (string.IsNullOrEmpty(clanName))
    {
      ctx.Reply("Please specify a clan name.");
      return;
    }

    var targetClan = GetClanEntity(clanName, ctx);
    if (targetClan.Equals(Entity.Null))
    {
      return;
    }

    var clanTeam = targetClan.Read<ClanTeam>();
    var effectiveClanName = clanTeam.Name.ToString();

    var players = EntityService.GetNearbyUserEntities(ctx.Event.SenderCharacterEntity, radius);
    int issueCount = 0;

    foreach (var playerEntity in players)
    {
      if (playerEntity.Equals(ctx.Event.SenderCharacterEntity))
        continue;

      IssueKey(ctx, senderUserEntity, playerEntity, clanName, canBypass, false, false);
      issueCount++;
    }

    ctx.Reply($"Issued keys to {issueCount} nearby players for clan '{effectiveClanName}'.");
  }
}
