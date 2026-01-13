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
│   ├── TextFieldOption.cs        # Opción especial para campos de texto con modo edición
│   ├── AccessibilityMgr.cs       # Gestor central (Screen + UI stack)
│   ├── IAccessibleScreen.cs      # Interfaz para pantallas
│   ├── IAccessibleUI.cs          # Interfaz para popups/diálogos
│   ├── BaseScreen.cs             # Clase base para pantallas
│   └── BaseUI.cs                 # Clase base para UIs (popups)
├── Screens/
│   ├── HeroSelectScreen.cs       # Pantalla de selección de héroe
│   ├── MainMenuScreen.cs         # Pantalla del menú principal
│   ├── ChestSceneScreen.cs       # Pantalla de apertura de baúles
│   ├── CollectionScreen.cs       # Pantalla de colección/cosméticos
│   └── BattlePassScreen.cs       # Pantalla del pase de temporada
├── Gameplay/
│   ├── GameplayScreen.cs         # Pantalla principal del gameplay
│   ├── GameplayNavigator.cs      # Navegador principal por secciones
│   ├── BoardNavigator.cs         # Navegación alternativa por zonas
│   ├── ItemReader.cs             # Lectura de info de cartas/items
│   ├── ActionHelper.cs           # Comprar/vender/mover sin drag-drop
│   └── CombatDescriber.cs        # Narración del combate en tiempo real
├── Core/
│   ├── TolkWrapper.cs            # Wrapper para Tolk (screen reader)
│   ├── KeyboardNavigator.cs      # Manejo de entrada de teclado
│   └── MessageBuffer.cs          # Buffer circular de mensajes del juego
├── UI/
│   ├── OptionsUI.cs              # Diálogo de opciones (main + gameplay settings)
│   ├── FightMenuUI.cs            # Menú de pausa durante gameplay
│   ├── ConfirmActionUI.cs        # Popup de confirmación compra/venta
│   ├── GenericPopupUI.cs         # Popups genéricos (tutoriales, mensajes)
│   ├── TutorialUI.cs             # UI accesible para el tutorial (FTUE)
│   ├── ChestRewardsUI.cs         # Popup de recompensas de cofres (solo Enter cierra)
│   └── Login/                    # Sistema de login/cuenta accesible
│       ├── LoginBaseUI.cs        # Clase base con modo edición para campos de texto
│       ├── LandingUI.cs          # Pantalla inicial (Link/Create Account)
│       ├── LoginUI.cs            # Email + Password
│       ├── CreateAccountEmailUI.cs       # Email + Confirm Email
│       ├── CreateAccountUserPasswordUI.cs # Username + Password + Confirm
│       ├── CreateAccountTermsUI.cs       # ToS + EULA toggles
│       ├── ForgotPasswordUI.cs   # Recuperación de contraseña
│       ├── ResetEmailUI.cs       # Cambio de email
│       ├── ForgotPasswordConfirmUI.cs    # Confirmación de reset
│       ├── AccountVerifiedUI.cs  # Cuenta verificada
│       ├── RegistrationFailedUI.cs       # Error de registro
│       └── AccessDeniedUI.cs     # Acceso denegado
└── Patches/
    ├── ViewControllerPatch.cs    # Detecta cambios de vista
    ├── PopupPatch.cs             # Popups genéricos
    ├── OptionsDialogPatch.cs     # Menú de opciones desde menú principal
    ├── FightMenuPatch.cs         # Menú de pausa y opciones durante gameplay
    ├── HeroChangedPatch.cs       # Cambio de héroe
    ├── GameplayPatch.cs          # Detecta entrada al gameplay (BoardManager.OnAwake)
    ├── StateChangePatch.cs       # Suscripción a eventos del juego en tiempo real
    ├── TutorialPatch.cs          # Accesibilidad del tutorial (FTUE)
    ├── EndOfRunPatch.cs          # Pantalla de fin de partida
    ├── LoginPatch.cs             # Sistema de login/cuenta (11 StateViews)
    └── MenuPatches.cs            # Detección de menús (baúles, colección, pase)
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
- **Home**: Ir al primer elemento de la lista
- **End**: Ir al último elemento de la lista
- **Page Up**: Retroceder 10 elementos (o al principio si hay menos de 10)
- **Page Down**: Avanzar 10 elementos (o al final si hay menos de 10)

**Nota**: La navegación NO hace wrap-around. Al llegar al límite de una lista, se anuncia "Start of list" o "End of list".

### Controles en Hero Select

