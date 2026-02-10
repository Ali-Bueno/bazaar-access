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

## Creating Releases

### File Structure
```
BazaarAccess release/          # Complete BepInEx installation template
├── BepInEx/plugins/           # Put BazaarAccess.dll and TolkDotNet.dll here
├── BepInEx/core/              # BepInEx core files
├── Tolk.dll, nvdaControllerClient64.dll  # Screen reader bridge
├── changelog.txt, README.txt  # Documentation
└── winhttp.dll, doorstop_config.ini      # BepInEx loader

releases/                      # Generated ZIPs for GitHub
├── BazaarAccess-update.zip    # DLL + changelog only (for existing BepInEx users)
└── BazaarAccess-full.zip      # Complete installation (includes BepInEx)
```

### Installation Simplification
**IMPORTANT**: The full release ZIP includes BepInEx, so users only need to:
1. Extract BazaarAccess-full.zip
2. Copy all files to the game's main folder
3. Launch the game

No separate BepInEx installation required!

### Release Process
1. Build the project: `dotnet build BazaarAccess/BazaarAccess.csproj`
2. Update `changelog.txt` (newest entries at top)
3. Copy files to release folder:
   ```bash
   cp BazaarAccess/bin/Debug/net46/BazaarAccess.dll "BazaarAccess release/BepInEx/plugins/"
   cp changelog.txt "BazaarAccess release/"
   cp README.txt "BazaarAccess release/"
   ```
4. Create ZIPs (names must stay consistent for permanent links):
   ```powershell
   Compress-Archive -Path 'BazaarAccess release/BepInEx/plugins/BazaarAccess.dll', 'BazaarAccess release/changelog.txt' -DestinationPath 'releases/BazaarAccess-update.zip' -Force
   Compress-Archive -Path 'BazaarAccess release/*' -DestinationPath 'releases/BazaarAccess-full.zip' -Force
   ```
5. Create GitHub release:
   ```bash
   gh release create vX.X.X releases/BazaarAccess-update.zip releases/BazaarAccess-full.zip --title "vX.X.X - Title" --notes "Release notes"
   ```

### Permanent Download Links
These always point to the latest release (keep filenames consistent!):
- **Update**: https://github.com/Ali-Bueno/bazaar-access/releases/latest/download/BazaarAccess-update.zip
- **Full**: https://github.com/Ali-Bueno/bazaar-access/releases/latest/download/BazaarAccess-full.zip

## Important Notes
- **Debounce**: StateChangePatch uses 0.4s debounce + 1.0s throttle for announcements
- **Combat waves**: CombatDescriber groups effects with 1.5s inactivity timeout
- **UI discovery**: Use `FindButtonByName()` or `FindButtonByText()` from BaseUI/BaseScreen
- **Game data**: Access via `Data.` singleton (Data.Run, Data.CurrentState, etc.)
- **Reflection**: Event subscriptions use reflection to avoid compile-time dependencies

## Pause Menu System (FightMenuPatch.cs)

**IMPORTANT**: The game's Escape key closes the ENTIRE pause system, not just one level.

### Flow
1. Escape opens pause menu → `FightMenuShowPatch` creates `FightMenuUI`
2. User clicks Settings → `FightMenuOptionsClickPatch` hides FightMenuUI, creates `OptionsUI`
3. Escape closes everything → `HideDialogs` fires, cleans up all UIs

### Key Design Decisions
- **Don't map Escape**: Let the game handle Escape natively. Our patches detect when menus close.
- **Don't reset `_isOpen` when going to Options**: Prevents duplicate FightMenuUI creation
- **Check UI stack before creating**: `FightMenuShowPatch` skips if any UI is already on stack
- **HideDialogs cleans everything**: Both OptionsUI and FightMenuUI are closed
- **OptionsUI from main menu**: Must be cleaned up when opening Options from pause menu during gameplay

---

## Recent Merge: oasis1701 Contributions (Jan 18, 2026)

**DO NOT modify these areas without understanding the full implementation:**

