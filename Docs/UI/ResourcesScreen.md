# Australian gambling support resources

Open the **Resources** tab (rightmost dock button) in
`Assets/Scenes/PetCreationUIToolkit.unity`. The scene already references
`Assets/Prefabs/UI/ResourcesScreen.prefab`; no Inspector wiring is required.
The screen starts inactive and is available from Home, My Pet and Interventions.

The national gambling helpline and online chat appear first, followed by filters
for money support, self-exclusion tools, community support, and local services.
The directory includes BetStop, National Debt Helpline, Gamblers Anonymous,
SMART Recovery, Gambling Help Online's community and family support, Gam-Anon,
and services in all eight Australian states and territories.

## Editing

- `Assets/Resources/Support/AustralianGamblingResources.json`: service names,
  descriptions, phone numbers, website destinations and review date.
- `Assets/UI/Elements/ResourcesContent.uxml`: page introduction, helpline card,
  filter controls and footer. Open via **Game > UI > Edit Resources Content**.
- `Assets/UI/Themes/ResourcesScreen.uss`: colours, spacing and touch targets.
- `Assets/Scripts/UI/ResourcesScreen.cs`: rendering, filters and link actions.

The hero and filters appear in UI Builder. Directory cards are populated from
the JSON at runtime. Use a 390 x 844 preview. Content scrolls while the bottom
dock remains accessible; buttons are at least 44 UI units high.

## Phone and website actions

On Android, phone buttons open an `ACTION_DIAL` intent with a `tel:` URI.
The player taps Call in their phone app to place the call. No `CALL_PHONE`
permission is needed. If the device has no phone app, the page displays the
number for manual use. The service numbers are Australian domestic numbers.

In the Windows build and Unity Editor, phone buttons copy the number and display
a confirmation. Website buttons open the service in the default browser.
The page itself makes no network requests and stores no support interactions.

Android behaviour follows [Android's common intents documentation](https://developer.android.com/guide/components/intents-common#Phone).
On-device dialler behaviour still requires an Android device check.

## Source verification

Service details were reviewed on **19 October 2026** against the following
official/service-operated sources. Review these before release and update the
catalog's `reviewed` date when contact details are checked again.

| Service | Source |
| --- | --- |
| National Gambling Helpline and national support overview | [Department of Social Services](https://www.dss.gov.au/gambling/gambling-support-services) |
| Online counselling | [Gambling Help Online chat](https://www.gamblinghelponline.org.au/tools-resources/chat-counselling) |
| BetStop scope and registration | [BetStop](https://www.betstop.gov.au/) |
| BetStop phone number and opening hours | [Contact BetStop](https://www.betstop.gov.au/contact/) |
| Financial counselling | [National Debt Helpline](https://ndh.org.au/) |
| GA meetings | [Gamblers Anonymous Australia](https://gaaustralia.org.au/meetings/) |
| SMART meetings and gambling support | [SMART Recovery meetings](https://smartrecoveryaustralia.com.au/en/smart-recovery-meetings), [general information](https://smartrecoveryaustralia.com.au/en/general-information) |
| Family support | [Gambling Help Online](https://www.gamblinghelponline.org.au/support-yourself-or-others/helping-others) |
| Gam-Anon | [Australia/New Zealand meeting information](https://gam-anon.org/meeting-directory/meeting-details/international/australia-new-zealand) |
| Interpreter and relay access | [Gambling Helpline telephone support](https://www.gamblinghelponline.org.au/support-yourself-or-others/taking-action/telephone-support) |
| NSW | [GambleAware NSW directory](https://www.gamblinghelponline.org.au/nsw/local-support-services) |
| Victoria | [Gambler's Help directory](https://www.gamblinghelponline.org.au/vic/local-support-services) |
| Queensland | [Gambling Help directory](https://www.gamblinghelponline.org.au/qld/local-support-services) |
| South Australia | [Gambling Help directory](https://www.gamblinghelponline.org.au/sa/local-support-services) |
| Western Australia | [Gambling Help WA directory](https://www.gamblinghelponline.org.au/wa/local-support-services) |
| Tasmania | [Gamblers Help / Anglicare directory](https://www.gamblinghelponline.org.au/tas/local-support-services) |
| ACT | [ACT Gambling Support directory](https://www.gamblinghelponline.org.au/act/local-support-services) |
| Northern Territory | [NT support directory](https://www.gamblinghelponline.org.au/nt/local-support-services) |

Regional phone buttons use the national gambling helpline except Tasmania's
Anglicare contact. Regional websites provide individual local service contacts.
The 24/7 label applies only to the national helpline and chat, not every service.
This is a curated directory of major services, not an exhaustive provider registry.

## Validation

`Tools/ResourcesValidation.cs` runs in an isolated Unity project. It checks the
shipped scene reference, catalog coverage, outbound phone/website callbacks
without contacting services, filters, navigation, repeated activation and
layouts at 390 x 844, 320 x 568 and 844 x 390. It writes screenshots to the
isolated project's `Validation` directory.

Validation passed with Unity 6000.6.2f1, including all 16 service entries and
navigation back to the Resources page. The Android compilation branch was also
compiled against Unity's assemblies. No real calls or browser sessions were
started by validation. Physical Android dialler testing remains outstanding.

Saved previews: [phone](Resources.png), [local services](Resources-Local.png),
and [small phone](Resources-Mobile.png).
