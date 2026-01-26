BAZAAR ACCESS - Accessibility Mod for The Bazaar
================================================

A BepInEx plugin that makes The Bazaar accessible for blind players using screen readers (via Tolk).


INSTALLATION
------------
1. Install BepInEx 5.x in your game folder
2. Copy BazaarAccess.dll to BepInEx/plugins/
3. Copy Tolk.dll and TolkDotNet.dll to the game folder


CONTROLS
--------

NAVIGATION
  Up/Down arrows     Navigate options/items
  Left/Right arrows  Navigate within section / adjust values
  Tab                Cycle sections (Selection > Board > Stash > Skills > Hero)
  Enter              Confirm / Buy / Select
  Escape             Back / Cancel

QUICK NAVIGATION
  B                  Go to Board
  V                  Go to Hero (stats/skills)
  C                  Go to Choices/Selection
  F                  View enemy info (outside combat: navigate enemy items)
  G                  Go to Stash

ITEM MANAGEMENT
  Shift+Up           Move item to Stash
  Shift+Down         Move item to Board
  Shift+Left/Right   Reorder items on Board

DETAILED INFO
up/down arrows, Read card text line by line and hero stats
Alternative keys,   Ctrl+Up/Down       Read item details line by line / Navigate hero stats
  Ctrl+Left/Right    Switch hero subsection (Stats <-> Skills)
  I                  Show item properties/keywords descriptions

GAME ACTIONS
  E                  Exit current state
  R                  Reroll/Refresh shop
  Space              Open/Close Stash

MESSAGE BUFFER
  . (period)         Read last message
  , (comma)          Read previous message

INFO
  T                  Board capacity (slots used/available)
  S                  Stash capacity (items/total)
control+m, switch combat reading modes, batched/wave mode > individual actions mode.

OTHER
  F1                 Help


DURING COMBAT
-------------
Board Navigation (available after items appear):
  B                  Navigate your board (arrow keys to move, Up/Down for details)
  G                  Navigate enemy board (arrow keys to move, Up/Down for details)
  F                  Navigate enemy stats (Right arrow for skills)
  V                  Navigate your hero stats
  Backspace          Exit current navigation mode

Quick Stats (number row):
  1                  Your health
  2                  Enemy health
  3                  Damage dealt
  4                  Damage taken

Other Combat Keys:
  H                  Combat summary (damage dealt/taken, health)
  Ctrl+M             Toggle combat mode (batched waves vs individual actions)

Combat Modes:
- Batched mode: Wave-based narration, effects grouped into summaries
- Individual mode: Every card trigger announced, use 1-4 for health info
- "Low health!" / "Critical health!" alerts in both modes
- "Victory! X wins" or "Defeat! Lost X prestige" at end

POST-COMBAT
-----------
  Enter              Continue to next phase
  R                  Replay the combat (with narration)
  E                  Open Recap (static view of both boards)
  G                  View opponent's board (navigate with arrows)
  V                  View your hero stats
  F                  View enemy stats


LOGIN SCREENS
-------------
- Up/Down to navigate fields and buttons
- Enter on text field: enter edit mode ("editing")
- Enter again: exit edit mode ("done")
- Left/Right on toggles: toggle on/off


FEATURES
--------
- Full keyboard navigation (no mouse required)
- Screen reader announcements via Tolk
- Real-time combat narration
- Victory/defeat announcements
- Item property descriptions (I key)
- Visual feedback for sighted spectators
- Tutorial support
- Login/account creation accessible


REQUIREMENTS
------------
- The Bazaar (Steam)
- BepInEx 5.x
- Screen reader (NVDA, JAWS, etc.)
- Tolk library


SOURCE CODE
-----------
https://github.com/Ali-Bueno/bazaar-access
