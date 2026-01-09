# Bazaar Access

Plugin de BepInEx para hacer el juego "The Bazaar" accesible para personas ciegas usando Tolk como lector de pantalla.

## Código del juego

El código descompilado del juego está en: `D:\code\unity and such\bazaar access\bazaar code`
- **NO buscar en otros sitios**
- **Código más importante**: `bazaar code\TheBazaarRuntime` - buscar aquí primero
- Si no se encuentra, buscar en otras carpetas de `bazaar code`
- Subcarpetas clave en TheBazaarRuntime:
  - `TheBazaar\` - Clases principales (Events, AppState, Data, BoardManager)
  - `TheBazaar.SequenceFramework\` - Condiciones de eventos
  - Archivos raíz - BoardManager.cs, CardController.cs, etc.

## Estructura del proyecto

```
BazaarAccess/
├── Plugin.cs                      # Punto de entrada del plugin
├── Accessibility/
│   ├── AccessibleMenu.cs         # Menú navegable con posición
│   ├── MenuOption.cs             # Opción de menú con delegados
│   ├── AccessibilityMgr.cs       # Gestor central (Screen + UI stack)
│   ├── IAccessibleScreen.cs      # Interfaz para pantallas
│   ├── IAccessibleUI.cs          # Interfaz para popups/diálogos
│   ├── BaseScreen.cs             # Clase base para pantallas
│   └── BaseUI.cs                 # Clase base para UIs (popups)
├── Screens/
│   ├── HeroSelectScreen.cs       # Pantalla de selección de héroe
│   └── MainMenuScreen.cs         # Pantalla del menú principal
├── Gameplay/
│   ├── GameplayScreen.cs         # Pantalla principal del gameplay
│   ├── GameplayNavigator.cs      # Navegador principal por secciones
│   ├── BoardNavigator.cs         # Navegación alternativa por zonas
│   ├── ItemReader.cs             # Lectura de info de cartas/items
│   └── ActionHelper.cs           # Comprar/vender/mover sin drag-drop
├── Core/
│   ├── TolkWrapper.cs            # Wrapper para Tolk (screen reader)
│   ├── KeyboardNavigator.cs      # Manejo de entrada de teclado
│   └── MessageBuffer.cs          # Buffer circular de mensajes del juego
├── UI/
│   ├── OptionsUI.cs              # Diálogo de opciones (main + gameplay settings)
│   ├── FightMenuUI.cs            # Menú de pausa durante gameplay
│   ├── ConfirmActionUI.cs        # Popup de confirmación compra/venta
│   └── GenericPopupUI.cs         # Popups genéricos (tutoriales, mensajes)
└── Patches/
    ├── ViewControllerPatch.cs    # Detecta cambios de vista
    ├── PopupPatch.cs             # Popups genéricos
    ├── OptionsDialogPatch.cs     # Menú de opciones desde menú principal
    ├── FightMenuPatch.cs         # Menú de pausa y opciones durante gameplay
    ├── HeroChangedPatch.cs       # Cambio de héroe
    ├── GameplayPatch.cs          # Detecta entrada al gameplay (BoardManager.OnAwake)
    └── StateChangePatch.cs       # Suscripción a eventos del juego en tiempo real
```

## Arquitectura: Screens y UIs

Seguimos el patrón de accesibilidad de Hearthstone, separando:

- **Screens**: Pantallas principales (menú principal, selección de héroe, etc.)
- **UIs**: Popups/diálogos que se apilan sobre las pantallas (opciones, confirmaciones, etc.)

### AccessibilityMgr

Gestor central que maneja:
- Una **Screen** activa (la pantalla de fondo)
- Un **stack de UIs** (popups apilados)
- Distribuye input al componente con foco (UI más reciente o Screen)

```csharp
AccessibilityMgr.SetScreen(screen);  // Cambiar pantalla (limpia stack de UIs)
AccessibilityMgr.ShowUI(ui);         // Mostrar popup (push al stack)
AccessibilityMgr.HideUI(ui);         // Cerrar popup específico
AccessibilityMgr.PopUI();            // Cerrar popup más reciente
```

### AccessibleMenu

Menú navegable que:
- Mantiene lista de opciones y índice actual
- Lee con posición: "Texto, elemento X de Y"
- Soporta navegación vertical y ajuste horizontal

### MenuOption

Opción de menú con delegados para máxima flexibilidad:
- `Func<string> GetText`: Texto dinámico (para valores que cambian)
- `Action OnConfirm`: Al presionar Enter
- `Action<int> OnAdjust`: Al presionar izq/der (-1/+1)

## Controles

- **Flechas arriba/abajo**: Navegar por opciones
- **Flechas izquierda/derecha**: Ajustar valores (sliders, dropdowns, toggles)
- **Enter**: Activar opción
- **Escape**: Volver/cerrar
- **F1**: Ayuda

## Menús durante Gameplay

### Menú de Pausa (FightMenuPatch.cs)

`FightMenuDialog` es el controlador del menú de pausa. Los parches:
- `FightMenuShowPatch`: Crea FightMenuUI al abrir el menú de pausa
- `FightMenuHidePatch`: Cierra FightMenuUI
- `FightMenuOptionsClickPatch`: Transición pausa → opciones
- `FightMenuOptionsClosedPatch`: Transición opciones → pausa

### OptionsUI con Secciones

`OptionsUI` detecta automáticamente si estamos en la sección principal o en "Gameplay Settings" y muestra solo las opciones relevantes. Los keybinds se leen con reflexión para mostrar "Acción: Tecla".

## Añadir una nueva pantalla (Screen)

1. Crear clase en `Screens/` que herede de `BaseScreen`:

```csharp
public class MiScreen : BaseScreen
{
    public override string ScreenName => "Mi Pantalla";

