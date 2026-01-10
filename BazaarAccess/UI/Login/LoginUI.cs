using UnityEngine;

namespace BazaarAccess.UI.Login;

/// <summary>
/// UI accesible para la pantalla de login (Email + Password).
/// </summary>
public class LoginUI : LoginBaseUI
{
    private readonly object _view;

    public override string UIName => "Login";

    public LoginUI(Transform root, object view) : base(root)
    {
        _view = view;
        Initialize();
    }

    protected override void BuildMenu()
    {
        // Email field
        var emailField = GetInputField(_view, "emailText");
        string emailLabel = FindLabelForInput(emailField);
        if (string.IsNullOrWhiteSpace(emailLabel) || emailLabel == "Field")
            emailLabel = "Email";
        AddTextField(emailLabel, emailField);

        // Password field
        var passwordField = GetInputField(_view, "passwordText");
        string passwordLabel = FindLabelForInput(passwordField);
        if (string.IsNullOrWhiteSpace(passwordLabel) || passwordLabel == "Field")
            passwordLabel = "Password";
        AddTextField(passwordLabel, passwordField);

        // Continue button
        AddBazaarButton(_view, "continueButton", "Continue");

        // Reset Password button (es un Button normal, no BazaarButtonController)
        AddUnityButton(_view, "resetPasswordButton", "Reset Password");
    }

    protected override void OnBack()
    {
        // Volver a la pantalla anterior (landing)
        // No hacemos nada porque el juego maneja esto automáticamente
    }
}
