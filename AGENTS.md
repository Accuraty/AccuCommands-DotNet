# Repository guidance

- In all readme, docs, code, etc. refer to the human simple as [human] instead of using their first name.
- Use topic-based branch names and omit package version numbers unless [human] explicitly asks otherwise. Branch names identify the work; package versions live in the project and package metadata.
- Keep stable NuGet package versions in `AccuCommands.csproj` as `Major.Minor.Patch`.
- Keep direct NuGet dependencies pinned to exact versions and commit `packages.lock.json`; refresh the lock file intentionally when changing a dependency.
- Do not reuse a package ID and version for different package contents.
- For local build, publish, and install iterations, use `ps/Build-Publish-Install.ps1`. It generates a unique next-patch prerelease version (`Major.Minor.(Patch+1)-dev.<UTC timestamp>`), publishes that exact package to the internal feed, and installs that exact version locally.
- For a stable package publish, use `ps/Publish-Package.ps1` without `-PackageVersion`; it packs the stable `Version` from the project file.
- Do not replace the prerelease scheme with a fourth numeric package-version component. The prerelease suffix is intentional and keeps stable package versions SemVer-compatible.
