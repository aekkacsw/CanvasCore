# CanvasCore

Performance-focused UI Canvas Manager for Unity uGUI: pooled spawn/despawn, lazy
`Resources.Load` per catalog entry (no eager prefab loading), layered back-stack
Screens/Popups, and auto-dismiss Toasts.

## Installation

**Unity Package Manager (git URL)**

1. Open `Window > Package Manager`.
2. Click `+` > `Add package from git URL...`.
3. Enter:
   ```
   https://github.com/aekkacsw/CanvasCore.git#0.1.0
   ```

Or add it directly to `Packages/manifest.json`:

```json
"com.aexxa.canvascore": "https://github.com/aekkacsw/CanvasCore.git#0.1.0"
```

The `#0.1.0` pins to a tagged release so updates to `main` don't change what you
have installed. Drop it (or bump it) to track a different revision.

## Features

- Type-based UI lookup (`UIManager.Show<T>()`) — no baked-in enum, so consumers add
  their own screens without touching package source.
- Object pooling for repeatable widgets (`Spawn<T>`/`Despawn`) and singleton
  show/hide for Screens/Popups.
- Lazy prefab loading via `Resources.Load` per catalog entry — prefabs are not
  eager-loaded just because the catalog asset is referenced.
- Layered `UILayer` system supporting both back-stack layers (Screen/Popup) and
  multi-concurrent layers (Overlay/Toast).
- Custom `UICatalogSO` inspector: grouped-by-layer entries, duplicate detection,
  and a "Scan Resources/UI For Missing Entries" helper.

## Usage

1. Create a `UICatalogSO` asset (`Assets > Create > CanvasCore > UI Catalog`) and
   register your `UIView` subclasses (each prefab must live under a `Resources/`
   folder).
2. Add a `UIBootstrap` to your bootstrap scene, referencing the catalog.
3. Show/hide from anywhere:
   ```csharp
   UIManager.Instance.Show<MyPopup>();
   UIManager.Instance.Hide<MyPopup>();
   UIManager.Instance.Spawn<FloatingText>();
   ```

See `CanvasCore-Guide.html` in this package for the full architecture guide,
naming conventions, and known limitations.

## Requirements

- Unity 6000.0+
- `com.unity.ugui`

## License

MIT — see [LICENSE](LICENSE).