- **Flechas arriba/abajo**: Navegar entre héroes y botones
- **Enter**: Seleccionar héroe (o abrir diálogo de compra si está bloqueado)
- **Ctrl+Arriba/Abajo**: Leer detalles del héroe (nombre, título, descripción, estado)
- **Escape**: Volver

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

## Changelog

El archivo `changelog.txt` documenta los cambios del mod. **Formato importante**:
- Ordenar de más reciente a más antiguo (las entradas más nuevas van arriba)
- Agrupar por fecha (ej: "## January 12, 2026")
- Cada fecha tiene secciones: "### New Features", "### Bug Fixes", "### Improvements"
- No mencionar nombres de archivos de código, solo describir qué se arregló o añadió

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
- `I`: Ver propiedades/keywords del item (tags, descripciones)
- `Shift+U`: Upgradear item en el Pedestal
- `T`: Ver capacidad del tablero (slots usados/disponibles)
- `S`: Ver capacidad del stash (slots usados/disponibles)

**Controles en Hero (V)**:
- `Ctrl+Arriba`: Siguiente stat o skill
- `Ctrl+Abajo`: Stat o skill anterior
- `Ctrl+Derecha`: Cambiar subsección (Stats → Skills)
- `Ctrl+Izquierda`: Cambiar subsección (Skills → Stats)
- `Enter`: Leer todos los stats resumidos

**Modo Enemigo (F)**:
Fuera de combate, presionar F activa el "modo enemigo":
- Muestra stats del enemigo + número de items/skills
- `Ctrl+Arriba/Abajo`: Navegar items del enemigo
- `Enter`: Leer detalles del item actual del enemigo
- `Escape` o cualquier otra tecla: Salir del modo enemigo

**Modo Combate**:
Durante el combate, la navegación se simplifica:
- Solo está disponible la sección Hero (V) con Ctrl+flechas
- Usa `F` para ver los stats del enemigo (vida, escudo) - sin navegación de items
- Se anuncia "Entering combat" al iniciar y "Exiting combat" al terminar
- No se anuncia el tablero ni número de items al entrar en combate

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

Lee información localizada de las cartas con resolución de tokens:
- `GetCardName()`: Nombre traducido
- `GetTierName()`: Bronze/Silver/Gold/Diamond/Legendary
- `GetTags()`: Tipos de item (Aquatic, Friend, Weapon, Food, etc.)
- `GetBuyPrice()`, `GetSellPrice()`: Precios
- `GetDetailedDescription()`: Info completa con stats y efectos
- `GetDescription()`: Descripción con tokens resueltos (ej: `{DamageAmount}` → "25")
- `GetAbilityTooltips()`: Tooltips de habilidades con valores reales
- `GetEncounterInfo()`: Nombre + tipo de encuentro (para PvP: "NombreJugador (Héroe), PvP")
- `GetEncounterDetailedInfo()`: Para PvP incluye: nombre, héroe, nivel, victorias, prestigio
- `GetFlavorText()`: Texto narrativo
- `GetDetailLines()`: Líneas de detalle sin prefijos ("Description:", etc.) para lectura limpia

**Tipos de Items (ECardTag)**: Weapon, Property, Food, Potion, Tool, Vehicle, Aquatic, Friend, Core, Ray, Dinosaur, Apparel, Toy, Tech, Dragon, Ingredient, Relic, Reagent, Map, Key, Drone.

**Resolución de tokens**: Los textos del juego usan tokens como `{DamageAmount}`, `{Cooldown}`, `{ability.0}`, etc. que se reemplazan automáticamente con los valores reales de la carta. Usa el sistema nativo `TooltipBuilder` del juego para resolver tokens de abilities (ej: `{ability.0}` busca la ability con ID "0" en el template). Los tiempos (Cooldown, Haste, etc.) se convierten de milisegundos a segundos (ej: "4.5s").

### ActionHelper

Ejecuta acciones del juego sin drag-drop:
- `BuyItem(card, toStash)`: Compra item al Board o Stash
- `SellItem(card)`: Vende item
- `MoveItem(card, toStash)`: Mueve entre Board y Stash
- `SelectSkill(card)`: Selecciona habilidad
- `SelectEncounter(card)`: Selecciona encuentro
- `UpgradeItem(card)`: Upgradea item en el Pedestal (solo en estado Pedestal)
- `ReorderItem(card, slot, direction)`: Reordena items en el Board

### StateChangePatch

Suscripción a eventos nativos del juego con sistema de debounce para evitar spam:

**Sistema de Debounce**: Cuando múltiples eventos se disparan casi simultáneamente (transiciones, cartas reveladas, etc.), los anuncios se agrupan en uno solo con un delay de 0.4 segundos. El refresh de datos es inmediato, solo el anuncio se agrupa.

