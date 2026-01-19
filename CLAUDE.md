# BazaarAccess - Claude Guide

A BepInEx mod making "The Bazaar" accessible to blind players via screen reader (Tolk) and full keyboard navigation.

## Tech Stack
- **C# / .NET Framework 4.6** targeting Unity 2019.4.16
- **BepInEx 5.x** for mod loading
- **HarmonyLib** for runtime patching
- **TolkDotNet** for screen reader output (NVDA, JAWS, etc.)

## Project Structure
```
BazaarAccess/
├── Accessibility/    # Framework: AccessibilityMgr, BaseScreen, BaseUI, AccessibleMenu
├── Core/             # TolkWrapper, KeyboardNavigator, MessageBuffer
├── Gameplay/         # GameplayScreen, GameplayNavigator, ItemReader, CombatDescriber, ActionHelper
├── Patches/          # Harmony patches hooking into game events
├── Screens/          # Main screens (MainMenu, HeroSelect, Collection, BattlePass, ChestScene)
├── UI/               # Dialog/popup UIs including Login/ subdirectory
└── Plugin.cs         # Entry point
```

## Core Architecture

### Focus Management (AccessibilityMgr.cs)
- **_currentScreen**: Active main screen (IAccessibleScreen)
- **_uiStack**: Stack of popup UIs (IAccessibleUI) - top gets input priority
- `SetScreen()` clears UI stack; `ShowUI()`/`HideUI()` push/pop dialogs

### Keyboard Input Flow (KeyboardNavigator.cs)
```
Unity OnGUI → MapKey(event) → AccessibleKey enum → AccessibilityMgr.HandleInput()
              → FocusedUI?.HandleInput() OR CurrentScreen?.HandleInput()
```

### Screen Reader Output (TolkWrapper.cs)
- `Speak(text, interrupt)` - 0.3s dedup window prevents spam
- `SpeakForced()` - bypasses dedup for intentional repeats

### Menu Pattern (AccessibleMenu.cs)
Composition-based navigation used by all screens/UIs:
- `AddOption(text, onConfirm, onRead?, onAdjust?)`
- Up/Down navigates
- Home/End/PageUp/PageDown for fast navigation

## Key Files for Common Tasks

| Task | Files |
|------|-------|
| Add new game screen | `Screens/`, implement `IAccessibleScreen`, register in `ViewControllerPatch.cs` |
| Add new popup/dialog | `UI/`, extend `BaseUI`, register in `PopupPatch.cs` |
| Hook game events | `Patches/StateChangePatch.cs` - subscribe via reflection |
| Modify item reading | `Gameplay/ItemReader.cs` |
| Combat narration | `Gameplay/CombatDescriber.cs` |
| Keyboard shortcuts | `Core/KeyboardNavigator.cs` (mapping), `GameplayScreen.cs` (handling) |
| Item actions | `Gameplay/ActionHelper.cs` (buy/sell/move/reorder) |

## Keyboard Mapping (AccessibleKey enum)
- **Arrows**: Navigate items/sections
- **Ctrl+Arrows**: Detail reading, subsection switching
- **Shift+Arrows**: Item movement, reordering
- **B/V/C/F/G**: Quick nav (Board/Hero/Choices/Enemy/Stash)
- **Enter**: Confirm | **Backspace**: Back | **Tab**: Next section
- **E**: Exit | **R**: Reroll | **Space**: Toggle stash
- **Period/Comma**: Message history navigation
- **H**: Combat summary | **I**: Item properties | **T/S**: Board/Stash info

## Game State Detection
`StateChangePatch.cs` subscribes to 20+ game events:
- `StateChanged` - Core transitions (Choice→Combat→Loot)
- `CombatStarted/CombatEnded` - Combat lifecycle
- `CardPurchased/CardSold` - Item transactions
- `BoardTransitionFinished` - Animation completion (safe to read UI)

Game states: `Choice` (shop), `Combat`, `PVPCombat`, `Loot`, `LevelUp`, `Pedestal`, `Encounter`, `EndRunVictory`, `EndRunDefeat`

## Item Data Access (ItemReader.cs)
```csharp
card.GetAttributeValue(ECardAttributeType.X)  // DamageAmount, Cooldown, etc.
Data.Run.Player  // Player stats
Data.Run.Gold    // Currency
Data.SimPvpOpponent  // PvP opponent info
```

## Patterns & Conventions

### Creating a New Screen
```csharp
public class MyScreen : BaseScreen, IAccessibleScreen
{
    protected override void BuildMenu()
    {
        Menu.AddOption("Option 1", () => DoAction());
    }

    public override bool IsValid() => /* check if screen should be active */;
}
```

### Creating a New UI/Dialog
```csharp
public class MyUI : BaseUI, IAccessibleUI
{
    protected override void BuildMenu()
    {
        Menu.AddOption("Button", () => ClickButtonByName("Btn_Name"));
    }
}
```

### Harmony Patching
```csharp
[HarmonyPatch(typeof(TargetClass), nameof(TargetClass.Method))]
class MyPatch
{
    static void Postfix() => /* run after original */;
}
```

### Announcing (with dedup)
```csharp
TolkWrapper.Speak("Message");  // Normal
TolkWrapper.SpeakForced("Message");  // Bypass dedup
MessageBuffer.Add("Message");  // Add to history
```

## Build & Deploy
```bash
dotnet build BazaarAccess/BazaarAccess.csproj
# Auto-copies to: D:\games\steam\steamapps\common\The Bazaar\BepInEx\plugins\
```

## Important Notes
- **Debounce**: StateChangePatch uses 0.4s debounce + 1.0s throttle for announcements
- **Combat waves**: CombatDescriber groups effects with 1.5s inactivity timeout
- **UI discovery**: Use `FindButtonByName()` or `FindButtonByText()` from BaseUI/BaseScreen
- **Game data**: Access via `Data.` singleton (Data.Run, Data.CurrentState, etc.)
- **Reflection**: Event subscriptions use reflection to avoid compile-time dependencies