### Combat Describer Overhaul (`Gameplay/CombatDescriber.cs`)
- **Dual mode system**: Toggle between "batched" (wave summaries) and "individual" (per-effect) announcements
- **Ctrl+M**: Toggle combat announcement mode
- **Keys 1-4**: Quick stats during combat:
  - 1 = Player health
  - 2 = Enemy health
  - 3 = Damage dealt
  - 4 = Damage taken
- New methods: `GetPlayerHealth()`, `GetEnemyHealth()`, `GetDamageDealt()`, `GetDamageTaken()`
- `FormatEffectAnnouncement()` for immediate per-card announcements

### Day/Hour Announcements (`Patches/StateChangePatch.cs`)
- `_lastDay`, `_lastHour` tracking fields
- `CheckAndAnnounceDayHourChanges()` method
- `DelayedCheckDayHourChanges()` coroutine with 0.5s delay (waits for game data update)
- Announces "Day X" or "Hour X" on progression

### Enhanced Card Reading (`Gameplay/ItemReader.cs`)
- Improved cooldown reading with `GetCooldownText()`
- Better card name announcements

### Navigation Improvements (`Gameplay/GameplayNavigator.cs`, `GameplayScreen.cs`)
- Arrow keys enabled for reading cards and hero stats
- Enhanced encounter scrolling
- UI scrolling improvements across multiple screens

### Plugin Configuration (`Plugin.cs`)
- Combat mode toggle configuration added

**Reference**: See `Progress.md` for detailed implementation notes on these features.

---

## End of Run Screens (Jan 21, 2026)

### EndOfRunPatch.cs
Patches `TheBazaar.UI.EndOfRun.EndOfRunScreenController.Start` to create accessible UI for post-run screens.

**Key Components:**
- `EndOfRunUI` class implements `IAccessibleScreen`
- Two-pass text grouping:
  1. Parent-based grouping for stats (label + value pairs)
  2. Y-position grouping for challenges/achievements (horizontal spread detection)
- Automatic challenge progress formatting: "Use items 200 times: 77/200"
- Achievement formatting: "Achievement Herbalist: Heal 15000"
- Section headers detected and announced separately

**Navigation:**
- Arrow Up/Down: Read lines
- Enter/Backspace: Continue to next screen

### Recap Mode Changes (`GameplayScreen.cs`, `GameplayNavigator.cs`)
- V = Hero stats (arrow keys navigate)
- F = Enemy stats (arrow keys navigate)
- G = Enemy board (arrow keys navigate items)
- B = Player board (arrow keys navigate items)
- Backspace exits recap mode entirely (not individual sections)
- No Ctrl required for navigation within sections

### PvP Opponent Info (`ItemReader.cs`)
- `GetPvpOpponentRank()`: Uses reflection to get Rank and Division properties
- `GetPvpEncounterDetailLines()`: Provides detailed info for arrow key reading
- Rank format: "Bronze 1", "Silver 3", "Gold 2", etc.

---

## Action Menu System & Combat Health (Jan 26, 2026)

### Combat Health with Shield (`Gameplay/CombatDescriber.cs`)
- **Keys 1 and 2** now show shield: "400 with 50 shield" instead of just "400"
- `GetPlayerHealth()` and `GetEnemyHealth()` updated

### Action Menu System (`GameplayScreen.cs`)
Press **Enter** on a board/stash item to open the action menu:

**Menu Navigation:**
- **Up/Down**: Navigate options (Sell, Upgrade, Enchant, Move to Stash/Board)
- **Enter**: Confirm selected option
- **Backspace**: Exit action menu

**Keyboard Shortcuts (in action menu):**
- **S**: Sell item directly
- **U**: Upgrade/Enchant item directly
- **M**: Move to stash/board directly
- **Left/Right arrows**: Reorder item on board (stays in action menu)
- **Home/End**: Move item to left/right edge of board

**Reorder Feedback:**
- "Between [item1] and [item2]" - when between two items
- "After [item]" / "Before [item]" - when adjacent to one item
- "Left edge" / "Right edge" - at board boundaries

### Pedestal Detection (`Gameplay/ActionHelper.cs`)
- `GetCurrentPedestalInfo()`: Detects altar type (Upgrade, Enchant, EnchantRandom)
- `GetPedestalActionDescription()`: Returns human-readable description
- `IsEnchantPedestal()` / `IsUpgradePedestal()`: Quick type checks
- Uses reflection to access `TCardEncounterPedestal.Behavior` property

