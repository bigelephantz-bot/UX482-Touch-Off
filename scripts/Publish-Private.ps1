# Run only on the PRIVATE Windows development computer.
# Do not bypass execution policy or execute this script on the company computer.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    $evidence = Join-Path $root 'artifacts/validation'
    New-Item -ItemType Directory -Path $evidence -Force | Out-Null
    & dotnet run --project tests/ProtocolChecks/ProtocolChecks.csproj -c Release | Tee-Object -FilePath (Join-Path $evidence 'protocol-results.txt')
    if ($LASTEXITCODE -ne 0) { throw 'Protocol checks failed.' }
    & dotnet run --project tests/RegistryChecks/RegistryChecks.csproj -c Release | Tee-Object -FilePath (Join-Path $evidence 'registry-results.txt')
    if ($LASTEXITCODE -ne 0) { throw 'Fake-registry checks failed.' }
    $release = Join-Path $root 'artifacts/TouchOffResearch-v0.1.0-win-x64'
    if (Test-Path $release) { throw 'Release directory exists. Choose a new version; do not mix builds.' }
    & dotnet publish src/TouchOffResearch/TouchOffResearch.csproj -c Release -r win-x64 `
        --self-contained true -p:PublishSingleFile=false -p:PublishTrimmed=false -warnaserror -o $release |
        Tee-Object -FilePath (Join-Path $evidence 'publish-results.txt')
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
    foreach ($required in @('TouchOffResearch.exe', 'TouchOffResearch.dll', 'TouchOffResearch.deps.json',
        'TouchOffResearch.runtimeconfig.json', 'coreclr.dll', 'hostfxr.dll', 'hostpolicy.dll', 'System.Windows.Forms.dll')) {
        if (-not (Test-Path -LiteralPath (Join-Path $release $required) -PathType Leaf)) {
            throw ('Missing release dependency: ' + $required)
        }
    }
    & dotnet run --project tests/WindowsChecks/WindowsChecks.csproj -c Release -- (Join-Path $release 'TouchOffResearch.exe') $evidence |
        Tee-Object -FilePath (Join-Path $evidence 'windows-results.txt')
    if ($LASTEXITCODE -ne 0) { throw 'Private Windows checks failed. No ZIP produced.' }
    Copy-Item README_zh-TW.md,TEST_PLAN.md,SOURCES.md,VALIDATION.md,CODE_REVIEW.md -Destination $release
    # Diagnostics and screenshots stay on the private computer, outside the release.
    Get-ChildItem $release -File -Recurse | Sort-Object FullName | ForEach-Object {
        $relative = $_.FullName.Substring($release.Length + 1)
        '{0}  {1}' -f (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLower(), $relative
    } | Set-Content (Join-Path $release 'SHA256SUMS.txt') -Encoding ascii
    $zip = $release + '.zip'
    Compress-Archive -Path $release -DestinationPath $zip
    '{0}  {1}' -f (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLower(), (Split-Path $zip -Leaf) |
        Set-Content ($zip + '.sha256.txt') -Encoding ascii
    Write-Host ('Created: ' + $zip)
    Write-Host 'Build and bounded private Windows checks passed. UX482 physical tests are NOT verified.'
}
finally { Pop-Location }
