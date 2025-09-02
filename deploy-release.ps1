$ErrorActionPreference = "Stop"
$outDir = "dist"
$version = nbgv get-version -v NuGetPackageVersion

# Upload to GitHub
$tag = "v$version"
gh release create $tag `
    --title "Release $tag" `
    --notes "Cross-platform build of FakeHttpClient" `
    $(Get-ChildItem "$outDir\*.zip" | ForEach-Object { $_.FullName })
