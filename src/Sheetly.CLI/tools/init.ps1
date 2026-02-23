# Export functions when package is loaded in PMC
Import-Module "$PSScriptRoot\Add-SheetlyMigration.ps1" -Force
Set-Alias -Name add-sheetly-migration -Value Add-SheetlyMigration -Scope Global