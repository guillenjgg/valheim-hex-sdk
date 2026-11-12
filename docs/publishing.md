# Publishing and installing the SDK

The NuGet package is hosted on GitHub Packages in `guillenjgg/valheim-hex-sdk`.
No NuGet.org account or publishing API key is required. GitHub Actions uses its
own short-lived `GITHUB_TOKEN` to publish.

## Publish a version

1. Commit and push the package setup to `main`.
2. Open the repository's **Actions** tab.
3. Select **Build and publish NuGet package**, then **Run workflow** on `main`.
4. Wait for both the build and publish jobs to finish.
5. Find `HexValheimSdk` under the repository/account's **Packages** listing.

The first version is `0.1.0-alpha.1`. Each changed release needs a new version in
`HexValheimSdk.csproj`; package versions cannot be overwritten. Publishing a
GitHub release also triggers publication: its tag must be `v` followed by the
exact project version, for example `v0.1.0-alpha.1`.

Builds on pushes and pull requests test and package without publishing. The
configuration checks use an official BepInEx runtime downloaded during the build.
Only the SDK DLL, XML documentation, and README are included in the NuGet package.

## One-time Visual Studio setup

GitHub's NuGet registry requires authentication for package installation, including
public packages. Create a **personal access token (classic)** in GitHub with
`read:packages`. Keep it in your local NuGet credentials, never in the repository
or chat.

Run this once in a local terminal, replacing `YOUR_TOKEN` locally:

```powershell
dotnet nuget add source "https://nuget.pkg.github.com/guillenjgg/index.json" `
  --name GitHub-guillenjgg --username guillenjgg --password YOUR_TOKEN
```

On Windows the saved password is encrypted by default. Do not add
`--store-password-in-clear-text`. This SDK repository's `nuget.config` intentionally
clears sources for reproducible SDK builds; add the authenticated GitHub source
in the consuming mod's NuGet configuration, not this SDK's configuration.

Then, in the mod's Visual Studio project:

1. Remove the old DLL reference to `HexValheimSdk`.
2. Remove the old `CopyHexValheimSdk` build target; NuGet manages the SDK assembly.
3. Open **Manage NuGet Packages** and select `GitHub-guillenjgg` as the source.
4. Enable **Include prerelease**, search for `HexValheimSdk`, and install it.
5. Rebuild. The SDK assembly should appear alongside the mod assembly.

Old .NET Framework projects can use PackageReference or packages.config. If
Visual Studio asks, PackageReference keeps dependencies in the project file and
avoids a solution-local packages directory.

BepInEx, Harmony, Unity, and game assemblies remain the mod's own references with
Copy Local disabled. BepInEx.BaseLib is a private build dependency of the SDK and
is not included or installed transitively. This prevents the package from adding
extra runtime copies of the loader's assemblies. Installing the SDK package does
not install BepInEx or deploy anything into the game.

GitHub Packages may create a package as private initially. Adjust its visibility
and repository access in the package settings if other users or repositories need
it. A download token is required even after making the NuGet package public.

## License

No license has been selected for this repository yet. The package does not claim
an open-source license. Choose a license before inviting others to reuse the SDK.

## Documentation

- [GitHub NuGet registry](https://docs.github.com/en/packages/working-with-a-github-packages-registry/working-with-the-nuget-registry)
- [Publishing through GitHub Actions](https://docs.github.com/en/packages/learn-github-packages/publishing-a-package)
