# Paket yuklanganda PMC da funksiyalarni eksport qiladi
Import-Module "$PSScriptRoot\Add-SheetlyMigration.ps1" -Force
# Alias qo'shish (Xuddi EF kabi qisqa buyruq bo'lishi uchun)
Set-Alias -Name add-sheetly-migration -Value Add-SheetlyMigration -Scope Global