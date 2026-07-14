using BazaarAccess.Core;
using UnityEngine;
using UnityEngine.UI;
using System.Reflection;

namespace BazaarAccess.UI.Login;

public class CreateAccountTermsUI : LoginBaseUI
{
    private readonly object _view;

    public override string UIName => Loc.T("ui.login.create_account_terms_title");

    public CreateAccountTermsUI(Transform root, object view) : base(root)
    {
        _view = view;
        Initialize();
    }

    protected override void BuildMenu()
    {
        // ToS toggle
        var tosToggle = GetViewToggle("tosToggle");
        if (tosToggle != null)
        {
            Menu.AddOption(
                () => $"{Loc.T("ui.login.tos_label")}: {(tosToggle.isOn ? Loc.T("ui.login.accepted") : Loc.T("ui.login.not_accepted"))}",
                () => ToggleAndAnnounce(tosToggle),
                null,
                (dir) => ToggleAndAnnounce(tosToggle)
            );
        }

        // EULA toggle
        var eulaToggle = GetViewToggle("eulaToggle");
        if (eulaToggle != null)
        {
            Menu.AddOption(
                () => $"{Loc.T("ui.login.eula_label")}: {(eulaToggle.isOn ? Loc.T("ui.login.accepted") : Loc.T("ui.login.not_accepted"))}",
                () => ToggleAndAnnounce(eulaToggle),
                null,
                (dir) => ToggleAndAnnounce(eulaToggle)
            );
        }

        // Promo toggle (optional)
        var promoToggle = GetViewToggle("promoToggle");
        if (promoToggle != null)
        {
            Menu.AddOption(
                () => $"{Loc.T("ui.login.marketing_emails_label")}: {(promoToggle.isOn ? Loc.T("ui.on") : Loc.T("ui.off"))}",
                () => ToggleAndAnnounce(promoToggle),
                null,
                (dir) => ToggleAndAnnounce(promoToggle)
            );
        }

        // Continue button
        AddBazaarButton(_view, "continueButton", Loc.T("ui.continue"));
    }

    private Toggle GetViewToggle(string fieldName)
    {
        var field = _view.GetType().GetField(fieldName,
            BindingFlags.NonPublic | BindingFlags.Instance);
        return field?.GetValue(_view) as Toggle;
    }

    private void ToggleAndAnnounce(Toggle toggle)
    {
        toggle.isOn = !toggle.isOn;
        TolkWrapper.Speak(toggle.isOn ? Loc.T("ui.login.accepted") : Loc.T("ui.login.not_accepted"));
    }

    protected override void OnBack()
    {
        // No back from terms
    }
}
