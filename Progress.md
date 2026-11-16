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

## November 16, 2026 — Game update (2026-11-07 build, patches 17–20.1)

First update since v1.10.1 (Jul 14). `bazaar code/` re-decompiled from the 2026-11-07 DLLs; the previous decompile is kept in `bazaar code 2026-07-11/` (gitignored) for diffing.

### What the game changed (Steam patch notes, app 1617400)
- 17.0 (Aug 5): The Dragons (Rin & Jin) — Instrument items, Note skills, **Tempo** meter. New encounters/merchants.
- 18.x (Aug–Sep): Tempo/Instrument follow-ups, balance.
- 19.0 (Oct 7): controller support (alpha), Collections inspect info. 19.4 (Oct 28): Partner Portal event.
- Oct 13 (PTR only): Tournament queue + lobby browser + encounter bans. Oct 15: hero DLC retired, free/demo accounts are Vanessa-only.
- 20.0 (Nov 4): Tiny World expedition (**Tiny** items), Thieves Guild, prestige counter toggle, collection screen visuals.

### Mod breakages and fixes
- **Main menu + hero select merged** into one scene driven by `TheBazaar.UI.MainMenu.MainMenuBaseView` (a MonoBehaviour, not a `View`), so `ViewController.SwitchView` no longer fires for them. New `Patches/MainMenuPatch.cs` hooks `MainMenuBaseView.OnEnable`; `Screens/MainMenuScreen.cs` was rewritten (play-mode toggles from `PlaymodeSelectionViewComponent._playmodeOptions`, hero button, play button, demo unlock, prize pass, settings, and the supported scenes of `NavigationMenuController`'s list via `SceneSelectOptionContainer.HandleSceneChange`). `HeroSelectScreen.cs` deleted.
- **Hero picker is a popup**: `TheBazaar.UI.Menu.HeroSelectPopupController` (`UIPopup`, pooled list of `HeroData`; Random = `EHero.Common`). `UI/HeroSelectUI.cs` reads `GetData()` instead of the pooled views; Enter mirrors `HeroOptionController.HeroClicked`, Left/Right toggles `PlayerPreferences.Data.SetHeroExcludedFromRandom` (the game's `HeroRandomExcludeController`).
- **Popups**: almost every popup (incl. `GenericPopup`) now derives `UIScripts.UIPopup`, not `PopupBase`. New `Patches/UIPopupPatch.cs` hooks `UIPopup.Show`/`Hide` and maps instance → accessible UI; `PopupBase` patch kept only for legacy popups (`NotEnoughSpacePopup`).
- Smaller: `MarketplaceScreenController.Awake` → `Initialize`; `BattlePassView._backButton/_chestButton` gone (back = `NavigationMenuController.HomeButton`, chests = `ChestsHUDViewComponent.PurchaseButton`); `BoardManager.IsPlayingUpgradeOrFuseOrEnchantAnimation` → `IsPlayingUpgradeOrFuseAnimation` + `EnchantManager.IsEnchanting`; `ResultComponent` now inherits `Show` from `SequencePopup` (postfix type-filtered); `BazaarConfirmationDialogController` is in the global namespace.
- The game's native `TheBazaar.Inputs.Navigation` (MenuNavigator/GameplayNavigator) only runs for Gamepad/SteamDeck schemes — no conflict with the mod's keyboard.

### New mechanics made readable
- **Tempo**: `ActionType.PlayerTempoApply/Remove` narrated in both combat modes; Tempo row in hero/opponent stats when the player has Tempo or owns a card with `TempoCost/TempoApplyAmount/TempoRemoveAmount`; those attributes in item stats. Passive gain (attribute ticks) is deliberately not announced.
- **Tiny**: `CardExtensions.IsTiny` → "Tiny" name prefix like the game's card title. Size is read from `card.Size`, which the game updates.
- **Socket effects / music notes**: `CardReading/SocketEffectReader.cs` lists the socket-effect cards under an item's slots (same source as the game's socket legend, `CardTooltipData.AppendSocketEffectLegendTooltips`) in the item detail lines.

### Controller support (mod-driven)
- Decision: the mod owns the gamepad instead of following the game's alpha controller navigation (`TheBazaar.Inputs.Navigation`), because the game's navigation covers only runs and some menus and has no access to the mod's stats/history/recap features.
- `Core/GamepadNavigator.cs` polls `Gamepad.current` (Unity Input System, referenced compile-only from the game) and dispatches the same `AccessibleKey`s as `KeyboardNavigator.Dispatch`. Layers: LT = action keys, RT = info keys. Hold-repeat and deadzone mirror the game's `NavInputRepeater` (0.4 s / 0.12 s / 0.5).
- `Core/GamepadOwnership.cs` sets `InputManager.Actions.asset.devices` to every non-gamepad device, so the game's actions (and its UI input module, which uses the same asset) never see the controller and the input scheme stays KeyboardMouse. Re-applied on device changes; `ModControlsGamepad=false` clears it.
- Menu (Start) = keyboard Escape: invokes `PopupManager.HandleEscape(true)`, the same path as the game's `OpenSettings` action for non-gamepad devices.

### Controller: hybrid mode (supersedes "mod-driven" as the default)
- User decision: follow the game's own controller navigation where it exists. Config `Controller.Mode` = Hybrid (default) / KeyboardEmulation / GameOnly.
- Hybrid, phase 1: inside a run (GameplayScreen, no mod UI on top) the device filter is lifted so the game's `GameplayNavigator` drives. `FocusAnnouncer` postfixes `FocusController.SetFocus` and describes `IFocusable`s with the mod's existing texts (`SelectionNavigator.GetCardDescription`, enemy compact description, reroll/exit/stash strings). `MoveSessionAnnouncer` postfixes the internal `ItemMoveSession` (`Pickup/Move/Up/Down`, `Place/Cancel`) reading `_item/_area/_handSlot/_stashSlot`; board neighbours come from `Player.Hand` cards matched to `BoardManager.playerItemSockets[].SocketNumber`.
- Free in-run buttons (unused by the game): View (toggle mod navigation), L3 (focused card details via `GameplayNavigator.CurrentFocus`), R3 (`HeroNavigator.SpeakSummary`), right stick (history). Holding RT hands the controller to the mod for its info layer.
- Outside runs the mod still owns the controller (phase 2: use `MenuNavigator`/EventSystem selection with mod labels where the game supports it).

### Developer test mode (Nov 16, 2026)
- Config `Debug.TestMode` (default false): every spoken line is logged as `SPEECH:` and `Core/TestCommandRunner.cs` runs commands from `BepInEx/BazaarAccess-commands.txt` (`key <AccessibleKey>`, `nav <NavCommand>` = game's controller navigation via `GameplayNavigator.Dispatch`, `scheme Gamepad` = force the game's input scheme so its controller navigation runs without a real pad, `escape`, `wait <s>`, `where`). Lets the session be driven and verified from the log without a keyboard or controller.
- Verified with it in-game: main menu labels (Settings, Gems, Chests with count), Collection open/back, Chests screen, and in a run the game's focus (encounters, board item, stash toy), item pickup/move/cancel narration, stash open/close, message history.
- Found: chests now open inside the Collection scene (`ChestsHUDViewComponent.RequestChestScene`), not the navigation menu; the top-bar icon buttons carry a hidden "Button Large" placeholder text.

### In-game test pass with the test mode (Nov 16, 2026)
Played a ranked run (Pygmalien, days 1-3) driven from the log. Verified: main menu, Collection, Chests, Profile, Prize Pass, pause menu, options; shop buy/sell/reroll (mod and game nav), events, loot, rewards, level-up, PvE and PvP fights with narration, replay and recap, monster preview, controller pickup/move/cancel, stash.
Root causes worth remembering:
- Build 2026-11 never raises `Events.BoardTransitionFinished`; rewards raise no reveal event at all. New offers inside a state are detected in `StateChangePatch.TriggerRefresh` by comparing the offered cards' ids (`SelectionNavigator.Signature`); only fully new offers in the same state are announced (`GameplayScreen.AnnounceNewOffer`).
- Event merchants/loot keep `ERunState.Encounter`, and level-up rewards keep `LevelUp` while showing paths then items: `GameplayAnnouncer.VerifyStateMatchesContent` maps by content.
- Choosing an encounter/path is reported as `CardPurchased`: purchase announcements are limited to items and skills. Sales arrive after the card is gone: names are remembered (`CardEventHandler.RememberName`).
- Resuming a run re-equips skills (~3 s after the board loads): learned-skill notices are ignored for 10 s (`GameplayPatch.EnteredGameplayAt`).
- Haste/Slow/Freeze/Charge attributes are milliseconds.
- The game's Y inspect is the `Gameplay/LockTooltip` action (`GameplayNavigator.OnInspect`), not a `NavCommand`.
- `GamepadNavigator.GameNavigationActive` requires the game's input scheme to be Gamepad/SteamDeck; focus and move narration only speak then.

### Menus made accessible (Nov 16, 2026)
- Gem / chest stores: `PurchaseGemsView` / `PurchaseChestsView` (`PurchaseBaseView : SequencePopup : UIPopup`) -> `UI/PurchasePanelUI.cs` (packs = `BundlePurchaseInfoContainer`, Enter = `PrimaryButton.onClick`); "not enough gems" prompt `GemPurchasePromptComponent` -> `GemPromptUI`. Opened from the main menu (`GemHUDViewComponent.OnGemButtonClick`, `ChestsHUDViewComponent.ShowPurchaseChestsPrompt`).
- Challenges: its own scene with no View; the main menu option clicks the game's button then attaches `BattlePassScreen(challengesOnly: true)` to `PrizePassChallengesView` once `SceneLoader.ActiveScene == SceneID.Challenges`.
- Equip Collectibles: `LoadoutPopupController` (UIPopup) -> `UI/LoadoutUI.cs` (categories from `CosmeticsPanelController._cosmeticsButton`, equipped via `CollectionManager.GetEquippedSaleItem`; items = active `CosmeticItem`s, equip = `_buttonComponent.OnMouseClickCustom`). The game hides its button while a run is in progress.
- `BazaarButtonController.onClick` also raises `MouseClickEvent` (OnEnable wires it), so `onClick.Invoke()` is enough for those buttons.
- Test mode gained `click <GameObject name>` (presses any game button, even hidden ones).
