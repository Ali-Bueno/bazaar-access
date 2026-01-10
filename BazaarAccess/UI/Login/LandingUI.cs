using UnityEngine;

namespace BazaarAccess.UI.Login;

/// <summary>
/// UI for the initial screen (Link Account / Create Account).
/// </summary>
public class LandingUI : LoginBaseUI
{
    private readonly object _view;

    public override string UIName => "Welcome";

    public LandingUI(Transform root, object view) : base(root)
    {
        _view = view;
        Initialize();
    }

    protected override void BuildMenu()
    {
        // Link Account button
        AddBazaarButton(_view, "linkAccountButton", "Link Account");

        // Create Account button
        AddBazaarButton(_view, "createAccountButton", "Create Account");
    }

    protected override void OnBack()
    {
        // No back from landing - it's the first screen
    }
}
