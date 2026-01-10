using BazaarAccess.Core;
using UnityEngine;
using UnityEngine.UI;
using System.Reflection;

namespace BazaarAccess.UI.Login;

public class CreateAccountTermsUI : LoginBaseUI
{
    private readonly object _view;

    public override string UIName => "Create Account - Terms";

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
                () => $"Terms of Service: {(tosToggle.isOn ? "accepted" : "not accepted")}",
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
                () => $"End User License Agreement: {(eulaToggle.isOn ? "accepted" : "not accepted")}",
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
                () => $"Marketing emails: {(promoToggle.isOn ? "on" : "off")}",
                () => ToggleAndAnnounce(promoToggle),
                null,
                (dir) => ToggleAndAnnounce(promoToggle)
            );
        }

        // Continue button
        AddBazaarButton(_view, "continueButton", "Continue");
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
        TolkWrapper.Speak(toggle.isOn ? "accepted" : "not accepted");
    }

    protected override void OnBack()
    {
        // No back from terms
    }
}
