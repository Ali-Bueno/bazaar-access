using System;
using DavyKager;

namespace BazaarAccess.Core;

/// <summary>
/// Wrapper para Tolk con manejo de errores centralizado.
/// </summary>
public static class TolkWrapper
{
    private static bool _isInitialized = false;
    private static bool _initFailed = false;

    public static bool IsAvailable => _isInitialized && !_initFailed;

    public static bool Initialize()
    {
        if (_isInitialized) return true;
        if (_initFailed) return false;

        try
        {
            Tolk.Load();
            _isInitialized = true;
            Plugin.Logger.LogInfo("Tolk inicializado correctamente");
            return true;
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError($"Error al inicializar Tolk: {ex.Message}");
            _initFailed = true;
            return false;
        }
    }

    public static void Speak(string text, bool interrupt = true)
    {
        if (!_isInitialized || string.IsNullOrWhiteSpace(text)) return;

        try
        {
            if (interrupt)
            {
                Tolk.Output(text);
            }
            else
            {
                Tolk.Speak(text);
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning($"Error en Tolk.Output: {ex.Message}");
        }
    }

    public static void Silence()
    {
        if (!_isInitialized) return;

        try
        {
            Tolk.Silence();
        }
        catch
        {
            // Ignorar errores al silenciar
        }
    }

    public static void Shutdown()
    {
        if (!_isInitialized) return;

        try
        {
            Tolk.Unload();
        }
        catch
        {
            // Ignorar errores al cerrar
        }

        _isInitialized = false;
    }
}
