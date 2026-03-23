#Requires -Version 5.1
<#
.SYNOPSIS
    AgriMitra -- Full project setup script (Windows / PowerShell 5+).

.DESCRIPTION
    Sets up the Python AI pipeline, builds the ONNX model, and builds the .NET MAUI
    Android app. Intelligently skips steps that are already complete.

.PARAMETER Resume
    Resume interrupted model training from last checkpoint.

.PARAMETER SyntheticOnly
    Skip Bhoonidhi satellite-data fetch; generate synthetic training records instead.

.PARAMETER SkipAI
    Skip all Python / AI steps (venv, PyTorch, model build). Useful when you only
    need to rebuild the mobile app.

.PARAMETER SkipMobile
    Skip MAUI workload install and Android build. Useful on a headless CI server.

.PARAMETER SkipData
    Skip training-data fetch/generation (assume data already exists under ai/data/).

.PARAMETER Force
    Force reinstall of PyTorch and rebuild of the ONNX model even if they exist.

.PARAMETER Ci
    Non-interactive mode -- never prompt; use defaults for all env vars.

.EXAMPLE
    .\install.ps1                        # Full setup (smart skip if already done)
    .\install.ps1 -SyntheticOnly         # Skip Bhoonidhi, use synthetic data
    .\install.ps1 -SkipAI                # Mobile-only rebuild
    .\install.ps1 -Force                 # Force retrain model
    .\install.ps1 -Ci -SyntheticOnly     # Fully non-interactive CI run
#>

