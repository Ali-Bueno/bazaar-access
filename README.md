# BAZAAR ACCESS - Accessibility Mod for The Bazaar

A BepInEx plugin that makes The Bazaar accessible for blind players using screen readers (via Tolk).

---

## Installation

### Full Release (includes BepInEx)
1. Download [BazaarAccess-full.zip](https://github.com/Ali-Bueno/bazaar-access/releases/latest/download/BazaarAccess-full.zip)
2. Extract all files from the ZIP
3. Copy all extracted files to your game's main folder (e.g., `D:\games\steam\steamapps\common\The Bazaar\`)
4. The mod will load automatically when you launch the game

### Update Only (if you already have BepInEx installed)
1. Download [BazaarAccess-update.zip](https://github.com/Ali-Bueno/bazaar-access/releases/latest/download/BazaarAccess-update.zip)
2. Extract `BazaarAccess.dll`
3. Copy it to the `BepInEx/plugins/` folder in your game directory

---

## Controls

### Navigation

| Key | Action |
| :--- | :--- |
| **Up/Down arrows** | Navigate options/items |
| **Left/Right arrows** | Navigate within section / adjust values |
| **Tab** | Cycle sections (Selection > Board > Stash > Skills > Hero) |
| **Enter** | Confirm / Open Action Menu on items / Buy from shop |
| **Backspace** | Back / Cancel |

### Quick Navigation

| Key | Action |
| :--- | :--- |
| **B** | Go to Board |
| **V** | Go to Hero (stats/skills) |
| **C** | Go to Choices/Selection |
| **F** | View enemy info (outside combat: navigate enemy items) |
| **G** | Go to Stash |

### Item Management

| Key | Action |
| :--- | :--- |
| **Shift+Up** | Move item to Stash |
| **Shift+Down** | Move item to Board |
| **Shift+Left/Right** | Reorder items on Board |

### Action Menu 
*(Press `Enter` on board/stash item to open)*

| Key | Action |
| :--- | :--- |
| **Up/Down** | Navigate options (Sell, Upgrade, Enchant, Move) |
| **Enter** | Confirm selected option |
| **Backspace** | Exit action menu |
| **S** | Sell item directly |
| **U** | Upgrade/Enchant item directly |
| **M** | Move to stash/board directly |
| **Left/Right** | Reorder item on board |
| **Home/End** | Move item to left/right edge of board |

### Detailed Info

| Key | Action |
| :--- | :--- |
| **Up/Down arrows** | Read card text line by line and hero stats |
| **Ctrl+Up/Down** | Read item details line by line / Navigate hero stats |
| **Ctrl+Left/Right** | Switch hero subsection (Stats <-> Skills) |
| **I** | Show item properties/keywords descriptions |

### Game Actions & Info

| Key | Action |
| :--- | :--- |
| **E** | Exit current state |
| **R** | Reroll/Refresh shop |
| **Space** | Open/Close Stash |
| **T** | Board capacity (slots used/available) |
| **S** | Stash capacity (items/total) |
| **Ctrl+M** | Switch combat reading modes (batched/wave mode > individual actions mode) |
| **. (Period)** | Read last message |
| **, (Comma)** | Read previous message |
| **F1** | Help |

---

## During Combat

### Board Navigation 
*(Available after items appear)*

| Key | Action |
| :--- | :--- |
| **B** | Navigate your board (arrow keys to move, Up/Down for details) |
| **G** | Navigate enemy board (arrow keys to move, Up/Down for details) |
| **F** | Navigate enemy stats (Right arrow for skills) |
| **V** | Navigate your hero stats |
| **Backspace** | Exit current navigation mode |

### Quick Stats & Combat Actions

| Key | Action |
| :--- | :--- |
| **1** | Your health |
| **2** | Enemy health |
| **3** | Damage dealt |
| **4** | Damage taken |
| **H** | Combat summary (damage dealt/taken, health) |
| **Ctrl+M** | Toggle combat mode (batched waves vs individual actions) |

### Combat Modes
* **Batched mode:** Wave-based narration, effects grouped into summaries.
* **Individual mode:** Every card trigger announced, use `1`-`4` for health info.
* **Alerts:** "Low health!" / "Critical health!" alerts function in both modes.
* **End of Match:** "Victory! X wins" or "Defeat! Lost X prestige" announced at the end.

---

## Post-Combat

| Key | Action |
| :--- | :--- |
| **Enter** | Continue to next phase |
| **R** | Replay the combat (with narration) |
| **E** | Open Recap (static view of both boards) |
| **G** | View opponent's board (navigate with arrows) |
| **V** | View your hero stats |
| **F** | View enemy stats |

---

## Login Screens

* **Up/Down:** Navigate fields and buttons.
* **Enter (on text field):** Enter edit mode ("editing").
* **Enter (again):** Exit edit mode ("done").
* **Left/Right (on toggles):** Toggle on/off.

---

## Features

* Full keyboard navigation (no mouse required)
* Screen reader announcements via Tolk
* Real-time combat narration
* Victory/defeat announcements
* Item property descriptions (I key)
* Visual feedback for sighted spectators
* Tutorial support
* Login/account creation accessible

---

## Requirements

* [The Bazaar (Steam)](https://store.steampowered.com/app/1617400/The_Bazaar/)
* BepInEx 5.x
* Screen reader (NVDA, JAWS, etc.)
* Tolk library

---

## Source Code

[GitHub Repository: Ali-Bueno/bazaar-access](https://github.com/Ali-Bueno/bazaar-access)