using System.Collections.Generic;
using System.Linq;
using BazaarAccess.Accessibility;
using BazaarAccess.Core;
using HarmonyLib;
using TheBazaar.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BazaarAccess.UI;

/// <summary>
/// Gem and chest purchase panels (PurchaseGemsView / PurchaseChestsView): one option per pack
/// ("1000 Gems, +10%, $9.99"); Enter presses the pack like a click (the platform still asks to confirm the payment).
/// </summary>
public class PurchasePanelUI : BaseUI
{
    private readonly MonoBehaviour _view;

    public PurchasePanelUI(MonoBehaviour view) : base(view.transform, buildMenuNow: false)
    {
        _view = view;
        Initialize();
    }

    public override string UIName
    {
        get
        {
            // BaseUI reads the name before this class's fields are set.
            if (_view == null) return Loc.T("ui.purchase.title");
            var title = AccessTools.Field(AccessTools.TypeByName("TheBazaar.PurchaseBaseView"), "PurchaseTitle")?.GetValue(_view) as TMP_Text;
            string text = title != null && title.isActiveAndEnabled ? TextHelper.CleanText(title.text) : null;
            return string.IsNullOrWhiteSpace(text) ? Loc.T("ui.purchase.title") : text;
        }
    }

    protected override void BuildMenu()
    {
        // Packs are spawned after the listings load: read them when the menu is navigated.
        foreach (var pack in Root.GetComponentsInChildren<BundlePurchaseInfoContainer>(false))
        {
            var current = pack;
            Menu.AddOption(
                () => PackLabel(current, _view.GetType().Name == "PurchaseGemsView"),
                () => current.PrimaryButton?.onClick.Invoke(),
                () => current != null && current.isActiveAndEnabled);
        }
    }

    public override void OnFocus()
    {
        // The pack list fills in asynchronously after the view opens.
        Menu.Clear();
        BuildMenu();
        base.OnFocus();
        if (Menu.OptionCount == 0) Plugin.Instance.StartCoroutine(RebuildWhenLoaded());
    }

    private System.Collections.IEnumerator RebuildWhenLoaded()
    {
        const float pollSeconds = 0.5f;
        const int maxPolls = 10; // the listings arrive from the server within a few seconds
        for (int i = 0; i < maxPolls && Menu.OptionCount == 0; i++)
        {
            yield return new WaitForSecondsRealtime(pollSeconds);
            if (!IsValid()) yield break;
            BuildMenu();
        }
        if (Menu.OptionCount > 0) Menu.StartReading(announceMenuName: false);
    }

    // "525 Gems, $4.99, 110%, Bonus, first purchase bonus ×2": amount and price first, then the extras.
    private static string PackLabel(BundlePurchaseInfoContainer pack, bool gems)
    {
        string quantity = FieldText(pack, "_quantityText");
        string descriptor = FieldText(pack, "_quantityDescriptorText");
        string price = FieldText(pack, "_priceText");

        var parts = new List<string>();
        // Gem packs show the amount next to a gem icon, without a word.
        if (quantity != null)
            parts.Add(descriptor != null ? $"{quantity} {descriptor}" : gems ? Loc.T("screen.mainmenu.gems", quantity) : quantity);
        if (price != null) parts.Add(price);

        var used = new HashSet<string> { quantity, descriptor, price };
        string pending = null;
        foreach (var text in pack.GetComponentsInChildren<TMP_Text>(false)
                     .Where(t => t.isActiveAndEnabled)
                     .Select(t => TextHelper.CleanText(t.text)?.Trim())
                     .Where(t => !string.IsNullOrEmpty(t) && !used.Contains(t)))
        {
            // The multiplier is drawn as "×" and "2" in separate labels.
            if (text == "×" || text == "x") { pending = text; continue; }
            parts.Add(pending != null ? pending + text : text);
            pending = null;
        }
        return string.Join(", ", parts.Distinct());
    }

    private static string FieldText(BundlePurchaseInfoContainer pack, string field)
    {
        var text = AccessTools.Field(typeof(BundlePurchaseInfoContainer), field)?.GetValue(pack) as TMP_Text;
        string value = text != null && text.isActiveAndEnabled ? TextHelper.CleanText(text.text)?.Trim() : null;
        return string.IsNullOrEmpty(value) ? null : value;
    }

    protected override void OnBack()
    {
        AccessTools.Method(_view.GetType(), "Close")?.Invoke(_view, null);
    }
}

/// <summary>
/// "Not enough gems / buy gems?" prompt (GemPurchasePromptComponent): its text, then Confirm and Cancel.
/// </summary>
public class GemPromptUI : BaseUI
{
    private readonly MonoBehaviour _prompt;

    public GemPromptUI(MonoBehaviour prompt) : base(prompt.transform, buildMenuNow: false)
    {
        _prompt = prompt;
        Initialize();
    }

    public override string UIName => FieldText("_title") ?? Loc.T("ui.purchase.title");

    protected override void BuildMenu()
    {
        // The texts are set after the localization loads, so every label is read live.
        Menu.AddOption(() => FieldText("_description") ?? string.Empty, () => { });
        Menu.AddOption(() => FieldText("_confirm") ?? Loc.T("ui.purchase.confirm"), () => Invoke("Confirm"));
        Menu.AddOption(() => FieldText("_cancel") ?? Loc.T("action.cancel"), () => Invoke("Close"));
    }

    public override void OnFocus()
    {
        Plugin.Instance.StartCoroutine(ReadAfterTextLoads());
    }

