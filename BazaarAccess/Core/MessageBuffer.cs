using System.Collections.Generic;

namespace BazaarAccess.Core;

/// <summary>
/// Buffer circular para almacenar mensajes del juego/tutorial.
/// Permite navegar hacia atrás y adelante por los mensajes.
/// </summary>
public static class MessageBuffer
{
    private const int MaxMessages = 50;
    private static readonly List<string> _messages = new List<string>();
    private static int _currentIndex = -1;

    /// <summary>
    /// Agrega un mensaje al buffer.
    /// </summary>
    public static void Add(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;

        _messages.Add(message);

        // Limitar tamaño del buffer
        while (_messages.Count > MaxMessages)
        {
            _messages.RemoveAt(0);
        }

        // Mover índice al mensaje más reciente
        _currentIndex = _messages.Count - 1;

        Plugin.Logger.LogInfo($"MessageBuffer: {message}");
    }

    /// <summary>
    /// Lee el mensaje más reciente (punto).
    /// </summary>
    public static void ReadNewest()
    {
        if (_messages.Count == 0)
        {
            TolkWrapper.Speak("No messages");
            return;
        }

        _currentIndex = _messages.Count - 1;
        ReadCurrent();
    }

    /// <summary>
    /// Lee el mensaje anterior (coma).
    /// </summary>
    public static void ReadPrevious()
    {
        if (_messages.Count == 0)
        {
            TolkWrapper.Speak("No messages");
            return;
        }

        if (_currentIndex > 0)
        {
            _currentIndex--;
        }
        else
        {
            TolkWrapper.Speak("First message");
        }

        ReadCurrent();
    }

    /// <summary>
    /// Lee el mensaje siguiente.
    /// </summary>
    public static void ReadNext()
    {
        if (_messages.Count == 0)
        {
            TolkWrapper.Speak("No messages");
            return;
        }

        if (_currentIndex < _messages.Count - 1)
        {
            _currentIndex++;
        }
        else
        {
            TolkWrapper.Speak("Last message");
        }

        ReadCurrent();
    }

    /// <summary>
    /// Lee el mensaje actual.
    /// </summary>
    private static void ReadCurrent()
    {
        if (_currentIndex < 0 || _currentIndex >= _messages.Count)
        {
            TolkWrapper.Speak("No message");
            return;
        }

        string message = _messages[_currentIndex];
        TolkWrapper.Speak(message);
    }

    /// <summary>
    /// Limpia el buffer.
    /// </summary>
    public static void Clear()
    {
        _messages.Clear();
        _currentIndex = -1;
    }

    /// <summary>
    /// Cantidad de mensajes en el buffer.
    /// </summary>
    public static int Count => _messages.Count;

    /// <summary>
    /// Verifica si un mensaje está en los últimos N mensajes (para evitar duplicados).
    /// </summary>
    public static bool ContainsRecent(string message, int lookback = 3)
    {
        if (string.IsNullOrWhiteSpace(message)) return false;

        int startIndex = System.Math.Max(0, _messages.Count - lookback);
        for (int i = startIndex; i < _messages.Count; i++)
        {
            if (_messages[i].Equals(message, System.StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
