# Build script with GitVersion integration
param(
    [Parameter()]
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [Parameter()]
    [switch]$Pack
)

Write-Host "Gmail MCP Build Script" -ForegroundColor Cyan
Write-Host "=====================" -ForegroundColor Cyan
Write-Host ""

# Get version from GitVersion
Write-Host "Determining version with GitVersion..." -ForegroundColor Yellow
$version = dotnet-gitversion /showvariable FullSemVer
Write-Host "Version: $version" -ForegroundColor Green
Write-Host ""

# Restore dependencies
Write-Host "Restoring dependencies..." -ForegroundColor Yellow
dotnet restore src/GmailMcp/GmailMcp.csproj
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Write-Host ""

# Build
Write-Host "Building project..." -ForegroundColor Yellow
dotnet build src/GmailMcp/GmailMcp.csproj --configuration $Configuration --no-restore /p:Version=$version
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Write-Host ""

# Pack if requested
if ($Pack) {
    Write-Host "Packing NuGet package..." -ForegroundColor Yellow
    dotnet pack src/GmailMcp/GmailMcp.csproj --configuration $Configuration --no-build --output ./packages /p:PackageVersion=$version
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    Write-Host ""
    Write-Host "Package created:" -ForegroundColor Green
    Get-ChildItem ./packages/*.nupkg | ForEach-Object {
        Write-Host "  - $($_.Name)" -ForegroundColor Cyan
    }
}

Write-Host ""
Write-Host "Build completed successfully! " -ForegroundColor Green
Write-Host "Version: $version" -ForegroundColor Cyan
