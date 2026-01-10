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

    public override string UIName => "Create Account - Username and Password";

    public CreateAccountUserPasswordUI(Transform root, object view) : base(root)
    {
        _view = view;
        Initialize();
    }

    protected override void BuildMenu()
    {
        // Username field with validation status
        var usernameField = GetInputField(_view, "username");
        AddTextFieldWithValidation("Username", usernameField, "_usernameValid", "available", "not available");

        // Password field with validation status
        var passwordField = GetInputField(_view, "password");
        AddTextFieldWithPasswordValidation("Password", passwordField);

        // Confirm Password field
        var confirmPasswordField = GetInputField(_view, "confirmPassword");
        AddTextFieldWithValidation("Confirm Password", confirmPasswordField, "_confirmPasswordValid", "matches", "empty or mismatch");

        // Validation status summary (read-only)
        Menu.AddOption(
            () => GetValidationSummary(),
            () => TolkWrapper.Speak(GetValidationSummary())
        );

        // Continue button
        AddBazaarButton(_view, "continueButton", "Continue");
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
            return "valid";

        var issues = new System.Collections.Generic.List<string>();
        if (!lengthValid) issues.Add("too short");
        if (!charsValid) issues.Add("needs letter");
        if (!numbersValid) issues.Add("needs number");

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

        if (usernameValid) passed++; else issues.Add("username");
        if (lengthValid) passed++; else issues.Add("password length");
        if (charsValid) passed++; else issues.Add("password letters");
        if (numbersValid) passed++; else issues.Add("password numbers");
        if (confirmValid) passed++; else issues.Add("confirm password");

        if (passed == total)
            return "All requirements met. Ready to continue.";

        return $"Requirements: {passed} of {total}. Missing: {string.Join(", ", issues)}";
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
