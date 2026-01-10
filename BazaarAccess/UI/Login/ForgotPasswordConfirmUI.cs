using UnityEngine;

namespace BazaarAccess.UI.Login;

public class ForgotPasswordConfirmUI : LoginBaseUI
{
    private readonly object _view;

    public override string UIName => "Password Reset Sent";

    public ForgotPasswordConfirmUI(Transform root, object view) : base(root)
    {
        _view = view;
        Initialize();
    }

    protected override void BuildMenu()
    {
        AddBazaarButton(_view, "continueButton", "Continue");
        AddBazaarButton(_view, "resendButton", "Resend Email");
    }

    protected override void OnBack()
    {
        var button = GetBazaarButton(_view, "continueButton");
        if (button != null) ClickBazaarButton(button);
    }
}