**Eventos de TheBazaar.Events:**
- `StateChanged`: Cambios de estado del juego
- `BoardTransitionFinished`: Cuando terminan las animaciones de transición
- `NewDayTransitionAnimationFinished`: Cuando termina la animación de nuevo día
- `ItemCardsRevealed` / `SkillCardsRevealed`: Cuando se revelan cartas (después de animación)
- `CombatStarted` / `CombatEnded`: Inicio y fin de combate
- `CardPurchasedSimEvent` / `CardSoldSimEvent`: Compra/venta
- `PlayerSkillEquippedSimEvent`: Cuando una skill es equipada

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
- **R**: Repetir el combate (replay animado con Combat Describer)
- **E**: Ver Recap (vista estática de ambos tableros, SIN Combat Describer)
- **V**: Ver stats del héroe
- **F**: Ver stats del enemigo
- **G**: Ver tablero del oponente (Backspace para salir)
- Otras teclas están desactivadas

**Navegación del tablero del oponente (G):**
- **Flechas izq/der**: Navegar items o skills
- **Ctrl+Izq/Der**: Cambiar subsección (Items ↔ Skills)
- **Ctrl+Arriba/Abajo**: Leer detalles línea por línea
- **Enter**: Leer descripción completa
- **Backspace**: Salir del modo oponente
- **Recomendado**: Usar E (Recap) primero, luego G para navegar sin interrupciones
- Si R (Replay) está activo, G sugiere usar E en su lugar

**Diferencia entre Replay y Recap:**
- **Replay (R)**: Reproduce la batalla con animaciones. El Combat Describer narra los efectos.
- **Recap (E)**: Vista estática de los tableros. Sin animaciones ni narración. Ideal para revisar builds.

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

