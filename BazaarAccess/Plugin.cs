using BazaarAccess.Core;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System.Reflection;

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

        // Apply Harmony patches. Patch each class individually so that a single failing
        // patch (e.g. a game type removed/renamed by an update) only disables that one hook
        // instead of aborting the whole mod like PatchAll() does.
        _harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
        int patched = 0, failed = 0;
        foreach (var type in Assembly.GetExecutingAssembly().GetTypes())
        {
            try
            {
                var processor = _harmony.CreateClassProcessor(type);
                var result = processor.Patch();
                if (result != null && result.Count > 0)
                    patched++;
            }
            catch (System.Exception ex)
            {
                failed++;
                Logger.LogError($"Failed to apply patch class {type.FullName}: {ex.Message}");
            }
        }
        Logger.LogInfo($"Harmony patches applied: {patched} class(es) patched, {failed} failed");

        Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} loaded");
    }

    private void OnDestroy()
    {
        KeyboardNavigator.Destroy();
        _harmony?.UnpatchSelf();
        TolkWrapper.Shutdown();
    }
}
