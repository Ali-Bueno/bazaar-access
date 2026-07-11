using System;
using System.Reflection;
using BazaarGameClient.Domain.Models;
using BazaarGameClient.Domain.Models.Cards;
using TheBazaar;
using TheBazaar.AppFramework;
using UnityEngine;
using UnityEngine.EventSystems;

namespace BazaarAccess.Gameplay.Navigation;

/// <summary>
/// Handles visual selection/feedback for cards and skills.
/// Triggers hover effects, sounds, and tooltip display on the selected controller.
/// </summary>
public static class VisualSelector
{
    private static int _syntheticPointerNonce;

    /// <summary>
    /// Finds the CardController for a card and triggers visual selection (hover, sound, tooltip).
    /// </summary>
    public static void SelectCard(Card card)
    {
        if (card == null) return;

        try
        {
            var bm = GetBoardManager();
            if (bm == null) return;

            var controller = FindCardController(card, bm);
            if (controller == null) return;

            ResetAllCardSelections(bm);
            ApplySelection(controller);
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning($"VisualSelector.SelectCard error: {ex.Message}");
        }
    }

    /// <summary>
    /// Triggers visual selection on a known CardController (hover, sound, tooltip).
    /// </summary>
    public static void SelectSocket(CardController controller)
    {
        if (controller == null) return;

        try
        {
            var bm = GetBoardManager();
            if (bm != null)
                ResetAllCardSelections(bm);

            ApplySelection(controller);
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning($"VisualSelector.SelectSocket error: {ex.Message}");
        }
    }

