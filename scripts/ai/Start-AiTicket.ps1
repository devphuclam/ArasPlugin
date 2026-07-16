[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [ValidatePattern('^[A-Z]+-\d{2}$')]
    [string]$TicketId,
    [switch]$NoBranch
)

$ErrorActionPreference = 'Stop'
Write-Host 'The legacy tasks/ai ticket workflow is retired.' -ForegroundColor Yellow
Write-Host 'For feature behavior, create and review Spec Kit artifacts under specs/<feature>/.' -ForegroundColor Cyan
Write-Host 'For bugs, hotfixes, and chores, use the approved GitHub Issue workflow.' -ForegroundColor Cyan
Write-Host 'No branch, prompt, or ticket was created.' -ForegroundColor Yellow
exit 1
