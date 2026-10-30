# Updates

[Help home](index.md)

Every change to Theseus that passes its build on GitHub is published as a release, with an installer
and its checksum.

## How Theseus finds an update

Theseus checks GitHub for a newer release a minute after it starts, then every six hours. When there
is one, a yellow bar appears under the toolbar:

- **Update and restart** downloads the installer, checks it against the release's SHA-256 checksum,
  closes Theseus, runs the installer (Windows asks for permission) and opens Theseus again. Your
  running sessions are not touched.
- **Later** skips that version until a newer one is published.

Nothing is downloaded or installed until you press **Update and restart**.

To check straight away, choose **Help > Check for updates**. It also offers a version you put aside
with Later, and the status bar says what it found: up to date, an update, or that the check failed.

**Help > What's new in this version** opens the running version's release page.

## Installing by hand

Download the latest installer from
https://github.com/nigelbasel/Theseus/releases/latest/download/Theseus.msi

Windows may warn that the file is from an unknown publisher, because the installer is not signed. The
update inside Theseus does not get this warning, because Theseus downloads the file itself and checks
its checksum.

## Where Theseus keeps things

**Help > About** shows where Theseus is installed and where it keeps its board, settings, runs and
logs. **Tools > Open data folder** and **Open logs folder** open them. The installer neither writes nor
removes your data.
