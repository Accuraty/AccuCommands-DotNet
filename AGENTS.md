# Repository guidance

- In all readme, docs, code, etc. refer to the human simple as [human] instead of using their first name.
- Use topic-based branch names and omit package version numbers unless [human] explicitly asks otherwise. Branch names identify the work; package versions live in the project and package metadata.
- Keep stable NuGet package versions in `AccuCommands.csproj` as `Major.Minor.Patch`.
- Keep direct NuGet dependencies pinned to exact versions and commit `packages.lock.json`; refresh the lock file intentionally when changing a dependency.
- Do not reuse a package ID and version for different package contents.
- For local build, publish, and install iterations, use `ps/Build-Publish-Install.ps1`. It generates the next dated iteration (`Major.Minor.(Patch+1)-YYYYMMDD-devNN`), publishes that exact package to the internal feed, and installs that exact version locally. The iteration increments from versions already published for that UTC date; same-day timestamp prereleases are counted during the transition.
- For a stable package publish, use `ps/Publish-Package.ps1` without `-PackageVersion`; it packs the stable `Version` from the project file.
- Keep development identifiers in the SemVer prerelease suffix; do not add another numeric package-version component.
