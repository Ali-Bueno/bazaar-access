using BazaarAccess.Core;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace BazaarAccess;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    internal static new ManualLogSource Logger;
    internal static Plugin Instance { get; private set; }
    private static Harmony _harmony;
    internal static ConfigEntry<bool> UseBatchedCombatMode;

    private void Awake()
    {
        Instance = this;
        Logger = base.Logger;

        // Initialize config
        UseBatchedCombatMode = Config.Bind(
            "Combat",
            "UseBatchedMode",
            true,  // Default: batched mode (original)
            "True = batched wave announcements with auto health. False = individual per-card announcements."
        );

        // Initialize Tolk with error handling
        if (TolkWrapper.Initialize())
        {
            TolkWrapper.Speak("Bazaar Access loaded");
        }

        // Create keyboard navigator
        KeyboardNavigator.Create(gameObject);

        // Apply Harmony patches
        _harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
        try
        {
            _harmony.PatchAll();
            Logger.LogInfo("Harmony patches applied successfully");
        }
        catch (System.Exception ex)
        {
            Logger.LogError($"Error applying Harmony patches: {ex.Message}");
            Logger.LogError(ex.StackTrace);
        }

        Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} loaded");
    }

    private void OnDestroy()
    {
        KeyboardNavigator.Destroy();
        _harmony?.UnpatchSelf();
        TolkWrapper.Shutdown();
    }
}
