# BazaarAccess Progress Log

## January 16, 2026

### Combat Announcement System Overhaul

Changed from wave-based accumulated summaries to immediate per-card announcements for better real-time feedback.

**Before:** Effects were accumulated and announced as summaries when activity paused.
- Format: "You: [damage] ([top item]). Enemy: [damage] ([top item]), [status]."

**After:** Each card trigger is announced immediately as it happens.
- Format: "[ItemName]: [amount] [effect]"
- Player items: "Sword: 10 damage"
- Enemy items: "Enemy Dagger: 5 damage"

**Changes in CombatDescriber.cs:**

1. **Disabled periodic health announcements**
   - Removed automatic health announcement loop during combat
   - Users can press H for summary instead
   - Health threshold warnings (low/critical) remain active

2. **Immediate effect announcements**
   - Replaced wave accumulation system with instant per-effect announcements
   - Each effect is spoken immediately via `TolkWrapper.Speak()`

3. **New `FormatEffectAnnouncement()` method**
   - Formats effects as "[ItemName]: [amount] [effect]"
   - Prefixes enemy items with "Enemy "
   - Handles damage, heal, shield, burn, poison, slow, freeze
   - Adds "crit" suffix for critical hits
   - Special "Frozen!" alert for enemy freeze effects

4. **Simplified `OnEffectTriggered()` handler**
   - Removed wave accumulation logic
   - Now builds and announces each effect immediately
   - Still tracks damage totals for H key summary

---

### Combat Quick Stats Keys (1-4)

Added number keys 1-4 to announce individual combat stats with numbers only for quick reference during combat.

**New Keybindings:**
- **1** - Player health (number only)
- **2** - Enemy health (number only)
- **3** - Damage dealt (number only)
- **4** - Damage taken (number only)

**Files Modified:**

1. **BazaarAccess/Accessibility/AccessibleMenu.cs**
   - Added 4 new enum values to `AccessibleKey`: `PlayerHealth`, `EnemyHealth`, `DamageDealt`, `DamageTaken`

2. **BazaarAccess/Core/KeyboardNavigator.cs**
   - Added key mappings for `KeyCode.Alpha1` through `KeyCode.Alpha4` in the `MapKey()` method

3. **BazaarAccess/Gameplay/GameplayScreen.cs**
   - Added handlers in the combat switch block for the 4 new keys, calling the corresponding `CombatDescriber` methods

4. **BazaarAccess/Gameplay/CombatDescriber.cs**
   - Added `GetPlayerHealth()` - returns player health as string
   - Added `GetEnemyHealth()` - returns enemy health as string
   - Added `GetDamageDealt()` - returns total damage dealt as string
   - Added `GetDamageTaken()` - returns total damage taken as string

**Testing:**
- Build succeeds with no errors or warnings
- During combat, press 1-4 for quick stats, H for full summary

---

### Day/Hour Announcement Feature

Added automatic announcements when the day or hour changes during gameplay, helping blind players track run progression.

**Features:**
- Announces "Hour X" when progressing to a new hour
- Announces "Day X" when progressing to a new day (takes priority over hour)
- Resets tracking on new run

**Implementation Journey:**

1. **Initial Implementation**
   - Added `_lastDay` and `_lastHour` tracking variables
   - Added `CheckAndAnnounceDayHourChanges()` method to detect and announce changes
   - Called it from `OnStateChanged()` and transition events

2. **Diagnosis Phase**
   - Hour 1 announced correctly on new game
   - Subsequent hours NOT announced after shop choices
   - Added logging to investigate
   - User tested and reported logs showing the issue

3. **Root Cause Discovery**
   - `OnStateChanged` fires BEFORE `Data.Run.Hour` is updated by game engine
   - When checking for hour changes, the hour hadn't actually changed yet
   - Comparison (1 != 1) failed because hour still showed old value

4. **Fix: Delayed Coroutine**
   - Replaced immediate check with `DelayedCheckDayHourChanges()` coroutine
   - Waits 0.5 seconds for game data to update before checking
   - Now hour changes announce correctly after shop choices

