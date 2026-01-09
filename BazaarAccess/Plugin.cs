using BazaarAccess.Core;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace BazaarAccess;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    internal static new ManualLogSource Logger;
    internal static Plugin Instance { get; private set; }
    private static Harmony _harmony;

    private void Awake()
    {
        Instance = this;
        Logger = base.Logger;

        // Inicializar Tolk con manejo de errores
        if (TolkWrapper.Initialize())
        {
            TolkWrapper.Speak("Bazaar Access cargado");
        }

        // Crear el navegador de teclado
        KeyboardNavigator.Create(gameObject);

        // Aplicar parches de Harmony
        _harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
        _harmony.PatchAll();

        Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} cargado");
    }

    private void OnDestroy()
    {
        KeyboardNavigator.Destroy();
        _harmony?.UnpatchSelf();
        TolkWrapper.Shutdown();
    }
}
