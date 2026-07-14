using System.Reflection;
using BazaarAccess.Accessibility;
using BazaarAccess.Core;
using UnityEngine;

namespace BazaarAccess.UI.Login;

/// <summary>
/// UI for the username and password step of account creation.
/// Shows validation status for each requirement.
/// </summary>
public class CreateAccountUserPasswordUI : LoginBaseUI
{
    private readonly object _view;

    public override string UIName => Loc.T("ui.login.create_account_userpass_title");

    public CreateAccountUserPasswordUI(Transform root, object view) : base(root)
    {
        _view = view;
        Initialize();
    }

    protected override void BuildMenu()
    {
        // Username field with validation status
        var usernameField = GetInputField(_view, "username");
        AddTextFieldWithValidation(Loc.T("ui.login.username_label"), usernameField, "_usernameValid", Loc.T("ui.login.available"), Loc.T("ui.login.not_available"));

        // Password field with validation status
        var passwordField = GetInputField(_view, "password");
        AddTextFieldWithPasswordValidation(Loc.T("ui.login.password_label"), passwordField);

        // Confirm Password field
        var confirmPasswordField = GetInputField(_view, "confirmPassword");
        AddTextFieldWithValidation(Loc.T("ui.login.confirm_password_label"), confirmPasswordField, "_confirmPasswordValid", Loc.T("ui.login.matches"), Loc.T("ui.login.empty_or_mismatch"));

        // Validation status summary (read-only)
        Menu.AddOption(
            () => GetValidationSummary(),
            () => TolkWrapper.Speak(GetValidationSummary())
        );

        // Continue button
        AddBazaarButton(_view, "continueButton", Loc.T("ui.continue"));
    }

    private void AddTextFieldWithValidation(string label, TMPro.TMP_InputField inputField, string validFieldName, string validText, string invalidText)
    {
        if (inputField == null) return;

        var textFieldOption = new TextFieldOption(label, inputField);
        textFieldOption.OnEditModeChanged += OnTextFieldEditModeChanged;
        _textFieldOptions.Add(textFieldOption);

        Menu.AddOption(
            () => {
                string baseText = textFieldOption.GetDisplayText();
                bool isValid = GetBoolField(validFieldName);
                string status = isValid ? validText : invalidText;
                return $"{baseText} ({status})";
            },
            () => textFieldOption.ToggleEditMode()
        );
    }

    private void AddTextFieldWithPasswordValidation(string label, TMPro.TMP_InputField inputField)
    {
        if (inputField == null) return;

        var textFieldOption = new TextFieldOption(label, inputField);
        textFieldOption.OnEditModeChanged += OnTextFieldEditModeChanged;
        _textFieldOptions.Add(textFieldOption);

        Menu.AddOption(
            () => {
                string baseText = textFieldOption.GetDisplayText();
                string status = GetPasswordValidationStatus();
                return $"{baseText} ({status})";
            },
            () => textFieldOption.ToggleEditMode()
        );
    }

    private string GetPasswordValidationStatus()
    {
        bool lengthValid = GetBoolField("_passwordLengthValid");
        bool charsValid = GetBoolField("_passwordCharactersValid");
        bool numbersValid = GetBoolField("_passwordNumbersValid");

        if (lengthValid && charsValid && numbersValid)
            return Loc.T("ui.login.valid");

        var issues = new System.Collections.Generic.List<string>();
        if (!lengthValid) issues.Add(Loc.T("ui.login.pwd_too_short"));
        if (!charsValid) issues.Add(Loc.T("ui.login.pwd_needs_letter"));
        if (!numbersValid) issues.Add(Loc.T("ui.login.pwd_needs_number"));

        return string.Join(", ", issues);
    }

    private string GetValidationSummary()
    {
        bool usernameValid = GetBoolField("_usernameValid");
        bool lengthValid = GetBoolField("_passwordLengthValid");
        bool charsValid = GetBoolField("_passwordCharactersValid");
        bool numbersValid = GetBoolField("_passwordNumbersValid");
        bool confirmValid = GetBoolField("_confirmPasswordValid");

        int passed = 0;
        int total = 5;
        var issues = new System.Collections.Generic.List<string>();

        if (usernameValid) passed++; else issues.Add(Loc.T("ui.login.issue_username"));
        if (lengthValid) passed++; else issues.Add(Loc.T("ui.login.issue_pwd_length"));
        if (charsValid) passed++; else issues.Add(Loc.T("ui.login.issue_pwd_letters"));
        if (numbersValid) passed++; else issues.Add(Loc.T("ui.login.issue_pwd_numbers"));
        if (confirmValid) passed++; else issues.Add(Loc.T("ui.login.issue_confirm_password"));

        if (passed == total)
            return Loc.T("ui.login.requirements_met");

        return Loc.T("ui.login.requirements_summary", passed, total, string.Join(", ", issues));
    }

    private bool GetBoolField(string fieldName)
    {
        var field = _view.GetType().GetField(fieldName,
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (field != null)
            return (bool)field.GetValue(_view);
        return false;
    }

    private void OnTextFieldEditModeChanged(bool isEditing)
    {
        // Update the internal state via reflection
        var field = typeof(LoginBaseUI).GetField("_isInEditMode",
            BindingFlags.NonPublic | BindingFlags.Instance);
        field?.SetValue(this, isEditing);
    }

    protected override void OnBack()
    {
        // Go back to previous screen
    }
}
