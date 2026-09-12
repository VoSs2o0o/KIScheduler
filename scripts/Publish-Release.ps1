[CmdletBinding()]
param(
    [string]$OutputDirectory = "",
    [switch]$SkipTests
)

$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $repositoryRoot "KIScheduler.sln"
$project = Join-Path $repositoryRoot "src/KIScheduler.WinForms/KIScheduler.WinForms.csproj"
$publishDirectory = if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    Join-Path $repositoryRoot "artifacts/publish/win-x64"
} else {
    [System.IO.Path]::GetFullPath($OutputDirectory, (Get-Location).Path)
}

dotnet restore $solution -r win-x64
if ($LASTEXITCODE -ne 0) { throw "Restore fehlgeschlagen." }

dotnet build $solution -c Release --no-restore
if ($LASTEXITCODE -ne 0) { throw "Build fehlgeschlagen." }

if (-not $SkipTests) {
    dotnet test $solution -c Release --no-build
    if ($LASTEXITCODE -ne 0) { throw "Tests fehlgeschlagen." }
}

dotnet publish $project -p:PublishProfile=win-x64 --no-restore -o $publishDirectory
if ($LASTEXITCODE -ne 0) { throw "Veröffentlichung fehlgeschlagen." }

$publishedExecutable = Join-Path $publishDirectory "KIScheduler.WinForms.exe"
$smokeDataDirectory = Join-Path $repositoryRoot "artifacts/startup-check"
$dataDirectoryVariable = "KISCHEDULER_Runtime__DataDirectory"
$previousDataDirectory = [Environment]::GetEnvironmentVariable($dataDirectoryVariable, "Process")
try {
    [Environment]::SetEnvironmentVariable($dataDirectoryVariable, $smokeDataDirectory, "Process")
    $startupCheck = Start-Process -FilePath $publishedExecutable -ArgumentList "--startup-check" `
        -Wait -PassThru -WindowStyle Hidden
    if ($startupCheck.ExitCode -ne 0) { throw "Startcheck der veröffentlichten Anwendung fehlgeschlagen." }
} finally {
    [Environment]::SetEnvironmentVariable($dataDirectoryVariable, $previousDataDirectory, "Process")
}

Write-Host "KIScheduler wurde nach '$publishDirectory' veröffentlicht."
