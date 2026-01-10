using BazaarAccess.Core;
using TMPro;
using UnityEngine;
using System.Reflection;

namespace BazaarAccess.UI.Login;

public class AccessDeniedUI : LoginBaseUI
{
    private readonly object _view;

    public override string UIName => "Access Denied";

    public AccessDeniedUI(Transform root, object view) : base(root)
    {
        _view = view;
        Initialize();
    }

    protected override void BuildMenu()
    {
        // Read error message if exists
        var descriptionField = _view.GetType().GetField("descriptionText",
            BindingFlags.NonPublic | BindingFlags.Instance);
        var descriptionText = descriptionField?.GetValue(_view) as TMP_Text;
        if (descriptionText != null && !string.IsNullOrWhiteSpace(descriptionText.text))
        {
            Menu.AddOption(
                () => descriptionText.text,
                () => TolkWrapper.Speak(descriptionText.text)
            );
        }

        AddBazaarButton(_view, "continueButton", "Continue");
    }

    protected override void OnBack()
    {
        var button = GetBazaarButton(_view, "continueButton");
        if (button != null) ClickBazaarButton(button);
    }
}
