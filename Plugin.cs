using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using System.Reflection;
using UnityEngine;
using VampireCommandFramework;
using Keys.Services;
using static Keys.Services.ConfigService.ConfigInitialization;

namespace Keys;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency("gg.deca.VampireCommandFramework")]
internal class Plugin : BasePlugin
{
  internal static Plugin Instance { get; private set; }

  Harmony _harmony;

  public static Harmony Harmony => Instance._harmony;
  public static bool IsServer { get; private set; }

  public static ManualLogSource LogInstance => Instance.Log;

  public override void Load()
  {
    Instance = this;

    IsServer = Application.productName == "VRisingServer";

    if (!IsServer)
    {
      Core.Log.LogWarning("This plugin is intended to run on the server only. It will not function correctly on the client.");
      return;
    }
    _harmony = Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly());

    InitializeConfig();

    CommandRegistry.RegisterAll();

    Log.LogInfo($"{MyPluginInfo.PLUGIN_NAME} v{MyPluginInfo.PLUGIN_VERSION} loaded successfully!");
  }

  public override bool Unload()
  {
    _harmony.UnpatchSelf();

    if (IsServer)
    {
      PlayerDataService.FlushSaveToDisk();
    }

    return true;
  }
}