    public MiScreen(Transform root) : base(root) { }

    protected override void BuildMenu()
    {
        // Usar texto dinámico del juego para multilenguaje
        Menu.AddOption(
            () => GetButtonTextByName("Btn_Play"),
            () => ClickButtonByName("Btn_Play"));

        Menu.AddOption(
            () => GetButtonTextByName("Btn_Options"),
            () => ClickButtonByName("Btn_Options"));
    }
}
```

2. Registrar en `ViewControllerPatch.cs`:

```csharp
case "MiView":
    AccessibilityMgr.SetScreen(new MiScreen(root));
    break;
```

## Añadir un popup/diálogo (UI)

1. Crear clase en `UI/` que herede de `BaseUI`:

```csharp
public class MiPopupUI : BaseUI
{
    public override string UIName => "Mi Popup";

    public MiPopupUI(Transform root) : base(root) { }

    protected override void BuildMenu()
    {
        AddButtonIfActive("Btn_Confirm");
        AddButtonIfActive("Btn_Cancel");
    }

    protected override void OnBack()
    {
        ClickButtonByName("Btn_Cancel");
    }
}
```

2. Crear patch con Harmony:

```csharp
[HarmonyPatch(typeof(MiPopupController), "OnEnable")]
public static class MiPopupShowPatch
{
    [HarmonyPostfix]
    public static void Postfix(MonoBehaviour __instance)
    {
        var ui = new MiPopupUI(__instance.transform);
        AccessibilityMgr.ShowUI(ui);
    }
}

[HarmonyPatch(typeof(MiPopupController), "OnDisable")]
public static class MiPopupHidePatch
{
    [HarmonyPostfix]
    public static void Postfix()
    {
        AccessibilityMgr.PopUI();
    }
}
```

## Helpers disponibles en BaseScreen/BaseUI

```csharp
// Buscar y hacer click en botones
ClickButtonByName("Btn_Play");      // Por nombre del GameObject
ClickButtonByText("Play");          // Por texto visible

// Obtener texto dinámico (multilenguaje)
GetButtonTextByName("Btn_Play");    // Retorna el texto actual del botón

// Buscar controles
FindToggle("Toggle_VSync");
FindSlider("Slider_Master");

// Logging
LogAllButtons();                    // Debug: lista todos los botones
```

## Anuncio de estados

Al interactuar con controles, se anuncia el nuevo valor:
- **Toggles**: "on" / "off"
- **Sliders**: "65%"
- **Dropdowns**: El texto de la opción seleccionada

## Convenciones

- No decir el tipo de elemento (botón, slider) - solo estados y valores
- Usar `GetButtonTextByName()` para obtener textos del juego (multilenguaje)
- Al cambiar de menú/pantalla, anunciar el título
- Usar `Plugin.Logger.LogInfo()` para debug
- Las búsquedas por nombre son case-insensitive

## Dependencias

- BepInEx 5.x
- Harmony (incluido en BepInEx)
- Tolk (TolkDotNet.dll en carpeta references/)
- Referencias del juego en `TheBazaar_Data/Managed/`

## Compilación

```bash
cd BazaarAccess
dotnet build
```

El DLL se copia automáticamente a la carpeta de plugins de BepInEx.

## Notas

- El código descompilado del juego está en `bazaar code/` (no incluido en git)
- Las referencias de Tolk están en `references/` (no incluido en git)
- Plugin.Instance está disponible para iniciar coroutines desde cualquier lugar

---

## Gameplay - Estructura del Juego

### Flujo de una Partida (Run)

1. **Selección de héroe**: Vanessa, Pygmalien, Dooley, Mak, Stelle
2. **Días y horas**: Cada día tiene 6 horas
   - Horas 1-5: Eventos (Merchants, PvE, eventos especiales)
   - Hora 6: PvP automático contra otro jugador
3. **Objetivo**: Conseguir 10 victorias PvP

### Tablero del Jugador

- **5-10 slots de items** (`playerItemSockets[0-9]`) - empieza con 5, se expande al subir de nivel
- **4 slots de habilidades** (`playerSkillSockets[0-3]`)
- **Stash/Almacén**: Guardar items sin efecto en batalla
- **Prestigio**: Vida de la partida (si llega a 0, termina)

### Clases Principales del Juego

```
BoardManager              - Gestor central del tablero
├── playerItemSockets[]   - ItemSocketController[10]
├── playerSkillSockets[]  - SkillSocketController[4]
├── playerStashAnchor     - Almacén
└── playerBoardAnchor     - Tablero

