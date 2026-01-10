using UnityEngine;

namespace BazaarAccess.UI.Login;

/// <summary>
/// UI accesible para la pantalla de cambio de email.
/// </summary>
public class ResetEmailUI : LoginBaseUI
{
    private readonly object _view;

    public override string UIName => "Reset Email";

    public ResetEmailUI(Transform root, object view) : base(root)
    {
        _view = view;
        Initialize();
    }

    protected override void BuildMenu()
    {
        // Email field
        var emailField = GetInputField(_view, "email");
        AddTextField("New Email", emailField);

        // Confirm Email field
        var confirmEmailField = GetInputField(_view, "confirmEmail");
        AddTextField("Confirm New Email", confirmEmailField);

        // Continue button
        AddBazaarButton(_view, "continueButton", "Continue");
    }

    protected override void OnBack()
    {
        // Volver a la pantalla anterior
    }
}
