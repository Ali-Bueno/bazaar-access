using System;
using DavyKager;
using UnityEngine;

namespace BazaarAccess.Core;

/// <summary>
/// Wrapper para Tolk con manejo de errores centralizado.
/// Includes global deduplication to prevent spam.
/// </summary>
public static class TolkWrapper
{
    private static bool _isInitialized = false;
    private static bool _initFailed = false;

    // Global deduplication to prevent the same message being spoken twice rapidly
    private static string _lastSpokenText = "";
    private static float _lastSpokenTime = 0f;
    private const float DEDUP_WINDOW = 0.3f; // Seconds to consider as duplicate

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

    // Test mode: log each spoken line with the methods that produced it, to trace stray announcements.
    private const int SpeechCallerFrames = 4;

    private static void LogSpeech(string text)
    {
        if (Plugin.TestMode == null || !Plugin.TestMode.Value) return;
        var frames = new System.Diagnostics.StackTrace(2, false).GetFrames() ?? new System.Diagnostics.StackFrame[0];
        var callers = new System.Collections.Generic.List<string>();
        foreach (var frame in frames)
        {
            var method = frame.GetMethod();
            if (method == null) continue;
            callers.Add($"{method.DeclaringType?.Name}.{method.Name}");
            if (callers.Count >= SpeechCallerFrames) break;
        }
        Plugin.Logger.LogInfo($"SPEECH: {text}   <= {string.Join(" < ", callers)}");
    }

    public static void Speak(string text, bool interrupt = true)
    {
        if (!_isInitialized || string.IsNullOrWhiteSpace(text)) return;

        // Global deduplication: skip if same text was just spoken
        float currentTime = Time.realtimeSinceStartup;
        if (text == _lastSpokenText && (currentTime - _lastSpokenTime) < DEDUP_WINDOW)
        {
            Plugin.Logger.LogInfo($"TolkWrapper: Skipping duplicate speech: {text.Substring(0, Math.Min(text.Length, 30))}...");
            return;
        }

        _lastSpokenText = text;
        _lastSpokenTime = currentTime;

        LogSpeech(text);

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

    /// <summary>
    /// Speaks without deduplication check. Use for intentional repeats.
    /// </summary>
    public static void SpeakForced(string text, bool interrupt = true)
    {
        if (!_isInitialized || string.IsNullOrWhiteSpace(text)) return;

        _lastSpokenText = text;
        _lastSpokenTime = Time.realtimeSinceStartup;

        LogSpeech(text);

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