    private System.Collections.IEnumerator ReadAfterTextLoads()
    {
        const float textLoadDelaySeconds = 0.5f; // OnEnable fills the labels asynchronously
        yield return new WaitForSecondsRealtime(textLoadDelaySeconds);
        // The menu name was fixed before the texts loaded: read the prompt's own title and message.
        string text = string.Join(". ", new[] { FieldText("_title"), FieldText("_description") }.Where(t => t != null));
        TolkWrapper.Speak(string.IsNullOrEmpty(text) ? UIName : text);
    }

    protected override void OnBack() => Invoke("Close");

    private void Invoke(string method) => AccessTools.Method(_prompt.GetType(), method)?.Invoke(_prompt, null);

    private string FieldText(string field)
    {
        if (_prompt == null) return null; // BaseUI reads the name before this class's fields are set
        var text = AccessTools.Field(_prompt.GetType(), field)?.GetValue(_prompt) as TMP_Text;
        string value = text != null ? TextHelper.CleanText(text.text) : null;
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}

/// <summary>
/// Store purchase confirmation (ConfirmationComponent: "Buy 10 chests", gem price): Confirm or Cancel.
/// </summary>
public class PurchaseConfirmationUI : BaseUI
{
    private readonly MonoBehaviour _popup;

    public PurchaseConfirmationUI(MonoBehaviour popup) : base(popup.transform, buildMenuNow: false)
    {
        _popup = popup;
        Initialize();
    }

    public override string UIName => Loc.T("ui.purchase.title");

    protected override void BuildMenu()
    {
        // Title and price are set right after the popup appears, so they are read live.
        Menu.AddOption(() => Summary(), () => { });
        Menu.AddOption(() => ButtonText("_confirmButton") ?? Loc.T("ui.purchase.confirm"), () =>
        {
            int gemsBefore = PurchaseAnnouncer.CurrentGems();
            Invoke("Confirm");
            PurchaseAnnouncer.AnnounceWhenGemsChange(gemsBefore);
        });
        Menu.AddOption(() => ButtonText("_cancelButton") ?? Loc.T("action.cancel"), () => Invoke("Close"));
    }

    public override void OnFocus()
    {
        Plugin.Instance.StartCoroutine(ReadAfterSetup());
    }

    private System.Collections.IEnumerator ReadAfterSetup()
    {
        const float setupDelaySeconds = 0.3f; // SetTitle / SetPrice run after the node spawns
        yield return new WaitForSecondsRealtime(setupDelaySeconds);
        TolkWrapper.Speak(Summary());
    }

    private string Summary()
    {
        string title = FieldText("_itemTitle");
        string price = FieldText("_itemPrice");
        if (price != null) price = Loc.T("screen.mainmenu.gems", price);
        return string.Join(". ", new[] { title, price }.Where(t => !string.IsNullOrEmpty(t)));
    }

    protected override void OnBack() => Invoke("Close");

    private void Invoke(string method) => AccessTools.Method(_popup.GetType(), method)?.Invoke(_popup, null);

    private string FieldText(string field)
    {
        if (_popup == null) return null; // BaseUI reads the name before this class's fields are set
        var text = AccessTools.Field(_popup.GetType(), field)?.GetValue(_popup) as TMP_Text;
        string value = text != null ? TextHelper.CleanText(text.text) : null;
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private string ButtonText(string field)
    {
        if (_popup == null) return null;
        var button = AccessTools.Field(_popup.GetType(), field)?.GetValue(_popup) as Component;
        string value = button != null ? TextHelper.CleanText(button.GetComponentInChildren<TMP_Text>(true)?.text) : null;
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}

/// <summary>
/// The game confirms store purchases only with a sound: announce the result once the wallet changes.
/// </summary>
public static class PurchaseAnnouncer
{
    private const float PollSeconds = 0.25f;
    private const float TimeoutSeconds = 15f; // the purchase is a server round trip

    public static int CurrentGems() => TheBazaar.ClientCache.Wallet.HasData ? TheBazaar.ClientCache.Wallet.Value.Gems : 0;

    public static void AnnounceWhenGemsChange(int gemsBefore) => Plugin.Instance.StartCoroutine(Wait(gemsBefore));

    /// <summary>For purchases the game reports as done (gem packs): read the new balance once it is applied.</summary>
    public static void AnnounceDone() => Plugin.Instance.StartCoroutine(AnnounceDoneAfterApply());

    private static System.Collections.IEnumerator AnnounceDoneAfterApply()
    {
        const float applyDelaySeconds = 1f; // the wallet delta is applied right after the success callback
        yield return new WaitForSecondsRealtime(applyDelaySeconds);
        string text = Loc.T("ui.purchase.done", CurrentGems());
        MessageBuffer.Add(text);
        TolkWrapper.Speak(text, interrupt: false);
    }

    private static System.Collections.IEnumerator Wait(int gemsBefore)
    {
        for (float waited = 0f; waited < TimeoutSeconds; waited += PollSeconds)
        {
            yield return new WaitForSecondsRealtime(PollSeconds);
            int gems = CurrentGems();
            if (gems == gemsBefore) continue;

            string text = Loc.T("ui.purchase.done", gems);
            MessageBuffer.Add(text);
            TolkWrapper.Speak(text, interrupt: false);
            yield break;
        }
        TolkWrapper.Speak(Loc.T("ui.purchase.not_confirmed"), interrupt: false);
    }
}

[HarmonyLib.HarmonyPatch]
public static class GemPurchaseSuccessPatch
{
    static System.Reflection.MethodBase TargetMethod()
        => AccessTools.Method(AccessTools.TypeByName("TheBazaar.UI.PurchaseGemsView"), "OnPurchaseSuccess");

    [HarmonyLib.HarmonyPostfix]
    public static void Postfix() => PurchaseAnnouncer.AnnounceDone();
}
