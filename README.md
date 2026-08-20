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
   https://github.com/aexxacsw/CanvasCore.git#0.2.0
   ```

Or add it directly to `Packages/manifest.json`:

```json
"com.aexxa.canvascore": "https://github.com/aexxacsw/CanvasCore.git#0.2.0"
```

The `#0.2.0` pins to a tagged release so updates to `main` don't change what you
have installed. Drop it (or bump it) to track a different revision.

**After installing, run `Tools > CanvasCore > Import Resources Into Project` once.**
This copies everything you are meant to own and edit out of the (read-only)
package and into your project — mirrors TextMeshPro's "Import TMP Essential
Resources". What lands where:

| From the package | To your project | What it is |
|---|---|---|
| `Prefabs/` | `Assets/Plugins/aexxa/CanvasCore/Prefabs/` | `UIRoot`, `UIBootstrap`, Design System Button/ScrollView |
| `Resources/` | `…/Resources/` | `CanvasCoreSettings` and the `en`/`th` locale tables |
| `Examples/Resources/`, `Examples/ScriptableObjects/` | `…/Examples/` | The five example screens and their `UICatalogSO` |
| `Examples/Scenes/` | `…/Examples/Scenes/` | `ExampleScene` — press Play, it already works |
| `Examples/StreamingAssets/` | `Assets/StreamingAssets/` | `Localization/ja.csv`, a language added by file rather than by asset |

References between the copies are repointed at the copies, so editing the
imported `Button.prefab` changes the imported screens that nest it, and adding
an entry to the imported catalog is what the imported scene reads. (The
Examples' component *scripts* stay in the package — an assembly name must be
unique project-wide, so they can't also live in Assets/; the imported prefabs
still reference them fine across the Assets/Packages boundary.) Everything that scans for prefabs
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

Running `Tools > CanvasCore > Import Resources Into Project` (see above) gives
you your own editable copy of that scene and everything it uses, wired to the
copies rather than to the package.

`Examples/README.md` maps each thing you might want to learn to the one file
that shows it — including the localization walkthrough.

## Localization

Multi-language support with no external dependency: one `LocaleTableSO` asset
per language, only the active language resident at runtime, and translations
that can be added or corrected *after* the game ships.

- **`LocalizedText`** on a label, key set in the Inspector — the label follows
  the language forever, with no per-screen code, and re-reads itself when a
  pooled view comes back in a language it was not put away in.
- **`Localization.Get(key, arg)`** for strings built at the moment they are
  shown, with fixed-arity overloads that don't allocate for per-frame callers.
- **`LocaleSelector`** on a `TMP_Dropdown` is a complete language picker.
- **CSV round trip.** Export every language as one file, hand it to a
  translator, import it back. RFC 4180, UTF-8 with BOM so Excel doesn't mangle
  non-Latin text.
- **Players can add languages.** Drop a CSV into `StreamingAssets/Localization/`
  or `persistentDataPath/Localization/` and that language appears in the picker,
  overriding shipped strings key by key. Desktop only, by design.
- **Fonts follow the language.** Give a locale a `Font Resource Path` and put
  `LocalizedFont` on a screen root; every label under it switches, and switches
  back for languages that don't ask for one. `LocalizedAsset<T>` does the same
  for sprites, audio, or anything else.
- **Two checks that catch what testing doesn't:** `Font Coverage...` asks every
  font whether it can draw every character your translations actually contain
  (the □□□□ failure), and `Key Usage...` finds keys used but never translated —
  and keys translated but never used — reading both your code *and* the keys set
  in the Inspector.

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
