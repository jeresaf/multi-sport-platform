param(
    [string]$DeviceId = 'emulator-5554'
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$mobileRoot = Join-Path $projectRoot 'apps\mobile'
$androidRoot = Join-Path $mobileRoot 'android'
$testFile = Join-Path $mobileRoot 'integration_test\mobile_ui_offline_restart_test.dart'
$packageName = 'com.sportsplatform.sports_platform_mobile'
$testPackageName = "$packageName.test"

$adbCommand = Get-Command adb -ErrorAction SilentlyContinue
if ($adbCommand) {
    $adb = $adbCommand.Source
}
else {
    $sdkRoot = if ($env:ANDROID_HOME) {
        $env:ANDROID_HOME
    }
    elseif (Test-Path -LiteralPath 'C:\androidsdk') {
        'C:\androidsdk'
    }
    else {
        Join-Path $env:LOCALAPPDATA 'Android\Sdk'
    }
    $adb = Join-Path $sdkRoot 'platform-tools\adb.exe'
}
if (-not (Test-Path -LiteralPath $adb)) {
    throw 'adb was not found on PATH or in the default Android SDK location.'
}

$connected = & $adb devices
if (-not ($connected | Select-String -SimpleMatch "$DeviceId`tdevice")) {
    throw "Android device '$DeviceId' is not connected and ready."
}

$apiDefine = [Convert]::ToBase64String(
    [Text.Encoding]::UTF8.GetBytes('API_BASE_URL=http://10.0.2.2:8080')
)

# Start from a clean install once. Both test phases then reuse these exact APKs.
& $adb -s $DeviceId uninstall $testPackageName 2>$null | Out-Null
& $adb -s $DeviceId uninstall $packageName 2>$null | Out-Null

Push-Location -LiteralPath $androidRoot
try {
    Write-Host 'Building the application and instrumentation APKs...'
    & .\gradlew.bat app:assembleDebug app:assembleDebugAndroidTest --console=plain `
        "-Ptarget=$testFile" `
        "-Pdart-defines=$apiDefine"
    if ($LASTEXITCODE -ne 0) {
        throw "Android test build failed with exit code $LASTEXITCODE."
    }

    $appApk = Join-Path $projectRoot 'apps\mobile\build\app\outputs\apk\debug\app-debug.apk'
    $testApk = Join-Path $projectRoot 'apps\mobile\build\app\outputs\apk\androidTest\debug\app-debug-androidTest.apk'
    foreach ($apk in @($appApk, $testApk)) {
        if (-not (Test-Path -LiteralPath $apk)) {
            throw "Expected APK was not produced: $apk"
        }
        & $adb -s $DeviceId install -r $apk
        if ($LASTEXITCODE -ne 0) {
            throw "Could not install APK '$apk' (exit code $LASTEXITCODE)."
        }
    }

    Write-Host 'Phase 1/2: queueing a fresh contest through the visible mobile UI...'
    $offlineInstrumentation = & $adb -s $DeviceId shell am instrument -w -r `
        "$testPackageName/androidx.test.runner.AndroidJUnitRunner" 2>&1
    $offlineInstrumentation | Write-Host
    if ($LASTEXITCODE -ne 0 -or
        ($offlineInstrumentation | Select-String -Pattern 'FAILURES!!!|INSTRUMENTATION_FAILED|shortMsg=')) {
        throw "Offline UI phase failed with exit code $LASTEXITCODE."
    }

    Write-Host 'Force-stopping the Android application process...'
    & $adb -s $DeviceId shell am force-stop $packageName
    if ($LASTEXITCODE -ne 0) {
        throw "adb force-stop failed with exit code $LASTEXITCODE."
    }

    Write-Host 'Phase 2/2: relaunching with retained app data and reconnecting...'
    $instrumentation = & $adb -s $DeviceId shell am instrument -w -r `
        "$testPackageName/androidx.test.runner.AndroidJUnitRunner" 2>&1
    $instrumentation | Write-Host
    if ($LASTEXITCODE -ne 0 -or
        ($instrumentation | Select-String -Pattern 'FAILURES!!!|INSTRUMENTATION_FAILED|shortMsg=')) {
        throw "Reconnect UI phase failed with exit code $LASTEXITCODE."
    }
}
finally {
    Pop-Location
}

Write-Host 'Mobile offline process-restart journey passed.'
