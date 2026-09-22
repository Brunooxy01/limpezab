$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$sourcePath = Join-Path $projectRoot 'src\Program.cs'
$outputPath = Join-Path $projectRoot 'limpezaB.exe'
$iconPath = Join-Path $projectRoot 'assets\limpezaB.ico'

if (Test-Path -LiteralPath $outputPath) {
    Remove-Item -LiteralPath $outputPath -Force
}

$compilerPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compilerPath)) {
    $compilerPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}

if (-not (Test-Path -LiteralPath $iconPath)) {
    & (Join-Path $projectRoot 'tools\make-icon.ps1')
}

& $compilerPath /nologo /target:exe /optimize+ "/out:$outputPath" "/win32icon:$iconPath" `
    /reference:System.dll /reference:System.Core.dll $sourcePath
if ($LASTEXITCODE -ne 0) {
    throw "Falha ao compilar o limpezaB.exe (código $LASTEXITCODE)."
}

Write-Host "Executável criado em: $outputPath"
