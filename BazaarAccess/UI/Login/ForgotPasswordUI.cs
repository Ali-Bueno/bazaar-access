using BazaarAccess.Core;
using UnityEngine;

namespace BazaarAccess.UI.Login;

/// <summary>
/// UI accesible para la pantalla de recuperación de contraseña.
/// </summary>
public class ForgotPasswordUI : LoginBaseUI
{
    private readonly object _view;

    public override string UIName => Loc.T("ui.login.reset_password");

    public ForgotPasswordUI(Transform root, object view) : base(root)
    {
        _view = view;
        Initialize();
    }

    protected override void BuildMenu()
    {
        // Email field
        var emailField = GetInputField(_view, "emailText");
        AddTextField(Loc.T("ui.login.email_label"), emailField);

        // Continue button
        AddBazaarButton(_view, "continueButton", Loc.T("ui.login.send_reset_link_button"));
    }

    protected override void OnBack()
    {
        // Volver a la pantalla anterior
    }
}
