# CanvasCore

Performance-focused UI Canvas Manager for Unity uGUI: pooled spawn/despawn, lazy
`Resources.Load` per catalog entry (no eager prefab loading), layered back-stack
Screens/Popups with a built-in Popup queue, and auto-dismiss Toasts.

## Documentation

Full architecture guide, naming conventions, and known limitations:
**[CanvasCore-Guide.html](https://aexxacsw.github.io/CanvasCore/CanvasCore-Guide.html)**

## Installation

**Unity Package Manager (git URL)**

1. Open `Window > Package Manager`.
2. Click `+` > `Add package from git URL...`.
3. Enter:
   ```
   https://github.com/aexxacsw/CanvasCore.git#0.1.7
   ```

Or add it directly to `Packages/manifest.json`:

```json
"com.aexxa.canvascore": "https://github.com/aexxacsw/CanvasCore.git#0.1.7"
```

The `#0.1.7` pins to a tagged release so updates to `main` don't change what you
have installed. Drop it (or bump it) to track a different revision.

**After installing, run `Tools > CanvasCore > Import Resources Into Project` once.**
This copies the Design System prefabs, the settings asset, and the Examples'
prefabs/`UICatalogSO` out of the (read-only) package and into
`Assets/Plugins/aexxa/CanvasCore/` as your own editable copy — mirrors
TextMeshPro's "Import TMP Essential Resources". (The Examples' component
*scripts* stay in the package — an assembly name must be unique project-wide,
so they can't also live in Assets/; the imported prefabs still reference them
fine across the Assets/Packages boundary.) Everything that scans for prefabs
(the `GameObject > Canvas Core > Create` menu, `UICatalogSO`'s "Scan
Resources/UI", `CanvasCoreSettings`) only ever reads from that `Assets/` copy,
never from the package itself, so it's always safe to edit, rename, or replace what
lands there.

## Example Scene

`Examples/Scenes/ExampleScene.unity` is playable directly from the package —
no import needed, since its `UIBootstrap` references the Examples' own
`UICatalogSO` and Resources prefabs by GUID, wherever they physically live.
Open it and press Play. It walks through the whole lifecycle:

- **Show UI the moment a scene starts** — `ExampleBootstrap.Start()` calls
  `UIManager.Instance.Show<AppBackground>()` and
  `UIManager.Instance.Show<MainMenuScreen>()`. `AppBackground` is a
  `UIBackground`: a persistent backdrop, shown once at boot and never hidden.
- **Click-to-navigate between screens** — `MainMenuScreen` (the Home Screen)
  has a "Settings" button that calls
  `UIManager.Instance.Show<SettingsScreen>()`. Because the `Screen` layer is a
  back-stack, that auto-hides Main Menu; `SettingsScreen`'s "Back" button
  calls `UIManager.Instance.HandleBack()`, which auto-shows Main Menu again —
  no manual bookkeeping on either screen's part. Main Menu also has buttons
  demonstrating `Toast<T>()` and a repeatable `Show<InventoryScreen>()`.

If you'd rather build your own starter content, run `Tools > CanvasCore >
Import Resources Into Project` (see above) — that copies the Examples'
prefabs/`UICatalogSO` into `Assets/` as your own editable copy, separate from
the scene above.

## Features

- Type-based UI lookup (`UIManager.Show<T>()`) — no baked-in enum, so consumers add
  their own screens without touching package source.
- Object pooling for repeatable widgets (`Spawn<T>`/`Despawn`) and singleton
  show/hide for Screens/Popups.
- Lazy prefab loading via `Resources.Load` per catalog entry — prefabs are not
  eager-loaded just because the catalog asset is referenced.
- Layered `UILayer` system supporting both back-stack layers (Screen/Popup) and
  multi-concurrent layers (Background/Overlay/Toast/Blocker).
- A dedicated base class per layer — `UIScreen`, `UIPopup`, `UIWidget`/`UIToast`,
  `UIBackground`, `UIBlocker` — so the Catalog Inspector's layer/pool-size
  defaults are inferred correctly from the type you inherit, not guessed.
- **Popups queue automatically.** `Show<T>()` on a `UIPopup` while another is
  already up queues the request instead of overwriting it, so two unrelated
  systems calling `Show` around the same time can't hijack each other's dialog.
  `ClearPopupQueue()` / `CancelQueued<T>()` drop what's still queued (e.g. before
  a scene transition), and the queue is capped (`maxQueuedPopups`, default 20)
  so a runaway caller can't grow it forever.
- **`UIBlocker` reference-counts.** `Show`/`Hide` on a blocker type track how many
  callers are holding it open, so a loading spinner triggered by two independent
  systems only actually hides once both release it.
- `UIPopup` backdrop-click-to-close and `UIScreen.IsRootScreen` (kept safe from
  `HandleBack()`) are both plain Inspector toggles — no subclassing required.
- Custom `UICatalogSO` inspector: grouped-by-layer entries, duplicate detection,
  a "Scan Resources/UI For Missing Entries" helper, and a one-click "Reset" to
  clear the catalog (with a confirm dialog, undoable).

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

See [Documentation](#documentation) above for the full architecture guide,
naming conventions, and known limitations.

## Requirements

- Unity 6000.0+
- `com.unity.ugui`

## License

MIT — see [LICENSE](LICENSE).
