using Keys.Services;
using System.Text;
using VampireCommandFramework;
using Unity.Entities;
using ProjectM;
using ProjectM.Network;

namespace Keys.Commands;

[CommandGroup("keys")]
internal static class ClanKeyCommands
{
  [Command("register", "Register your clan with the keys system")]
  public static void RegisterClanCommand(ChatCommandContext ctx)
  {
    KeyManagementService.RegisterClan(ctx);
  }

  [Command("owner", "Transfer ownership to another player (owner only)")]
  public static void TransferOwnershipCommand(ChatCommandContext ctx, string playerName)
  {
    if (string.IsNullOrEmpty(playerName))
    {
      ctx.Reply("Please specify a player name.");
      return;
    }

    if (!EntityService.TryFindPlayer(playerName, out var userEntity, out var targetUserEntity))
    {
      ctx.Reply($"Player '{playerName}' not found.");
      return;
    }

    KeyManagementService.TransferOwnership(ctx, targetUserEntity);
  }

  [Command("owner", "Transfer ownership from one player to another (admin)", adminOnly: true)]
  public static void TransferOwnershipCommand(ChatCommandContext ctx, string originalOwnerName, string newOwnerName)
  {
    if (string.IsNullOrEmpty(originalOwnerName))
    {
      ctx.Reply("Please specify the original owner's name.");
      return;
    }

    if (!EntityService.TryFindPlayer(originalOwnerName, out var originalOwnerUserEntity, out var originalUser))
    {
      ctx.Reply($"Original owner '{originalOwnerName}' not found.");
      return;
    }

    if (string.IsNullOrEmpty(newOwnerName))
    {
      ctx.Reply("Please specify the new owner's name.");
      return;
    }

    if (!EntityService.TryFindPlayer(newOwnerName, out var newUserEntity, out var newUser))
    {
      ctx.Reply($"New owner '{newOwnerName}' not found.");
      return;
    }

    KeyManagementService.TransferOwnership(ctx, newUser, originalUser);
  }

  [Command("give", "Issue a key to a player")]
  public static void IssueKeyCommand(ChatCommandContext ctx, string playerName)
  {
    if (string.IsNullOrEmpty(playerName))
    {
      ctx.Reply("Please specify a player name.");
      return;
    }

    if (!EntityService.TryFindPlayer(playerName, out var playerEntity, out var userEntity))
    {
      ctx.Reply($"Player '{playerName}' not found.");
      return;
    }

    var senderUserEntity = ctx.Event.SenderUserEntity;
    KeyManagementService.IssueKey(ctx, senderUserEntity, userEntity, null, false, false, true);
  }

  [Command("give", "Issue a key for a specific clan (admin)", adminOnly: true)]
  public static void IssueKeyCommand(ChatCommandContext ctx, string playerName, string clanName)
  {
    if (string.IsNullOrEmpty(playerName))
    {
      ctx.Reply("Please specify a player name.");
      return;
    }

    if (!EntityService.TryFindPlayer(playerName, out var playerEntity, out var userEntity))
    {
      ctx.Reply($"Player '{playerName}' not found.");
      return;
    }

    if (string.IsNullOrEmpty(clanName))
    {
      ctx.Reply("Please specify a clan name.");
      return;
    }

    var senderUserEntity = ctx.Event.SenderUserEntity;
    KeyManagementService.IssueKey(ctx, senderUserEntity, userEntity, clanName, false, true, false);
  }

  [Command("give", "Issue a key for a specific clan with optional bypass (admin)", adminOnly: true)]
  public static void IssueKeyCommand(ChatCommandContext ctx, string playerName, string clanName, string bypassKeyword)
  {
    if (string.IsNullOrEmpty(playerName))
    {
      ctx.Reply("Please specify a player name.");
      return;
    }

    if (!EntityService.TryFindPlayer(playerName, out var playerEntity, out var userEntity))
    {
      ctx.Reply($"Player '{playerName}' not found.");
      return;
    }

    if (string.IsNullOrEmpty(clanName))
    {
      ctx.Reply("Please specify a clan name.");
      return;
    }

    bool canBypass = !string.IsNullOrEmpty(bypassKeyword) &&
                    bypassKeyword.Equals("bypass", StringComparison.OrdinalIgnoreCase);

    var senderUserEntity = ctx.Event.SenderUserEntity;
    KeyManagementService.IssueKey(ctx, senderUserEntity, userEntity, clanName, canBypass, true, false);
  }

