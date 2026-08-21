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
   https://github.com/aexxacsw/CanvasCore.git#0.5.0
   ```

Or add it directly to `Packages/manifest.json`:

```json
"com.aexxa.canvascore": "https://github.com/aexxacsw/CanvasCore.git#0.5.0"
```

The `#0.5.0` pins to a tagged release so updates to `main` don't change what you
have installed. Drop it (or bump it) to track a different revision.

**After installing, run `Tools > CanvasCore > Import Essential Resources` once.**
Nothing is optional about this step: the package ships **no loadable assets at all**,
so until you run it there is no settings asset and no prefabs — CanvasCore will say so
in the console rather than half-work. There is a second command,
`Tools > CanvasCore > Import Examples`, for the starter content. Same two commands as
TextMeshPro's "Essential Resources" and "Examples & Extras", for the same reason.

**Import Essential Resources** — what CanvasCore cannot run without:

| From the package | To your project | What it is |
|---|---|---|
| `PackageResources~/Prefabs/` | `Assets/Plugins/aexxa/CanvasCore/Prefabs/` | `UIRoot.prefab` and `UIBootstrap.prefab` |
| `PackageResources~/Resources/` | `…/Resources/` | `CanvasCoreSettings` — the one asset the framework reads at boot |

`UIBootstrap.prefab` arrives with its `Catalog` field **empty**. That is deliberate: the
catalog is yours, and shipping it pre-wired to the example one would make the essentials
depend on the examples.

**Import Examples** — starter content, safe to skip and safe to delete afterwards:

| From the package | To your project | What it is |
|---|---|---|
| `Samples~/Examples/` | `…/Examples/` | Design System prefabs, the `en`/`th` locale tables, five example screens, their `UICatalogSO`, `ExampleScene`, **and their scripts** |
| `Samples~/Examples/StreamingAssets/` | `Assets/StreamingAssets/` | `Localization/ja.csv`, a language added by file rather than by asset |

The example scene instantiates `UIBootstrap`, so importing Examples first offers to bring
the essentials along rather than leave you with a scene full of missing prefabs.

Deleting the imported `Examples/` folder leaves a working framework. It takes the `en`/`th`
tables with it — deliberately: those are sample data, not part of the tool, and a real game
supplies its own. Clear the `Locales` list in `CanvasCoreSettings` afterwards, or it will
point at tables that are gone.

Neither command touches the `GameObject > Canvas Core > Create` menu. That menu is a
generated script, and generating one means a domain reload, so it is left to
`Tools > CanvasCore > Scan Create Menu Prefabs` (or the button on `CanvasCoreSettings`)
rather than fired off inside an import.

### Why the package ships nothing Unity can see

Both source folders end in `~`, and Unity's AssetDatabase ignores any folder whose
name ends that way. Their contents are never imported, compiled, assigned a GUID,
reachable through `Resources.Load`, or included in a build — they are just files on
disk that the importer copies.

That is not tidiness, it is the fix for a real bug. When these were ordinary package
assets, importing them left the project holding **two** assets at the Resources path
`Localization/en`: yours and the package's. `Resources.Load` takes a path, not an
asset, and does not define which of two same-path assets it returns — so the Inspector's
key dropdown (which filtered to `Assets/`) and the running game (which did not) could
disagree, and a translation you had edited would not be the one that appeared. In a
build there is no `AssetDatabase` to prefer your copy with, so the same collision was
unfixable there by definition. With the shipped copies invisible, the duplicate cannot
be created in the first place.

Two things follow from it that are worth knowing:

- **The example scripts are yours now.** An assembly name has to be unique project-wide,
  which used to mean the example components had to stay in the package while only their
  prefabs were copied. The package copy is no longer compiled, so `Examples/Scripts/`
  comes with everything else — edit the example screens, don't just read them.
- **References survive the copy untouched.** `.meta` files come along verbatim, so the
  imported screens nest the imported Button and the imported bootstrap reads the imported
  catalog with no GUID rewriting. *Upgrading from 0.3.0 or earlier:* that older importer
  generated fresh GUIDs, so re-importing repoints these assets one final time — anything
  of yours referencing an imported prefab needs relinking once.

Everything that scans for prefabs (the `GameObject > Canvas Core > Create` menu,
`UICatalogSO`'s "Scan Resources/UI", `CanvasCoreSettings`) reads only from that
`Assets/` copy, so it is always safe to edit, rename, or replace what lands there.

## Example Scene

`Examples/Scenes/ExampleScene.unity` — **after importing** (it does not exist in the
project before that; see above). Open it and press Play. It walks through the whole
lifecycle:

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
- **Fonts, sizes, and line spacing all follow the language.** Some scripts need
  more room than a design tuned on Latin gives them; that is a per-language
  constant, not something a layout can work out for itself.

## Gamepad and keyboard

Focus that keeps up with the UI, because Unity's selection cannot: it is a single
global GameObject with no idea a popup went up, so by default the highlight stays
on the button behind it and the player answers a dialog they cannot see.

- **A focus stack that mirrors the back-stack.** Opening a view takes the
  selection; closing it gives back the *exact* control that was selected before,
  not the first one on screen.
- **Modality is enforced on input, not just visually.** A popup makes the screen
  behind it non-interactable, so navigation can't step past it — which also stops
  a mouse click reaching through a backdrop that doesn't cover the full screen.
- **A dead selection is repaired every frame.** Unity nulls the selection whenever
  the selected object is deactivated (a hidden button, a recycled list cell), and
  from then on every direction press does nothing. That silent failure is most of
  what "gamepad support doesn't work" turns out to be.
- **Mouse and pad take turns.** By default nothing is highlighted until the player
  presses a direction, and the highlight goes away again when they reach for the
  mouse. `Focus Mode` in settings makes it always-on for a pad-first game.
- **A highlight you can actually see.** `UISelectionIndicator` switches on a frame
  or glow, because Unity's stock selected colour is a 4% tint that is invisible on
  a saturated or dark button.
- **Virtualized lists are navigable.** `RecycledScrollNavigator` keeps the
  selection on an *index* rather than a GameObject — the only way that works when
  the item being navigated to has no GameObject yet. See the example inventory.
- **Bring your own bindings.** `UINavigationInput.Source` accepts your Input
  Actions, including whatever the player rebound them to, without CanvasCore
  taking a hard dependency on one input backend.

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
- **`RecycledScrollView` handles vertical or horizontal, uniform or per-item
  sizes, single column or grid** — all through one recycling path. The layout
  maths lives in a plain `ScrollLayout` class taking numbers and returning
  numbers, so the index arithmetic (where "a row blinks while scrolling" bugs
  actually live) is tested without a Canvas or a play mode session. Variable
  sizes use a prefix sum and a binary search: ~13 comparisons for 10,000 items on
  any frame, instead of a loop that grows with the list.
- `ScrollTo(index)` scrolls the least it can and does nothing if the item is
  already visible; the list also rebuilds itself when the viewport is resized,
  keeping the player's scroll position.

## Usage

1. Create a `UICatalogSO` asset (`Assets > Create > Canvas Core > UI Catalog`) and
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