### GameServiceManager Events
- `OnCombatPvEFinish`: Resultado del combate (Win/Lose) - funciona para PvE y PvP

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
- ✅ Reordenamiento de items en el Board (Shift+Izq/Der) - el navegador sigue al item movido
- ✅ Mover items entre Board y Stash (Shift+Arriba=Stash, Shift+Abajo=Board)
- ✅ Lectura detallada línea por línea (Ctrl+Arriba/Abajo)
- ✅ Detección de estado del Stash (abierto/cerrado) via Events.StorageToggled
- ✅ Post-combate accesible (ReplayState con Enter=continuar, R=replay, E=recap)
- ✅ Refresh de UI después de seleccionar items (CardSelected, ItemPurchased, CardDisposed)
- ✅ Detección de auto-exit ("select to continue")
- ✅ Anuncios simplificados de estado (Shop/Encounters/Loot sin redundancia)
- ✅ Subsecciones de Hero (Stats/Skills) con navegación Ctrl+Izq/Der
- ✅ Navegación en Hero con Ctrl+Arriba/Abajo (en vez de flechas normales)
- ✅ Anuncios de combate ("Entering combat" / "Exiting combat")
- ✅ V y F funcionan durante ReplayState para ver stats
- ✅ Secciones vacías no se anuncian
- ✅ Modo enemigo: navegar items del oponente fuera de combate (F + Ctrl+flechas)
- ✅ PvP encounters muestran nombre del jugador + héroe (no solo el héroe)
- ✅ No se anuncia tablero al entrar en combate
- ✅ **Combat Describer**: Narración del combate en tiempo real
- ✅ Fix de sliders en opciones (ahora muestran 0-100% correctamente)
- ✅ **Skills en Hero**: Usa `Data.Run.Player.Skills` (actualización inmediata al equipar)
- ✅ **Resolución de tokens**: Tooltips muestran valores reales (`{DamageAmount}` → "25")
- ✅ **Sistema de debounce**: Reduce spam en transiciones agrupando anuncios (0.4s delay)
- ✅ **Reordenamiento mejorado**: Al mover con Shift+flechas, el navegador sigue al item
- ✅ **Tipos de items**: Tooltips muestran tipos (Aquatic, Friend, Weapon, etc.) via `GetTags()`
- ✅ **Tooltips limpios**: Sin prefijos innecesarios ("Description:", "Ability:", etc.)
- ✅ **Feedback visual**: Al navegar con teclado, las cartas muestran hover visual
- ✅ **Skills con descripción**: En Hero subsección Skills, se lee nombre + descripción
- ✅ **Tutorial (FTUE) accesible**: Lee diálogos del tutorial, Enter para continuar
- ✅ **Mensajes del tutorial en buffer**: Releer con punto/coma
- ✅ **Fix duplicados**: Eliminados mensajes duplicados (stash, end of run)
- ✅ **Sistema de Login Accesible**: Menús de inicio de sesión y creación de cuenta
- ✅ **FTUE no bloquea gameplay**: Tutorial permite navegación normal mientras muestra diálogos
- ✅ **Fix tokens de abilities**: Tooltips resuelven `{ability.0}`, `{ability.1}` usando TooltipBuilder del juego
- ✅ **Level up mejorado**: Anuncia "Level up to X! Choose a skill, Y available"
- ✅ **Prestige en Hero**: Se muestra el prestigio en la sección de stats del héroe (Ctrl+flechas)
- ✅ **Fix nombre oponente**: En PvE muestra "Enemy", en PvP muestra el nombre correcto del jugador
- ✅ **Anuncios de victoria/derrota**: "Victory! X wins" al ganar, "Defeat! Lost X prestige" al perder
- ✅ **I key para propiedades**: Presiona I para escuchar descripciones de tags y keywords del item
- ✅ **Código en inglés**: Todos los mensajes y comentarios del mod están en inglés
- ✅ **BaseUI initialization pattern**: Delayed BuildMenu() for UIs that need view fields (buildMenuNow: false + Initialize())
- ✅ **BazaarButtonController access fix**: Correct reflection for ButtonText (field not property) and Button/Selectable casting
- ✅ **Password validation display**: Shows validation status (too short, needs letter, needs number)
- ✅ **Username validation display**: Shows (available) or (not available) status
- ✅ **Validation summary in Create Account**: Shows "Requirements: X of 5. Missing: ..." for account creation
- ✅ **All Login UIs inherit from LoginBaseUI**: Consistent initialization and BazaarButton handling
- ✅ **Tutorial deduplication**: Fixed repeated tutorial messages with 2-second dedup window
- ✅ **Tutorial UI cleanup**: Closes previous TutorialUI before creating new one (no stack buildup)
- ✅ **Shift+U Upgrade**: Upgrade items at Pedestal stations (Bronze→Silver→Gold→Diamond)
- ✅ **PvE combat results**: "Victory!" and "Defeat!" now announced for PvE fights (not just PvP)
- ✅ **Combat result events**: Uses GameServiceManager.OnCombatPvEFinish for reliable win/lose detection
- ✅ **Fix massive speech deduplication**: Comprehensive fixes to prevent duplicate/triple announcements
  - `AnnounceSection()` no longer calls `AnnounceCurrentItem()` - user hears item on arrow keys
  - `AnnounceHeroSubsection()` no longer announces first stat/skill automatically
  - Removed duplicate tutorial patches from PopupPatch.cs (TutorialPatch.cs handles all)
  - Removed `BaseDialogPatch` that was patching base class (caused ALL dialogs to double-announce)
  - Combat results consolidated: "Victory! 3 wins" instead of "Victory!" then "3 wins"
  - `AccessibleMenu.StartReading()` only announces menu name, not first option
  - `RefreshAndAnnounce()` now only refreshes, doesn't announce (avoids StateChangePatch conflict)
  - `TolkWrapper.Speak()` has global 0.3s deduplication as safety net
- ✅ **Fix move item error messages**: ActionHelper.MoveItem already speaks, GameplayScreen no longer duplicates
- ✅ **Fix stash close behavior**: Only switches to Board if user was in Stash section, otherwise stays in current section
- ✅ **Fix sell navigation**: Refresh() no longer auto-switches sections when current section is empty (keeps user in place)
- ✅ **Improved visual feedback**: TriggerVisualSelection now calls OnPointerEnter + HoverMove for full feedback (sounds + tooltips + animation)
- ✅ **Board capacity info (T key)**: Press T to hear "Board: X of Y capacity used, Z items, W slots free"
- ✅ **Hero description reading**: In Hero Select screen, use Ctrl+Up/Down to read hero details (name, title, description, lock status)
- ✅ **Fix enchanted/upgraded items**: Card detail cache now clears on `CardEnchantedSimEvent` and `CardUpgradedSimEvent` - shows updated stats
- ✅ **Fix hero selection position**: After selecting a hero, menu stays on the same option instead of returning to start
- ✅ **Chest Scene accessible**: Full chest opening experience with keyboard navigation
  - Navigate chest types with left/right arrows
  - Open single chest with Enter
  - Open 10 chests at once with "Open 10 at once" option (auto-triggers lever)
  - Rewards announced after opening: rarity, collection items, gems, vouchers, bonus chests
  - Press Enter to dismiss rewards and return to selection (only Enter closes popup)