  [Command("giveall", "Give keys to everyone for a specific clan (admin)", adminOnly: true)]
  public static void IssueKeysToAllCommand(ChatCommandContext ctx, string clanName, string bypassKeyword = null)
  {
    if (string.IsNullOrEmpty(clanName))
    {
      ctx.Reply("Please specify a clan name.");
      return;
    }

    bool canBypass = !string.IsNullOrEmpty(bypassKeyword) &&
                    bypassKeyword.Equals("bypass", StringComparison.OrdinalIgnoreCase);

    var senderUserEntity = ctx.Event.SenderUserEntity;
    KeyManagementService.IssueKeysToAll(ctx, senderUserEntity, clanName, canBypass);
  }

  [Command("giveradius", "Give keys to everyone in radius for a specific clan (admin)", adminOnly: true)]
  public static void IssueKeysInRadiusCommand(ChatCommandContext ctx, string clanName, float radius = 10f, string bypassKeyword = null)
  {
    if (string.IsNullOrEmpty(clanName))
    {
      ctx.Reply("Please specify a clan name.");
      return;
    }

    if (radius <= 0)
    {
      ctx.Reply("Please specify a valid radius.");
      return;
    }

    bool canBypass = !string.IsNullOrEmpty(bypassKeyword) &&
                    bypassKeyword.Equals("bypass", StringComparison.OrdinalIgnoreCase);

    var senderUserEntity = ctx.Event.SenderUserEntity;
    KeyManagementService.IssueKeysInRadius(ctx, senderUserEntity, clanName, radius, canBypass);
  }

  [Command("list mine", "List all keys you have been granted")]
  public static void ListMyKeysCommand(ChatCommandContext ctx)
  {
    var userEntity = ctx.Event.SenderUserEntity;
    var user = userEntity.Read<User>();
    var playerData = KeyManagementService.GetPlayerData(user);

    var keys = new List<(string ClanName, string ClanGuid, string KeyType)>();
    foreach (var key in playerData.ClanKeys)
    {
      if (string.IsNullOrEmpty(key.ClanName) || string.IsNullOrEmpty(key.ClanGuid)) continue;
      var keyType = key.IsOwnerKey ? "OWNER" : (key.CanIgnoreClanLimit ? "ADMIN (bypasses limits)" : "MEMBER");
      keys.Add((key.ClanName, key.ClanGuid, keyType));
    }

    if (keys.Count == 0)
    {
      ctx.Reply("You have no keys.");
      return;
    }

    StringBuilder sb = new StringBuilder();
    sb.AppendLine("Keys you possess:");
    foreach (var (clanName, clanGuid, keyType) in keys)
    {
      sb.AppendLine($"  Clan: {clanName} ({clanGuid}), Type: {keyType}");
    }
    sb.AppendLine("Use <color=green>.keys use \"Clan Name\"</color> to join a clan.");

    ctx.Reply(sb.ToString());
  }

  [Command("list clan", "List all keys for your clan (owner only)")]
  public static void ListClanKeysCommand(ChatCommandContext ctx)
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

    if (!KeyManagementService.IsClanRegistered(clanGuid))
    {
      ctx.Reply($"Clan '{clanName}' is not registered with the keys system. Use .keys register.");
      return;
    }

    if (!KeyManagementService.CanIssueKeys(userEntity, clanGuid))
    {
      ctx.Reply("Only the clan owner can list keys.");
      return;
    }

    var allKeys = KeyManagementService.GetKeysForClan(clanGuid);
    if (allKeys.Count == 0)
    {
      ctx.Reply($"No keys issued for clan '{clanName}'.");
      return;
    }

    StringBuilder sb = new StringBuilder();
    sb.AppendLine($"Keys issued for clan '{clanName}':");
    foreach (var (playeData, key) in allKeys)
    {
      var keyType = key.IsOwnerKey ? "OWNER" : (key.CanIgnoreClanLimit ? "ADMIN" : "MEMBER");
      sb.AppendLine($"  {playeData.CharacterName} - {keyType}");
    }

