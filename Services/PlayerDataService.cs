using System.Text.Json;
using Keys.Models;
using Unity.Entities;
using System.Collections;
using UnityEngine;
using Stunlock.Core;
using ProjectM;
using ProjectM.Network;

namespace Keys.Services;

public static class PlayerDataService
{
  private static readonly string SaveDirectory = Path.Combine(Directory.GetCurrentDirectory(), "BepInEx", "config", "Keys");
  private static readonly string SavePath = Path.Combine(SaveDirectory, "keys_player_data.json");
  private static List<PlayerData> _playerDataList = new List<PlayerData>();
  private static Dictionary<int, PlayerData> _playerDataCache = new Dictionary<int, PlayerData>();
  private static bool _dataDirty = false;
  private static bool _periodicSaveCoroutineRunning = false;
  private const float PERIODIC_SAVE_INTERVAL = 30f;
  public static readonly PrefabGUID CHAR_VampireMale = new PrefabGUID(38526109);

  private static void MigrateLegacyPlayerData()
  {
    if (File.Exists(SavePath))
    {
      string json = File.ReadAllText(SavePath);
      bool isMigrated = false;
      try
      {
        var arrayCheck = JsonSerializer.Deserialize<List<PlayerData>>(json);
        if (arrayCheck != null && arrayCheck.Count > 0)
        {
          isMigrated = true;
        }
      }
      catch { }

      if (isMigrated) return;

      try
      {
        var legacyDict = JsonSerializer.Deserialize<Dictionary<ulong, PlayerData>>(json);
        if (legacyDict != null && legacyDict.Count > 0)
        {
          Core.Log.LogInfo($"Found legacy player data. Records: {legacyDict.Count}");
          var playerList = legacyDict.Values.ToList();
          string newJson = JsonSerializer.Serialize(playerList);
          File.WriteAllText(SavePath, newJson);
          Core.Log.LogInfo($"Migrated legacy player data to array format. Records: {playerList.Count}");
          return;
        }
      }
      catch { }

      Core.Log.LogError("Player data file is invalid or corrupt. Migration failed.");
    }
  }

  public static void Initialize()
  {
    Directory.CreateDirectory(SaveDirectory);
    MigrateLegacyPlayerData();
    LoadData();
  }

  public static PlayerData GetPlayerData(Entity characterEntity)
  {
    Entity actualCharacterEntity = characterEntity;
    if (Core.EntityManager.TryGetComponentData<PrefabGUID>(characterEntity, out var prefabGuid) && !prefabGuid.Equals(CHAR_VampireMale))
    {
      if (Core.EntityManager.TryGetComponentData<ControlledBy>(characterEntity, out var controlledBy))
      {
        Entity userEntity = controlledBy.Controller;
        User user = userEntity.GetUser();
        actualCharacterEntity = user.LocalCharacter._Entity;
      }
    }

    ulong steamId = actualCharacterEntity.GetSteamId();
    string characterName = actualCharacterEntity.GetUser().CharacterName.ToString();
    if (string.IsNullOrEmpty(characterName)) characterName = "Unknown DAFUQ HAPPENED";

    int guidHash = GetGuidHash(actualCharacterEntity);

    if (guidHash != 0 && _playerDataCache.TryGetValue(guidHash, out var cached))
      return cached;

    return GetOrCreatePlayerData(guidHash, characterName, actualCharacterEntity);
  }

  private static PlayerData GetOrCreatePlayerData(int guidHash, string characterName, Entity characterEntity)
  {
    ulong steamId = characterEntity.GetSteamId();

    if (guidHash != 0)
    {
      var guidData = _playerDataList.Find(pd => pd.GuidHash == guidHash);
      if (guidData != null)
      {
        guidData.CharacterName = characterName;
        _playerDataCache[guidHash] = guidData;
        return guidData;
      }
    }

    var legacyByName = _playerDataList.Find(pd => pd.CharacterName == characterName && pd.GuidHash == 0);
    if (legacyByName != null)
    {
      var newGuid = new ProjectM.SequenceGUID(System.Guid.NewGuid().GetHashCode());
      Core.EntityManager.AddComponentData(characterEntity, newGuid);
      legacyByName.GuidHash = newGuid.GuidHash;
      legacyByName.PrefabGuidHash = CHAR_VampireMale.GuidHash;
      guidHash = newGuid.GuidHash;
      _playerDataCache[guidHash] = legacyByName;
      SaveData();
      return legacyByName;
    }

    var newData = new PlayerData
    {
      SteamId = steamId,
      CharacterName = characterName,
      GuidHash = guidHash != 0 ? guidHash : System.Guid.NewGuid().GetHashCode(),
      PrefabGuidHash = characterEntity.GetPrefabGuid().GuidHash
    };
    if (guidHash == 0)
    {
      var newGuid = new ProjectM.SequenceGUID(newData.GuidHash);
      Core.EntityManager.AddComponentData(characterEntity, newGuid);
      guidHash = newData.GuidHash;
    }
    _playerDataList.Add(newData);
    _playerDataCache[guidHash] = newData;
    SaveData();
    return newData;
  }

  public static List<PlayerData> GetAllPlayerData()
  {
    return _playerDataList;
  }

  public static void SaveData()
  {
    MarkDirty();
  }

  public static void MarkDirty()
  {
    _dataDirty = true;
    if (!_periodicSaveCoroutineRunning)
    {
      Core.StartCoroutine(PeriodicSaveCoroutine());
      _periodicSaveCoroutineRunning = true;
    }
  }

  private static IEnumerator PeriodicSaveCoroutine()
  {
    while (true)
    {
      yield return new WaitForSeconds(PERIODIC_SAVE_INTERVAL);
      if (_dataDirty)
      {
        FlushSaveToDisk();
        _dataDirty = false;
      }
    }
  }

  public static void FlushSaveToDisk()
  {
    try
    {
      string json = JsonSerializer.Serialize(_playerDataList);
      File.WriteAllText(SavePath, json);
    }
    catch (Exception ex)
    {
      Core.Log.LogError($"Failed to save player data: {ex.Message}");
    }
  }

  private static void LoadData()
  {
    if (File.Exists(SavePath))
    {
      try
      {
        string json = File.ReadAllText(SavePath);
        _playerDataList = JsonSerializer.Deserialize<List<PlayerData>>(json)
          ?? new List<PlayerData>();

        _playerDataCache.Clear();
        foreach (var playerData in _playerDataList)
        {
          if (playerData.GuidHash != 0)
            _playerDataCache[playerData.GuidHash] = playerData;
        }
      }
      catch (Exception ex)
      {
        Core.Log.LogError($"Failed to load player data: {ex.Message}");
        _playerDataList = new List<PlayerData>();
        _playerDataCache.Clear();
      }
    }
  }

  private static int GetGuidHash(Entity characterEntity)
  {
    int guidHash = 0;
    if (Core.EntityManager.HasComponent<ProjectM.SequenceGUID>(characterEntity))
    {
      var seqGuid = Core.EntityManager.GetComponentData<ProjectM.SequenceGUID>(characterEntity);
      guidHash = seqGuid.GuidHash;
    }
    return guidHash;
  }
}
