# Interventions

Click the heart in the bottom dock to open **Interventions**. The shared page
header and scroll view display example CBT-informed activities; the dock stays
visible. Each card shows its category, practice duration, title, summary and XP reward.
Select **My Pet** to return to the gameplay view.

**Open activity** opens that intervention's numbered instructions in a focused
page matching grounding: Back at the top, a scrollable body with hidden scrollbars,
no dock, and a fixed completion button at the bottom. Reflection can be done
mentally or on paper; no personal answers are collected.

| Activity | Practice time | XP |
| --- | --- | --- |
| Pause and check a thought | 5 min | 25 |
| Challenge the 'due a win' thought | 3 min | 15 |
| Make space before acting | 5 min | 25 |
| Notice your triggers | 5 min | 25 |
| Create a coping reminder | 4 min | 20 |

The initial balance is 5 XP per minute. Shop items unlock using cumulative XP
(it is not spent): these rewards fit the 5-40 XP starting thresholds and 25 XP
increments in `ShopCatalog`. One 3-minute practice unlocks eligible 15 XP items;
two 5-minute practices reach 50 XP. These times/rewards are game design settings,
not clinically prescribed durations.

Opening an activity starts its timer. Only foreground time on that activity page
counts, using unscaled frame time. Leaving the page, switching activities or
backgrounding the app pauses it. Returning to that activity with the same pet
resumes it during the current scene/session. Unclaimed timers are in memory and
restart after closing the game or reloading the scene. The completion button is
disabled until the whole duration has elapsed; then the player chooses
**Finish & collect N XP**. Time alone never awards XP automatically.

Each completed practice awards once. **Practise again** starts a new full timer;
there is no daily limit. XP and a unique claim receipt are saved together before
the success message appears. Failed saves leave XP unchanged and allow retry.
Reopening or repeatedly clicking cannot duplicate the reward. Rewards belong to
the pet the practice started with, and a saved pet is required to collect them.
Returning to My Pet reloads the persisted total for the HUD and shop. Grounding
remains available immediately without an XP timer.

The large **I feel like gambling** button at the top opens a focused grounding
subpage. Each opening randomly selects one of five exercises: five senses,
physical support, exploring an object, finding colours, or noticing sounds.
**Previous** and **Next exercise** cycle through the complete set, wrapping at
either end. The current position is shown above these controls. **Back to
interventions** returns to the activity list and restores keyboard focus to the
entry button. The dock and activity-list header are hidden during grounding.
Back and exercise navigation remain outside the scrolling instructions, including
on small screens and in landscape. Exercises use short numbered steps, approximate
durations and permission to skip unsuitable steps; there is no countdown.

Edit `Assets/Resources/Interventions/Grounding.json` to change these exercises.
Each record needs a unique `id`, a `title`, a `duration` label and non-empty
`steps`. `GroundingView` filters invalid/duplicate records and supplies a simple
object-noticing exercise if no usable content loads. A single exercise disables
Previous/Next. Text is literal, with rich text disabled. Grounding doesn't record
completion or change pet stats. These are brief sensory grounding adaptations,
not a claim of validated treatment for gambling urges. Content references:

- [Royal United Hospitals Bath: mindfulness and grounding](https://www.ruh.nhs.uk/patients/patient_information/PAE129_Mindfulness_and_grounding_exercises.pdf)
  (five senses and noticing sounds).
- [Royal Berkshire: grounding techniques](https://www.royalberkshire.nhs.uk/media/0ptda3ym/grounding-techniques-to-help-with-anxiety_apr26.pdf)
  (physical support, objects and colours).

Edit `Assets/Resources/Interventions/Activities.json` to change the content.
Each activity has a unique `id`, `title`, `category`, `minutes`, `description`,
`rewardXp` and a non-empty `steps` array. Detail pages require positive reward XP,
1-60 minutes and non-empty instructions; legacy summary-only records remain
visible but do not expose an activity button.
The content is loaded from Resources on first opening. Invalid entries and
duplicate IDs are skipped; missing, empty or malformed catalogs show an empty
state. JSON text is displayed literally, with rich text disabled.

Interventions is a separate screen, with its own `UIDocument` and
`InterventionsScreen` controller on `Assets/Prefabs/UI/InterventionsScreen.prefab`.
Its layout is `Assets/UI/Screens/InterventionsScreen.uxml`; it shares the BasePage
template for the title, scrolling area and dock, just as the game layout does.
`InterventionsView` builds the activity cards within this document.

`AppNavigationManager` listens to the two screens' dock events. Selecting the heart
deactivates GameScreen and activates InterventionsScreen; selecting My Pet does
the reverse. Both prefab instances are wired in `PetCreationUIToolkit.unity`, with
InterventionsScreen initially inactive. For another scene, assign the two screen
objects on AppNavigationManager. GameScreen now includes BasePageScreen so its
dock dispatches navigation events. The generic BasePageScreen contains no
interventions-specific content or screen-switching rules.

The sample activities are educational examples, not a validated treatment program.
The screen does not record personal reflections. Its XP rewards recognise timed
practice, not clinical outcomes. Background references for the instructions:

- https://www.gamblinghelponline.org.au/support-yourself-or-others/taking-action/changing-thoughts-and-beliefs
- https://www.gamblinghelponline.org.au/support-yourself-or-others/maintaining-change/understanding-urges

`Tools/InterventionsValidation.cs` checks the five sample records, malformed and
empty data, duplicate filtering, literal text, heart/My Pet navigation,
random grounding entry, cycling and wrapping through all five exercises,
back navigation and focus restoration, hidden dock, scroll reset and component
re-enabling (including while grounding is open). It renders each exercise at
390 x 844, 320 x 568 and 844 x 390 layouts in an isolated Unity project. It uses
the actual GameScreen and InterventionsScreen prefabs to verify independent
documents and screen activation/deactivation. Copy it under `Assets/Editor` with
Scripts, UI, Resources, both screen prefabs and Outfit fonts, use a separate
product name beginning with `PetShopValidation`, and run
`-executeMethod InterventionsValidation.Run`. Copy `Tools/InterventionRewardsValidation.cs`
beside it: this additionally checks early claims, pause/resume, retries after a
failed disk write, saved receipt replay, old-save migration, corrupt saves,
XP overflow and shop unlocks. The UI runner also visits all five activity pages
at each size, verifies hidden scrollbars, back/focus, timed collection and fresh
timers for repeat practice. It advances the same timer model deterministically
instead of waiting several minutes for every test.
The latest portrait preview is
`Docs/UI/Interventions.png`; a grounding preview is `Docs/UI/Grounding.png`.
An activity preview is `Docs/UI/InterventionActivity.png`.