param(
    [switch]$Resume,
    [switch]$SyntheticOnly,
    [switch]$SkipAI,
    [switch]$SkipMobile,
    [switch]$SkipData,
    [switch]$Force,
    [switch]$Ci
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$ROOT  = $PSScriptRoot
$STEPS_SKIPPED = 0
$STEPS_OK      = 0
$WARNINGS      = @()

# --------------------------------------------------------------------------- #
# Helpers
# --------------------------------------------------------------------------- #
function Write-Step  { param($n, $msg) Write-Host "`n--- Step ${n}: $msg" -ForegroundColor Cyan }
function Write-Ok    { param($msg) Write-Host "  [+] $msg" -ForegroundColor Green;  $script:STEPS_OK++ }
function Write-Skip  { param($msg) Write-Host "  --> $msg (skipped)" -ForegroundColor DarkGray; $script:STEPS_SKIPPED++ }
function Write-Warn  { param($msg) Write-Host "  [!] $msg" -ForegroundColor Yellow; $script:WARNINGS += $msg }
function Write-Fail  { param($msg) Write-Host "`n  [x] $msg`n" -ForegroundColor Red; exit 1 }
function Write-Info  { param($msg) Write-Host "  .   $msg" -ForegroundColor Gray }

function Invoke-Safely {
    param([scriptblock]$Block, [string]$OnFail = "")
    try   { & $Block; return $true  }
    catch { if ($OnFail) { Write-Warn $OnFail }; return $false }
}

function Read-EnvFile {
    param([string]$Path)
    $map = @{}
    if (Test-Path $Path) {
        Get-Content $Path | ForEach-Object {
            if ($_ -match '^\s*([A-Za-z_][A-Za-z0-9_]*)\s*=\s*(.*)$') {
                $map[$Matches[1]] = $Matches[2].Trim()
            }
        }
    }
    return $map
}

function Set-EnvKey {
    # Adds key=value to .env only if the key is not already present (non-empty).
    param([string]$Path, [string]$Key, [string]$Value, [string]$Comment = "")
    $existing = Read-EnvFile $Path
    if ($existing.ContainsKey($Key) -and $existing[$Key] -ne "") { return $false }
    if ($Comment) { Add-Content $Path "# $Comment" }
    Add-Content $Path "$Key=$Value"
    return $true
}

function Prompt-EnvKey {
    param([string]$Path, [string]$Key, [string]$Prompt, [string]$Default = "", [bool]$Secret = $false)
    $existing = Read-EnvFile $Path
    if ($existing.ContainsKey($Key) -and $existing[$Key] -ne "") {
        Write-Ok "$Key already set in .env"
        return
    }
    if ($Ci -or $Default -eq "" -and $Ci) {
        $val = $Default
    } else {
        if ($Secret) {
            $val = (Read-Host "$Prompt (leave blank to skip)").Trim()
        } else {
            $val = (Read-Host "$Prompt [default: $Default]").Trim()
            if ($val -eq "") { $val = $Default }
        }
    }
    if ($val -ne "") {
        Add-Content $Path "$Key=$val"
        Write-Ok "$Key saved to .env"
    } else {
        Write-Warn "$Key not set -- some features may be unavailable"
    }
}

# --------------------------------------------------------------------------- #
# Banner
# --------------------------------------------------------------------------- #
Write-Host @"

+==============================================================+
|       AgriMitra -- AI Crop Yield Prediction Setup            |
|       Windows installer  *  PowerShell 5+                    |
+==============================================================+

  Flags: SkipAI=$SkipAI  SkipMobile=$SkipMobile  SyntheticOnly=$SyntheticOnly  Force=$Force

"@ -ForegroundColor Green

# --------------------------------------------------------------------------- #
# Step 0 -- Network check
# --------------------------------------------------------------------------- #
Write-Step 0 "Network connectivity"
$NET_OK = $false
try {
    $null = Invoke-WebRequest -Uri "https://1ved.cloud" -UseBasicParsing -TimeoutSec 8 -ErrorAction Stop
    $NET_OK = $true
    Write-Ok "Internet reachable (1ved.cloud OK)"
} catch {
    Write-Warn "Cannot reach 1ved.cloud -- offline steps will be skipped automatically"
}

# --------------------------------------------------------------------------- #
# Step 1 -- Python 3.11+
# --------------------------------------------------------------------------- #
if (-not $SkipAI) {
    Write-Step 1 "Python 3.11+"
    $pyCmd = $null
    foreach ($cmd in @("python", "py", "python3", "python3.12", "python3.11")) {
        try {
            $ver = (& $cmd --version 2>&1).ToString()
            if ($ver -match "Python 3\.(\d+)") {
                if ([int]$Matches[1] -ge 11) { $pyCmd = $cmd; break }
            }
        } catch {}
    }

    if (-not $pyCmd) {
        Write-Host ""
        Write-Host "  Python 3.11+ not found. Install one of these ways:" -ForegroundColor Yellow
        Write-Host "    winget install Python.Python.3.12" -ForegroundColor White
        Write-Host "    https://www.python.org/downloads/" -ForegroundColor White
        Write-Host ""
        # Try winget auto-install in non-CI mode
        if (-not $Ci) {
            $ans = (Read-Host "  Install Python 3.12 via winget now? [y/N]").Trim()
            if ($ans -match "^[Yy]") {
                Write-Info "Running: winget install Python.Python.3.12 --silent"
                winget install Python.Python.3.12 --silent --accept-package-agreements --accept-source-agreements
                # Refresh PATH
                $env:PATH = [System.Environment]::GetEnvironmentVariable("PATH", "Machine") + ";" + [System.Environment]::GetEnvironmentVariable("PATH", "User")
                $pyCmd = "python"
            }
        }
        if (-not $pyCmd) { Write-Fail "Python 3.11+ is required. Install it then re-run this script." }
    }
    Write-Ok "Python: $pyCmd  ($( & $pyCmd --version 2>&1 ))"
}

# --------------------------------------------------------------------------- #
# Step 2 -- .NET 10 SDK
# --------------------------------------------------------------------------- #
if (-not $SkipMobile) {
    Write-Step 2 ".NET 10 SDK"
    $dotnetOk = $false
    try {
        $dotnetVer = (& dotnet --version 2>&1).ToString().Trim()
        if ($dotnetVer -match "^10\.") {
            Write-Ok ".NET SDK $dotnetVer"
            $dotnetOk = $true
        } else {
            Write-Warn ".NET $dotnetVer found but .NET 10 required"
        }
    } catch { Write-Warn ".NET not found in PATH" }

    if (-not $dotnetOk) {
        Write-Host ""
        Write-Host "  Install .NET 10 SDK:" -ForegroundColor Yellow
        Write-Host "    winget install Microsoft.DotNet.SDK.10" -ForegroundColor White
        Write-Host "    https://dotnet.microsoft.com/download/dotnet/10.0" -ForegroundColor White
        if (-not $Ci) {
            $ans = (Read-Host "  Install .NET 10 SDK via winget now? [y/N]").Trim()
            if ($ans -match "^[Yy]") {
                winget install Microsoft.DotNet.SDK.10 --silent --accept-package-agreements --accept-source-agreements
                $env:PATH = [System.Environment]::GetEnvironmentVariable("PATH", "Machine") + ";" + [System.Environment]::GetEnvironmentVariable("PATH", "User")
            }
        }
        # Re-check
        try {
            $dotnetVer = (& dotnet --version 2>&1).ToString().Trim()
            if ($dotnetVer -notmatch "^10\.") { Write-Fail ".NET 10 SDK required. Found: $dotnetVer" }
            Write-Ok ".NET SDK $dotnetVer"
        } catch { Write-Fail ".NET 10 SDK not found. Install from https://dotnet.microsoft.com/download" }
    }
}

# --------------------------------------------------------------------------- #
# Step 3 -- MAUI Android workload
# --------------------------------------------------------------------------- #
if (-not $SkipMobile) {
    Write-Step 3 "MAUI Android workload"
    $workloadList = (& dotnet workload list 2>&1) -join " "
    if ($workloadList -match "maui-android") {
        Write-Skip "maui-android workload already installed"
    } else {
        Write-Info "Installing maui-android workload (this may take several minutes)..."
        $ok = Invoke-Safely { & dotnet workload install maui-android --skip-sign-check 2>&1 | Write-Host } `
                             "Workload install had issues -- trying to continue"
        if ($ok) { Write-Ok "MAUI Android workload installed" }
    }
}

# --------------------------------------------------------------------------- #
# Step 4 -- Node.js (optional, for nextjs-api)
# --------------------------------------------------------------------------- #
Write-Step 4 "Node.js (optional -- needed for nextjs-api development)"
try {
    $nodeVer = (& node --version 2>&1).ToString().Trim()
    $npmVer  = (& npm  --version 2>&1).ToString().Trim()
    Write-Ok "Node $nodeVer / npm $npmVer"
} catch {
    Write-Warn "Node.js not found -- nextjs-api won't run locally (not needed for mobile-only dev)"
    Write-Info "Install: winget install OpenJS.NodeJS.LTS  or  https://nodejs.org"
}

# --------------------------------------------------------------------------- #
# Step 5 -- Android SDK / ADB (optional)
# --------------------------------------------------------------------------- #
if (-not $SkipMobile) {
    Write-Step 5 "Android SDK / ADB (optional -- needed to sideload APK to device)"
    $adbPaths = @(
        "$env:LOCALAPPDATA\Android\Sdk\platform-tools\adb.exe",
        "$env:ANDROID_HOME\platform-tools\adb.exe",
        (Get-Command adb -ErrorAction SilentlyContinue)?.Source
    ) | Where-Object { $_ -and (Test-Path $_) }

    if ($adbPaths) {
        $adb = $adbPaths[0]
        $adbVer = (& $adb version 2>&1 | Select-Object -First 1).ToString().Trim()
        Write-Ok "ADB found: $adbVer"
        Write-Info "  Path: $adb"
    } else {
        Write-Warn "ADB not found -- install Android Studio or SDK platform-tools to deploy to device"
        Write-Info "  Install Android Studio: winget install Google.AndroidStudio"
    }
}

if (-not $SkipAI) {

# --------------------------------------------------------------------------- #
# Step 6 -- Python venv
# --------------------------------------------------------------------------- #
Write-Step 6 "Python virtual environment (ai\.venv)"
$venvPath = Join-Path $ROOT "ai\.venv"
if (Test-Path $venvPath) {
    Write-Skip "venv already exists at ai\.venv"
} else {
    & $pyCmd -m venv $venvPath
    Write-Ok "venv created"
}

$pip    = Join-Path $venvPath "Scripts\pip.exe"
$python = Join-Path $venvPath "Scripts\python.exe"

# --------------------------------------------------------------------------- #
# Step 7 -- pip upgrade
# --------------------------------------------------------------------------- #
Write-Step 7 "pip / setuptools upgrade"
& $python -m pip install --upgrade pip setuptools wheel --quiet
Write-Ok "pip / setuptools up to date"

# --------------------------------------------------------------------------- #
# Step 8 -- PyTorch 2.2 CPU
# --------------------------------------------------------------------------- #
Write-Step 8 "PyTorch 2.2.0 (CPU)"
$torchOk = $false
if (-not $Force) {
    $torchOk = (Invoke-Safely {
        $v = (& $python -c "import torch; print(torch.__version__)" 2>&1).ToString().Trim()
        if ($v -match "^2\.") { $script:torchOk = $true }
    })
    if ($torchOk) { Write-Skip "PyTorch already installed ($( & $python -c 'import torch; print(torch.__version__)' 2>&1 ))" }
}
if (-not $torchOk) {
    Write-Info "Downloading PyTorch 2.2.0 CPU wheels (~500 MB)..."
    & $pip install torch==2.2.0 torchvision==0.17.0 torchaudio==2.2.0 `
        --index-url https://download.pytorch.org/whl/cpu --quiet
    Write-Ok "PyTorch 2.2.0 installed"
}

# --------------------------------------------------------------------------- #
# Step 9 -- torch-geometric + other deps
# --------------------------------------------------------------------------- #
Write-Step 9 "Python dependencies (ai\requirements.txt)"
$reqFile = Join-Path $ROOT "ai\requirements.txt"

# torch-geometric wheels need a special index
Write-Info "Installing torch-geometric from PyG wheel server..."
Invoke-Safely {
    & $pip install torch_scatter torch_sparse torch_cluster torch_spline_conv `
        -f https://data.pyg.org/whl/torch-2.2.0+cpu.html --quiet
} "torch-geometric wheels failed -- falling back to source build (may be slow)"

Write-Info "Installing remaining requirements..."
& $pip install -r $reqFile --quiet
Write-Ok "All Python dependencies installed"

# --------------------------------------------------------------------------- #
# Step 10 -- Configure ai\.env
# --------------------------------------------------------------------------- #
Write-Step 10 "Environment configuration (ai\.env)"
$envFile = Join-Path $ROOT "ai\.env"

# Ensure file exists
if (-not (Test-Path $envFile)) {
    New-Item -ItemType File -Path $envFile -Force | Out-Null
    Write-Info "Created ai\.env"
}

# Always-present keys (use defaults, never prompt)
if (Set-EnvKey $envFile "AGRIMITRA_PROXY" "https://1ved.cloud" `
        "Satellite + inference proxy -- all Bhoonidhi calls route through here") {
    Write-Ok "AGRIMITRA_PROXY set to https://1ved.cloud"
} else {
    Write-Skip "AGRIMITRA_PROXY already in .env"
}

# Optional keys (prompt unless -Ci)
if (-not $Ci) {
    Write-Host ""
    Write-Host "  Optional -- press Enter to skip any key" -ForegroundColor DarkGray
    Prompt-EnvKey $envFile "DATA_GOV_KEY" `
        "  data.gov.in API key (for AGMARKNET price data)" `
        "" $false
} else {
    $null = Set-EnvKey $envFile "DATA_GOV_KEY" "" "data.gov.in API key (optional)"
}

Write-Ok "ai\.env configured"

# --------------------------------------------------------------------------- #
# Step 11 -- Training data
# --------------------------------------------------------------------------- #
Write-Step 11 "Training data"
$dataDir  = Join-Path $ROOT "ai\data"
$csvCount = 0
if (Test-Path $dataDir) {
    $csvCount = (Get-ChildItem $dataDir -Recurse -Include "*.csv","*.parquet" -ErrorAction SilentlyContinue).Count
}

if ($csvCount -gt 0 -and -not $Force -and -not $SkipData) {
    Write-Skip "$csvCount training file(s) already present in ai\data\"
} elseif ($SkipData) {
    Write-Skip "-SkipData flag set"
} else {
    $fetchScript = Join-Path $ROOT "ai\scripts\fetch_bhoonidhi_data.py"
    if ($SyntheticOnly -or -not $NET_OK) {
        if (-not $NET_OK) { Write-Warn "No internet -- using synthetic data" }
        else               { Write-Info "SyntheticOnly flag -- generating synthetic dataset" }
        & $python (Join-Path $ROOT "ai\scripts\generate_demo_data.py") --records 200
    } else {
        Write-Info "Fetching real satellite data for Kankavli, Sindhudurg (bbox 73.6,16.4-73.9,16.7)..."
        & $python $fetchScript --bbox 73.6,16.4,73.9,16.7 --n-records 50 --env-file $envFile
        if ($LASTEXITCODE -ne 0) {
            Write-Warn "Bhoonidhi fetch failed -- falling back to synthetic data"
            & $python (Join-Path $ROOT "ai\scripts\generate_demo_data.py") --records 200
        }
    }
    Write-Ok "Training data ready"
}

# --------------------------------------------------------------------------- #
# Step 12 -- Build ONNX model
# --------------------------------------------------------------------------- #
Write-Step 12 "ONNX model (FP32 -> INT8)"
$onnxSrc = Join-Path $ROOT "ai\outputs\agrimitra_int8.onnx"

if ((Test-Path $onnxSrc) -and -not $Force) {
    $age = [int]((Get-Date) - (Get-Item $onnxSrc).LastWriteTime).TotalDays
    Write-Skip "agrimitra_int8.onnx exists ($age day(s) old) -- use -Force to retrain"
} else {
    $buildScript = Join-Path $ROOT "ai\scripts\build_demo_model.py"
    $buildArgs   = @("--demo-epochs", "1")
    if ($Resume) { $buildArgs += "--resume" }
    & $python $buildScript @buildArgs
    if ($LASTEXITCODE -ne 0) { Write-Fail "Model build failed. Check the Python output above." }
    Write-Ok "ONNX model built -> ai\outputs\agrimitra_int8.onnx"
}

# --------------------------------------------------------------------------- #
# Step 13 -- Copy ONNX to mobile + deploy
# --------------------------------------------------------------------------- #
Write-Step 13 "Copy model -> mobile app + deploy service"
if (Test-Path $onnxSrc) {
    $mobileDst = Join-Path $ROOT "mobile\AgriMitraMobile\Resources\Raw"
    $deployDst = Join-Path $ROOT "deploy"
    New-Item -ItemType Directory -Force -Path $mobileDst | Out-Null
    New-Item -ItemType Directory -Force -Path $deployDst  | Out-Null
    Copy-Item $onnxSrc (Join-Path $mobileDst "agrimitra_int8.onnx") -Force
    Copy-Item $onnxSrc (Join-Path $deployDst  "agrimitra_int8.onnx") -Force
    Write-Ok "Copied to mobile\Resources\Raw\ and deploy\"
} else {
    Write-Warn "agrimitra_int8.onnx not found -- mobile inference will use bundled demo model"
}

} # end -not $SkipAI

# --------------------------------------------------------------------------- #
# Step 14 -- MAUI restore + build
# --------------------------------------------------------------------------- #
if (-not $SkipMobile) {
    Write-Step 14 "MAUI Android build (net10.0-android)"
    $mauiProj = Join-Path $ROOT "mobile\AgriMitraMobile\AgriMitraMobile.csproj"
    $apkPath  = Join-Path $ROOT "mobile\AgriMitraMobile\bin\Release\net10.0-android\com.agrimitra.mobile-Signed.apk"

    if ((Test-Path $apkPath) -and -not $Force) {
        $age = [int]((Get-Date) - (Get-Item $apkPath).LastWriteTime).TotalMinutes
        Write-Skip "Signed APK exists ($age min old) -- use -Force to rebuild"
        Write-Info "  APK: $apkPath"
    } else {
        Write-Info "Restoring NuGet packages..."
        & dotnet restore $mauiProj --verbosity quiet
        if ($LASTEXITCODE -ne 0) { Write-Fail "dotnet restore failed" }

        Write-Info "Building APK (Release)..."
        & dotnet build $mauiProj -f net10.0-android -c Release `
            -t:SignAndroidPackage --verbosity quiet --no-restore
        if ($LASTEXITCODE -ne 0) { Write-Fail "dotnet build failed -- check output above" }

        if (Test-Path $apkPath) {
            $sz = [math]::Round((Get-Item $apkPath).Length / 1MB, 1)
            Write-Ok "APK built ($sz MB) -> mobile\AgriMitraMobile\bin\Release\net10.0-android\"
        } else {
            Write-Warn "Build completed but APK not found at expected path"
        }
    }
}

# --------------------------------------------------------------------------- #
# Summary
# --------------------------------------------------------------------------- #
Write-Host ""
Write-Host "-------------------------------------------------------------" -ForegroundColor DarkGray
Write-Host "  Steps OK: $STEPS_OK   Skipped: $STEPS_SKIPPED   Warnings: $($WARNINGS.Count)" -ForegroundColor White
if ($WARNINGS.Count -gt 0) {
    Write-Host "  Warnings:" -ForegroundColor Yellow
    $WARNINGS | ForEach-Object { Write-Host "    [!] $_" -ForegroundColor Yellow }
}

Write-Host @"

+====================================================================+
|                        SETUP COMPLETE                              |
+====================================================================+

LOCAL DEVELOPMENT
-----------------

1. Start inference service (runs the ONNX model):
   cd deploy
   pip install fastapi uvicorn onnxruntime
   uvicorn inference_service:app --host 0.0.0.0 --port 8001
   -> API docs: http://localhost:8001/docs

2. Start model training UI:
   cd ai && .venv\Scripts\activate
   uvicorn training_ui.app:app --reload --port 8000
   -> Dashboard: http://localhost:8000

3. Run Next.js API locally:
   cd nextjs-api && npm install && npm run dev
   -> http://localhost:3000

4. Build + deploy APK to a connected device/emulator:
   dotnet build mobile\AgriMitraMobile\AgriMitraMobile.csproj `
     -f net10.0-android -c Debug -t:SignAndroidPackage
   `$env:LOCALAPPDATA\Android\Sdk\platform-tools\adb install -r `
     mobile\AgriMitraMobile\bin\Debug\net10.0-android\com.agrimitra.mobile-Signed.apk

PRODUCTION DEPLOYMENT
---------------------

Inference service -> 1ved.cloud:
   scp -r deploy/ user@1ved.cloud:~/agrimitra-infer/
   ssh user@1ved.cloud "cd agrimitra-infer && docker compose up -d --build"

Next.js API routes -> Vercel:
   cd nextjs-api && vercel deploy
   Set env vars in Vercel dashboard:
     INFER_URL        = https://1ved.cloud/infer
     DATABASE_URL     = (your Neon / Vercel Postgres URL)
     DATA_GOV_KEY     = (your data.gov.in key)
     BHOONIDHI_USER   = (NRSC credentials)
     BHOONIDHI_PASS   = (NRSC credentials)

"@ -ForegroundColor Green
