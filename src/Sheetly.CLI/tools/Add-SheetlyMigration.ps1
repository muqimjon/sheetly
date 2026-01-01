<#
.SYNOPSIS
    Yangi Sheetly migratsiyasini yaratadi.
.PARAMETER Name
    Migratsiya nomi.
#>
param(
    [Parameter(Mandatory=$true, Position=0)]
    [string]$Name
)

Write-Host "🚀 Sheetly: Yangi migratsiya yaratilmoqda: $Name..." -ForegroundColor Cyan

# dotnet tool orqali asosiy CLI ni chaqiramiz
dotnet sheetly migrations add $Name

if ($LASTEXITCODE -eq 0) {
    Write-Host "✅ Migratsiya muvaffaqiyatli yaratildi!" -ForegroundColor Green
} else {
    Write-Error "❌ Migratsiya yaratishda xatolik yuz berdi."
}