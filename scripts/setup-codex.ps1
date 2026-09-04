[CmdletBinding()]
param(
    [switch]$WithRewardSystem
)

$ErrorActionPreference = 'Stop'

if (-not (Get-Command codex -ErrorAction SilentlyContinue)) {
    throw 'Codex CLI is not installed or is not available on PATH.'
}

$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$marketplaceOutput = & codex plugin marketplace list
if ($LASTEXITCODE -ne 0) {
    throw 'Could not list Codex plugin marketplaces.'
}

$marketplaceEntry = $marketplaceOutput | Select-String -Pattern '^nbg-unity\s+' | Select-Object -First 1
if ($marketplaceEntry) {
    $registeredRoot = ($marketplaceEntry.Line -replace '^nbg-unity\s+', '').Trim()
    if ([System.IO.Path]::GetFullPath($registeredRoot) -ne [System.IO.Path]::GetFullPath($repoRoot)) {
        throw "The nbg-unity marketplace already points to '$registeredRoot'. Remove it first with 'codex plugin marketplace remove nbg-unity'."
    }
} else {
    & codex plugin marketplace add $repoRoot
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not register the nbg-unity marketplace.'
    }
}

$plugins = @('unity-standard', 'unity-mcp')
if ($WithRewardSystem) {
    $plugins += 'reward-system'
}

foreach ($plugin in $plugins) {
    & codex plugin add "$plugin@nbg-unity"
    if ($LASTEXITCODE -ne 0) {
        throw "Could not install $plugin."
    }
}

Write-Host ''
Write-Host 'Codex plugins installed.'
Write-Host 'Open Codex, run /hooks, review and trust the unity-standard SessionStart hook, then start a new task from a Unity project root.'
