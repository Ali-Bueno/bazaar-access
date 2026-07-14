using BazaarAccess.Core;
using UnityEngine;

namespace BazaarAccess.UI.Login;

public class ForgotPasswordConfirmUI : LoginBaseUI
{
    private readonly object _view;

    public override string UIName => Loc.T("ui.login.password_reset_sent_title");

    public ForgotPasswordConfirmUI(Transform root, object view) : base(root)
    {
        _view = view;
        Initialize();
    }

    protected override void BuildMenu()
    {
        AddBazaarButton(_view, "continueButton", Loc.T("ui.continue"));
        AddBazaarButton(_view, "resendButton", Loc.T("ui.login.resend_email_button"));
    }

    protected override void OnBack()
    {
        var button = GetBazaarButton(_view, "continueButton");
        if (button != null) ClickBazaarButton(button);
    }
}
