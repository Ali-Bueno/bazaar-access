using UnityEngine;

namespace BazaarAccess.UI.Login;

public class AccountVerifiedUI : LoginBaseUI
{
    private readonly object _view;

    public override string UIName => "Account Verified";

    public AccountVerifiedUI(Transform root, object view) : base(root)
    {
        _view = view;
        Initialize();
    }

    protected override void BuildMenu()
    {
        AddBazaarButton(_view, "continueButton", "Continue");
    }

    protected override void OnBack()
    {
        var button = GetBazaarButton(_view, "continueButton");
        if (button != null) ClickBazaarButton(button);
    }
}
