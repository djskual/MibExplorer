param(
    [string]$OfficialScriptsPath = ".\Scripts\Official",
    [string]$OutputManifestPath = ".\Scripts\Official\manifest.json"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-ScriptVersion {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ScriptPath
    )

    $lines = Get-Content -LiteralPath $ScriptPath -Encoding UTF8

    foreach ($line in $lines) {
        $trimmed = $line.Trim()

        if ($trimmed -like "# Version:*") {
            $version = $trimmed.Substring("# Version:".Length).Trim()
            if (-not [string]::IsNullOrWhiteSpace($version)) {
                return $version
            }
        }
    }

    throw "Missing '# Version:' header in script: $ScriptPath"
}

function Get-FileSha256 {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath
    )

    return (Get-FileHash -LiteralPath $FilePath -Algorithm SHA256).Hash.ToUpperInvariant()
}

function Get-PackageSha256 {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PackagePath
    )

    $files = Get-ChildItem -LiteralPath $PackagePath -Recurse -File |
        Sort-Object { $_.FullName.Substring($PackagePath.Length).TrimStart('\','/').Replace('\','/') }

    $sha256 = [System.Security.Cryptography.SHA256]::Create()

    try {
        foreach ($file in $files) {
            $relativePath = $file.FullName.Substring($PackagePath.Length).TrimStart('\','/').Replace('\','/')
            $pathBytes = [System.Text.Encoding]::UTF8.GetBytes($relativePath + "`n")
            $null = $sha256.TransformBlock($pathBytes, 0, $pathBytes.Length, $pathBytes, 0)

            $contentBytes = [System.IO.File]::ReadAllBytes($file.FullName)
            $null = $sha256.TransformBlock($contentBytes, 0, $contentBytes.Length, $contentBytes, 0)

            $separatorBytes = [System.Text.Encoding]::UTF8.GetBytes("`n")
            $null = $sha256.TransformBlock($separatorBytes, 0, $separatorBytes.Length, $separatorBytes, 0)
        }

        $null = $sha256.TransformFinalBlock(@(), 0, 0)
        return ([System.BitConverter]::ToString($sha256.Hash)).Replace("-", "").ToUpperInvariant()
    }
    finally {
        $sha256.Dispose()
    }
}

function New-ScriptEntry {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,

        [Parameter(Mandatory = $true)]
        [string]$Version,

        [Parameter(Mandatory = $true)]
        [string]$Sha256
    )

    [ordered]@{
        Name    = $Name
        Version = $Version
        Sha256  = $Sha256
    }
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$officialRoot = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $OfficialScriptsPath))
$outputManifest = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $OutputManifestPath))

if (-not (Test-Path -LiteralPath $officialRoot)) {
    throw "Official scripts folder not found: $officialRoot"
}

$packageEntries = New-Object System.Collections.Generic.List[object]
$singleEntries  = New-Object System.Collections.Generic.List[object]

# Single scripts = *.sh directly inside Official root
Get-ChildItem -LiteralPath $officialRoot -File -Filter "*.sh" |
    Sort-Object Name |
    ForEach-Object {
        $version = Get-ScriptVersion -ScriptPath $_.FullName
        $sha = Get-FileSha256 -FilePath $_.FullName

        $singleEntries.Add((New-ScriptEntry -Name $_.Name -Version $version -Sha256 $sha))
    }

# Packages = directories containing run.sh directly inside Official root
Get-ChildItem -LiteralPath $officialRoot -Directory |
    Sort-Object Name |
    ForEach-Object {
        $runSh = Join-Path $_.FullName "run.sh"
        if (-not (Test-Path -LiteralPath $runSh)) {
            return
        }

        $version = Get-ScriptVersion -ScriptPath $runSh
        $sha = Get-PackageSha256 -PackagePath $_.FullName

        $packageEntries.Add((New-ScriptEntry -Name $_.Name -Version $version -Sha256 $sha))
    }

$manifest = [ordered]@{
    PackagesScripts = $packageEntries
    SingleScripts   = $singleEntries
}

$outputDir = Split-Path -Parent $outputManifest
if (-not (Test-Path -LiteralPath $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir | Out-Null
}

$json = $manifest | ConvertTo-Json -Depth 10
[System.IO.File]::WriteAllText($outputManifest, $json, [System.Text.UTF8Encoding]::new($false))

Write-Host "Manifest generated:"
Write-Host "  $outputManifest"
Write-Host ""
Write-Host "Packages: $($packageEntries.Count)"
Write-Host "Single scripts: $($singleEntries.Count)"