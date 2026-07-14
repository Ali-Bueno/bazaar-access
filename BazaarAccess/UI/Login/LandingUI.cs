using BazaarAccess.Core;
using UnityEngine;

namespace BazaarAccess.UI.Login;

/// <summary>
/// UI for the initial screen (Link Account / Create Account).
/// </summary>
public class LandingUI : LoginBaseUI
{
    private readonly object _view;

    public override string UIName => Loc.T("ui.login.welcome_title");

    public LandingUI(Transform root, object view) : base(root)
    {
        _view = view;
        Initialize();
    }

    protected override void BuildMenu()
    {
        // Link Account button
        AddBazaarButton(_view, "linkAccountButton", Loc.T("ui.login.link_account_button"));

        // Create Account button
        AddBazaarButton(_view, "createAccountButton", Loc.T("ui.login.create_account_button"));
    }

    protected override void OnBack()
    {
        // No back from landing - it's the first screen
    }
}
