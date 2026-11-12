# Appearance

Every signed-in identity chooses how KleeneStar looks to them on the profile page
*Appearance* (`WWW/Profile/Appearance`) - and on the portal profile (`/portal/profile`), which
carries the same two cards. There is **one record per identity for both applications**: a theme
picked in the portal is in use on the operator side wherever it is registered there, and the
other way round; picking either application's default stores no theme, so both show their own
default. The choice is the person's: it is stored with the account
and follows them to every browser and device. A caller who is not signed in keeps the
installation's defaults and cannot store a choice.

## What can be chosen

| Choice          | Values                                         | Stored as (no choice = `null`/`false`) |
|-----------------|------------------------------------------------|----------------------------------------|
| Theme           | every theme WebExpress registered for the app  | theme id (`kleenestar.theme.webtheme.springtheme`) |
| Mode            | light, dark                                    | `light` / `dark`                       |
| Accent colour   | from the theme, or a custom colour             | `#rrggbb`                              |
| Font size       | 90, 100, 110, 125 %                            | percentage (100 = no choice)           |
| Density         | comfortable, compact                           | `compact`                              |
| Motion          | as the operating system says, always reduced   | `true`                                 |

The themes are not the core's to list. The core ships `LightTheme` (the application default) and
WebExpress adds whatever a plugin brings - `KleeneStar.Theme` ships the four seasons. Picking the
application default stores no theme, so a later change of the default reaches the reader too; a
stored id that names no registered theme (its plugin was removed) is no choice.

## Where it is kept

`WebTheme/AppearancePreference` is one JSON value in the identity's key/value preferences
(`UserSession`, scope `appearance`, key `preference`, through `ISessionManager.GetValue/SetValue`),
so the model needs no table and no migration. A record without any choice is deleted, not kept.
`WebManager/AppearanceManager` (`CoreHub.AppearanceManager`) reads it **once per request** (cached
beside the request), normalizes every value on the way in (`WebTheme/AppearanceOptions`) and
raises `AppearanceChanged`. **Every change is read, merge, write under one gate per identity** - a
monitor in the process and, when clustered, the cluster lock `kleenestar.core.appearance:<id>`
(the `HierarchyLock` pattern) - and the base is the record read from the store *inside* the gate,
never the request's cached copy: two requests of the same person (theme selector here, dark mode
switch there) used to read the same state, each write the whole record, and lose the other's
change. The request cache is refreshed only after the write. Nothing is audited - it is a personal preference like a table layout.

## One implementation, one thin class per application

An endpoint, a fragment and a theme registration belong to one application, so the behaviour
lives in abstract bases in the core and each application derives sealed, attribute-only classes:

| Base (core)                                         | Core                                            | Portal                                         |
|-----------------------------------------------------|-------------------------------------------------|------------------------------------------------|
| `WebRestApi/RestApiAppearanceTheme`                 | `WWW/Api/_1_/Appearance/Theme`                  | `KleeneStar.Portal/WWW/Api/_1_/Appearance/Theme` |
| `WebRestApi/RestApiAppearanceScheme`                | `WWW/Api/_1_/Appearance/Scheme`                 | `…/Appearance/Scheme`                          |
| `WebRestApi/RestApiAppearance`                      | `WWW/Api/_1_/Appearance/Index`                  | `…/Appearance/Index`                           |
| `WebFragment/Appearance/AppearanceFragmentBase`     | `AppearanceFragment` (general + admin scope)    | `PortalAppearanceFragment` (portal scope)      |
| `WebFragment/Appearance/AppearanceThemeCardFragmentBase` | `Profile/ProfileAppearanceThemeFragment`   | `PortalProfileAppearanceThemeFragment`         |
| `WebFragment/Appearance/AppearanceFormFragmentBase<T>` | `Profile/ProfileAppearanceFormFragment`      | `PortalProfileAppearanceFormFragment`          |
| `WebTheme/AppearanceRegistration.Register<T,S>`     | `KleeneStarApplication` ctor                    | `KleeneStarPortalApplication` ctor             |

The portal declares a default theme of its own (`PortalLightTheme`), because the core's
`LightTheme` is registered for the core only and an application without a default takes the first
theme registered for it. The theme card styles its captions inline: the portal does not load
`kleenestar.css`.

## How it reaches the page

- **Framework registration** (`AppearanceRegistration.Register`, once per application):
  `ThemePreferences.Register` (resolver + the framework's theme selector over
  `/api/1/appearance/theme`) and `ThemePreferences.RegisterColorScheme<Scheme>`. With that the
  framework marks every page root with `data-wx-theme` and `data-wx-theme-service`, so the dark
  mode switch of the avatar menu stores into the same preference, and its own settings pages
  (system → theme) apply and offer the same choice.
- **Theme on every page**: WebExpress applies a per-user theme only on its settings pages; for all
  other pages the application has to call `UseTheme` itself. KleeneStar has no common page base,
  so `WebFragment/Appearance/AppearanceFragment` (section `SectionBodyPreferences`, general + admin
  scope) calls `visualTree.UseTheme(ThemePreferences.Resolve(…))` while the page renders. This works
  because the visual tree renders that section with itself as argument before the head is
  serialized, and the head's stylesheet links are read lazily.
- **Adjustments**: the same fragment emits a `<style>` built by `WebTheme/AppearanceStyle`. It
  stands in the body on purpose - head styles are written *before* the stylesheet links, so the
  theme would win every token. The accent redefines the primary, link and highlight token families
  for light and dark (dark block last); the link colour is shifted towards the ink until it reaches
  4.5:1 against the page. Font size sets the root size (everything is in `rem`), density the body
  line height, motion the same reduction the framework applies under `prefers-reduced-motion`.

## Surfaces

- `ProfileAppearanceThemeFragment` - theme and mode, the framework's own selectors
  (`ThemePreferences.CreateSelector` / `CreateColorSchemeSelector`); each stores at once, the theme
  selector reloads the page.
- `ProfileAppearanceFormFragment` - the adjustments, a `FragmentControlDataFormEdit` over
  `/api/1/appearance` (form DTO `WebTheme/AppearanceForm`, one record per caller whatever id is
  sent, `POST` refused, `DELETE` clears the adjustments). The endpoint answers `PUT` **and
  `PATCH`** (the framework maps both onto one update), so **only the fields the payload names are
  changed**; a sent value that is not offered is refused (`profile.appearance.unoffered`) instead of
  resetting the choice, and a custom accent sent without a colour keeps the stored one. Every input is a selection (no checkbox -
  see the checkbox binding trap), the colour picker excepted, which the endpoint validates. The form
  reloads the page on success (`TypeRestFormSuccess.Reload`).

## Endpoints

| Route                              | Base                  | Purpose                         |
|------------------------------------|-----------------------|---------------------------------|
| `/api/1/appearance/theme`          | `RestApiTheme`        | theme list, pick (`PUT v=<id>`) |
| `/api/1/appearance/scheme`         | `RestApiColorScheme`  | `PUT v=light|dark`              |
| `/api/1/appearance?id=<identity>`  | `RestApiCrud`         | the adjustments form            |

The portal answers the same three under `/portal/api/1/appearance…`.

## Tests

`UnitTestAppearanceStyle` (tokens, link contrast, unoffered values dropped),
`UnitTestAppearanceManager` (round trip, merge, empty record removed, nobody stores nothing, form
projection), `UnitTestAppearanceEndpoint` (partial payload, accent merge, unoffered values,
concurrent changes - fails without the gate); the plugin's `UnitTestThemes`.
