# Implementation

As planned. Files: `.github/workflows/ci.yml`, `src/Theseus.Core/Settings/TheseusSettings.cs`,
`src/Theseus.Core/Updates/GitHubReleaseFeed.cs`, `src/Theseus.App/App.xaml.cs`,
`src/Theseus.App/ViewModels/SettingsViewModel.cs`, `src/Theseus.App/SettingsWindow.xaml`; tests in
`tests/Theseus.Tests/Updates/GitHubReleaseFeedTests.cs` and
`tests/Theseus.Tests/ViewModels/IncludePreReleasesSettingTests.cs`.

The check box sits after the holdout fields in the same settings section. The workflow change cannot be run locally;
its first run is the first push of `nigel/theseus` after this merges, and the evidence records that run.
