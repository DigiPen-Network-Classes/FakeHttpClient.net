$ErrorActionPreference = "Stop"

$outDir = "dist"

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
    $outDir = "$outDir/$rid"
    Write-Host "Building package for $rid..."
    dotnet publish -c Release -r $rid --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=false -o $outDir\$rid
}
