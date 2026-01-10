using UnityEngine;

namespace BazaarAccess.UI.Login;

/// <summary>
/// UI accesible para el paso de email de creación de cuenta.
/// </summary>
public class CreateAccountEmailUI : LoginBaseUI
{
    private readonly object _view;

    public override string UIName => "Create Account - Email";

    public CreateAccountEmailUI(Transform root, object view) : base(root)
    {
        _view = view;
        Initialize();
    }

    protected override void BuildMenu()
    {
        // Email field
        var emailField = GetInputField(_view, "email");
        AddTextField("Email", emailField);

        // Confirm Email field
        var confirmEmailField = GetInputField(_view, "confirmEmail");
        AddTextField("Confirm Email", confirmEmailField);

        // Continue button
        AddBazaarButton(_view, "continueButton", "Continue");
    }

    protected override void OnBack()
    {
        // Volver a la pantalla anterior
    }
}
