# Base page and bottom dock

Open **Game > UI > Open Base Page Scene** and press Play. Drag
`Assets/Prefabs/UI/BasePageUI.prefab` into a scene to reuse the page.
**Game > UI > Edit Base Page** opens the layout in UI Builder.

The page uses the existing dark navy, green and white app theme from `App.uss`.
The rounded dock has five icons, with **Interventions** as the green centre button.
Left to right: **Home, My Pet, Interventions, Settings, Resources**. The dock stays
visible while the page content scrolls. Buttons have 48-unit touch targets,
hover/pressed/focus states and tooltips. The controller applies device safe areas.

- `Assets/UI/Screens/BasePage.uxml`: base page with a `page-content` container.
- `Assets/UI/Elements/BottomDock.uxml`: reusable dock template.
- `Assets/UI/Themes/App.uss`: shared page/button styles and the necessary dock layout rules.
- `Assets/Scripts/UI/DockIcon.cs`: vector icons rendered in runtime and UI Builder.
- `Assets/Scripts/UI/BasePageScreen.cs`: selected destination and navigation events.

Add page-specific UI under `page-content`, or populate `BasePageScreen.Content`
at runtime. The base intentionally starts with an empty content area.

Connect **On Navigate (String)** in the prefab's Inspector to your navigation
handler. The dynamic string value is `home`, `interventions`, `my-pet`, `settings`
or `resources`. Buttons update the active icon and page heading. Screen switching and
destination content belong to the handler; this template does not create those
features. Call `SelectDestination(id)` to synchronize the selected icon without
raising another event. Selection survives disabling/re-enabling the component.

`Tools/BasePageValidation.cs` generates the prefab/preview scene and checks event
dispatch, callback cleanup, selected state, icon instantiation, touch targets and
content/dock layout at 390 x 844, 320 x 568 and 844 x 390. Run it only in an isolated
Unity 6000.6.2f1 project with copies of the UI assets, UI scripts (with metadata),
and Outfit fonts. Put the utility under `Assets/Editor` and launch Unity with
`-batchmode -projectPath <isolated-project> -executeMethod BasePageValidation.Run`.
Screenshots are written under `Validation` in that project.
