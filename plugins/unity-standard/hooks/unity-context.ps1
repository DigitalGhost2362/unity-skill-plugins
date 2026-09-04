[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)

$projectDir = if ($env:CLAUDE_PROJECT_DIR) {
    $env:CLAUDE_PROJECT_DIR
} else {
    (Get-Location).Path
}

$projectVersion = Join-Path $projectDir 'ProjectSettings\ProjectVersion.txt'
if (-not (Test-Path -LiteralPath $projectVersion -PathType Leaf)) {
    exit 0
}

$pluginRoot = if ($env:PLUGIN_ROOT) {
    $env:PLUGIN_ROOT
} else {
    $env:CLAUDE_PLUGIN_ROOT
}

if (-not $pluginRoot) {
    Write-Error 'PLUGIN_ROOT is not set.'
    exit 1
}

Get-Content -LiteralPath (Join-Path $pluginRoot 'hooks\unity-always.md') -Raw -Encoding UTF8
