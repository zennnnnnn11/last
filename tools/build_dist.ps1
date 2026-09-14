<#
.SYNOPSIS
    last 一键自动化打包脚本：构建并生成绿色免安装 Zip、单文件自解压包与 Inno Setup 安装包
#>

$ErrorActionPreference = "Stop"

$workspaceRoot = (Resolve-Path "$PSScriptRoot\..").Path
Set-Location $workspaceRoot

Write-Host "=================================================" -ForegroundColor Cyan
Write-Host "  last - 正在构建 Native AOT 生产分发包" -ForegroundColor Cyan
Write-Host "=================================================" -ForegroundColor Cyan

# 1. 执行 Native AOT 发布编译
Write-Host "[1/4] 正在执行 dotnet publish (Release / win-x64)..." -ForegroundColor Yellow
dotnet publish src/last/last.csproj -c Release -r win-x64 --nologo -v q
if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet publish 编译失败！"
    exit 1
}

$publishDir = Join-Path $workspaceRoot "src\last\bin\Release\net10.0\win-x64\publish"
$distDir = Join-Path $workspaceRoot "dist"
if (!(Test-Path $distDir)) {
    New-Item -ItemType Directory -Path $distDir | Out-Null
}

$requiredFiles = @(
    (Join-Path $publishDir "last.exe"),
    (Join-Path $publishDir "av_libglesv2.dll"),
    (Join-Path $publishDir "libHarfBuzzSharp.dll"),
    (Join-Path $publishDir "libSkiaSharp.dll")
)

# 2. 生成标准绿色免安装便携版 (.zip) - 最推荐发好友
Write-Host "[2/4] 正在打包绿色免安装便携版 (.zip)..." -ForegroundColor Yellow
$zipPath = Join-Path $distDir "last-win-x64-portable.zip"
if (Test-Path $zipPath) { Remove-Item -Force $zipPath }

$has7z = Get-Command "7z" -ErrorAction SilentlyContinue
if ($has7z) {
    & "7z" a -tzip $zipPath $requiredFiles -mx=9 | Out-Null
} else {
    $tempStaging = Join-Path $distDir "staging_zip"
    if (Test-Path $tempStaging) { Remove-Item -Recurse -Force $tempStaging }
    New-Item -ItemType Directory -Path $tempStaging | Out-Null
    Copy-Item -Path $requiredFiles -Destination $tempStaging
    Compress-Archive -Path "$tempStaging\*" -DestinationPath $zipPath -CompressionLevel Optimal
    Remove-Item -Recurse -Force $tempStaging
}

$zipItem = Get-Item $zipPath
Write-Host "  [OK] 绿色免安装包已生成: $($zipItem.FullName)" -ForegroundColor Green
Write-Host "       文件体积: $([math]::Round($zipItem.Length / 1MB, 2)) MB (解压即用，目标机无需安装任何 .NET 环境)" -ForegroundColor Gray

# 3. 生成单文件自解压 EXE (7z SFX) - 体积最小 (~11.5MB)
Write-Host "[3/4] 正在生成单文件便携自解压程序 (.exe)..." -ForegroundColor Yellow
$sfxModule = "C:\Users\zen\scoop\apps\7zip\current\7z.sfx"
if (!(Test-Path $sfxModule)) {
    $sfxModule = "C:\Program Files\7-Zip\7z.sfx"
}

if (Test-Path $sfxModule) {
    $temp7z = Join-Path $distDir "temp_payload.7z"
    $sfxExe = Join-Path $distDir "last-win-x64-sfx.exe"
    if (Test-Path $temp7z) { Remove-Item -Force $temp7z }
    if (Test-Path $sfxExe) { Remove-Item -Force $sfxExe }

    & "7z" a -t7z $temp7z $requiredFiles -mx=9 | Out-Null
    $sfxBytes = [System.IO.File]::ReadAllBytes($sfxModule)
    $payloadBytes = [System.IO.File]::ReadAllBytes($temp7z)
    $combined = New-Object byte[] ($sfxBytes.Length + $payloadBytes.Length)
    [System.Buffer]::BlockCopy($sfxBytes, 0, $combined, 0, $sfxBytes.Length)
    [System.Buffer]::BlockCopy($payloadBytes, 0, $combined, $sfxBytes.Length, $payloadBytes.Length)
    [System.IO.File]::WriteAllBytes($sfxExe, $combined)
    Remove-Item -Force $temp7z

    $sfxItem = Get-Item $sfxExe
    Write-Host "  [OK] 单文件自解压包已生成: $($sfxItem.FullName)" -ForegroundColor Green
    Write-Host "       文件体积: $([math]::Round($sfxItem.Length / 1MB, 2)) MB (极速单文件，双击指定目录自动释放并运行)" -ForegroundColor Gray
} else {
    Write-Host "  [跳过] 未找到 7z.sfx 模块" -ForegroundColor Gray
}

# 4. 检查 Inno Setup 编译器，若已安装则编译标准 Setup.exe 向导安装包
Write-Host "[4/4] 检查 Inno Setup 编译器 (ISCC.exe)..." -ForegroundColor Yellow
$iscc = Get-Command "iscc" -ErrorAction SilentlyContinue
if (!$iscc) {
    $commonIsccPaths = @(
        "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
        "C:\Program Files\Inno Setup 6\ISCC.exe",
        "C:\Program Files (x86)\Inno Setup 7\ISCC.exe",
        "C:\Program Files\Inno Setup 7\ISCC.exe",
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
    )
    foreach ($path in $commonIsccPaths) {
        if (Test-Path $path) {
            $iscc = $path
            break
        }
    }
}

if ($iscc) {
    Write-Host "  发现 Inno Setup 编译器: $iscc" -ForegroundColor Gray
    $issScript = Join-Path $workspaceRoot "tools\installer.iss"
    & $iscc $issScript | Out-Null
    $setupFiles = Get-ChildItem -Path $distDir -Filter "*-setup-x64.exe" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($setupFiles) {
        Write-Host "  [OK] 标准向导安装包已生成: $($setupFiles.FullName) ($([math]::Round($setupFiles.Length / 1MB, 2)) MB)" -ForegroundColor Green
    }
} else {
    Write-Host "  提示: 未检测到 Inno Setup。如需生成带桌面快捷方式与卸载器的安装包 (Setup.exe)：" -ForegroundColor Cyan
    Write-Host "        可在终端执行安装: winget install JRSoftware.InnoSetup" -ForegroundColor Cyan
    Write-Host "        安装后再次运行此脚本，即可自动生成标准 Setup 安装程序。" -ForegroundColor Cyan
}

Write-Host "=================================================" -ForegroundColor Cyan
Write-Host "  分发包构建完成！输出目录: $distDir" -ForegroundColor Cyan
Write-Host "=================================================" -ForegroundColor Cyan
