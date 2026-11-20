# Review

An independent reviewer read the diff against `nigel/theseus` on 2026-11-20. **Must fix:** none.

**Should fix, both fixed:**
1. Turning the setting off did not take back a pre-release already offered: Check for updates said "up to date" while
   the banner still offered it. Now an answer that is not newer withdraws the offer, in the timed check and in Check
   for updates; a failed check (no answer) keeps it. Three tests.
2. Nothing tested that the setting is read on every check with one call each. The feed takes a message handler; a
   test switches the setting between three checks and asserts the address asked and the release offered each time.

**Checked and fine:** a pre-release is never latest, so `releases/latest` and the download link keep answering with
`main`; `main`'s release step is unchanged; the bash array is valid on the runner's shell; runs on the two branches
never cancel each other; tags cannot collide (each build stamps its own clock); the newest release by stamp is safe
over the list; turning the setting off after installing a pre-release never offers a downgrade; the records hold no
machine paths or card ids.
