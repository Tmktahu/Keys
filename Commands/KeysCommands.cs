using Keys.Services;
using VampireCommandFramework;

namespace Keys.Commands;

[CommandGroup("keys")]
internal static class KeyCommands
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

  [Command("list", "List keys. Usage: .keys list mine|clan|all|<clanName>")]
  public static void ListCommand(ChatCommandContext ctx, string target = null)
  {
    if (string.IsNullOrEmpty(target))
    {
      ctx.Reply("Usage: .keys list mine|clan|all|<clanName>");
      return;
    }

    if (target.Equals("mine", StringComparison.OrdinalIgnoreCase))
    {
      KeyManagementService.ListMine(ctx);
    }
    else if (target.Equals("clan", StringComparison.OrdinalIgnoreCase))
    {
      KeyManagementService.ListClan(ctx);
    }
    else if (target.Equals("all", StringComparison.OrdinalIgnoreCase))
    {
      KeyManagementService.ListAll(ctx);
    }
    else
    {
      KeyManagementService.ListClanByName(ctx, target);
    }
  }

  [Command("remove", "Remove a key. Usage: .keys remove <player>|clan")]
  public static void RemoveCommand(ChatCommandContext ctx, string target)
  {
    if (target.Equals("clan", StringComparison.OrdinalIgnoreCase))
    {
      KeyManagementService.RemoveClanKeys(ctx);
    }
    else
    {
      KeyManagementService.RemovePlayerKey(ctx, target);
    }
  }

  [Command("remove", "Remove a player's key for any clan (admin)", adminOnly: true)]
  public static void RemoveCommand(ChatCommandContext ctx, string playerName, string clanName)
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
