using UnityEngine;

namespace BazaarAccess.UI.Login;

public class RegistrationFailedUI : LoginBaseUI
{
    private readonly object _view;

    public override string UIName => "Registration Failed";

    public RegistrationFailedUI(Transform root, object view) : base(root)
    {
        _view = view;
        Initialize();
    }

    protected override void BuildMenu()
    {
        AddBazaarButton(_view, "continueButton", "Try Again");
    }

    protected override void OnBack()
    {
        var button = GetBazaarButton(_view, "continueButton");
        if (button != null) ClickBazaarButton(button);
    }
}
