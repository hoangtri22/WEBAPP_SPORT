param(
    [string]$DeviceSerial,
    [string]$AndroidSdkDirectory,
    [string]$JavaSdkDirectory,
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $PSScriptRoot 'SportShop.Mobile/SportShop.Mobile.csproj'

# Tìm SDK hiện có; không đổi JAVA_HOME hoặc cấu hình hệ thống.
if (-not $AndroidSdkDirectory) {
    $AndroidSdkDirectory = @(
        $env:ANDROID_HOME, $env:ANDROID_SDK_ROOT,
        "$env:LOCALAPPDATA/Android/Sdk", 'C:/Program Files (x86)/Android/android-sdk'
    ) | Where-Object { $_ -and (Test-Path -LiteralPath (Join-Path $_ 'platform-tools/adb.exe')) } | Select-Object -First 1
}
if (-not $AndroidSdkDirectory) { throw 'Chưa tìm thấy Android SDK. Truyền -AndroidSdkDirectory.' }
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
if (-not $JavaSdkDirectory) { throw 'Cần JDK 21 cho Android workload này. Truyền -JavaSdkDirectory.' }

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
        throw 'Cần đúng một thiết bị (điện thoại hoặc máy ảo) đang chạy, hoặc truyền -DeviceSerial. Kiểm tra bằng adb devices.'
    }
}
if ($DeviceSerial -notin $allDevices) { throw 'Thiết bị chưa sẵn sàng. Mở khóa và cho phép gỡ lỗi.' }
$abi = (& $adb -s $DeviceSerial shell getprop ro.product.cpu.abi).Trim()
$runtimeId = switch ($abi) {
    'arm64-v8a' { 'android-arm64' }
    'armeabi-v7a' { 'android-arm' }
    'x86_64' { 'android-x64' }
    default { throw ('CPU Android chưa hỗ trợ: ' + $abi) }
}

if (-not $SkipBuild) {
    & dotnet build $projectPath -c Debug -r $runtimeId "-p:AndroidSdkDirectory=$AndroidSdkDirectory" "-p:JavaSdkDirectory=$JavaSdkDirectory"
    if ($LASTEXITCODE -ne 0) { throw 'Build APK chưa thành công.' }
}

# Chạy API local nếu chưa có; log và PID chỉ lưu trong thư mục Git bỏ qua.
try { $live = Invoke-RestMethod 'http://localhost:5080/health/live' -TimeoutSec 3 }
catch { $live = $null }
if ($null -eq $live) {
    $artifacts = Join-Path $repositoryRoot '.test-artifacts'
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
    if ($ready.status -ne 'ready') { throw 'API chưa sẵn sàng.' }
    $catalog = Invoke-RestMethod 'http://localhost:5080/api/v1/products?pageSize=1' -TimeoutSec 10
    $image = $catalog.items | Where-Object imageUrl | Select-Object -First 1
    if ($image) {
        $imageResponse = Invoke-WebRequest -UseBasicParsing -Uri $image.imageUrl -Method Head -TimeoutSec 10
        if ($imageResponse.StatusCode -ne 200) { throw 'Không tải được ảnh sản phẩm.' }
    }
}
catch { throw 'API/DB/ảnh chưa sẵn sàng. Xem Backend/SportShop.Api/README.md và kiểm tra appsettings.Local.json, migration, Media:PublicBaseUrl.' }

# Reverse localhost cho cả API JSON lẫn URL ảnh từ backend; không cần mở cổng LAN.
& $adb -s $DeviceSerial reverse tcp:5080 tcp:5080
if ($LASTEXITCODE -ne 0) { throw 'Không tạo được kết nối USB tới API.' }
$apk = Join-Path $PSScriptRoot "SportShop.Mobile/bin/Debug/net10.0-android/$runtimeId/vn.sportshop.mobile-Signed.apk"
if (-not (Test-Path -LiteralPath $apk)) { throw 'Không tìm thấy APK. Chạy lại không có -SkipBuild.' }
& $adb -s $DeviceSerial install --no-incremental -r $apk
if ($LASTEXITCODE -ne 0) { throw 'Cài APK chưa thành công. Kiểm tra màn hình xác nhận trên điện thoại.' }
$component = (& $adb -s $DeviceSerial shell cmd package resolve-activity --brief vn.sportshop.mobile |
    Where-Object { $_ -match '^vn\.sportshop\.mobile/' } | Select-Object -Last 1)
if (-not $component) { throw 'Chưa xác định được màn hình khởi động của SportShop.' }
$launchResult = & $adb -s $DeviceSerial shell am start -W -n $component.Trim()
$launchResult | Write-Output
if ($LASTEXITCODE -ne 0 -or ($launchResult -join ' ') -match 'Error:|Exception') { throw 'Không mở được SportShop.' }
Write-Host "Đã mở SportShop trên $DeviceSerial. API có $($catalog.totalItems) sản phẩm. Giữ cáp USB để tải dữ liệu và ảnh."
