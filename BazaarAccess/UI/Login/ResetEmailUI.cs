using BazaarAccess.Core;
using UnityEngine;

namespace BazaarAccess.UI.Login;

/// <summary>
/// UI accesible para la pantalla de cambio de email.
/// </summary>
public class ResetEmailUI : LoginBaseUI
{
    private readonly object _view;

    public override string UIName => Loc.T("ui.login.reset_email_title");

    public ResetEmailUI(Transform root, object view) : base(root)
    {
        _view = view;
        Initialize();
    }

    protected override void BuildMenu()
    {
        // Email field
        var emailField = GetInputField(_view, "email");
        AddTextField(Loc.T("ui.login.new_email_label"), emailField);

        // Confirm Email field
        var confirmEmailField = GetInputField(_view, "confirmEmail");
        AddTextField(Loc.T("ui.login.confirm_new_email_label"), confirmEmailField);

        // Continue button
        AddBazaarButton(_view, "continueButton", Loc.T("ui.continue"));
    }

    protected override void OnBack()
    {
        // Volver a la pantalla anterior
    }
}
