# Home and screen time

The Home tab now opens `Assets/Prefabs/UI/HomeScreen.prefab` in
`PetCreationUIToolkit.unity`. Existing saved pets start here; completing pet creation
also opens Home. My Pet and Interventions are connected through the existing dock
and the home cards. Pet loading updates data without changing the current page.

The shell includes a sports-betting screen-time card, Today / Last 7 days filters,
per-app totals, a seven-day bar chart, a companion preview, and a pause shortcut.
Content scrolls independently of the fixed bottom dock.

## Editing

- Layout: `Assets/UI/Elements/HomeContent.uxml` (**Game > UI > Edit Home Content**).
- Styling: `Assets/UI/Themes/HomeScreen.uss`, using the shared app palette and font.
- Page frame: `Assets/UI/Screens/HomeScreen.uxml` and the existing BasePage template.
- Controller: `HomeScreen.cs`; rendering and formatting: `ScreenTimeView.cs`.

HomeContent is instantiated into BasePage's `page-content` on enable and removed
on disable. Keep named elements when editing: the controller/view bind by name.
The prefab's Content Template field points at HomeContent.

## Current behaviour

No device usage is collected, no permission is requested, and no betting app is
opened. With no provider, the card shows **Not connected** and an unknown total
(`—`), not a misleading zero. **Preview example** explicitly opts into fictitious
app data; the badge and explanation remain visible. **Exit example** restores
the actual provider state. A confirmed Ready snapshot with no recorded usage
shows `0 min`. Demo values are never saved to a profile or used for pet rewards.

Loading, permission-required, unavailable and error states clear the displayed
statistics. A connected provider gets a Refresh action. No nonfunctional tracking
or permission button is presented in the shell.

## Connecting tracking later

Implement `GamblingSeriousGame.Data.IScreenTimeUsageProvider` (defined in
`Assets/Scripts/Data/ScreenTimeUsage.cs`):

```csharp
event Action Changed;
UsageSnapshot GetSnapshot(UsagePeriod period);
void Refresh();
```

Either attach a MonoBehaviour implementing it to the prefab's **Usage Provider**
field or inject a service with `homeScreen.SetUsageProvider(provider)`.
`GetSnapshot` must return cached data immediately. `Refresh` can start asynchronous
work; publish `Changed` on Unity's main thread when the data or status changes.
The controller rereads the currently selected period on each change and detaches
listeners when disabled or when a different provider is installed.

The provider owns platform access, user consent, app selection/classification,
local-date boundaries and aggregation. The view knows nothing about those APIs.
Supply only the sports-betting apps the user has chosen to track, not raw events:

- `status`: NotConnected, Loading, Ready, PermissionRequired, Unavailable or Error.
- `apps`: unique `appId`, display name and foreground duration **in seconds** for
  the requested period. The UI derives the total from these rows.
- `days`: seven entries, oldest to newest, with short labels and foreground
  seconds, for LastSevenDays. Omit incomplete data; unknown days must not be
  silently treated as zero. Their sum should match the app totals for that period.
- `updatedAtLabel`: an optional user-facing last-updated label. Do not provide it
  until data has actually been received.

Today is the current local calendar day; LastSevenDays includes today and the
preceding six local days. Negative durations display as zero; per-app values
are bounded to the period length, duplicate IDs are ignored and strings are
rendered literally. Null snapshots are treated as NotConnected. The chart is
hidden unless all seven daily entries are present. The provider should handle
errors by publishing the corresponding status; thrown exceptions also produce
the recoverable error view.

`HomeScreen.SelectPeriod(...)` and `SetExample(...)` are available for integration
tests and explicit UI controls. Example mode stays visibly labelled while active
and is cleared when a new provider is injected.

## Validation

`Tools/HomeScreenValidation.cs` checks shipped-scene references, startup and dock
navigation, provider lifecycle/refresh/errors, unknown versus zero usage, sample
filters, safe app names, scrolling and 390×844, 320×568 and 844×390 layouts.
Run in an isolated Unity project named `HomeScreenValidation*` with copied Scripts,
Resources, UI, prefabs, scenes and Outfit fonts. It saves screenshots under
`Validation/` and uses an isolated player-save location.