**Files Modified:**
- `BazaarAccess/Patches/StateChangePatch.cs`
  - Added `_lastDay` and `_lastHour` tracking fields
  - Added `CheckAndAnnounceDayHourChanges()` method
  - Added `DelayedCheckDayHourChanges()` coroutine (0.5s delay)
  - Called from `OnStateChanged()`, `OnBoardTransitionFinished()`, `OnNewDayTransitionFinished()`
  - Reset tracking on `NewRun` state

## July 11, 2026

### Aura tooltip attribute reading (`Gameplay/CardReading/TextResolver.cs`)

Aura tooltips like "Your items have +1" or "Your rightmost Shield item has +10" were read
without the affected attribute. The game's tooltip template only holds the numeric value token
(`{aura.0}`); the attribute (damage, shield, ...) is shown purely as an ICON, invisible to a
screen reader. Normal ability tooltips spell the word out in the template, so they read fine.

**Fix:** `RenderWithAttributeNames()` iterates the parsed `TooltipBuilder.Components` instead of
calling `builder.Render()`. For each `TooltipComponentAura`, it reads the modified attribute from
the aura's action (`TAuraActionCardModifyAttribute.AttributeType` for items,
`TAuraActionPlayerModifyAttribute.AttributeType` for the player) and appends the friendly name
(reusing `EffectFormatter.GetFriendlyAttributeName`). `NextTextStartsWith` skips the append when
the following text already spells the word, avoiding "10 damage damage". Only aura tokens are
touched, so weapon/ability tooltips are unchanged. Note: the component's own `ReferencedAttribute`
is null for flat auras (it only reflects reference-typed values), which is why we read the action.

### Options dialog rework (`UI/OptionsUI.cs`, `Patches/OptionsDialogPatch.cs`)

Two problems, both from how the current build's options dialog is structured.

**1. "Tabs" list everything at once.** The dialog is NOT swapped panels. All settings live in one
`ScrollRect`; the tabs are a `ScrollSpyController` (`TheBazaar.UI.Components`) whose nav Toggles
just scroll the ScrollRect to each section. Non-selected sections stay `activeInHierarchy` and
interactable — only clipped by the viewport. So a hierarchy sweep lists every section's controls.

Fix: build a flat, section-headed list. `GetScrollSpySections()` reads `ScrollSpyController._entries`
by reflection (each entry: public `NavButtonRoot` = tab, `SectionRoot` = panel); we emit a
non-interactive header (tab label) then that section's controls, then footer controls outside any
section. Everything stays reachable and every control is always active, so changing values works
regardless of scroll. Falls back to a flat sweep if no ScrollSpy is present. Controls are filtered
by `IsInteractable()` (effective, respects CanvasGroups), not the serialized flag.

**2. Had to press Escape twice to focus the menu.** The dialog instance is cached by
`PopupManager`. On first instantiation it fires a spurious `OnEnable`/`OnDisable` pair (prefab is
active, then `SetActive(false)`), before the real open. Our old hook on `OnEnable` treated the
spurious `OnDisable` as a close → set the 0.3s reopen cooldown → the real `OnEnable` (within that
window) was dropped. `UIPopup.Show()`/`Hide()` are plain `SetActive`, so `Show()` is only called
on genuine opens (never the spurious load activation).

Fix: hook `UIPopup.Show()` (type-filtered to `OptionsDialogController`) instead of `OnEnable`.
Also, `IsReallyVisible` retries via a short coroutine while the dialog animates in (was giving up
on the first frame), and accepts any active slider/toggle/dropdown as the "loaded" signal.
Remaining edge: `PopupManager.ShowSettings` is `async void` with request-id supersession; if a
competing `HideSettings` fires during the first async Addressables load the game itself may skip
the open. If double-open persists from hero select, pre-warm the dialog at main-menu load.

**3. Dropdowns browse without committing.** Arrows previewed by setting `dropdown.value` each
step, which fired the game handler — the language dropdown re-localized / prompted a restart on
every arrow press. Now dropdowns track a pending index (read from `options[index]`), speak it on
arrow, and only commit (`dropdown.value = pending`, firing onValueChanged) on Enter.
