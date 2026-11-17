# Code signing policy

Free code signing provided by [SignPath.io](https://signpath.io), certificate by [SignPath Foundation](https://signpath.org).

## What is signed

Only the Windows binaries built from this repository are signed:

- `FogForge.exe` – the pack manager
- `FogForge Setup.exe` – the installer (contains `FogForge.exe`)

Both are built from the source code in this repository by the GitHub Actions workflow
[`.github/workflows/release.yml`](.github/workflows/release.yml) whenever a release is published.
No binaries from other projects are signed. The icon packs (`fog-forge-packs`) are data files and are not signed.

## Team and roles

| Role | Member |
|------|--------|
| Author / committer | [DetainedStudio](https://github.com/DetainedStudio) |
| Reviewer | [DetainedStudio](https://github.com/DetainedStudio) |
| Approver (signing requests) | [DetainedStudio](https://github.com/DetainedStudio) |

All team members use multi-factor authentication for GitHub and SignPath.
Every release is approved manually in SignPath before it is signed.

## Privacy

See [PRIVACY.md](PRIVACY.md).
