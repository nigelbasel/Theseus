# Plan

1. `ci.yml`: push trigger and cancel-in-progress add `nigel/theseus`; the release job runs for both branches and adds
   `--prerelease --latest=false` and a pre-release title for any branch but `main`. (AC 1, 2)
2. `TheseusSettings.IncludePreReleases`, default false. (AC 3)
3. `GitHubReleaseFeed`: an optional `Func<bool>` read on each check; when true, `ListEndpoint` (one call, 20 newest)
   and `Newest`, which reads each entry with pre-releases allowed and takes the highest build stamp. `Parse` keeps
   refusing pre-releases. (AC 4, 5)
4. `App.StartUpdateChecks` passes the settings store's value; `SettingsViewModel` and `SettingsWindow` gain the
   check box. (AC 3, 5)
5. Tests: `Newest` (newest wins, a newer main release wins, drafts and no-installer skipped, empty or not a list),
   the endpoint, the setting's default and round trip through the panel.
