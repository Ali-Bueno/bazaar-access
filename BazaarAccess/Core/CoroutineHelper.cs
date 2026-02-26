using System.Collections;
using UnityEngine;

namespace BazaarAccess.Core;

/// <summary>
/// Safe coroutine management utilities to prevent null-reference issues.
/// </summary>
public static class CoroutineHelper
{
    /// <summary>
    /// Stops a coroutine safely, checking for null Plugin instance.
    /// Sets the reference to null after stopping.
    /// </summary>
    public static void StopSafe(ref Coroutine coroutine)
    {
        if (coroutine != null && Plugin.Instance != null)
        {
            Plugin.Instance.StopCoroutine(coroutine);
            coroutine = null;
        }
    }

    /// <summary>
    /// Stops any existing coroutine and starts a new one safely.
    /// Returns the new coroutine reference.
    /// </summary>
    public static Coroutine StartSafe(ref Coroutine coroutine, IEnumerator routine)
    {
        StopSafe(ref coroutine);
        if (Plugin.Instance != null)
            coroutine = Plugin.Instance.StartCoroutine(routine);
        return coroutine;
    }
}
