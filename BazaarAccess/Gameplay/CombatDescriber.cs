using System;
using System.Collections;
using System.Collections.Generic;
using BazaarAccess.Core;
using BazaarGameClient.Domain.Models.Cards;
using BazaarGameShared.Domain.Core.Types;
using BazaarGameShared.Domain.Runs;
using BazaarGameShared.Infra.Messages.CombatSimEvents;
using TheBazaar;
using UnityEngine;

namespace BazaarAccess.Gameplay;

/// <summary>
/// Narra el combate en tiempo real para accesibilidad.
/// Formato: "[Dueño]: [Item]: [Cantidad] [Efecto]. [Crítico]. [Estado]."
/// </summary>
public static class CombatDescriber
{
    // Configuración
    private const float HealthInterval = 5f;        // Segundos entre anuncios de vida
    private const float HealthThreshold = 0.25f;    // 25% cambio = anuncio inmediato
    private const float EnemyBufferDelay = 0.8f;    // Segundos para agrupar efectos del enemigo

    // Estado
    private static bool _active;
    private static float _lastHealthTime;
    private static int _lastPlayerHealth;
    private static int _lastPlayerMaxHealth;
    private static int _lastEnemyHealth;
    private static int _lastEnemyMaxHealth;
    private static string _enemyName;
    private static Coroutine _healthCoroutine;
    private static Coroutine _enemyBufferCoroutine;


    // Buffer para agrupar efectos del enemigo
    private static Dictionary<ActionType, int> _enemyEffectBuffer = new Dictionary<ActionType, int>();
    private static bool _enemyHadCrit = false;

    /// <summary>
    /// Inicia la narración del combate.
    /// </summary>
    public static void StartDescribing()
    {
        // Si ya estaba activo, detener primero para reiniciar limpiamente
        if (_active)
        {
            Plugin.Logger.LogInfo("CombatDescriber: Already active, restarting...");
            StopDescribing();
        }

        _active = true;

        // Inicializar estado COMPLETAMENTE
        _lastHealthTime = Time.time;
        _lastPlayerHealth = 0;
        _lastPlayerMaxHealth = 0;
        _lastEnemyHealth = 0;
        _lastEnemyMaxHealth = 0;
        _enemyEffectBuffer.Clear();
        _enemyHadCrit = false;

        // Obtener nombre del enemigo FRESCO
        _enemyName = GetEnemyName();

        // Capturar vida inicial
        CaptureHealthState();

        // Iniciar coroutine de anuncios periódicos
        if (Plugin.Instance != null)
        {
            _healthCoroutine = Plugin.Instance.StartCoroutine(HealthAnnouncementLoop());
        }

        Plugin.Logger.LogInfo($"CombatDescriber: Started, enemy = {_enemyName}, playerHealth = {_lastPlayerHealth}, enemyHealth = {_lastEnemyHealth}");
    }

    /// <summary>
    /// Detiene la narración del combate.
    /// </summary>
    public static void StopDescribing()
    {
        if (!_active) return;
        _active = false;

        // Detener coroutines
        if (_healthCoroutine != null && Plugin.Instance != null)
        {
            Plugin.Instance.StopCoroutine(_healthCoroutine);
            _healthCoroutine = null;
        }
        if (_enemyBufferCoroutine != null && Plugin.Instance != null)
        {
            Plugin.Instance.StopCoroutine(_enemyBufferCoroutine);
            _enemyBufferCoroutine = null;
        }

        // Limpiar estado para el próximo combate
        _enemyName = null;
        _enemyEffectBuffer.Clear();
        _enemyHadCrit = false;

        Plugin.Logger.LogInfo("CombatDescriber: Stopped");
    }

    /// <summary>
    /// Obtiene el nombre del enemigo (PvP o PvE).
    /// Solo usa el nombre de PvP si estamos realmente en un combate PvP.
    /// </summary>
    private static string GetEnemyName()
    {
        try
        {
            // Verificar si estamos en PvP combat
            var currentState = Data.CurrentState?.StateName;
            bool isPvpCombat = currentState == ERunState.PVPCombat;

            Plugin.Logger.LogInfo($"GetEnemyName: currentState={currentState}, isPvpCombat={isPvpCombat}");

            // Solo usar SimPvpOpponent si estamos realmente en PvP
            if (isPvpCombat)
            {
                var pvp = Data.SimPvpOpponent;
                if (pvp != null && !string.IsNullOrEmpty(pvp.Name))
                {
                    Plugin.Logger.LogInfo($"GetEnemyName: Using PvP opponent name: {pvp.Name}");
                    return pvp.Name;
                }
            }

            // PvE: usar "Enemy" como fallback
            Plugin.Logger.LogInfo("GetEnemyName: Using 'Enemy' (PvE)");
            return "Enemy";
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning($"GetEnemyName error: {ex.Message}");
            return "Enemy";
        }
    }