### Upgrade/Enchant Timing
- `DelayedRefreshAfterUpgrade()`: Waits up to 10 seconds for game animations
- Polls `IsProcessingUpgradeOrFuseOrEnchant` and `IsPlayingUpgradeOrFuseOrEnchantAnimation` flags
- Announces "Upgrading [name]" / "Enchanting [name]" before action
- Announces "Done" when animation completes

### Board Navigation Fix (`Gameplay/GameplayNavigator.cs`)
- `RefreshBoard()` now uses `HashSet<InstanceId>` to track seen items
- Large items (size 2-3) only appear once in navigation, not multiple times
- Prevents incorrect slot calculation when moving items

---

## Combat Board Navigation & Enemy Reading Improvements (Jan 26, 2026)

### Combat Navigation (`GameplayScreen.cs`)
During combat, after a 1.5s delay for items to load:
- **B**: Navigate player board with arrow keys
- **G**: Navigate enemy board with arrow keys
- **F**: Navigate enemy stats, Right arrow for skills
- **V**: Navigate hero stats
- **Backspace**: Exit current navigation mode

Combat navigation state tracked via `CombatNavSection` enum.

### Combat Board Ready Detection (`StateChangePatch.cs`)
- `_combatBoardReady` flag prevents navigation before items appear
- `DelayedSetCombatBoardReady()` coroutine sets flag after 1.5s
- `IsCombatBoardReady` property for checking state

### Enemy Board Reading Improvements (`ItemReader.cs`)
New `GetEnemyDetailLines()` method optimized for enemy analysis:
1. Name
2. Description (what it does)
3. Abilities/effects
4. Cooldown
5. Combat stats (Damage, Heal, Shield, etc.)
6. Speed stats
7. Tier, Tags, Size (metadata at end)

New `GetEnemyCompactDescription()` for quick navigation: "Name, Xs, X damage"

### Simplified Announcements
- Enemy board (G): Just "Opponent's board, X items" (no skills count)
- Enemy stats (F): Just "Enemy stats" (skills via Right arrow)

---

## Bug Fixes & Combat Improvements (Jan 30, 2026)

### Item Tracking Improvements (`Gameplay/GameplayNavigator.cs`)
- New `GoToItemById(InstanceId)` method for reliable item tracking after moves
- `GoToBoardSlot()` now validates index and logs warnings when slot not found
- `Refresh()` now calls `ClearDetailCache()` to prevent stale card references
- All reorder operations use ID-based tracking instead of slot-based (more reliable)

**Why this matters:** Items could "disappear" from navigation when:
- Large items (size 2-3) were moved and slot calculations were wrong
- `_detailCard` held stale references after moves
- `_currentIndex` pointed to invalid data after board changes

### Combat Event Handling (`Gameplay/CombatDescriber.cs`)
New action types added to `IsRelevantAction()`:
- `ActionType.CardReload` (700) - Announces "[ItemName] reloaded [amount]"
- `ActionType.CardModifyAttribute` (600) - Announces "[ItemName] modified by [amount]"

Both batched and individual modes handle these events.

### Unsellable Item Detection
**ActionHelper.cs:**
- `CanSell(card)` now checks `card.HiddenTags.Contains(EHiddenTag.Unsellable)`

**GameplayScreen.cs:**
- Action menu only shows "Sell" if both state and card allow selling

**StateChangePatch.cs:**
- Subscribed to `UnsellableItemSaleAttempt` event
- Announces "[ItemName] cannot be sold" when game rejects sale

### Action Menu Order at Pedestals (`GameplayScreen.cs`)
- At pedestals, Upgrade/Enchant option now appears FIRST (before Sell)
- This prioritizes the main action when at upgrade or enchant altars

