# Bazaar Access

Plugin de BepInEx para hacer el juego "The Bazaar" accesible para personas ciegas usando Tolk como lector de pantalla.

## Estructura del proyecto

```
BazaarAccess/
├── Plugin.cs                      # Punto de entrada del plugin
├── Core/
│   ├── TolkWrapper.cs            # Wrapper para Tolk (screen reader)
│   └── KeyboardNavigator.cs      # Manejo de entrada de teclado
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
├── UI/
│   └── OptionsUI.cs              # Diálogo de opciones
└── Patches/
    ├── ViewControllerPatch.cs    # Detecta cambios de vista
    ├── PopupPatch.cs             # Popups genéricos
    ├── OptionsDialogPatch.cs     # Menú de opciones
    └── HeroChangedPatch.cs       # Cambio de héroe
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
