using System;
using System.Linq;
using System.Reflection;
using BazaarAccess.Core;
using BazaarAccess.Gameplay.CardReading;
using BazaarGameClient.Domain.Models.Cards;
using HarmonyLib;
using TheBazaar;

namespace BazaarAccess.Gameplay.ControllerFocus;

/// <summary>
/// Narrates the game's controller item-move session (ItemMoveSession): pick up, where the item
/// would land (board neighbours, stash slot, sell price, altar), place and cancel.
/// </summary>
[HarmonyPatch]
public static class MoveSessionAnnouncer
{
    private static readonly Type SessionType = AccessTools.TypeByName("TheBazaar.Inputs.Navigation.ItemMoveSession");

    static bool Prepare() => SessionType != null;

    static MethodBase[] TargetMethods() => new[]
    {
        AccessTools.Method(SessionType, "Pickup"),
        AccessTools.Method(SessionType, "Move"),
        AccessTools.Method(SessionType, "Up"),
        AccessTools.Method(SessionType, "Down"),
    };

    [HarmonyPostfix]
    public static void Postfix(object __instance, MethodBase __originalMethod)
    {
        if (!GamepadNavigator.GameNavigationActive) return;
        var item = Traverse.Create(__instance).Field("_item").GetValue() as ItemController;
        if (item?.CardData == null) return;

        string where = DescribeTarget(__instance, item);
        TolkWrapper.Speak(__originalMethod.Name == "Pickup"
            ? Loc.T("focus.move.holding", ItemReader.GetCardName(item.CardData), where)
            : where);
    }

    private static string DescribeTarget(object session, ItemController item)
    {
        var traverse = Traverse.Create(session);
        string area = traverse.Field("_area").GetValue()?.ToString();
        switch (area)
        {
            case "Hand":
                return Loc.T("focus.move.board", DescribeNeighbours(item, traverse.Field("_handSlot").GetValue<int>()));
            case "Stash":
                return Loc.T("focus.move.stash_slot", traverse.Field("_stashSlot").GetValue<int>() + 1);
            case "Sell":
                return Loc.T("focus.move.sell", CardProperties.GetSellPrice(item.CardData));
            case "Toy":
                return Loc.T("focus.move.to_stash");
            case "Pedestal":
                return Loc.T("focus.move.altar");
            default:
                return area;
        }
    }

    // Uses the same wording as the mod's keyboard reorder ("between X and Y", "left edge"...).
    private static string DescribeNeighbours(ItemController moved, int leftSlot)
    {
        var sockets = Singleton<BoardManager>.Instance?.playerItemSockets;
        if (sockets == null) return string.Empty;

        int size = (int)moved.CardData.Size;
        string left = NameAtSlot(sockets, leftSlot - 1, moved.CardData);
        string right = NameAtSlot(sockets, leftSlot + size, moved.CardData);
        bool atLeftEdge = leftSlot <= 0;
        bool atRightEdge = leftSlot + size >= sockets.Length;

        if (left != null && right != null) return Loc.T("action.reorder.between", left, right);
        if (atLeftEdge) return right != null ? Loc.T("action.reorder.left.edge.before", right) : Loc.T("action.reorder.left.edge");
        if (atRightEdge) return left != null ? Loc.T("action.reorder.right.edge.after", left) : Loc.T("action.reorder.right.edge");
        if (left != null) return Loc.T("action.reorder.after", left);
        if (right != null) return Loc.T("action.reorder.before", right);
        return Loc.T("focus.move.slot", leftSlot + 1, sockets.Length);
    }

    private static string NameAtSlot(ItemSocketController[] sockets, int slot, Card moved)
    {
        if (slot < 0 || slot >= sockets.Length) return null;

        var hand = Data.Run?.Player?.Hand?.GetItemsAsEnumerable().OfType<ItemCard>();
        if (hand == null) return null;

        foreach (var card in hand)
        {
            if (card == moved || !card.LeftSocketId.HasValue) continue;
            int start = Array.FindIndex(sockets, s => s != null && s.SocketNumber == card.LeftSocketId.Value);
            if (start >= 0 && slot >= start && slot < start + (int)card.Size)
                return ItemReader.GetCardName(card);
        }
        return null;
    }
}

/// <summary>
/// Confirms the end of a controller item move (placed or cancelled).
/// </summary>
[HarmonyPatch]
public static class MoveSessionEndAnnouncer
{
    private static readonly Type SessionType = AccessTools.TypeByName("TheBazaar.Inputs.Navigation.ItemMoveSession");

    static bool Prepare() => SessionType != null;

    static MethodBase[] TargetMethods() => new[]
    {
        AccessTools.Method(SessionType, "Place"),
        AccessTools.Method(SessionType, "Cancel"),
    };

    [HarmonyPrefix]
    public static void Prefix(object __instance, out bool __state)
        => __state = Traverse.Create(__instance).Property("Holding").GetValue<bool>();

    [HarmonyPostfix]
    public static void Postfix(MethodBase __originalMethod, bool __state)
    {
        if (!__state || !GamepadNavigator.GameNavigationActive) return;
        TolkWrapper.Speak(Loc.T(__originalMethod.Name == "Place" ? "focus.move.placed" : "focus.move.cancelled"));
    }
}
