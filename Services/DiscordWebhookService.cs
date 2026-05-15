using System.Text;
using System.Text.Json;
using ProjectM;
using ProjectM.Network;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Keys.Services;

public enum DiscordLogType
{
  ClanKeyJoin,
}

public static class DiscordWebhookService
{
  private static readonly HttpClient _httpClient = new HttpClient();

  public static async Task SendGameEventMessageToWebhook(DiscordLogType logType, Entity userEntity, Entity targetEntity = default)
  {
    string GameEventsWebhookUrl = ConfigService.DEV ? ConfigService.DiscordDevGameEventsWebhookUrl : ConfigService.DiscordProdGameEventsWebhookUrl;

    if (string.IsNullOrEmpty(GameEventsWebhookUrl))
    {
      if (ConfigService.DEV) Core.Log.LogInfo("Discord game events webhook URL not configured");
      return;
    }

    string playerName = "";
    string message;
    float3 position = new float3(0, 0, 0);

    if (userEntity.Has<User>() || userEntity.Has<PlayerCharacter>())
      playerName = userEntity.GetUser().CharacterName.ToString();

    if (userEntity.Has<Translation>())
      position = userEntity.Read<Translation>().Value;

    message = $"> ";

    if (logType == DiscordLogType.ClanKeyJoin)
      message += "🗝️🗝️🗝️ ";

    message += playerName != "" ? $"Character '{playerName}' " : "Someone ";

    if (logType == DiscordLogType.ClanKeyJoin)
    {
      if (targetEntity.Has<ClanTeam>())
      {
        var clanTeam = targetEntity.Read<ClanTeam>();
        message += $"has joined clan '{clanTeam.Name}' using a key";
      }
      else
      {
        message += "has joined a clan using a key";
      }
    }

    message += !position.Equals(new float3(0, 0, 0)) ? $" at position {position.x:F1} {position.y:F1} {position.z:F1}" : ".";

    try
    {
      var payload = new { content = message };
      string json = JsonSerializer.Serialize(payload);
      var content = new StringContent(json, Encoding.UTF8, "application/json");

      var response = await _httpClient.PostAsync(GameEventsWebhookUrl, content);

      if (!response.IsSuccessStatusCode)
      {
        string errorContent = await response.Content.ReadAsStringAsync();
        Core.Log.LogError($"Discord game events webhook failed: {response.StatusCode} - {errorContent}");
      }
      else
      {
        if (ConfigService.DEV) Core.Log.LogInfo("Discord game event message sent successfully.");
      }
    }
    catch (Exception ex)
    {
      Core.Log.LogError($"Exception sending Discord game event message webhook: {ex.Message}");
    }
  }
}
