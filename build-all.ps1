$ErrorActionPreference = "Stop"
$outDir = "dist"
$version = nbgv get-version -v NuGetPackageVersion
Write-Output $version

$platforms = @(
    @{ Name = "win-x64"   },
    @{ Name = "win-arm64" },
    @{ Name = "linux-x64" },
    @{ Name = "osx-x64"   },
    @{ Name = "osx-arm64" }
)
# clean
Remove-Item $outDir -Recurse -Force -ErrorAction SilentlyContinue
foreach ($platform in $platforms) {
    $rid = $platform.Name
    $platformDir = "$outDir/$rid"
    Write-Host "Building package for $rid..."
    dotnet publish FakeHttpClient -c Release `
            -r $rid `
            --self-contained true `
            -p:PublishSingleFile=true `
            -p:IncludeNativeLibrariesForSelfExtract=true `
            -p:PublishTrimmed=false `
            -o $platformDir

    $zipPath = "$outDir/FakeHttpClient.Net-$version-$rid.zip"
    Write-Host "Creating zip package at $zipPath..."
    Compress-Archive -Path "$outDir/$rid/*" -DestinationPath $zipPath -Force
}


# Upload to GitHub
#$tag = "v$version"
#gh release create $tag `
#    --title "Release $tag" `
#    --notes "Cross-platform build of FakeHttpClient" `
#    --target main `
#    $(Get-ChildItem "$outDir\*.zip" | ForEach-Object { "--assets" , $_.FullName })