### Reorder with Delays (`GameplayScreen.cs`)
- `MoveItemToEdgeCoroutine()` uses 50ms delays between moves
- Allows game to properly update adjacency effects (e.g., Swash Buckle's crit)
- Announces "Moving to [left/right] edge" before starting

### Accurate Upgrade Preview (v1.5.1)
- `GetActionOptionText()` now uses `ActionHelper.GetCurrentPedestalInfo().TargetTier`
- Compares target tier to current tier:
  - Same tier → "Upgrade Bronze stats (U)" (stats only, no tier change)
  - Different tier → "Upgrade to Silver (U)"

### Upgrade/Enchant Preview in Dialogs (v1.5.2)
New methods in `ActionHelper.cs`:
- `GetUpgradePreview(Card card)` - Returns list of stat changes like "Damage 10 to 15"
  - Uses reflection to access `Template.GetAttributeBaseValueAtTier(attrType, tier)`
  - Compares current tier vs target tier attributes
- `GetEnchantPreview(Card card, string enchantmentName)` - Returns enchantment effects
  - Accesses `Template.Enchantments` dictionary via reflection

Confirmation dialogs (`HandleUpgradeConfirm` in `GameplayScreen.cs`) now include:
- Upgrade: "Changes: Damage 10 to 15, Cooldown 3.0s to 2.5s"
- Enchant: "Effects: DamageAmount +5, BurnApplyAmount +10"

### Known Game Limitations
- **Item stats show BASE values only** - Combat-modified values (e.g., Orange Julian's +100 damage buff) are not accessible via the game's API
- During combat, ACTUAL damage IS announced correctly via combat events
- When pressing I to read item properties, only base stats are shown

---

## Combat Announcement Improvements (Jan 31, 2026)

### Critical Hit Announcements (`Gameplay/CombatDescriber.cs`)
- **Individual mode**: "Critical hit! Sword: 180 damage" (was "Sword: 180 damage, crit")
- **Batched mode**: "You: critical hit! 180 damage (Sword)" (crit at start of damage text)

### Reload Announcements
- Always include item name and amount: "Grenade reloaded 2 ammo"
- Added `ReloadAmount` to `CalculateEffectAmount()` for proper ammo tracking
- Enemy reloads also announced: "Enemy Grenade reloaded 1 ammo"

### Modified Attribute Announcements
- New `ModifyAttributeInfo` struct and `GetModifyAttributeInfo()` method
- Uses reflection to get `TargetCard`, `AttributeType`, and `Amount` from event
- `GetFriendlyAttributeName()` converts enum to readable text (DamageAmount → "damage")
- `FormatModifyAttributeText()` builds descriptive message: "damage increased by 50 on Dagger"

### Batched Mode Anti-Spam
Fast builds that modify items many times per second no longer spam announcements:
- `WaveData` now tracks: `ReloadsByItem`, `TotalBuffs`, `TotalDebuffs`
- Reloads and modifications accumulated into wave instead of immediate announcement
- Summary format: "You: 150 damage, 10 buffs, Grenade reloaded 5"
- Multiple items reloading: "8 reloads"
- Buff/debuff counts: "3 buffs, 2 debuffs"

---

## Game Update Compatibility & New Features (Feb 6, 2026)

### Hero Select Screen Fix (`Screens/HeroSelectScreen.cs`)
Game update made hero loading async (`HeroSelectButtonsView.Awake()` now hides heroes during `RefreshButtons()`).
- Hero options now use **visibility callbacks** instead of filtering by `activeInHierarchy` at build time
- All heroes from the serialized `HeroItemViews` list are added to the menu
- Each hero option checks `activeInHierarchy` dynamically when navigated
- Heroes appear automatically once async loading completes

### Random Hero Toggle (`Screens/HeroSelectScreen.cs`)
New game feature: checkbox to play a random hero from owned heroes.
- Added "Random Hero: on/off" option in hero select menu (Enter to toggle)
- Toggle visibility respects tutorial state (hidden during tutorial)
- `OnFocus()` announces "Random hero mode enabled" when active
- Hero "selected" indicator hidden when random mode is on
- Selecting a specific hero automatically disables random mode (game handles this)
- Uses `HeroSelectButtonsView.IsRandomHeroEnabled` static property and `RandomHeroToggle` field

### Repair Mechanic Support (`Gameplay/CombatDescriber.cs`, `Gameplay/ItemReader.cs`)
New game mechanic: items/skills can repair destroyed items during combat.
- `ActionType.CardRepair` (270) added to `IsRelevantAction()`
- **Individual mode**: "[SourceName]: repaired [TargetName]" (e.g., "Medkit: repaired Sword")
- **Batched mode**: Accumulates into wave - "repaired Sword" (single) or "3 repairs" (multiple)
- `WaveData` tracks: `TotalRepairs`, `RepairedItems` list
- Target card accessed via `CombatActionData.TargetCard` (public property)
- `ECardAttributeType.RepairTargets` (99) added to item stat reading (I key)
- `ItemReader.TokenToAttribute` maps "RepairTargets" and "Repair" aliases
- All 3 stat listing functions (compact, detailed, enemy) show "Repair Targets: X"

---

## Per-Card Combat Stats & Recap (Feb 10, 2026)

### Persistent Combat Tracking (`Gameplay/CombatDescriber.cs`)
- `CardCombatStats` class tracks per-card: Damage, Heal, Shield, Triggers, Crits, Repairs
- `_playerCardStats` and `_enemyCardStats` dictionaries (keyed by card name)
- `TrackCardStats()` called for every combat effect in both modes
- Cleared at `StartDescribing()`, preserved through `StopDescribing()` for recap
- `GetCombatStatsLines()` returns formatted lines sorted by damage (highest first)
- `HasCombatStats` property for checking if data is available

### Recap Combat Stats Navigation
- `RecapSection.CombatStats` added to enum
- **H key** in recap mode enters combat stats section
- Up/Down navigates through formatted stat lines
- Format: "Sword, 180 damage, 2 crits, 15 triggers"
- Sections: summary → player items → enemy items

---

## Player Rank Display & Bug Fixes (Feb 10, 2026)

### Player Rank (`Gameplay/ItemReader.cs`)
- `GetPlayerRank()`: Uses reflection to access `Data.Rank.CurrentSeasonRank`
  - Returns "Bronze 1", "Silver 3", "Legendary", etc.
  - Legendary has no division number
- `IsRankedMode()`: Checks `Data.RunConfiguration.RunType == EPlayMode.Ranked`

### Hero Select Rank Display (`Screens/HeroSelectScreen.cs`)
- Ranked button text includes rank when selected: "Ranked, selected. Rank: Silver 3"
- Clicking Ranked announces rank: "Ranked selected. Rank: Silver 3"

### Hero Stats Rank (`Gameplay/GameplayNavigator.cs`)
- `GetHeroStatCount()` returns HeroStats.Length + 1 in ranked mode
- `AnnounceHeroStat()` handles extra rank slot at end of stats list
- `AnnounceHeroSubsection()` includes rank in announcement when in ranked mode
- Recap hero mode also includes rank in count and announcement

### Quest Item Detection (`Gameplay/ItemReader.cs`)
- `IsQuestItem(Card)`: Checks `card.HiddenTags.Contains(EHiddenTag.Quest)`
- Quest items show "Quest:" prefix in shop navigation
- "Quest item" line added to detail view

### Stash Reordering (`Gameplay/ActionHelper.cs`, `GameplayNavigator.cs`, `GameplayScreen.cs`)
- `ReorderStashItem()` in ActionHelper uses `EInventorySection.Stash`
- `GetCurrentStashSlot()` and `GoToStashSlot()` in GameplayNavigator
- `HandleReorder()` now supports both board and stash sections
- `GoToItemById()` works for both board and stash

### PvP Hero Name Fix (`Gameplay/ItemReader.cs`)
- `GetPvpOpponentHeroName()` reads `SimPvpOpponent.Hero` (EHero enum) via reflection
- Used in `GetEncounterInfo()`, `GetEncounterDetailedInfo()`, `GetPvpEncounterDetailLines()`
- `SelectEncounterDirect()` in GameplayScreen now uses `GetEncounterInfo()` for PvP cards

### Loot Tag Fix (`Gameplay/ItemReader.cs`)
- Added `ECardTag.Loot` (value 19) to `RelevantTags` HashSet

### Enchant Altar Fix (`Gameplay/GameplayScreen.cs`)
- `HandleUpgrade()` (Shift+U) now routes through `HandleUpgradeConfirm()`
- Properly detects enchant vs upgrade pedestal type
