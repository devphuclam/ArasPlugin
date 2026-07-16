[CmdletBinding()]
param(
    [ValidateSet('pro','flash')]
    [string]$Profile = 'pro'
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
if (-not (Test-Path (Join-Path $repoRoot 'IdeaCadConnector.sln'))) {
    throw 'IdeaCadConnector.sln was not found. Run from the installed work kit.'
}

$claude = Get-Command claude -ErrorAction SilentlyContinue
if (-not $claude) {
    throw 'Claude Code is not installed. Install Node.js 18+, then run: npm install -g @anthropic-ai/claude-code'
}

$secure = Read-Host 'DeepSeek API key (not saved)' -AsSecureString
$ptr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
try {
    $apiKey = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($ptr)
} finally {
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($ptr)
}
if ([string]::IsNullOrWhiteSpace($apiKey)) { throw 'API key is required.' }

$env:ANTHROPIC_BASE_URL = 'https://api.deepseek.com/anthropic'
$env:ANTHROPIC_AUTH_TOKEN = $apiKey
if ($Profile -eq 'pro') {
    $env:ANTHROPIC_MODEL = 'deepseek-v4-pro[1m]'
    $env:ANTHROPIC_DEFAULT_OPUS_MODEL = 'deepseek-v4-pro[1m]'
    $env:ANTHROPIC_DEFAULT_SONNET_MODEL = 'deepseek-v4-pro[1m]'
    $env:ANTHROPIC_DEFAULT_HAIKU_MODEL = 'deepseek-v4-flash'
    $env:CLAUDE_CODE_SUBAGENT_MODEL = 'deepseek-v4-flash'
    $env:CLAUDE_CODE_EFFORT_LEVEL = 'max'
} else {
    $env:ANTHROPIC_MODEL = 'deepseek-v4-flash'
    $env:ANTHROPIC_DEFAULT_OPUS_MODEL = 'deepseek-v4-flash'
    $env:ANTHROPIC_DEFAULT_SONNET_MODEL = 'deepseek-v4-flash'
    $env:ANTHROPIC_DEFAULT_HAIKU_MODEL = 'deepseek-v4-flash'
    $env:CLAUDE_CODE_SUBAGENT_MODEL = 'deepseek-v4-flash'
    $env:CLAUDE_CODE_EFFORT_LEVEL = 'high'
}

Set-Location $repoRoot
Write-Host 'Starting DeepSeek-backed coding agent. API key exists only in this process environment.' -ForegroundColor Cyan
Write-Host 'Read AGENTS.md, .specify/memory/constitution.md, and CONTEXT.md first.' -ForegroundColor Yellow
Write-Host 'For feature work, use the approved Spec Kit artifacts under specs/<feature>/.' -ForegroundColor Yellow
Write-Host 'For bugs, hotfixes, or chores, use an approved GitHub Issue.' -ForegroundColor Yellow
& $claude.Source

# Best-effort cleanup after the child process exits.
Remove-Item Env:ANTHROPIC_AUTH_TOKEN -ErrorAction SilentlyContinue
$apiKey = $null
