using UnityEngine;

namespace BazaarAccess.UI.Login;

/// <summary>
/// UI accesible para la pantalla de recuperación de contraseña.
/// </summary>
public class ForgotPasswordUI : LoginBaseUI
{
    private readonly object _view;

    public override string UIName => "Reset Password";

    public ForgotPasswordUI(Transform root, object view) : base(root)
    {
        _view = view;
        Initialize();
    }

    protected override void BuildMenu()
    {
        // Email field
        var emailField = GetInputField(_view, "emailText");
        AddTextField("Email", emailField);

        // Continue button
        AddBazaarButton(_view, "continueButton", "Send Reset Link");
    }

    protected override void OnBack()
    {
        // Volver a la pantalla anterior
    }
}