ItemController           - Control de items (drag-drop)
├── CardData             - Datos de la carta (ItemCard)
├── boardSection         - ESections (Player/Storage/Opponent)
└── IsDragging           - Estado de arrastre

AppState                 - Comandos del juego
├── BuyItemCommand()     - Comprar item
├── SellCardCommand()    - Vender item
└── MoveCardCommand()    - Mover item entre sockets
```

### Atributos de Cartas (ECardAttributeType)

- `Strength`, `Toughness`, `Speed` - Stats base
- `BuyPrice`, `SellPrice` - Precios
- `Ammo`, `Haste`, `Cooldown` - Mecánicas
- `Heal`, `Poison`, `Burn`, `Slow` - Efectos

### Atributos del Jugador (EPlayerAttributeType)

- `Health`, `Shield` - Vida
- `Gold`, `Income` - Economía
- `Level`, `Experience`, `Prestige` - Progreso

### Sistema de Compra/Venta (Sin Drag-Drop)

```csharp
// Comprar item del merchant
AppState.CurrentState.BuyItemCommand(itemCard, EInventorySection.Hand);

// Vender item
AppState.CurrentState.SellCardCommand(itemCard);

// Mover item entre Hand y Stash
AppState.CurrentState.MoveCardCommand(itemCard, targetSockets, section);
```

### Secciones del Inventario

```csharp
enum EInventorySection {
    Hand,      // Items equipados (tablero activo)
    Stash,     // Almacén (sin efecto en batalla)
    Opponent   // Items del merchant
}