    /// <summary>
    /// Selects a hero skill socket by index, triggering visual feedback.
    /// </summary>
    public static void SelectHeroSkill(int skillIndex)
    {
        try
        {
            var bm = GetBoardManager();
            if (bm?.playerSkillSockets == null) return;

            ResetAllCardSelections(bm);

            if (skillIndex >= 0 && skillIndex < bm.playerSkillSockets.Length)
            {
                var controller = bm.playerSkillSockets[skillIndex]?.CardController;
                if (controller != null)
                {
                    ApplySelection(controller);
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning($"VisualSelector.SelectHeroSkill error: {ex.Message}");
        }
    }

    /// <summary>
    /// Resets the hover state of all cards on the board.
    /// </summary>
    public static void ResetAll()
    {
        try
        {
            var bm = GetBoardManager();
            if (bm != null)
                ResetAllCardSelections(bm);
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning($"VisualSelector.ResetAll error: {ex.Message}");
        }
    }

    /// <summary>
    /// Finds the CardController for a card using the game's lookup, with socket fallback.
    /// </summary>
    public static CardController FindCardController(Card card, BoardManager bm)
    {
        if (card == null) return null;

        try
        {
            var lookup = Data.CardAndSkillLookup;
            if (lookup != null)
            {
                var controller = lookup.GetCardController(card);
                if (controller != null) return controller;
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning($"FindCardController lookup failed: {ex.Message}");
        }

        // Fallback: search sockets manually
        if (bm == null) return null;

        if (bm.opponentItemSockets != null)
        {
            foreach (var socket in bm.opponentItemSockets)
            {
                if (socket?.CardController?.CardData == card)
                    return socket.CardController;
            }
        }

        if (bm.opponentSkillSockets != null)
        {
            foreach (var socket in bm.opponentSkillSockets)
            {
                if (socket?.CardController?.CardData == card)
                    return socket.CardController;
            }
        }

        if (bm.playerItemSockets != null)
        {
            foreach (var socket in bm.playerItemSockets)
            {
                if (socket?.CardController?.CardData == card)
                    return socket.CardController;
            }
        }

        return null;
    }

    /// <summary>
    /// Applies hover selection to a controller: pointer enter, hover move, and sound.
    /// </summary>
    private static void ApplySelection(CardController controller)
    {
        if (controller is EncounterController encounterController)
        {
            ApplyEncounterSelection(encounterController);
            return;
        }

        var eventSystem = EventSystem.current;
        Vector2 pointerPosition = GetSyntheticPointerPosition();
        var pointerData = new PointerEventData(eventSystem)
        {
            position = pointerPosition
        };

        controller.OnPointerEnter(pointerData);
        controller.HoverMove();
        TriggerHoverSound(controller);
    }

    private static void ApplyEncounterSelection(EncounterController controller)
    {
        try
        {
            if (controller == null || Data.TooltipParentComponent == null)
                return;

            SetCursorOverCard(controller, true);

            var tooltipData = controller.GetTooltipData();
            if (tooltipData != null)
            {
                Data.TooltipParentComponent.ShowCardTooltipController(
                    controller.transform,
                    GetTooltipOffset(controller),
                    tooltipData);
            }

            controller.HoverMove();
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning($"ApplyEncounterSelection error: {ex.Message}");
        }
    }

    /// <summary>
    /// EncounterController only starts hover when the pointer position changes.
    /// Use a non-zero screen position that keeps changing so revisiting the same
    /// encounter still counts as mouse movement.
    /// </summary>
    private static Vector2 GetSyntheticPointerPosition()
    {
        int nonce = _syntheticPointerNonce++;
        float jitterX = (nonce % 29) + 1f;
        float jitterY = ((nonce / 29) % 11) + 1f;

        try
        {
            Vector3 mousePosition = Input.mousePosition;
            if (mousePosition.x > 0f || mousePosition.y > 0f)
                return new Vector2(mousePosition.x + jitterX, mousePosition.y + jitterY);
        }
        catch
        {
            // Fall through to center-screen fallback.
        }

        float x = Screen.width > 0 ? Screen.width * 0.5f : 1f;
        float y = Screen.height > 0 ? Screen.height * 0.5f : 1f;
        return new Vector2(x + jitterX, y + jitterY);
    }

    /// <summary>
    /// Plays the hover sound for controller types that don't play it in HoverMove().
    /// EncounterController already plays sound in its HoverMove() override.
    /// </summary>
    private static void TriggerHoverSound(CardController controller)
    {
        try
        {
            if (controller is ItemController itemController)
            {
                Vector3 position = controller.transform.position;
                var handlerField = typeof(ItemController).GetField("soundCardHandler",
                    BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

                if (handlerField != null)
                {
                    var handler = handlerField.GetValue(itemController);
                    if (handler != null)
                    {
                        var method = handler.GetType().GetMethod("SoundCardRaise",
                            BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                        method?.Invoke(handler, new object[] { position });
                    }
                }
            }
            // SkillController doesn't have a specific hover sound in vanilla
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning($"TriggerHoverSound error: {ex.Message}");
        }
    }

    /// <summary>
    /// Resets the hover state of all card sockets on the board.
    /// </summary>
    private static void ResetAllCardSelections(BoardManager bm)
    {
        try
        {
            if (bm.playerItemSockets != null)
            {
                foreach (var socket in bm.playerItemSockets)
                {
                    socket?.CardController?.ResetPosition(hideTooltips: true);
                    SetCursorOverCard(socket?.CardController, false);
                }
            }

            if (bm.opponentItemSockets != null)
            {
                foreach (var socket in bm.opponentItemSockets)
                {
                    socket?.CardController?.ResetPosition(hideTooltips: true);
                    SetCursorOverCard(socket?.CardController, false);
                }
            }

            if (bm.playerSkillSockets != null)
            {
                foreach (var socket in bm.playerSkillSockets)
                {
                    socket?.CardController?.ResetPosition(hideTooltips: true);
                    SetCursorOverCard(socket?.CardController, false);
                }
            }

            if (bm.opponentSkillSockets != null)
            {
                foreach (var socket in bm.opponentSkillSockets)
                {
                    socket?.CardController?.ResetPosition(hideTooltips: true);
                    SetCursorOverCard(socket?.CardController, false);
                }
            }

            if (bm.playerStorageSockets != null)
            {
                foreach (var socket in bm.playerStorageSockets)
                {
                    socket?.CardController?.ResetPosition(hideTooltips: true);
                    SetCursorOverCard(socket?.CardController, false);
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning($"ResetAllCardSelections error: {ex.Message}");
        }
    }

    private static BoardManager GetBoardManager()
    {
        try { return Singleton<BoardManager>.Instance; }
        catch { return null; }
    }

    private static Vector3 GetTooltipOffset(CardController controller)
    {
        try
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
            PropertyInfo property = typeof(CardController).GetProperty("TooltipOffset", flags);
            if (property?.GetValue(controller) is Vector3 offset)
                return offset;

            FieldInfo field = typeof(CardController).GetField("tooltipOffset", flags);
            if (field?.GetValue(controller) is Vector3 fieldOffset)
                return fieldOffset;
        }
        catch
        {
            // Fall back to zero offset below.
        }

        return Vector3.zero;
    }

    private static void SetCursorOverCard(CardController controller, bool isOverCard)
    {
        try
        {
            if (controller == null)
                return;

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
            PropertyInfo property = typeof(CardController).GetProperty("IsCursorOverCard", flags);
            MethodInfo setter = property?.GetSetMethod(nonPublic: true);
            setter?.Invoke(controller, new object[] { isOverCard });
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning($"VisualSelector.SetCursorOverCard error: {ex.Message}");
        }
    }
}
