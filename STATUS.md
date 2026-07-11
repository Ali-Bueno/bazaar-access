# STATUS — The Bazaar

> Per-mod status ledger / dashboard. Open this first when resuming the mod so progress isn't re-derived from the code each session. Keep it short — a dashboard, not docs. Update the **Next step** line and the section table whenever you finish a chunk. Derive every value from the game's real data — no guessed offsets.

**Last updated:** 2026-07-11

## Identity
- **Engine / framework:** Unity Mono (Unity 2019.4.16), BepInEx 5, net46 + Harmony.
- **Screen-reader transport:** Tolk via TolkDotNet (`Core/TolkWrapper.cs`, namespace `DavyKager`). `Tolk.dll` + `TolkDotNet.dll` in the game folder.
- **Build command:** `dotnet build` (csproj copies the DLL to the plugins folder).
- **Mod install path:** `D:\games\steam\...\The Bazaar\BepInEx\plugins`.
- **Run / test:** Launch The Bazaar through Steam with a screen reader (NVDA/JAWS) running; the mod announces on load.

## Section status
`done` = works with the screen reader on; `wip` = started; `todo` = not begun.

| Section / feature | Status | Notes |
|---|---|---|
| Login / account creation screens | done | `UI/Login/*` (Landing, Login, CreateAccount*, ForgotPassword, ResetEmail, etc.), `Patches/LoginPatch.cs` |
| Main menu & navigation | done | `Screens/MainMenuScreen.cs`, `Patches/MenuPatches.cs`, `Core/KeyboardNavigator.cs` |
| Shop / board / stash gameplay | done | `Patches/GameplayPatch.cs`, item move/reorder, T/S capacity keys |
| Combat narration | done | Immediate per-card announcements; quick-stat keys 1-4; H summary (see Progress.md, Jan 16) |
| Day/Hour progression announce | done | `Patches/StateChangePatch.cs` delayed coroutine (Progress.md) |
| End-of-run screens | done | `Patches/EndOfRunPatch.cs`, `UI/ChestRewardsUI.cs`; stats/challenges/achievements (changelog v1.2.0) |
| PvP opponent rank / recap | done | Rank+division on ranked encounters; recap keys V/F/G/B (changelog v1.2.0) |
| Options dialog | done | `Patches/OptionsDialogPatch.cs`, `UI/OptionsUI.cs` — rebuilt 2026-07-11 as a flat, section-headed list from the ScrollSpy sections (reaches Language + Keybindings); hooks `UIPopup.Show` to fix the double-open; dropdowns preview on arrow / apply on Enter. Verified in-game (v1.9.1) |
| Fight menu | done | `Patches/FightMenuPatch.cs`, `UI/FightMenuUI.cs` |
| Tutorial / popups / confirm dialogs | done | `Patches/TutorialPatch.cs`, `PopupPatch.cs`, `UI/GenericPopupUI.cs`, `ConfirmationDialogUI.cs` |
| Item inspect (right-click panel) | done | `Gameplay/ItemInspect/ItemInspectNavigator.cs` — press `x` on an item or via context menu (PR #7) |
| Combat encounter preview | done | `Gameplay/CombatEncounterPreview/*` — press `x` on combat encounters; reads monster loadout, level/exp/gold (PR #7) |
| Shop item details menu | done | `Gameplay/ShopItemMenuHandler.cs` — details menu item; buy no longer needs confirmation (PR #7) |
| Native recap combat stats | done | `Gameplay/CardReading/RecapStatsReader.cs` replaced custom `CardStatsTracker` (deleted) with the game's own recap statistics (PR #7) |
| Rage / enrage announcements | done | Hero + opponent rage stat readable; gain/loss of enrage announced (PR #7) |

## Derived facts (so we never re-RE them)
| Fact | Value | Source |
|---|---|---|
| Decompiled game code | `bazaar code/` — re-decompiled from the **2026-07-11 game build** (ilspycmd, latest); the game updated twice in one day. Always confirm `bazaar code/` date matches the installed DLLs before trusting it | repo tree (gitignored) |
| Internal game types | The 2026-07-11 build made `FightMenuDialog` + `OptionsDialogController` `internal` (global ns). Patch them via `TargetMethod()` + `AccessTools.TypeByName` — NOT the `[HarmonyPatch("Name","Method")]` string overload (that uses `Type.GetType`, which can't see other assemblies, and killed the whole mod at load). | `FightMenuPatch.cs`, `OptionsDialogPatch.cs` |
| Patch resilience | `Plugin.cs` patches each class in a try/catch loop (not `PatchAll()`), so one broken hook after a game update no longer disables the entire mod. Log: `Harmony patches applied: N patched, M failed`. | `Plugin.cs` |
| `Data.GetStatic()` | Now **synchronous** — returns `JsonGameDataManager` directly (was `Task<...>`). No `.Result`/`.await`. | `bazaar code/.../Data.cs:492` |
| Challenge data lookup | `Data.GetStatic().GetChallengeById(Guid)` → `TChallenge`. `ChallengeDataManager`/`Services.Get<>` removed. | `.../JsonGameDataManager.cs:102` |
| `GetMonsterTemplate()` | Now synchronous — returns `TMonster` directly (was awaitable). | `.../DataExtensions.cs:405` |
| `TooltipContext` | Now a `readonly struct` with ctor `(Card, ITCard, ValueContext)`; `TooltipBuilder.Render()` takes 0 args. | `.../Tooltips/TooltipContext.cs` |
| Chest reward bonus | `ChestRewardResponse.bonusChestCount` (int) — was `bonusChest` (Guid[]). | `.../PlayerChestInventory.cs:40` |
| Run/state model | `RunManager`, `ActiveRun`, `Data.Run.Hour` / `.Day` (fires before engine updates the value) | Progress.md; `bazaar code/TheBazaarRuntime/RunManager.cs` |
| Tooltip system | `TooltipBuilder` / `TooltipComponent` (`BazaarGameClient.Domain.Tooltips`) | `bazaar code/.../Tooltips/*` |
| Screen abstractions | `Accessibility/BaseScreen.cs`, `BaseUI.cs`, `AccessibleMenu.cs`, `AccessibilityMgr.cs` | src tree |

## Next step
**v1.9.2 released publicly** (first public release since v1.8.5 — v1.9.0/1.9.1 were committed but never released). Merged PR features (item inspect `x`, combat encounter preview, shop details, native recap stats, rage/enrage) and the API-adaptation fixes are verified in gameplay. v1.9.2 also fixed: hero-select Back key (Backspace → main menu), Season Pass reading collection items in chest mode, and a stray "Hour 1" announce on the menus after a run; plus "Delivery package" objective (patch-16 Farai) and a "Free" announcement on free-item selections. No pending pass — monitor community bug reports.

## Known issues / open questions
- Combat is intentionally key-gated (only V/F/H etc. active during a fight) — confirm no gameplay key collisions after game updates.
- **Audit 2026-07-11** (mod code vs current game build): Skins/Collection menu and all other screens/UI verified intact. Two non-fatal breakages found from the update:
  - **Fixed** — `OptionsUI` keybind action names: game switched `KeyBindController._keybindAction` (enum) → `_action`/`_resolvedAction` (new Input System). `GetKeybindLabel` now reads the InputAction name by reflection.
  - **Fixed** — `PopupPatch.ImageTutorialPatch`/`HidePatch`: game replaced `ImageSequenceDialogController` with `TheBazaar.SequenceDialogController` (`: BasePointerDialogController : AbstractFeatureComponent`). Patch now hooks its `Show`/`Hide`, reads the `_text` field (recursive field lookup up the hierarchy), and continues via `_nodeSequenceComponent`. Matters for **new players** who hit the image tutorials.
- The 2026-07-01 game build made several game APIs synchronous (`Data.GetStatic`, `GetMonsterTemplate`) and reshaped others (`TooltipContext`, `ChestRewardResponse`, challenge lookup). Merged PR #6/#7 targeted an earlier build; 6 compile errors were fixed locally on 2026-07-11 to match the installed build. Runtime behavior verified in gameplay (v1.9.2).

**Detailed history:** see Progress.md and BazaarAccess/changelog.txt.
