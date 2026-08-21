# CanvasCore Examples

Five screens that run as they are, built to be read rather than admired. Each one exists to
show one thing; between them they cover every part of CanvasCore you are likely to need.

Open **`Scenes/ExampleScene.unity`** and press Play. Nothing needs wiring first — the scene
already has `UIBootstrap` pointed at `UIRoot.prefab` and the example `UICatalog.asset`.

Delete this whole `Examples/` folder whenever you are done with it. It is a separate assembly
(`Aexxa.CanvasCore.Examples`) on purpose, so nothing in `Runtime/`, `Editor/`, or `Prefabs/`
depends on any of it.

## Where to look for what

| If you want to learn… | Read | The point |
|---|---|---|
| Opening and closing a screen | `Scripts/ExampleBootstrap.cs`, `Scripts/Screens/MainMenu/MainMenuScreen.cs` | `UIManager.Instance.Show<T>()` / `Hide<T>()` — no prefab references, no `Instantiate`, no `Resources.Load` in your code |
| The view lifecycle | `Scripts/Screens/Settings/SettingsScreen.cs` | Why button listeners go in `OnCreated` (once per instance) and not `OnSpawn` (every time it is shown) — the bug that makes a pooled button fire twice |
| Back-stack navigation | `SettingsScreen` opened from `MainMenuScreen` | Opening a Screen auto-hides the one below it and shows it again on close. You write nothing for this |
| Popups and the popup queue | `Scripts/Popups/Confirm/ConfirmPopup.cs` | Ask two things at once and the second waits its turn instead of landing on top |
| Toasts | `Scripts/Toasts/Simple/SimpleToast.cs` | Fire-and-forget messages that clean themselves up |
| Long lists that stay at 60fps | `Scripts/Screens/Inventory/InventoryScreen.cs`, `InventoryCell.cs` | `RecycledScrollView` with 1000 rows and a handful of live cells |
| Pooling, concretely | `InventoryCell.cs` | `IPoolable.OnSpawn` / `OnDespawn` — what has to be reset when an object comes back for reuse |

## Localization

The examples are fully localized, so they double as the localization tutorial.

| If you want to learn… | Look at | The point |
|---|---|---|
| A fixed label in every language | The `LocalizedText` components on the example prefabs | Set a key in the Inspector; the label follows the language forever, with zero code |
| A label with a value in it | `Scripts/Screens/Inventory/InventoryCell.cs` | `Localization.Get("inventory.cell.item", index)` — the overload that does not allocate, because a recycled cell formats its label on every scroll frame |
| A one-off message from code | `Scripts/Screens/MainMenu/MainMenuScreen.cs` | `Localization.Get(key)` for a string built at the moment it is shown |
| Letting the player pick a language | The `LanguageDropdown` in `SettingsScreen.prefab` | `LocaleSelector` on a `TMP_Dropdown` — that is the entire integration |
| Where translations live | `Resources/Localization/en.asset`, `th.asset` | One asset per language, every key present in all of them. Open one and use the search box |
| **Adding a language after the game ships** | `StreamingAssets/Localization/ja.csv` | The Japanese here is not built into any asset — it is a plain CSV read at startup, and it shows up in the language picker like any other language |

### The Japanese sample is the interesting one

`ja.csv` is what a player or translator can write without your build. Export the CSV from the
editor, translate it, drop it back beside the game, and that language exists. Two rows in it
are not translations at all:

- `locale.displayname` — what the language calls itself in the picker (`日本語`, not `Japanese`).
- `locale.font` — a Resources path to the TMP font this language should be drawn with. Not
  used in this sample, because the font would have to already be in the build: a CSV can point
  at a font, it cannot supply one.

Files are read from `StreamingAssets/Localization/` first and then from
`persistentDataPath/Localization/`, which wins — so a player can override the translations you
shipped without touching them. Both are desktop-only by design.

> **⚠ Japanese renders as boxes unless you add a Japanese font.** Nothing is broken: the string
> is arriving correctly, and no font in a default project has kana or kanji. Run
> `Tools > CanvasCore > Localization > Font Coverage...` and it will tell you exactly which
> characters are missing and offer to build a TMP font asset from a `.ttf` you supply.
> Noto Sans JP is a good OFL-licensed choice.

## Two tools worth running once

- `Tools > CanvasCore > Localization > Key Usage...` — finds keys used in code or in the
  Inspector that no table has (those render as `#key#` on screen), and keys nobody uses.
- `Tools > CanvasCore > Localization > Font Coverage...` — checks every font in the project
  against every character your translations actually contain.
