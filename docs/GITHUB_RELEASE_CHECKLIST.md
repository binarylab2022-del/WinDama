# GitHub Release Checklist

## One-time repository setup

1. GitHub repository: `https://github.com/binarylab2022-del/WinDama`.
2. Copy this project to a clean folder.
3. Confirm `RepositoryUrl` in `Directory.Build.props` is set to `https://github.com/binarylab2022-del/WinDama`.
4. Review `LICENSE` and confirm that MIT is the intended open-source license.
5. Review `README.md`, `RELEASE_NOTES.md`, and `CONTRIBUTING.md`.
6. Commit the source code.

## Suggested first commit

```powershell
git init
git add .
git commit -m "Initial open-source WinDama release"
git branch -M main
git remote add origin https://github.com/binarylab2022-del/WinDama.git
git push -u origin main
```

## Validate locally

```powershell
dotnet restore .\WinDama1.0\WinDama.sln
dotnet build .\WinDama1.0\WinDama.sln -c Release
dotnet test .\WinDama.Tests\WinDama.Tests.csproj -c Release
```

Expected baseline:

```text
101 passed, 0 failed
```

## Create a release ZIP

```powershell
.\scripts\Publish-Release-x64.ps1
```

For a self-contained Windows x64 package:

```powershell
.\scripts\Publish-Release-x64.ps1 -SelfContained
```

The package is written to:

```text
artifacts/packages/WinDama-1.0.0-preview-win-x64.zip
```

## Suggested GitHub release

- Tag: `v1.0.0-preview`
- Title: `WinDama 1.0.0-preview`
- Attach: `WinDama-1.0.0-preview-win-x64.zip`
- Release notes: copy from `RELEASE_NOTES.md`.
