param([string[]]$Workloads = @())
$ErrorActionPreference = 'Stop'
$installDir = '.dotnet'; $channel = '10.0'; $quality = 'ga'
$version = ''; $rollForward = 'latestFeature'; $allowPrerelease = $false
$errorMessage = 'Required .NET SDK not found. Run ./install-dotnet.ps1 (or .sh) to install it locally.'
$installScript = Join-Path $env:TEMP "dotnet-install-$([guid]::NewGuid()).ps1"
try {
    Invoke-WebRequest -Uri 'https://dot.net/v1/dotnet-install.ps1' -OutFile $installScript
    $installArgs = @('-InstallDir', $installDir)
    if ($version) {
        $installArgs += @('-Version', $version)
        $rollForward = 'disable'
    } else {
        $installArgs += @('-Channel', $channel, '-Quality', $quality)
    }
    & $installScript @installArgs
}
finally {
    if (Test-Path -LiteralPath $installScript) {
        Remove-Item -LiteralPath $installScript -Force
    }
}
$sdkVersion = & "$installDir\dotnet.exe" --version
$globalJson = if (Test-Path 'global.json') {
    Copy-Item 'global.json' 'global.json.bak'
    Get-Content -Path 'global.json' -Raw | ConvertFrom-Json
} else {
    [pscustomobject]@{}
}
if (-not $globalJson.PSObject.Properties['sdk']) {
    $globalJson | Add-Member -MemberType NoteProperty -Name 'sdk' -Value ([pscustomobject]@{})
}
$updates = [ordered]@{
    version = $sdkVersion
    allowPrerelease = $allowPrerelease
    rollForward = $rollForward
    paths = @('.dotnet', '$host$')
    errorMessage = $errorMessage
}
foreach ($entry in $updates.GetEnumerator()) {
    $property = $globalJson.sdk.PSObject.Properties[$entry.Key]
    if ($property) {
        $property.Value = $entry.Value
    } else {
        $globalJson.sdk | Add-Member -MemberType NoteProperty -Name $entry.Key -Value $entry.Value
    }
}
$globalJson | ConvertTo-Json -Depth 10 | Set-Content -Path 'global.json' -Encoding UTF8
if (-not (Test-Path .gitignore) -or -not (Select-String -Path .gitignore -Pattern '^\.dotnet/$' -Quiet)) {
    Add-Content -Path .gitignore -Value '.dotnet/'
}
if ($Workloads.Count -gt 0) { & "$installDir\dotnet.exe" workload install @Workloads }
Write-Host "Done. SDK: $sdkVersion"
