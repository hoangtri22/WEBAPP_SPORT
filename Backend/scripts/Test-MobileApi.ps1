<#
.SYNOPSIS
Runs the API suite against a fresh, isolated local MySQL instance and exports OpenAPI.
.EXAMPLE
pwsh -File Backend/scripts/Test-MobileApi.ps1 -Port 13382
.NOTES
No existing database or datadir is reused or deleted. All artifacts are retained below
.test-artifacts. Only the MySQL process started by this invocation is stopped.
#>
[CmdletBinding()]
param(
    [ValidateRange(1025, 65535)]
    [int] $Port = 13381,
    [string] $MySqlBin = 'C:\Program Files\MySQL\MySQL Server 8.0\bin',
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Debug'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$workspaceRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$artifactRoot = [IO.Path]::GetFullPath((Join-Path $workspaceRoot '.test-artifacts'))
$runName = 'mobile-api-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
$runDirectory = [IO.Path]::GetFullPath((Join-Path $artifactRoot $runName))
$mysqlData = Join-Path $runDirectory 'mysql-data'
$mysqlTemp = Join-Path $runDirectory 'mysql-temp'
$schemaName = 'sports_shop_mobile_test'
$noCodSchema = 'sports_shop_mobile_test_no_cod'
$mysqlServer = $null
$previousVariables = @{}
$environmentNames = @('SPORTSHOP_TEST_MYSQL', 'SPORTSHOP_TEST_MYSQL_NO_COD', 'SPORTSHOP_EXPORT_OPENAPI')

# Resolve and verify every managed path before creating files or starting the server.
$workspacePrefix = $workspaceRoot.TrimEnd([char[]] '\/') + [IO.Path]::DirectorySeparatorChar
$artifactPrefix = $artifactRoot.TrimEnd([char[]] '\/') + [IO.Path]::DirectorySeparatorChar
if (-not $artifactRoot.StartsWith($workspacePrefix, [StringComparison]::OrdinalIgnoreCase) -or
    -not $runDirectory.StartsWith($artifactPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'The isolated test datadir must remain inside this workspace .test-artifacts directory.'
}
foreach ($managedPath in @($mysqlData, $mysqlTemp)) {
    if (-not ([IO.Path]::GetFullPath($managedPath)).StartsWith($artifactPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Unsafe managed test path.'
    }
}
$mysqlExecutable = Join-Path $MySqlBin 'mysql.exe'
$mysqldExecutable = Join-Path $MySqlBin 'mysqld.exe'
foreach ($executable in @($mysqlExecutable, $mysqldExecutable)) {
    if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) {
        throw "MySQL executable missing: $executable. Supply -MySqlBin with a MySQL 8 installation."
    }
}
$dotnetExecutable = (Get-Command dotnet -ErrorAction Stop).Source
$solution = Join-Path $workspaceRoot 'SportShop.slnx'
$testProject = Join-Path $workspaceRoot 'Backend\SportShop.Api.Tests\SportShop.Api.Tests.csproj'
$sourceSchema = Join-Path $workspaceRoot 'Backend\Database\sports_shop.sql'
foreach ($requiredFile in @($solution, $testProject, $sourceSchema)) {
    if (-not (Test-Path -LiteralPath $requiredFile -PathType Leaf)) { throw "Required source file missing: $requiredFile" }
}

# Fail if the requested port is occupied; never connect to or stop an existing shop server.
$portProbe = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, $Port)
try { $portProbe.Start() }
catch { throw "Loopback port $Port is unavailable. Select another port with -Port." }
finally { $portProbe.Stop() }

New-Item -ItemType Directory -Path $mysqlData, $mysqlTemp -Force | Out-Null
$mysqlBase = [IO.Path]::GetFullPath((Join-Path $MySqlBin '..'))
$clientArguments = @('--no-defaults', '--protocol=TCP', '--host=127.0.0.1', "--port=$Port",
    '--user=root', '--default-character-set=utf8mb4', '--batch', '--raw', '--skip-column-names',
    '--connect-timeout=2', '--ssl-mode=DISABLED')

function Invoke-TestSqlFile {
    param([Parameter(Mandatory)][string] $FilePath, [Parameter(Mandatory)][string] $LogName,
        [string] $Database = '')
    $sqlArguments = @($clientArguments)
    if ($Database) {
        if ($Database -notin @($schemaName, $noCodSchema)) { throw 'Refusing SQL against an unexpected schema.' }
        $sqlArguments += "--database=$Database"
    }
    $sqlProcess = Start-Process -FilePath $mysqlExecutable -ArgumentList $sqlArguments -WindowStyle Hidden -Wait -PassThru `
        -RedirectStandardInput $FilePath -RedirectStandardOutput (Join-Path $runDirectory "$LogName.stdout.log") `
        -RedirectStandardError (Join-Path $runDirectory "$LogName.stderr.log")
    if ($sqlProcess.ExitCode -ne 0) { throw "SQL step '$LogName' failed; inspect its logs in $runDirectory." }
}

function Invoke-TestDotnet {
    param([Parameter(Mandatory)][string[]] $Arguments, [Parameter(Mandatory)][string] $LogName)
    $commandLog = Join-Path $runDirectory "$LogName.log"
    Write-Host "Running dotnet $($Arguments[0]); log: $commandLog"
    & $dotnetExecutable @Arguments *> $commandLog
    if ($LASTEXITCODE -ne 0) { throw "dotnet $($Arguments[0]) failed; inspect $commandLog." }
}

try {
    $initializeArguments = @('--no-defaults', '--initialize-insecure', "--basedir=`"$mysqlBase`"", "--datadir=`"$mysqlData`"", '--console')
    Write-Host "Initializing a fresh isolated MySQL datadir: $mysqlData"
    $initializer = Start-Process -FilePath $mysqldExecutable -ArgumentList $initializeArguments -WindowStyle Hidden -Wait -PassThru `
        -RedirectStandardOutput (Join-Path $runDirectory 'mysql-initialize.stdout.log') `
        -RedirectStandardError (Join-Path $runDirectory 'mysql-initialize.stderr.log')
    if ($initializer.ExitCode -ne 0) { throw "MySQL initialization failed; inspect logs in $runDirectory." }

    $serverArguments = @('--no-defaults', "--basedir=`"$mysqlBase`"", "--datadir=`"$mysqlData`"",
        "--tmpdir=`"$mysqlTemp`"", '--bind-address=127.0.0.1', "--port=$Port", '--mysqlx=OFF',
        '--skip-log-bin', '--default-time-zone=+07:00', '--max-connections=50', '--console')
    $mysqlServer = Start-Process -FilePath $mysqldExecutable -ArgumentList $serverArguments -WindowStyle Hidden -PassThru `
        -RedirectStandardOutput (Join-Path $runDirectory 'mysql-server.stdout.log') `
        -RedirectStandardError (Join-Path $runDirectory 'mysql-server.stderr.log')

    $ready = $false
    $startupDeadline = [DateTime]::UtcNow.AddSeconds(45)
    while ([DateTime]::UtcNow -lt $startupDeadline) {
        $mysqlServer.Refresh()
        if ($mysqlServer.HasExited) { throw "The isolated MySQL server exited; inspect logs in $runDirectory." }
        $ping = $null
        $pingExitCode = -1
        try {
            $ping = & $mysqlExecutable @clientArguments '--execute=SELECT 1' 2>$null
            $pingExitCode = $LASTEXITCODE
        }
        catch { } # Windows PowerShell can turn the expected startup connection error into an exception.
        if ($pingExitCode -eq 0 -and (($ping | Out-String).Trim() -eq '1')) { $ready = $true; break }
        Start-Sleep -Milliseconds 250
    }
    if (-not $ready) { throw "Isolated MySQL did not become ready within 45 seconds; inspect $runDirectory." }

    # Verify server identity before the first schema write, including protection against a port race.
    $serverIdentity = & $mysqlExecutable @clientArguments '--execute=SELECT @@port, @@datadir, @@bind_address' 2>$null
    if ($LASTEXITCODE -ne 0) { throw 'Cannot verify the isolated MySQL server identity.' }
    $identityParts = ($serverIdentity | Out-String).Trim() -split "`t"
    if ($identityParts.Length -ne 3) { throw 'Unexpected isolated MySQL server identity response.' }
    $reportedDatadir = [IO.Path]::GetFullPath($identityParts[1]).TrimEnd([char[]] '\/')
    if ([int] $identityParts[0] -ne $Port -or
        -not $reportedDatadir.Equals($mysqlData.TrimEnd([char[]] '\/'), [StringComparison]::OrdinalIgnoreCase) -or
        $identityParts[2] -ne '127.0.0.1') {
        throw 'Safety guard rejected the MySQL instance: port, datadir, or loopback binding differs.'
    }

    $schemaText = [IO.File]::ReadAllText($sourceSchema)
    if (-not $schemaText.Contains('CREATE DATABASE sports_shop ') -or -not $schemaText.Contains('USE sports_shop;')) {
        throw 'Source schema declarations changed; update this runner before importing.'
    }
    $schemaText = $schemaText.Replace('CREATE DATABASE sports_shop ', "CREATE DATABASE $schemaName ").Replace('USE sports_shop;', "USE $schemaName;")
    $isolatedSchemaFile = Join-Path $runDirectory 'isolated-schema.sql'
    [IO.File]::WriteAllText($isolatedSchemaFile, $schemaText, [Text.UTF8Encoding]::new($false))
    Invoke-TestSqlFile -FilePath $isolatedSchemaFile -LogName 'schema'
    $migrationDirectory = Join-Path $workspaceRoot 'Backend\Database\Migrations'
    foreach ($migration in (Get-ChildItem -LiteralPath $migrationDirectory -Filter '*.sql' -File | Sort-Object Name)) {
        Invoke-TestSqlFile -FilePath $migration.FullName -Database $schemaName -LogName ("migration-" + $migration.BaseName)
    }
    # Also execute the additive migrations twice to catch non-repeatable DDL or duplicate seeds.
    foreach ($migration in (Get-ChildItem -LiteralPath $migrationDirectory -Filter '*.sql' -File | Sort-Object Name)) {
        Invoke-TestSqlFile -FilePath $migration.FullName -Database $schemaName -LogName ("migration-repeat-" + $migration.BaseName)
    }
    Invoke-TestSqlFile -FilePath (Join-Path $workspaceRoot 'Backend\SportShop.Api.Tests\Fixtures\mobile-edge-cases.sql') `
        -Database $schemaName -LogName 'catalog-edge-cases'

    $noCodSql = @"
CREATE DATABASE $noCodSchema CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE TABLE $noCodSchema.PHUONG_THUC_THANH_TOAN (
 ma_phuong_thuc_thanh_toan INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
 ten_phuong_thuc_thanh_toan VARCHAR(50) NOT NULL UNIQUE,
 trang_thai VARCHAR(10) NOT NULL DEFAULT 'hoat_dong'
);
INSERT INTO $noCodSchema.PHUONG_THUC_THANH_TOAN(ten_phuong_thuc_thanh_toan,trang_thai)
VALUES ('Chuyển khoản','hoat_dong'),('Thanh toán khi nhận hàng (COD)','ngung');
"@
    $noCodFile = Join-Path $runDirectory 'no-cod-schema.sql'
    [IO.File]::WriteAllText($noCodFile, $noCodSql, [Text.UTF8Encoding]::new($false))
    Invoke-TestSqlFile -FilePath $noCodFile -LogName 'no-cod-schema'

    foreach ($variableName in $environmentNames) {
        $previousVariables[$variableName] = [Environment]::GetEnvironmentVariable($variableName, 'Process')
    }
    # The empty credential exists only in this fresh loopback-only test instance; never print connection strings.
    [Environment]::SetEnvironmentVariable('SPORTSHOP_TEST_MYSQL', "Server=127.0.0.1;Port=$Port;Database=$schemaName;User ID=root;Password=;SslMode=None;Allow User Variables=true", 'Process')
    [Environment]::SetEnvironmentVariable('SPORTSHOP_TEST_MYSQL_NO_COD', "Server=127.0.0.1;Port=$Port;Database=$noCodSchema;User ID=root;Password=;SslMode=None;Allow User Variables=true", 'Process')
    [Environment]::SetEnvironmentVariable('SPORTSHOP_EXPORT_OPENAPI', (Join-Path $workspaceRoot 'Backend\docs\openapi-v1.json'), 'Process')

    Invoke-TestDotnet -Arguments @('restore', $solution) -LogName 'restore'
    Invoke-TestDotnet -Arguments @('build', $solution, '--no-restore', '--configuration', $Configuration) -LogName 'build'
    Invoke-TestDotnet -Arguments @('test', $testProject, '--no-build', '--no-restore', '--configuration', $Configuration,
        '--logger', 'trx;LogFileName=mobile-api.trx', '--results-directory', $runDirectory) -LogName 'test'
    Write-Host "API verification completed. Test evidence: $(Join-Path $runDirectory 'mobile-api.trx')"
    Write-Host "OpenAPI exported to: $(Join-Path $workspaceRoot 'Backend\docs\openapi-v1.json')"
}
finally {
    foreach ($variableName in $environmentNames) {
        if ($previousVariables.ContainsKey($variableName)) {
            [Environment]::SetEnvironmentVariable($variableName, $previousVariables[$variableName], 'Process')
        }
    }
    if ($null -ne $mysqlServer) {
        $mysqlServer.Refresh()
        if (-not $mysqlServer.HasExited) {
            # InputObject is the process instance created above; no process enumeration or broad termination.
            Stop-Process -InputObject $mysqlServer -ErrorAction SilentlyContinue
            $mysqlServer.WaitForExit(10000) | Out-Null
        }
        $mysqlServer.Dispose()
    }
    Write-Host "Retained isolated database, build logs, and test evidence in: $runDirectory"
}