enum EContainerSocketId {
    Socket_1 = 0, Socket_2 = 1, ... Socket_10 = 9
}
```

### Localización

- `LocalizationService`: SQLite con traducciones
- Idiomas: en-US, de-DE, pt-BR, zh-CN, ko-KR
- `TCardLocalization`: Nombres y descripciones localizados

---

## Gameplay Implementado

### GameplayScreen

Pantalla accesible principal del gameplay que implementa `IAccessibleScreen`. Se activa automáticamente cuando `BoardManager.OnAwake` se ejecuta (entrada al gameplay).

**Controles generales**:
- `Tab`: Cambiar sección (Selection → Board → Stash → Skills → Hero)
- `B`: Ir directamente al Board
- `V`: Ir directamente a Hero (Stats/Skills)
- `C`: Ir directamente a Choices/Selection
- `F`: Ver información del enemigo/NPC
- `Flechas izq/der`: Navegar items en la sección actual
- `Ctrl+Arriba/Abajo`: Leer información detallada del item línea por línea (Arriba=siguiente, Abajo=anterior)
- `Enter`: Acción contextual (comprar/vender/seleccionar)
- `E`: Salir del estado actual (Exit)
- `R`: Refrescar tienda (Reroll)
- `Espacio`: Ir al Stash
- `Shift+Arriba`: Mover item al Stash
- `Shift+Abajo`: Mover item al Board
- `Shift+Izq/Der`: Reordenar items en el Board
- `.` (punto): Leer último mensaje
- `,` (coma): Leer mensaje anterior

**Controles en Hero (V)**:
- `Ctrl+Arriba`: Siguiente stat o skill
- `Ctrl+Abajo`: Stat o skill anterior
- `Ctrl+Derecha`: Cambiar subsección (Stats → Skills)
- `Ctrl+Izquierda`: Cambiar subsección (Skills → Stats)
- `Enter`: Leer todos los stats resumidos

**Modo Combate**:
Durante el combate, la navegación se simplifica:
- Solo está disponible la sección Hero (V) con Ctrl+flechas
- Usa `F` para ver los stats del enemigo (vida, escudo)
- El combate se anuncia automáticamente al iniciar y terminar

### GameplayNavigator

Navegador principal con 5 secciones:

```csharp
enum NavigationSection {
    Selection,  // Items/skills/encounters del SelectionSet + Exit/Reroll
    Board,      // Items equipados del jugador (10 slots)
    Stash,      // Almacén del jugador
    Skills,     // Habilidades del jugador (4 slots)
    Hero        // Stats y skills del héroe
}
```

**Subsecciones de Hero**:
```csharp
enum HeroSubsection {
    Stats,      // Estadísticas (Health, Gold, Level, etc.)
    Skills      // Habilidades equipadas del héroe
}
```

**NavItem**: Puede ser una carta o una acción (Exit, Reroll):
```csharp
enum NavItemType { Card, Exit, Reroll }
```

### ItemReader

Lee información localizada de las cartas:
- `GetCardName()`: Nombre traducido
- `GetTierName()`: Bronze/Silver/Gold/Diamond/Legendary
- `GetBuyPrice()`, `GetSellPrice()`: Precios
- `GetDetailedDescription()`: Info completa con stats y efectos
- `GetEncounterInfo()`: Nombre + tipo de encuentro
- `GetFlavorText()`: Texto narrativo

### ActionHelper

Ejecuta acciones del juego sin drag-drop:
- `BuyItem(card, toStash)`: Compra item al Board o Stash
- `SellItem(card)`: Vende item
- `MoveItem(card, toStash)`: Mueve entre Board y Stash
- `SelectSkill(card)`: Selecciona habilidad
- `SelectEncounter(card)`: Selecciona encuentro

### StateChangePatch

Suscripción a eventos nativos del juego (sin delays arbitrarios):

**Eventos de TheBazaar.Events:**
- `StateChanged`: Cambios de estado del juego
- `BoardTransitionFinished`: Cuando terminan las animaciones de transición
- `NewDayTransitionAnimationFinished`: Cuando termina la animación de nuevo día
- `ItemCardsRevealed` / `SkillCardsRevealed`: Cuando se revelan cartas (después de animación)
- `CombatStarted` / `CombatEnded`: Inicio y fin de combate
- `CardPurchasedSimEvent` / `CardSoldSimEvent`: Compra/venta

**Eventos de AppState:**
- `StateExited`: Cuando se sale de un estado
- `EncounterEntered`: Cuando se entra en un encuentro

**Eventos de BoardManager:**
- `ItemCardsRevealed`: Cuando los items son revelados (UI lista)
- `SkillCardsRevealed`: Cuando las skills son reveladas

### ConfirmActionUI

UI de confirmación para compra/venta:
- Pregunta "Buy X for Y gold?" o "Sell X for Y gold?"
- Navega entre Confirm/Cancel con flechas
- Enter confirma, Escape cancela

### MessageBuffer

Buffer circular de hasta 50 mensajes del juego:
- `.` (punto): Lee el mensaje más reciente
- `,` (coma): Lee mensajes anteriores
- Los popups del juego añaden su contenido al buffer

### Estados del Juego (ERunState)

```
Choice      → "Shop, X items"
Encounter   → "X encounters"
Combat      → "Combat"
PVPCombat   → "PvP Combat"
Loot        → "X rewards"
LevelUp     → "Level up, X skills"
Pedestal    → "Upgrade"
EndRunVictory → "Victory"
EndRunDefeat  → "Defeat"
```

Los anuncios son simplificados y no redundantes.

### Modo Combate

Durante el combate (`ERunState.Combat` o `ERunState.PVPCombat`):
- **Solo V y F funcionan**: V para Hero stats, F para Enemy stats
- **Todas las demás teclas están desactivadas**: B, C, Tab, flechas de navegación, etc.
- El tablero está "volteado" (`IsBoardFlipped`) y no es accesible visualmente
- Se anuncia "Entering combat" al iniciar y "Exiting combat" al terminar

### Post-Combate (ReplayState)

Después del combate, se entra en `ReplayState`:
- Se anuncia: "Combat finished. Press Enter to continue, R to replay, or E for recap."
- **Enter**: Continuar (sale del ReplayState)
- **R**: Repetir el combate (replay)
- **E**: Ver resumen (recap)
- **V**: Ver stats del héroe
- **F**: Ver stats del enemigo
- Todas las demás teclas están desactivadas

### Stash (Almacén)

El stash se abre/cierra con **Espacio** en el juego:
- `Events.StorageToggled` dispara cuando cambia
- `Data.IsStorageOpen` indica el estado actual
- La navegación solo muestra el Stash cuando está abierto
- Si el stash se cierra mientras navegas en él, automáticamente sales a otra sección

### Auto-Exit vs Manual Exit

Algunos estados salen automáticamente después de seleccionar:
- `SelectionContextRules.CanExit = true` → Se muestra opción Exit, usuario debe presionar E
- `SelectionContextRules.CanExit = false` → No hay Exit, seleccionar auto-continúa

El anuncio indica "select to continue" cuando el estado auto-saldrá.

---

## Eventos del Juego Suscritos (StateChangePatch)

### TheBazaar.Events (via reflexión)
- `StateChanged`: Cambios de estado del juego
- `BoardTransitionFinished`: Animaciones de transición completadas
- `NewDayTransitionAnimationFinished`: Animación de nuevo día completada
- `ItemCardsRevealed` / `SkillCardsRevealed`: Cartas reveladas (UI lista)
- `CombatStarted` / `CombatEnded`: Inicio y fin de combate
- `CardPurchasedSimEvent` / `CardSoldSimEvent`: Compra/venta confirmada por servidor
- `CardDisposedSimEvent`: Cartas eliminadas de la selección
- `CardSelected`: Carta seleccionada (skills)
- `StorageToggled`: Stash abierto/cerrado
- `OnBoardChanged`: Cambios en el tablero

### AppState Events (C# events)
- `ItemPurchased`: Item comprado/seleccionado (incluye loot)

### AppState Static Events (via reflexión)
- `StateExited`: Salida de un estado
- `EncounterEntered`: Entrada a un encuentro

### BoardManager Events
- `ItemCardsRevealed`: Items revelados
- `SkillCardsRevealed`: Skills reveladas

---

## Sistema de Slots del Tablero

El tablero del jugador tiene un sistema de slots que se expande conforme subes de nivel:

- **Fórmula**: `boardSize = UnlockedSocketCount + 5`
- **Inicio**: 5 slots desbloqueados
- **Expansión**: El servidor envía `GameSimEventSocketsUnlocked` al subir de nivel
- **Validación**: 100% servidor - el mod NO puede saltarse restricciones

El mod usa los mismos comandos que el juego original:
- `BuyItemCommand()` → valida espacio con `HasSpaceFor(card)`
- `MoveCardCommand()` → valida slots desbloqueados con `IsSocketLocked()`

## Vender Items

Vender está permitido en casi todos los estados del juego (comportamiento original):

| Estado | Vender permitido |
|--------|------------------|
| Shop (Choice) | ✅ |
| Encounter | ✅ |
| Loot | ✅ |
| Level Up | ✅ |
| Pedestal | ✅ |
| ReplayState | ✅ |

Cada estado define `AllowedOps` que incluye `StateOps.SellItem`.

---

## Implementado Recientemente

- ✅ Sistema de eventos nativos del juego (sin delays arbitrarios)
- ✅ Modo combate simplificado (solo Hero y Enemy con V/F)
- ✅ Restricción completa de teclas durante combate (B, C, Tab desactivados)
- ✅ Información del enemigo con tecla F
- ✅ Reordenamiento de items en el Board (Shift+Izq/Der)
- ✅ Mover items entre Board y Stash (Shift+Arriba=Stash, Shift+Abajo=Board)
- ✅ Lectura detallada línea por línea (Ctrl+Arriba/Abajo)
- ✅ Detección de estado del Stash (abierto/cerrado) via Harmony patch
- ✅ Post-combate accesible (ReplayState con Enter=continuar, R=replay, E=recap)
- ✅ Refresh de UI después de seleccionar items (CardSelected, ItemPurchased, CardDisposed)
- ✅ Detección de auto-exit ("select to continue")
- ✅ Anuncios simplificados de estado (Shop/Encounters/Loot sin redundancia)
- ✅ Subsecciones de Hero (Stats/Skills) con navegación Ctrl+Izq/Der
- ✅ Navegación en Hero con Ctrl+Arriba/Abajo (en vez de flechas normales)
- ✅ Anuncios de combate ("Entering combat" / "Exiting combat")
- ✅ V y F funcionan durante ReplayState para ver stats
- ✅ Secciones vacías no se anuncian

## Pendiente por Implementar

### Anuncios de Eventos
- Subida de nivel
- Victoria/Derrota en PvP
- Cambios de prestigio

### Mejoras de Navegación
- Preview de sinergia de items
- Acceso rápido a información de cooldowns en batalla
