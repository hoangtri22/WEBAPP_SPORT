param(
    [string]$DeviceSerial,
    [string]$AndroidSdkDirectory,
    [string]$JavaSdkDirectory,
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $PSScriptRoot 'SportShop.Mobile/SportShop.Mobile.csproj'

# Tim SDK hien co; khong doi JAVA_HOME hoac cau hinh he thong.
if (-not $AndroidSdkDirectory) {
    $AndroidSdkDirectory = @(
        $env:ANDROID_HOME, $env:ANDROID_SDK_ROOT,
        "$env:LOCALAPPDATA/Android/Sdk", 'C:/Program Files (x86)/Android/android-sdk'
    ) | Where-Object { $_ -and (Test-Path -LiteralPath (Join-Path $_ 'platform-tools/adb.exe')) } | Select-Object -First 1
}
if (-not $AndroidSdkDirectory) { throw 'Chua tim thay Android SDK. Truyen -AndroidSdkDirectory.' }
$adb = Join-Path $AndroidSdkDirectory 'platform-tools/adb.exe'

if (-not $JavaSdkDirectory) {
    $javaCandidates = @($env:JAVA_HOME)
    foreach ($javaRoot in @((Join-Path $repositoryRoot '.test-artifacts/tools/jdk21'), 'C:/Program Files/Microsoft', 'C:/Program Files/Android/openjdk')) {
        if (Test-Path -LiteralPath $javaRoot) {
            $javaCandidates += @(Get-ChildItem -LiteralPath $javaRoot -Directory -Filter '*21*' | ForEach-Object FullName)
        }
    }
    foreach ($javaCandidate in $javaCandidates) {
        if (-not $javaCandidate -or -not (Test-Path -LiteralPath (Join-Path $javaCandidate 'bin/java.exe'))) { continue }
        $releaseFile = Join-Path $javaCandidate 'release'
        if ((Test-Path -LiteralPath $releaseFile) -and ([System.IO.File]::ReadAllText($releaseFile) -match 'JAVA_VERSION="21\.')) {
            $JavaSdkDirectory = $javaCandidate; break
        }
    }
}
if (-not $JavaSdkDirectory) { throw 'Can JDK 21 cho Android workload nay. Truyen -JavaSdkDirectory.' }

$deviceLines = @(& $adb devices)
$allDevices = @($deviceLines | Where-Object { $_ -match '^\S+\s+device\s*$' } |
    ForEach-Object { ($_ -split '\s+')[0] })
$physicalDevices = @($allDevices | Where-Object { $_ -notmatch '^emulator-' })
if (-not $DeviceSerial) {
    if ($physicalDevices.Count -eq 1) {
        $DeviceSerial = $physicalDevices[0]
    } elseif ($allDevices.Count -eq 1) {
        $DeviceSerial = $allDevices[0]
    } else {
        throw 'Can dung mot thiet bi (dien thoai hoac may ao) dang chay, hoac truyen -DeviceSerial. Kiem tra bang adb devices.'
    }
}
if ($DeviceSerial -notin $allDevices) { throw 'Thiet bi chua san sang. Mo khoa va cho phep go loi.' }
$abi = (& $adb -s $DeviceSerial shell getprop ro.product.cpu.abi).Trim()
$runtimeId = switch ($abi) {
    'arm64-v8a' { 'android-arm64' }
    'armeabi-v7a' { 'android-arm' }
    'x86_64' { 'android-x64' }
    default { throw ('CPU Android chua ho tro: ' + $abi) }
}

if (-not $SkipBuild) {
    & dotnet build $projectPath -c Debug -r $runtimeId "-p:AndroidSdkDirectory=$AndroidSdkDirectory" "-p:JavaSdkDirectory=$JavaSdkDirectory"
    if ($LASTEXITCODE -ne 0) { throw 'Build APK chua thanh cong.' }
}

# Khoi dong lai API local neu ban build chua co code moi hoac chua chay; log va PID chi luu trong thu muc Git bo qua.
$artifacts = Join-Path $repositoryRoot '.test-artifacts'
$pidFile = Join-Path $artifacts 'mobile-api.pid'
$apiDll = Join-Path $repositoryRoot 'Backend/SportShop.Api/bin/Debug/net10.0/SportShop.Api.dll'
$newestApiSource = Get-ChildItem -Path (Join-Path $repositoryRoot 'Backend/SportShop.Api') -Filter '*.cs' -Recurse | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
$needApiRestart = (-not (Test-Path -LiteralPath $apiDll)) -or ($newestApiSource -and $newestApiSource.LastWriteTimeUtc -gt (Get-Item -LiteralPath $apiDll).LastWriteTimeUtc)
if ($needApiRestart) {
    if (Test-Path -LiteralPath $pidFile) {
        $oldPid = Get-Content -LiteralPath $pidFile -ErrorAction SilentlyContinue
        if ($oldPid) { Stop-Process -Id ([int]$oldPid) -Force -ErrorAction SilentlyContinue }
    }
    Get-Process -Name 'SportShop.Api' -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 500
}
try { $live = Invoke-RestMethod 'http://localhost:5080/health/live' -TimeoutSec 3 }
catch { $live = $null }
if ($null -eq $live) {
    [System.IO.Directory]::CreateDirectory($artifacts) | Out-Null
    $apiProject = Join-Path $repositoryRoot 'Backend/SportShop.Api/SportShop.Api.csproj'
    $apiProcess = Start-Process -FilePath 'dotnet' -ArgumentList @('run', '--project', ('"' + $apiProject + '"'), '--launch-profile', 'http') `
        -WorkingDirectory $repositoryRoot -WindowStyle Hidden -PassThru `
        -RedirectStandardOutput (Join-Path $artifacts 'mobile-api.stdout.log') `
        -RedirectStandardError (Join-Path $artifacts 'mobile-api.stderr.log')
    Set-Content -LiteralPath (Join-Path $artifacts 'mobile-api.pid') -Value $apiProcess.Id
    for ($attempt = 0; $attempt -lt 40; $attempt++) {
        try { $live = Invoke-RestMethod 'http://localhost:5080/health/live' -TimeoutSec 1; break }
        catch { Start-Sleep -Milliseconds 500 }
    }
}
try {
    $ready = Invoke-RestMethod 'http://localhost:5080/health/ready' -TimeoutSec 10
    if ($ready.status -ne 'ready') { throw 'API chua san sang.' }
    $catalog = Invoke-RestMethod 'http://localhost:5080/api/v1/products?pageSize=1' -TimeoutSec 10
    $image = $catalog.items | Where-Object imageUrl | Select-Object -First 1
    if ($image) {
        $imageResponse = Invoke-WebRequest -UseBasicParsing -Uri $image.imageUrl -Method Head -TimeoutSec 10
        if ($imageResponse.StatusCode -ne 200) { throw 'Khong tai duoc anh san pham.' }
    }
}
catch { throw 'API/DB/anh chua san sang. Xem Backend/SportShop.Api/README.md va kiem tra appsettings.Local.json, migration, Media:PublicBaseUrl.' }

# Reverse localhost cho ca API JSON lan URL anh tu backend; khong can mo cong LAN.
& $adb -s $DeviceSerial reverse tcp:5080 tcp:5080
if ($LASTEXITCODE -ne 0) { throw 'Khong tao duoc ket noi USB toi API.' }
$apk = Join-Path $PSScriptRoot "SportShop.Mobile/bin/Debug/net10.0-android/$runtimeId/vn.sportshop.mobile-Signed.apk"
if (-not (Test-Path -LiteralPath $apk)) { throw 'Khong tim thay APK. Chay lai khong co -SkipBuild.' }
& $adb -s $DeviceSerial install --no-incremental -r $apk
if ($LASTEXITCODE -ne 0) { throw 'Cai APK chua thanh cong. Kiem tra man hinh xac nhan tren dien thoai.' }
$component = (& $adb -s $DeviceSerial shell cmd package resolve-activity --brief vn.sportshop.mobile |
    Where-Object { $_ -match '^vn\.sportshop\.mobile/' } | Select-Object -Last 1)
if (-not $component) { throw 'Chua xac dinh duoc man hinh khoi dong cua SportShop.' }
$launchResult = & $adb -s $DeviceSerial shell am start -W -n $component.Trim()
$launchResult | Write-Output
if ($LASTEXITCODE -ne 0 -or ($launchResult -join ' ') -match 'Error:|Exception') { throw 'Khong mo duoc SportShop.' }
Write-Host "Da mo SportShop tren $DeviceSerial. API co $($catalog.totalItems) san pham. Giu cap USB de tai du lieu va anh."