    ctx.Reply(sb.ToString());
  }

  [Command("list all", "List all clans and their key counts", adminOnly: true)]
  public static void ListAllClansCommand(ChatCommandContext ctx)
  {
    var clanCounts = KeyManagementService.GetAllClansKeyCounts();

    if (clanCounts.Count == 0)
    {
      ctx.Reply("No clans have been registered with keys.");
      return;
    }

    StringBuilder sb = new StringBuilder();
    sb.AppendLine("Clans registered with keys:");

    foreach (var (clanGuid, count) in clanCounts.OrderByDescending(kv => kv.Value))
    {
      var clans = EntityService.GetEntitiesByComponentType<ClanTeam>();
      string clanName = clanGuid;

      foreach (var clan in clans)
      {
        var clanTeam = clan.Read<ClanTeam>();
        if (clanTeam.ClanGuid.ToString().Equals(clanGuid, StringComparison.OrdinalIgnoreCase))
        {
          clanName = clanTeam.Name.ToString();
          break;
        }
      }

      sb.AppendLine($"  {clanName} ({clanGuid}): {count} key(s)");
    }

    ctx.Reply(sb.ToString());
  }

  [Command("list", "List all keys for any clan", adminOnly: true)]
  public static void ListClanKeysForAdminCommand(ChatCommandContext ctx, string clanName)
  {
    if (string.IsNullOrEmpty(clanName))
    {
      ctx.Reply("Please specify a clan name.");
      return;
    }

    var clanGuid = KeyManagementService.GetClanGuidFromName(clanName, ctx);
    if (clanGuid == null)
    {
      return;
    }

    var keys = KeyManagementService.GetKeysForClan(clanGuid);
    if (keys.Count == 0)
    {
      ctx.Reply($"No keys issued for clan '{clanName}'.");
      return;
    }

    StringBuilder sb = new StringBuilder();
    sb.AppendLine($"Keys for '{clanName}':");
    foreach (var (playerData, key) in keys)
    {
      var keyType = key.IsOwnerKey ? "OWNER" : (key.CanIgnoreClanLimit ? "ADMIN" : "MEMBER");
      sb.AppendLine($"  {playerData.CharacterName} - {keyType}");
    }

    ctx.Reply(sb.ToString());
  }

  [Command("remove", "Revoke a player's key")]
  public static void RevokeKeyCommand(ChatCommandContext ctx, string playerName)
  {
    if (string.IsNullOrEmpty(playerName))
    {
      ctx.Reply("Please specify a player name.");
      return;
    }

    if (!EntityService.TryFindPlayer(playerName, out var playerEntity, out var userEntity))
    {
      ctx.Reply($"Player '{playerName}' not found.");
      return;
    }

    KeyManagementService.RevokeKey(ctx, userEntity);
  }

  [Command("remove clan", "Revoke all keys for a clan (owner only)")]
  public static void RevokeClanKeysCommand(ChatCommandContext ctx)
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

    if (!KeyManagementService.IsClanRegistered(clanGuid))
    {
      ctx.Reply($"Clan '{clanName}' is not registered with the keys system. Use .keys register.");
      return;
    }

    if (!KeyManagementService.CanIssueKeys(userEntity, clanGuid))
    {
      ctx.Reply("Only the clan owner can revoke all keys.");
      return;
    }

    KeyManagementService.RevokeAllKeysForClan(ctx, clanName);
  }

  [Command("remove", "Remove any key (admin)", adminOnly: true)]
  public static void RevokeKeyCommand(ChatCommandContext ctx, string playerName, string clanName)
  {
    if (string.IsNullOrEmpty(playerName))
    {
      ctx.Reply("Please specify a player name.");
      return;
    }

    if (!EntityService.TryFindPlayer(playerName, out var playerEntity, out var userEntity))
    {
      ctx.Reply($"Player '{playerName}' not found.");
      return;
    }

    if (string.IsNullOrEmpty(clanName))
    {
      ctx.Reply("Please specify a clan name.");
      return;
    }

    KeyManagementService.RevokeKey(ctx, userEntity, true);
  }

  [Command("deregister", "Deregister a clan and remove all keys (admin)", adminOnly: true)]
  public static void DeregisterClanCommand(ChatCommandContext ctx, string clanName)
  {
    if (string.IsNullOrEmpty(clanName))
    {
      ctx.Reply("Please specify a clan name.");
      return;
    }

    KeyManagementService.DeregisterClan(ctx, clanName);
    ctx.Reply($"Clan '{clanName}' has been deregistered and all keys have been removed.");
  }

  [Command("use", "Join a clan using your key")]
  public static void JoinClanCommand(ChatCommandContext ctx, string clanName)
  {
    KeyManagementService.JoinClan(ctx, clanName);
  }
}
