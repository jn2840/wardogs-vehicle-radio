$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    dotnet publish src/WardogsBridge -c Release -r win-x64 --self-contained true -o dist/WardogsBridge --configfile NuGet.Config "-p:RestorePackagesPath=$PSScriptRoot\.packages"
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }
    Copy-Item -LiteralPath README.md -Destination dist/WardogsBridge/README.md
    foreach ($notice in @('LICENSE', 'NOTICE', 'THIRD-PARTY-NOTICES.txt')) {
        Copy-Item -LiteralPath $notice -Destination "dist/WardogsBridge/$notice"
    }
    Copy-Item -LiteralPath .packages/naudio/2.2.1/license.txt -Destination dist/WardogsBridge/NAudio-LICENSE.txt
    foreach ($runtime in @('microsoft.netcore.app.runtime.win-x64', 'microsoft.windowsdesktop.app.runtime.win-x64')) {
        $runtimePackage = Get-ChildItem ".packages/$runtime" -Directory | Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
        foreach ($notice in @('LICENSE.TXT', 'THIRD-PARTY-NOTICES.TXT')) {
            $noticePath = Join-Path $runtimePackage.FullName $notice
            if (Test-Path -LiteralPath $noticePath) {
                Copy-Item -LiteralPath $noticePath -Destination "dist/WardogsBridge/$runtime-$notice"
            }
        }
    }
    Compress-Archive -Path dist/WardogsBridge/* -DestinationPath dist/Wardogs-Vehicle-Radio-win-x64.zip -Force
} finally { Pop-Location }