- ✅ **Battle Pass restructured**: Menu with Challenges and Tiers/Rewards sections
  - Challenges mode: Daily challenges first, then weekly (navigate with arrows)
  - Tiers mode: Navigate through all tiers with reward info
  - Claim completed challenges with Enter
- ✅ **Marketplace hidden**: Button hidden from main menu until accessible implementation
- ✅ **Collection Screen accessible**: Full cosmetics browsing with keyboard
  - Navigate categories with left/right (Hero Skins, Boards, Card Skins, Carpets, Card Backs, Albums)
  - Navigate items with up/down arrows
  - Read item details with Ctrl+Up/Down (name, type, description, equipped status)
  - Equip items with Enter
  - Uses `CollectionManager.GetPlayerCollectables()` for real player data
  - Supports `ButtonCustom` for back navigation via reflection
- ✅ **Hero Select back button fix**: Back button now shows "Back" instead of "Button Large"
  - Added `IsGenericButtonName()` filter to skip useless button names
  - Added `AddBackButton()` method to find ButtonCustom back buttons
- ✅ **Hero Select game mode selection**: Shows Casual/Ranked with selection state
  - "Casual, selected" or "Ranked, selected" to indicate current mode
  - Ready button shows contextual text (Ready, Resume)
  - Menu position preserved when switching modes (doesn't jump to start)
  - Uses `PlaymodeSelectionViewComponent` and `PlaymodeReadyButtonComponent`
- ✅ **Fast navigation keys**: Home/End/PageUp/PageDown for all menus
  - **Home**: Go to first element
  - **End**: Go to last element
  - **Page Up**: Go back 10 items (or to start if less than 10)
  - **Page Down**: Go forward 10 items (or to end if less than 10)
- ✅ **No wrap-around navigation**: Lists no longer wrap from end to start
  - Announces "Start of list" or "End of list" when reaching limits
  - Applies to all menus, gameplay navigation, hero stats, enemy items
- ✅ **Board reorder limit messages**: Clear feedback when item can't move further
  - "Reached limit, cannot move further left"
  - "Reached limit, cannot move further right"
- ✅ **Jules Heat/Chill state announcements**: Items now announce Heated/Chilled state
  - Foods near heat sources show "Heated" after tier name
  - Foods near cold sources show "Chilled" after tier name
  - Works in quick navigation, detailed description, and detail lines
  - `GetTemperatureState()`, `IsHeated()`, `IsChilled()` methods added to ItemReader
- ✅ **Chest rewards with item names**: ChestRewardsUI now loads real item names via Addressables
  - Shows full item info: "Epic Hero Skin: Radiant Vanessa"
  - Navigate rewards with Up/Down arrows ("Reward 1 of 10...")
  - Summary announcement on open ("You received 10 rewards: 3 Common, 5 Uncommon...")
  - Enter closes and returns to chest selection
  - Works for single chest and multi-open (10 chests)
- ✅ **Free item pickup message**: Items with 0 cost now say "Acquired [name]" instead of "Bought for 0 gold"
- ✅ **Fix PvP/PvE opponent name confusion**: `Data.SimPvpOpponent` now correctly used for PvP encounters
  - ItemReader.GetEncounterInfo() shows opponent name for PvP encounters in selection menu
  - ItemReader.GetEncounterDetailedInfo() shows opponent name, level, wins, prestige
  - GameplayNavigator.ReadEnemyInfo() checks `ERunState.PVPCombat` for enemy stats during combat
  - CombatDescriber.OnEffectTriggered() verifies game state to ignore late events from previous combat
- ✅ **Hover sounds for keyboard navigation**: Cards now play hover sounds when navigating with keyboard
  - EncounterController: plays `SoundPortraitHover` via `soundPortraitHandler`
  - ItemController: plays `SoundCardRaise` via `soundCardHandler`
  - Works for items, encounters, skills, and enemy items
  - Fixed `UnityEngine.Input.mousePosition` error (game uses new Input System)
- ✅ **Enchantment support**: Enchanted items now show enchantment name and properties
  - `GetCardName()` prepends enchantment name (e.g., "Radiant Water Dagger")
  - `GetAbilityTooltips()` includes enchantment tooltips (e.g., Crit Chance)
  - `GetDetailLines()` shows "Enchanted: Type" line in Ctrl+Up/Down navigation
- ✅ **Simplified menu limits**: Menus read current item at limits instead of "Start/End of list"
  - Only Board section during gameplay keeps limit messages
- ✅ **Combat Describer improvements**: Reduced spam and clearer announcements
  - Shield no longer added to health (announced separately)
  - Enemy effects use passive voice ("5 damage received" instead of "Enemy: Item: 5 damage")
  - Enemy effects aggregated over 0.8 seconds ("13 damage received" instead of multiple announcements)
- ✅ **PvP encounter shows opponent name**: In encounter selection, PvP shows player name before hero
  - "PlayerName, Vanessa, PvP" instead of just "Vanessa, PvP"
  - Detailed info shows level, wins, and prestige
  - During combat, name is hidden to reduce Combat Describer spam
- ✅ **Opponent board in ReplayState (G key)**: View opponent's items after combat
  - Same navigation as your own board (consistent UX)
  - Left/Right arrows: Navigate items or skills
  - Ctrl+Left/Right: Switch between Items and Skills subsections
  - Ctrl+Up/Down: Read item details line by line
  - Enter: Read full description
  - Backspace: Exit opponent board
  - If Replay animation is active, suggests using Recap (E) instead
  - Best used during Recap for static view without Combat Describer
- ✅ **Enemy board verbosity improvements**: Consistent with player's own board navigation
  - Detail lines now show "X of Y" position like player's board
  - Enemy skills show name + description (same as player's Hero Skills)
  - Fallback to `Data.Run.Opponent.Skills` when socket data unavailable (fixes Recap mode)

---

## Sistema de Login/Cuenta Accesible

El sistema de login es accesible mediante `LoginPatch.cs` y las UIs en `UI/Login/`.

### Pantallas Soportadas

| Pantalla | Clase | Campos |
|----------|-------|--------|
| Welcome | `LandingUI` | Link Account, Create Account |
| Login | `LoginUI` | Email, Password, Continue, Reset Password |
| Create Account - Email | `CreateAccountEmailUI` | Email, Confirm Email, Continue |
| Create Account - Username | `CreateAccountUserPasswordUI` | Username, Password, Confirm Password, Continue |
| Create Account - Terms | `CreateAccountTermsUI` | ToS toggle, EULA toggle, Promo toggle, Continue |
| Reset Password | `ForgotPasswordUI` | Email, Continue |
| Reset Email | `ResetEmailUI` | Email, Confirm Email, Continue |
| Password Reset Sent | `ForgotPasswordConfirmUI` | Continue, Resend Email |
| Account Verified | `AccountVerifiedUI` | Continue |
| Registration Failed | `RegistrationFailedUI` | Try Again |
| Access Denied | `AccessDeniedUI` | Mensaje de error, Continue |

### Controles del Login

- **Flechas arriba/abajo**: Navegar entre campos y botones
- **Enter**: En campo de texto → entrar en modo edición; en botón → activar
- **Enter (en modo edición)**: Salir del modo edición
- **Escape**: Salir del modo edición / volver
- **Izquierda/Derecha**: Alternar toggles (ToS, EULA, etc.)

### Modo Edición

Los campos de texto usan un **modo edición** especial:

1. Usuario navega a un campo (ej: "Email: empty")
2. Presiona **Enter** → escucha "editing"
3. Escribe texto libremente (las flechas no navegan mientras edita)
4. Presiona **Enter** → escucha "done", vuelve a navegación
5. Navega al siguiente campo → escucha "Password: 5 characters entered"

### Lectura de Campos

- **Campos vacíos**: "Email: empty"
- **Campos con texto**: "Email: user@example.com"
- **Campos de contraseña**: "Password: 8 characters entered" (no revela contenido)
- **Toggles**: "Terms of Service: accepted" / "not accepted"
- **Botones deshabilitados**: "Continue (disabled)"

### TextFieldOption

Clase especial para campos de texto con modo edición:
- `IsEditing`: Indica si está en modo edición
- `ToggleEditMode()`: Alterna entre navegación y edición
- `GetDisplayText()`: Retorna "Label: contenido" o "Label: X characters" para passwords

### LoginBaseUI

Clase base para UIs de login que hereda de `BaseUI`:
- Maneja el modo edición para campos de texto
- Bloquea navegación cuando está editando
- Helpers de reflexión para obtener campos privados

### Clases del Juego (Managers.Login)

```csharp
LandingStateView              // Pantalla inicial
LoginStateView                // Login con email/password
CreateAccountEmailStateView   // Paso 1: email
CreateAccountUserNamePasswordStateView  // Paso 2: username/password
CreateAccountTermsStateView   // Paso 3: términos
ForgotPasswordStateView       // Recuperar contraseña
ForgotPasswordConfirmView     // Confirmación de reset
ResetEmailStateView           // Cambiar email
AccountVerifiedStateView      // Cuenta verificada
RegistrationFailedStateView   // Error de registro
AccessDeniedStateView         // Acceso denegado
```

---

## Combat Describer (CombatDescriber.cs)

Narra el combate en tiempo real para accesibilidad. Se activa automáticamente cuando empieza un combate y se detiene cuando termina.

### Formato de Mensajes

**Items del jugador (inmediato):**
```
"You: [Item]: [Efecto]. [Crítico]."
```
Ejemplos:
- "You: Water Dagger: 10 damage."
- "You: Fire Sword: 25 damage. critical."

**Items del enemigo (agregado):**
Los efectos del enemigo se acumulan durante 0.8 segundos y se anuncian juntos en formato pasivo:
```
"[Total] damage received, poisoned for [Total]"
```
Ejemplos:
- "13 damage received" (en vez de "5 damage, 5 damage, 3 damage")
- "8 damage received, burned for 5"
- "poisoned for 10. critical hit"

**Anuncio de vida (cada 5 segundos):**
```
"You: [vida] health, [escudo] shield. [Enemy]: [vida] health."
```
Nota: Shield se anuncia separado de health (nunca sumados).

### Eventos Suscritos

- `Events.EffectTriggered`: Cuando un item/skill activa un efecto
- `Events.PlayerHealthChanged`: Cuando cambia la vida de jugador/enemigo

### ActionTypes Narrados

**Items del jugador (activo):**
- `PlayerDamage` → "X damage"
- `PlayerHeal` → "X heal"
- `PlayerShieldApply` → "X shield"
- `PlayerBurnApply` → "X burn"
- `PlayerPoisonApply` → "X poison"

**Items del enemigo (pasivo):**
- `PlayerDamage` → "X damage received"
- `PlayerHeal` → "X healed"
- `PlayerShieldApply` → "X shield gained"
- `PlayerBurnApply` → "burned for X"
- `PlayerPoisonApply` → "poisoned for X"
- `CardSlow` → "slowed"
- `CardFreeze` → "frozen"

### Críticos

- Items del jugador: "critical"
- Items del enemigo: "critical hit"

### Nombre del Enemigo

- **PvP**: Usa el nombre del jugador oponente (`Data.SimPvpOpponent.Name`)
- **PvE**: Usa "Enemy" (fallback)

---

---

## Tutorial (FTUE) Accesible

El tutorial del juego (First Time User Experience) es accesible mediante `TutorialPatch.cs` y `TutorialUI.cs`.

### Funcionamiento

- Detecta diálogos de tutorial (`SequenceDialogController`, `FullScreenPopupDialogController`)
- Lee automáticamente el texto cuando aparece un diálogo
- Los mensajes se añaden al `MessageBuffer` para poder releerlos
- **El tutorial NO bloquea la navegación** - el usuario puede interactuar con el juego mientras el diálogo está visible

### Gameplay Durante el Tutorial

El tutorial del juego muestra diálogos pero NO bloquea la interacción. El mod replica este comportamiento:
- Los diálogos del tutorial se leen automáticamente
- El usuario puede navegar tienda/encounters/skills con las teclas normales (flechas, Tab, B, V, C, etc.)
- Presionar **Enter** avanza el tutorial al siguiente paso
- El `GameplayScreen` recibe todas las teclas de navegación aunque haya un diálogo activo

### Controles del Tutorial

- **Enter**: Continuar al siguiente paso del tutorial
- **Flechas/Tab/B/V/C/etc.**: Navegar el juego normalmente
- **. (punto)**: Leer último mensaje del buffer
- **, (coma)**: Leer mensaje anterior del buffer
- **F1**: Ayuda

### Flujo Durante el FTUE

```
[Diálogo del tutorial aparece]
     ↓
TutorialUI lee el texto automáticamente
     ↓
[Usuario puede navegar normalmente]
  - Flechas navegan items
  - Enter en item compra/selecciona
  - Enter (sin item seleccionado) avanza tutorial
     ↓
[Condición del tutorial se cumple]
  (ej: usuario compró el item correcto)
     ↓
[Siguiente diálogo del tutorial]
```

### Importante: Conflictos de Teclas

**NO usar estas teclas en UIs porque el juego las intercepta:**
- **Espacio**: Abre/cierra el stash
- **Escape**: Abre el menú de opciones

El sistema de accesibilidad tiene prioridad correcta (UI > Screen), pero el juego nativo tiene su propio sistema de keybinds que funciona en paralelo.

---

## Feedback Visual

Al navegar con el teclado, las cartas muestran feedback visual (hover) para que espectadores puedan seguir la navegación.

### Implementación

- `GameplayNavigator.TriggerVisualSelection()`: Activa el efecto hover en la carta actual
- Usa `CardController.HoverMove()` para levantar la carta y mostrar tooltips
- `FindCardController()` usa `Data.CardAndSkillLookup.GetCardController(card)` para encontrar cualquier tipo de carta

### Lugares con Feedback Visual

- ✅ Navegar items en tienda (Selection)
- ✅ Navegar items en board
- ✅ Navegar items en stash
- ✅ Navegar skills
- ✅ Navegar encounters
- ✅ Reordenar items (Shift+flechas)
- ✅ Mover items entre board y stash
- ✅ Navegar items del enemigo (modo F)
- ✅ Cambiar de sección
- ✅ Anunciar estado

---

## Menús Adicionales Accesibles

### Pantalla de Baúles (ChestSceneScreen)

Accesible mediante `MenuPatches.cs` cuando se abre la escena de baúles.

**Controles:**
- **Flechas arriba/abajo**: Navegar opciones (Back, tipo de baúl, multi-open)
- **Flechas izq/der**: Cambiar tipo de baúl (Season 1, Season 2, etc.)
- **Enter**: Abrir baúl seleccionado
- **Escape**: Volver al menú anterior

**Opciones del menú:**
- Back: Vuelve al menú anterior
- Tipo de baúl: Muestra "Season X Chest: Y" donde Y es la cantidad
- Open 10: Solo visible si tienes 10+ baúles del tipo seleccionado

**Recompensas (ChestRewardsUI):**
- Después de abrir, se muestra el popup de recompensas con nombres reales
- **Flechas arriba/abajo**: Navegar entre recompensas ("Reward 1 of 10...")
- **Enter**: Cerrar y volver al menú de cofres
- Muestra: rareza, tipo (Hero Skin, Board, etc.), nombre, gemas, vouchers, bonus chests
- Carga nombres via Unity Addressables para mostrar el nombre real del item

### Pantalla de Colección (CollectionScreen)

Accesible mediante `MenuPatches.cs` cuando se abre el menú de colección. Se activa al patchear `CollectionUIController.Start()`.

**Controles:**
- **Flechas izq/der**: Cambiar categoría de colección
- **Flechas arriba/abajo**: Navegar items dentro de la categoría
- **Ctrl+Arriba/Abajo**: Leer detalles del item línea por línea
- **Enter**: Equipar el item seleccionado
- **Backspace**: Salir del modo items / volver al menú principal

**Categorías disponibles:**
- Hero Skins
- Boards
- Card Skins
- Carpets
- Card Backs
- Albums

**Información anunciada:**
- Al entrar: "Hero Skins, X items"
- Al navegar items: "Nombre, Rareza, equipped, X of Y"
- Detalles: nombre, tipo (ej: "Legendary Vanessa Hero Skin"), descripción, estado

**Funcionalidades:**
- Obtiene items del jugador via `CollectionManager.GetPlayerCollectables()`
- Incluye items por defecto (default skins, etc.)
- Permite equipar items con Enter
- Muestra estado de equipado

### Pantalla de Pase de Temporada (BattlePassScreen)

Accesible mediante `MenuPatches.cs` cuando se abre el Battle Pass.

**Controles:**
- **Flechas arriba/abajo**: Navegar opciones
- **Flechas izq/der**: Cambiar sección (Overview, Tiers, Challenges)
- **Enter**: Confirmar acción
- **Escape**: Volver a Overview / salir

**Secciones:**
- Overview: Resumen del pase de temporada
- Tiers: Navegación por niveles/recompensas
- Challenges: Desafíos diarios y semanales

**Opciones:**
- Back: Vuelve al menú anterior
- Sección actual: Muestra info según la sección seleccionada
- Collect All Rewards: Recoge todas las recompensas pendientes
- Open Chests: Abre la escena de baúles

**Anuncios automáticos:**
- "Tier X unlocked" cuando se desbloquea un nivel

### Marketplace y Profile

También se detectan con anuncios básicos:
- "Marketplace" al abrir el mercado
- "Player Profile" al abrir el perfil

*Nota: Estos menús tienen anuncio básico pero aún no navegación completa.*

---

## Pendiente por Implementar

### Mejoras de Navegación
- Preview de sinergia de items
- Acceso rápido a información de cooldowns en batalla
- Navegación completa del Marketplace
- Navegación del Player Profile

### Combat Describer Mejoras
- Obtener nombre real del monstruo en PvE
