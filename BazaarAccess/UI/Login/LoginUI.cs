using BazaarAccess.Core;
using UnityEngine;

namespace BazaarAccess.UI.Login;

/// <summary>
/// UI accesible para la pantalla de login (Email + Password).
/// </summary>
public class LoginUI : LoginBaseUI
{
    private readonly object _view;

    public override string UIName => Loc.T("ui.login.login_title");

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
        if (string.IsNullOrWhiteSpace(emailLabel) || emailLabel == Loc.T("ui.field_default_label"))
            emailLabel = Loc.T("ui.login.email_label");
        AddTextField(emailLabel, emailField);

        // Password field
        var passwordField = GetInputField(_view, "passwordText");
        string passwordLabel = FindLabelForInput(passwordField);
        if (string.IsNullOrWhiteSpace(passwordLabel) || passwordLabel == Loc.T("ui.field_default_label"))
            passwordLabel = Loc.T("ui.login.password_label");
        AddTextField(passwordLabel, passwordField);

        // Continue button
        AddBazaarButton(_view, "continueButton", Loc.T("ui.continue"));

        // Reset Password button (es un Button normal, no BazaarButtonController)
        AddUnityButton(_view, "resetPasswordButton", Loc.T("ui.login.reset_password"));
    }

    protected override void OnBack()
    {
        // Volver a la pantalla anterior (landing)
        // No hacemos nada porque el juego maneja esto automáticamente
    }
}
