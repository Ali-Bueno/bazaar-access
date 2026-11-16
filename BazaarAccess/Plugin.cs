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
    internal static ConfigEntry<ControllerMode> ControllerMode;
    internal static ConfigEntry<bool> TestMode;

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

        ControllerMode = Config.Bind(
            "Controller",
            "Mode",
            Core.ControllerMode.Hybrid,
            "Hybrid = in a run the game's own controller navigation is used and the mod reads the focus (View switches to the mod's navigation, hold RT for quick info); menus use the mod's navigation. KeyboardEmulation = the controller always drives the mod's navigation. GameOnly = the mod never handles the controller."
        );

        TestMode = Config.Bind(
            "Debug",
            "TestMode",
            false,
            "Developer testing: logs every spoken line and runs commands from BepInEx/BazaarAccess-commands.txt."
        );

        // Load the mod's texts before anything can speak.
        Loc.Initialize();

        // Initialize Tolk with error handling
        if (TolkWrapper.Initialize())
        {
            TolkWrapper.Speak(Loc.T("mod.loaded"));
        }

        // Create keyboard navigator
        KeyboardNavigator.Create(gameObject);
        GamepadNavigator.Create(gameObject);
        TestCommandRunner.Create(gameObject);

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
        GamepadNavigator.Destroy();
        _harmony?.UnpatchSelf();
        Loc.Shutdown();
        TolkWrapper.Shutdown();
    }
}