    /// <summary>
    /// Captura el estado actual de vida.
    /// </summary>
    private static void CaptureHealthState()
    {
        try
        {
            var player = Data.Run?.Player;
            if (player != null)
            {
                _lastPlayerHealth = player.GetAttributeValue(EPlayerAttributeType.Health) ?? 0;
                _lastPlayerMaxHealth = player.GetAttributeValue(EPlayerAttributeType.HealthMax) ?? 100;
            }

            var opponent = Data.Run?.Opponent;
            if (opponent != null)
            {
                opponent.Attributes.TryGetValue(EPlayerAttributeType.Health, out _lastEnemyHealth);
                opponent.Attributes.TryGetValue(EPlayerAttributeType.HealthMax, out _lastEnemyMaxHealth);
                if (_lastEnemyMaxHealth == 0) _lastEnemyMaxHealth = 100;
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError($"CaptureHealthState error: {ex.Message}");
        }
    }

    /// <summary>
    /// Handler para cuando se activa un efecto de combate.
    /// Items del jugador se anuncian inmediatamente.
    /// Items del enemigo se acumulan y anuncian juntos después de un breve delay.
    /// </summary>
    internal static void OnEffectTriggered(EffectTriggeredEvent evt)
    {
        if (!_active) return;

        // Double-check we're actually in combat (events can arrive late)
        var currentState = Data.CurrentState?.StateName;
        if (currentState != ERunState.Combat && currentState != ERunState.PVPCombat)
        {
            Plugin.Logger.LogInfo($"OnEffectTriggered: Not in combat state ({currentState}), ignoring");
            return;
        }

        try
        {
            var data = evt?.Data;
            if (data == null) return;

            // Solo procesar acciones relevantes
            if (!IsRelevantAction(data.ActionType)) return;

            var sourceCard = data.SourceCard;
            if (sourceCard == null) return;

            // Determinar si es del jugador o enemigo
            string owner = GetCardOwner(sourceCard);
            if (string.IsNullOrEmpty(owner)) return;

            bool isPlayerItem = owner == "You";
            int amount = CalculateEffectAmount(data);

            if (isPlayerItem)
            {
                // Items del jugador: anunciar inmediatamente
                string itemName = ItemReader.GetCardName(sourceCard);
                string effectDesc = GetEffectDescription(data.ActionType, amount);
                if (string.IsNullOrEmpty(effectDesc)) return;

                string message = $"{owner}: {itemName}: {effectDesc}";
                if (data.IsCrit) message += ". critical";

                TolkWrapper.Speak(message, interrupt: false);
            }
            else
            {
                // Items del enemigo: acumular en buffer
                BufferEnemyEffect(data.ActionType, amount, data.IsCrit);
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError($"OnEffectTriggered error: {ex.Message}\n{ex.StackTrace}");
        }
    }

    /// <summary>
    /// Añade un efecto del enemigo al buffer y programa el anuncio.
    /// </summary>
    private static void BufferEnemyEffect(ActionType type, int amount, bool isCrit)
    {
        // Acumular cantidad
        if (_enemyEffectBuffer.ContainsKey(type))
            _enemyEffectBuffer[type] += amount;
        else
            _enemyEffectBuffer[type] = amount;

        // Recordar si hubo crítico
        if (isCrit) _enemyHadCrit = true;

        // Reiniciar timer para flush
        if (_enemyBufferCoroutine != null && Plugin.Instance != null)
        {
            Plugin.Instance.StopCoroutine(_enemyBufferCoroutine);
        }
        if (Plugin.Instance != null)
        {
            _enemyBufferCoroutine = Plugin.Instance.StartCoroutine(FlushEnemyBufferAfterDelay());
        }
    }

    /// <summary>
    /// Espera un momento y luego anuncia los efectos acumulados del enemigo.
    /// </summary>
    private static IEnumerator FlushEnemyBufferAfterDelay()
    {
        yield return new WaitForSeconds(EnemyBufferDelay);
        FlushEnemyBuffer();
    }

    /// <summary>
    /// Anuncia todos los efectos acumulados del enemigo.
    /// </summary>
    private static void FlushEnemyBuffer()
    {
        if (_enemyEffectBuffer.Count == 0) return;

        var parts = new List<string>();

        // Ordenar efectos por importancia (daño primero, luego estados)
        var orderedEffects = new[] {
            ActionType.PlayerDamage,
            ActionType.PlayerHeal,
            ActionType.PlayerShieldApply,
            ActionType.PlayerBurnApply,
            ActionType.PlayerPoisonApply,
            ActionType.PlayerRegenApply,
            ActionType.PlayerJoyApply,
            ActionType.CardSlow,
            ActionType.CardHaste,
            ActionType.CardFreeze
        };

        foreach (var type in orderedEffects)
        {
            if (_enemyEffectBuffer.TryGetValue(type, out int amount))
            {
                string desc = GetPassiveEffectDescription(type, amount);
                if (!string.IsNullOrEmpty(desc))
                    parts.Add(desc);
            }
        }

        if (parts.Count > 0)
        {
            string message = string.Join(", ", parts);
            if (_enemyHadCrit) message += ". critical hit";
            TolkWrapper.Speak(message, interrupt: false);
        }

        // Limpiar buffer
        _enemyEffectBuffer.Clear();
        _enemyHadCrit = false;
        _enemyBufferCoroutine = null;
    }

    /// <summary>
    /// Handler para cambios de vida del jugador/enemigo.
    /// Solo usado para anuncios periódicos, no para cada cambio.
    /// </summary>
    internal static void OnPlayerHealthChanged(PlayerHealthChangedEvent evt)
    {
        if (!_active) return;

        try
        {
            var update = evt?.Update;
            if (update == null) return;

            var combatantId = evt.CombatantId;

            Plugin.Logger.LogInfo($"OnPlayerHealthChanged: Combatant={combatantId}, Amount={update.Amount}, Type={update.DamageType}");

            // Actualizar estado de vida
            bool isPlayer = combatantId == ECombatantId.Player;

            if (isPlayer)
            {
                var player = Data.Run?.Player;
                if (player != null)
                {
                    _lastPlayerHealth = player.GetAttributeValue(EPlayerAttributeType.Health) ?? _lastPlayerHealth;
                }
            }
            else
            {
                var opponent = Data.Run?.Opponent;
                if (opponent != null)
                {
                    opponent.Attributes.TryGetValue(EPlayerAttributeType.Health, out _lastEnemyHealth);
                }
            }

            // Verificar si hay cambio significativo para anuncio inmediato
            CheckForSignificantHealthChange();
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError($"OnPlayerHealthChanged error: {ex.Message}");
        }
    }

    /// <summary>
    /// Verifica si la acción es relevante para narrar.
    /// </summary>
    private static bool IsRelevantAction(ActionType type)
    {
        return type switch
        {
            // Acciones de daño/curación
            ActionType.PlayerDamage => true,
            ActionType.PlayerHeal => true,
            ActionType.PlayerShieldApply => true,

            // Efectos de estado
            ActionType.PlayerBurnApply => true,
            ActionType.PlayerPoisonApply => true,
            ActionType.PlayerRegenApply => true,
            ActionType.PlayerJoyApply => true,

            // Efectos en cartas
            ActionType.CardSlow => true,
            ActionType.CardHaste => true,
            ActionType.CardFreeze => true,

            // Ignorar todo lo demás
            _ => false
        };
    }

    /// <summary>
    /// Formatea el mensaje de efecto.
    /// Formato para items del jugador: "[You]: [Item]: [Efecto]. [Crítico]."
    /// Formato para items del enemigo: Solo el efecto recibido (ej: "5 poison")
    /// </summary>
    private static string FormatEffectMessage(CombatActionData data)
    {
        var sourceCard = data.SourceCard;
        if (sourceCard == null) return null;

        // Determinar dueño del item
        string owner = GetCardOwner(sourceCard);
        if (string.IsNullOrEmpty(owner)) return null;

        bool isPlayerItem = owner == "You";

        // Calcular cantidad de efecto
        int amount = CalculateEffectAmount(data);

        // Descripción del efecto
        string effectDesc = GetEffectDescription(data.ActionType, amount);
        if (string.IsNullOrEmpty(effectDesc)) return null;

        // Construir mensaje
        var parts = new List<string>();

        if (isPlayerItem)
        {
            // Items del jugador: formato completo
            string itemName = ItemReader.GetCardName(sourceCard);
            parts.Add($"{owner}: {itemName}: {effectDesc}");

            // Crítico
            if (data.IsCrit)
            {
                parts.Add("critical");
            }
        }
        else
        {
            // Items del enemigo: solo anunciar el efecto recibido
            // Convertir a formato pasivo (ej: "5 damage received" o "poisoned")
            string passiveEffect = GetPassiveEffectDescription(data.ActionType, amount);
            parts.Add(passiveEffect);

            // Crítico para efectos del enemigo
            if (data.IsCrit)
            {
                parts.Add("critical hit");
            }
        }

        return string.Join(". ", parts);
    }

    /// <summary>
    /// Obtiene la descripción pasiva del efecto (cuando el enemigo te lo aplica).
    /// </summary>
    private static string GetPassiveEffectDescription(ActionType type, int amount)
    {
        return type switch
        {
            ActionType.PlayerDamage => $"{amount} damage received",
            ActionType.PlayerHeal => $"{amount} healed",
            ActionType.PlayerShieldApply => $"{amount} shield gained",
            ActionType.PlayerBurnApply => $"burned for {amount}",
            ActionType.PlayerPoisonApply => $"poisoned for {amount}",
            ActionType.PlayerRegenApply => "regen applied",
            ActionType.PlayerJoyApply => "joy applied",
            ActionType.CardSlow => "slowed",
            ActionType.CardHaste => "hasted",
            ActionType.CardFreeze => "frozen",
            _ => null
        };
    }

    /// <summary>
    /// Determina el dueño de una carta (jugador o enemigo).
    /// </summary>
    private static string GetCardOwner(Card card)
    {
        if (card == null) return null;

        try
        {
            // Verificar si la carta pertenece al jugador
            var bm = Singleton<BoardManager>.Instance;
            if (bm == null) return null;

            // Buscar en sockets del jugador
            if (bm.playerItemSockets != null)
            {
                foreach (var socket in bm.playerItemSockets)
                {
                    if (socket?.CardController?.CardData == card)
                        return "You";
                }
            }

            if (bm.playerSkillSockets != null)
            {
                foreach (var socket in bm.playerSkillSockets)
                {
                    if (socket?.CardController?.CardData == card)
                        return "You";
                }
            }

            // Buscar en sockets del oponente
            if (bm.opponentItemSockets != null)
            {
                foreach (var socket in bm.opponentItemSockets)
                {
                    if (socket?.CardController?.CardData == card)
                        return _enemyName;
                }
            }

            if (bm.opponentSkillSockets != null)
            {
                foreach (var socket in bm.opponentSkillSockets)
                {
                    if (socket?.CardController?.CardData == card)
                        return _enemyName;
                }
            }

            // No encontrado, asumir jugador
            return "You";
        }
        catch
        {
            return "You";
        }
    }

    /// <summary>
    /// Calcula la cantidad del efecto basándose en los atributos de la carta.
    /// No usar HealthBefore/HealthAfter porque pueden incluir escudo combinado con vida.
    /// </summary>
    private static int CalculateEffectAmount(CombatActionData data)
    {
        var card = data.SourceCard;
        if (card == null) return 0;

        // Usar atributos de la carta directamente para cada tipo de acción
        int amount = data.ActionType switch
        {
            ActionType.PlayerDamage => card.GetAttributeValue(ECardAttributeType.DamageAmount) ?? 0,
            ActionType.PlayerHeal => card.GetAttributeValue(ECardAttributeType.HealAmount) ?? 0,
            ActionType.PlayerShieldApply => card.GetAttributeValue(ECardAttributeType.ShieldApplyAmount) ?? 0,
            ActionType.PlayerBurnApply => card.GetAttributeValue(ECardAttributeType.BurnApplyAmount) ?? 0,
            ActionType.PlayerPoisonApply => card.GetAttributeValue(ECardAttributeType.PoisonApplyAmount) ?? 0,
            _ => 0
        };

        // Si no encontramos el atributo y tenemos datos de vida (solo para daño/curación, NO escudo)
        if (amount == 0 && (data.ActionType == ActionType.PlayerDamage || data.ActionType == ActionType.PlayerHeal))
        {
            if (data.HealthBefore > 0 || data.HealthAfter > 0)
            {
                amount = (int)Math.Abs(data.HealthBefore - data.HealthAfter);
            }
        }

        return amount;
    }

    /// <summary>
    /// Obtiene la descripción del efecto.
    /// </summary>
    private static string GetEffectDescription(ActionType type, int amount)
    {
        return type switch
        {
            ActionType.PlayerDamage => $"{amount} damage",
            ActionType.PlayerHeal => $"{amount} heal",
            ActionType.PlayerShieldApply => $"{amount} shield",
            ActionType.PlayerBurnApply => $"{amount} burn",
            ActionType.PlayerPoisonApply => $"{amount} poison",
            ActionType.PlayerRegenApply => "regen",
            ActionType.PlayerJoyApply => "joy",
            ActionType.CardSlow => "slow",
            ActionType.CardHaste => "haste",
            ActionType.CardFreeze => "freeze",
            _ => null
        };
    }

    /// <summary>
    /// Obtiene el nombre del efecto de estado (si aplica).
    /// </summary>
    private static string GetStatusEffectName(ActionType type)
    {
        // Solo devolver para efectos que son adicionales al daño
        return type switch
        {
            ActionType.CardSlow => "slow",
            ActionType.CardHaste => "haste",
            ActionType.CardFreeze => "freeze",
            _ => null
        };
    }

    /// <summary>
    /// Verifica si hubo un cambio significativo de vida para anunciar.
    /// </summary>
    private static void CheckForSignificantHealthChange()
    {
        try
        {
            var player = Data.Run?.Player;
            var opponent = Data.Run?.Opponent;

            int currentPlayerHealth = player?.GetAttributeValue(EPlayerAttributeType.Health) ?? 0;
            int currentEnemyHealth = 0;
            opponent?.Attributes.TryGetValue(EPlayerAttributeType.Health, out currentEnemyHealth);

            // Calcular cambio porcentual
            float playerChange = _lastPlayerMaxHealth > 0
                ? Math.Abs(currentPlayerHealth - _lastPlayerHealth) / (float)_lastPlayerMaxHealth
                : 0;

            float enemyChange = _lastEnemyMaxHealth > 0
                ? Math.Abs(currentEnemyHealth - _lastEnemyHealth) / (float)_lastEnemyMaxHealth
                : 0;

            // Si cambio significativo, anunciar
            if (playerChange >= HealthThreshold || enemyChange >= HealthThreshold)
            {
                AnnounceHealth();
                _lastHealthTime = Time.time;
                _lastPlayerHealth = currentPlayerHealth;
                _lastEnemyHealth = currentEnemyHealth;
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError($"CheckForSignificantHealthChange error: {ex.Message}");
        }
    }

    /// <summary>
    /// Coroutine para anuncios periódicos de vida.
    /// </summary>
    private static IEnumerator HealthAnnouncementLoop()
    {
        Plugin.Logger.LogInfo("CombatDescriber: HealthAnnouncementLoop started");

        // Primer anuncio después de 2 segundos
        yield return new WaitForSeconds(2f);
        if (_active)
        {
            Plugin.Logger.LogInfo("CombatDescriber: First health announcement");
            AnnounceHealth();
        }

        while (_active)
        {
            yield return new WaitForSeconds(HealthInterval);

            if (_active)
            {
                Plugin.Logger.LogInfo("CombatDescriber: Periodic health announcement");
                AnnounceHealth();
            }
        }

        Plugin.Logger.LogInfo("CombatDescriber: HealthAnnouncementLoop ended");
    }

    /// <summary>
    /// Anuncia el estado de vida actual.
    /// Formato: "You: [vida] health, [escudo] shield. [Enemy]: [vida] health."
    /// </summary>
    private static void AnnounceHealth()
    {
        try
        {
            var player = Data.Run?.Player;
            var opponent = Data.Run?.Opponent;

            var parts = new List<string>();

            // Vida del jugador
            if (player != null)
            {
                int health = player.GetAttributeValue(EPlayerAttributeType.Health) ?? 0;
                int shield = player.GetAttributeValue(EPlayerAttributeType.Shield) ?? 0;

                if (shield > 0)
                    parts.Add($"You: {health} health, {shield} shield");
                else
                    parts.Add($"You: {health} health");

                _lastPlayerHealth = health;
            }

            // Vida del enemigo
            if (opponent != null)
            {
                opponent.Attributes.TryGetValue(EPlayerAttributeType.Health, out int enemyHealth);
                opponent.Attributes.TryGetValue(EPlayerAttributeType.Shield, out int enemyShield);

                if (enemyShield > 0)
                    parts.Add($"{_enemyName}: {enemyHealth} health, {enemyShield} shield");
                else
                    parts.Add($"{_enemyName}: {enemyHealth} health");

                _lastEnemyHealth = enemyHealth;
            }

            if (parts.Count > 0)
            {
                TolkWrapper.Speak(string.Join(". ", parts), interrupt: false);
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError($"AnnounceHealth error: {ex.Message}");
        }
    }
}
