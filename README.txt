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
1 through 4 on number row: your health, enemy health, damage delt, damage taken.
- V (hero stats) and F (enemy stats)
- H                  Combat summary (damage dealt/taken, health)
- if in batched combat mode,  Wave-based narration: effects grouped into summaries
- If in individual action mode, You will hear every card trigger. auto health announcements are disabled, use the 1 through 4 row number keys to quickly read health related info.
- "Low health!" / "Critical health!" alerts
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
