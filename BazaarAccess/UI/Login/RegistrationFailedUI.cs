using BazaarAccess.Core;
using UnityEngine;

namespace BazaarAccess.UI.Login;

public class RegistrationFailedUI : LoginBaseUI
{
    private readonly object _view;

    public override string UIName => Loc.T("ui.login.registration_failed_title");

    public RegistrationFailedUI(Transform root, object view) : base(root)
    {
        _view = view;
        Initialize();
    }

    protected override void BuildMenu()
    {
        AddBazaarButton(_view, "continueButton", Loc.T("ui.login.try_again_button"));
    }

    protected override void OnBack()
    {
        var button = GetBazaarButton(_view, "continueButton");
        if (button != null) ClickBazaarButton(button);
    }
}
