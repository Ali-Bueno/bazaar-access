# Bazaar Access

Plugin de BepInEx para hacer el juego "The Bazaar" accesible para personas ciegas usando Tolk como lector de pantalla.

## Estructura del proyecto

```
BazaarAccess/
├── Plugin.cs              # Punto de entrada del plugin, manejo de teclas
├── Core/
│   ├── MenuManager.cs     # Lógica central de gestión de menús (Stack de navegación)
│   ├── KeyboardNavigator.cs # Manejo de entrada de teclado
│   ├── MenuState.cs       # Definición de estado de menú
│   └── TolkWrapper.cs     # Wrapper para la librería Tolk
├── Detection/
│   └── SelectableScanner.cs # Escaneo de elementos UI
└── Patches/               # Parches de Harmony por menú
    ├── ViewControllerPatch.cs # Detección de cambios de vista (Main Menu, Hero Select, etc.)
    ├── PopupPatch.cs          # Detección de Popups genéricos (PopupBase)
    ├── OptionsDialogPatch.cs  # Detección específica del menú de opciones
    └── HeroChangedPatch.cs    # Anuncio de cambio de héroe
```

## Arquitectura

### Sistema de Navegación (MenuManager)
- **Stack de Menús**: Mantiene una pila de estados (`MenuState`) para soportar popups anidados.
- **Root Isolation**: Cada menú define un `Transform` raíz. `SelectableScanner` solo busca elementos dentro de ese raíz, evitando la lectura de menús de fondo.
- **Tipos de Menú**: `View` (base), `Popup`, `Dialog`.

### Detección de Menús
1. **Vistas Principales**: `ViewControllerPatch` detecta `SwitchView` y resetea el stack.
2. **Popups Genéricos**: `PopupPatch` detecta `Show/Hide` en `PopupBase` y hace Push/Pop en el stack.
3. **Diálogos Específicos**: `OptionsDialogPatch` detecta `OnEnable/OnDisable` en controladores específicos que no heredan de `PopupBase` (como `OptionsDialogController`).

### Controles
- Flechas arriba/abajo: Navegar por opciones
- Flechas izquierda/derecha: Ajustar sliders/dropdowns/toggles
- Enter: Activar botón/toggle
- F5: Forzar re-escaneo y lectura del menú actual

## Buenas prácticas

### Al añadir soporte para una nueva pantalla
1. Identificar si es una **Vista** (cambia toda la pantalla) o un **Popup** (se superpone).
2. Si es Vista: Verificar si `ViewController` la gestiona. Si no, crear parche para su método de activación y usar `MenuManager.SwitchView`.
3. Si es Popup: Verificar si hereda de `PopupBase`. Si no, crear parche para `OnEnable/OnDisable` y usar `MenuManager.PushMenu/PopMenu`.
4. Usar `SelectableScanner` para verificar qué elementos se detectan.

## Compilación

```bash
cd BazaarAccess
dotnet build
```

El DLL se copia automáticamente a la carpeta de plugins de BepInEx.