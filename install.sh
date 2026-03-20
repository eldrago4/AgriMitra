#!/usr/bin/env bash
# AgriMitra — Full project setup script (Linux / macOS)
#
# Usage:
#   ./install.sh                     # Full setup (smart-skips completed steps)
#   ./install.sh --synthetic-only    # Skip Bhoonidhi fetch; use synthetic data
#   ./install.sh --skip-ai           # Skip Python/AI steps; rebuild mobile only
#   ./install.sh --skip-mobile       # Skip MAUI build; AI/model steps only
#   ./install.sh --skip-data         # Assume training data already present
#   ./install.sh --force             # Force retrain even if model exists
#   ./install.sh --resume            # Resume interrupted training from checkpoint
#   ./install.sh --ci                # Non-interactive; never prompt (use defaults)

ROOT="$(cd "$(dirname "$0")" && pwd)"

RESUME=0 SYNTHETIC_ONLY=0 SKIP_AI=0 SKIP_MOBILE=0 SKIP_DATA=0 FORCE=0 CI=0
STEPS_OK=0 STEPS_SKIPPED=0
WARNINGS=()

for arg in "$@"; do
    case $arg in
        --resume)         RESUME=1 ;;
        --synthetic-only) SYNTHETIC_ONLY=1 ;;
        --skip-ai)        SKIP_AI=1 ;;
        --skip-mobile)    SKIP_MOBILE=1 ;;
        --skip-data)      SKIP_DATA=1 ;;
        --force)          FORCE=1 ;;
        --ci)             CI=1 ;;
    esac
done

RED='\033[0;31m'; GREEN='\033[0;32m'; YELLOW='\033[1;33m'
CYAN='\033[0;36m'; GRAY='\033[0;37m'; NC='\033[0m'

step()  { echo -e "\n${CYAN}─── Step $1: $2${NC}"; }
ok()    { echo -e "  ${GREEN}✓ $1${NC}"; STEPS_OK=$((STEPS_OK+1)); }
skip()  { echo -e "  ${GRAY}→ $1 (skipped)${NC}"; STEPS_SKIPPED=$((STEPS_SKIPPED+1)); }
warn()  { echo -e "  ${YELLOW}⚠ $1${NC}"; WARNINGS+=("$1"); }
fail()  { echo -e "\n  ${RED}✗ $1${NC}\n"; exit 1; }
info()  { echo -e "  ${GRAY}· $1${NC}"; }

# Reads a value from a key=value .env file (returns empty string if not found)
env_get() {
    local file="$1" key="$2"
    [[ -f "$file" ]] && grep -E "^${key}=" "$file" | tail -1 | cut -d= -f2- | xargs || echo ""
}

# Adds key=value only if the key is absent or empty in the file
env_set_default() {
    local file="$1" key="$2" value="$3" comment="${4:-}"
    local existing
    existing=$(env_get "$file" "$key")
    if [[ -n "$existing" ]]; then return 1; fi
    touch "$file"
    [[ -n "$comment" ]] && echo "# $comment" >> "$file"
    echo "${key}=${value}" >> "$file"
    return 0
}

# Prompts for a key and appends to .env (skips if already set, or if --ci)
env_prompt() {
    local file="$1" key="$2" prompt="$3" default="${4:-}" secret="${5:-0}"
    local existing
    existing=$(env_get "$file" "$key")
    if [[ -n "$existing" ]]; then
        ok "$key already set in .env"
        return
    fi
    if [[ $CI -eq 1 ]]; then
        [[ -n "$default" ]] && echo "${key}=${default}" >> "$file"
        return
    fi
    local val
    if [[ $secret -eq 1 ]]; then
        read -r -s -p "  $prompt (leave blank to skip): " val; echo
    else
        read -r -p "  $prompt [default: ${default:-skip}]: " val
        [[ -z "$val" ]] && val="$default"
    fi
    if [[ -n "$val" ]]; then
        echo "${key}=${val}" >> "$file"
        ok "$key saved to .env"
    else
        warn "$key not set — some features may be unavailable"
    fi
}

# ── Banner ─────────────────────────────────────────────────────────────────────
echo -e "${GREEN}
╔══════════════════════════════════════════════════════════════╗
║          AgriMitra — AI Crop Yield Prediction Setup          ║
║          Linux / macOS installer                             ║
╚══════════════════════════════════════════════════════════════╝
${NC}"
echo -e "  Flags: skip_ai=$SKIP_AI  skip_mobile=$SKIP_MOBILE  synthetic=$SYNTHETIC_ONLY  force=$FORCE\n"

# ── Step 0 — Network check ────────────────────────────────────────────────────
step 0 "Network connectivity"
NET_OK=0
if curl -sf --connect-timeout 6 https://1ved.cloud -o /dev/null 2>/dev/null; then
    NET_OK=1
    ok "Internet reachable (1ved.cloud OK)"
else
    warn "Cannot reach 1ved.cloud — offline steps will be skipped automatically"
fi

# ── Step 1 — Python 3.11+ ─────────────────────────────────────────────────────
if [[ $SKIP_AI -eq 0 ]]; then
    step 1 "Python 3.11+"
    PY_CMD=""
    for cmd in python3.13 python3.12 python3.11 python3 python; do
        if command -v "$cmd" &>/dev/null; then
            # Compatible with macOS (no -P flag in grep)
            minor=$("$cmd" --version 2>&1 | grep -oE 'Python 3\.[0-9]+' | grep -oE '[0-9]+$')
            if [[ -n "$minor" ]] && [[ "$minor" -ge 11 ]]; then
                PY_CMD="$cmd"; break
            fi
        fi
    done

    if [[ -z "$PY_CMD" ]]; then
        echo ""
        echo -e "  ${YELLOW}Python 3.11+ not found. Install it:${NC}"
        if [[ "$(uname)" == "Darwin" ]]; then
            echo "    brew install python@3.12"
            echo "    or: https://www.python.org/downloads/"
            if [[ $CI -eq 0 ]]; then
                read -r -p "  Install python@3.12 via Homebrew now? [y/N] " ans
                if [[ "$ans" =~ ^[Yy] ]]; then
                    command -v brew &>/dev/null || fail "Homebrew not found. Install from https://brew.sh"
                    brew install python@3.12
                    PY_CMD="python3.12"
                fi
            fi
        else
            echo "    sudo apt install python3.12  (Debian/Ubuntu)"
            echo "    sudo dnf install python3.12  (Fedora/RHEL)"
            echo "    pyenv install 3.12           (any distro)"
        fi
        [[ -z "$PY_CMD" ]] && fail "Python 3.11+ is required. Install it then re-run."
    fi
    ok "Python: $PY_CMD  ($($PY_CMD --version 2>&1))"
fi

# ── Step 2 — .NET 10 SDK ──────────────────────────────────────────────────────
if [[ $SKIP_MOBILE -eq 0 ]]; then
    step 2 ".NET 10 SDK"
    DOTNET_OK=0
    if command -v dotnet &>/dev/null; then
        DOTNET_VER=$(dotnet --version 2>/dev/null || echo "0")
        DOTNET_MAJOR=$(echo "$DOTNET_VER" | cut -d. -f1)
        if [[ "$DOTNET_MAJOR" -ge 10 ]]; then
            ok ".NET SDK $DOTNET_VER"
            DOTNET_OK=1
        else
            warn ".NET $DOTNET_VER found — .NET 10 required"
        fi
    else
        warn ".NET not found in PATH"
    fi

    if [[ $DOTNET_OK -eq 0 ]]; then
        echo ""
        echo -e "  ${YELLOW}Install .NET 10 SDK:${NC}"
        if [[ "$(uname)" == "Darwin" ]]; then
            echo "    brew install dotnet@10"
        else
            echo "    # Automatic install via Microsoft script:"
            echo "    wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh"
            echo "    chmod +x dotnet-install.sh && ./dotnet-install.sh --channel 10.0"
            echo "    export DOTNET_ROOT=\$HOME/.dotnet && export PATH=\$PATH:\$HOME/.dotnet"
        fi
        if [[ $CI -eq 0 ]]; then
            read -r -p "  Install .NET 10 via official script now? [y/N] " ans
            if [[ "$ans" =~ ^[Yy] ]]; then
                curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 10.0
                export DOTNET_ROOT="$HOME/.dotnet"
                export PATH="$PATH:$HOME/.dotnet"
            fi
        fi
        # Re-check
        DOTNET_VER=$(dotnet --version 2>/dev/null || echo "0")
        DOTNET_MAJOR=$(echo "$DOTNET_VER" | cut -d. -f1)
        [[ "$DOTNET_MAJOR" -lt 10 ]] && fail ".NET 10 SDK not found. Install from https://dotnet.microsoft.com/download"
        ok ".NET SDK $DOTNET_VER (after install)"
    fi
fi

# ── Step 3 — MAUI Android workload ────────────────────────────────────────────
if [[ $SKIP_MOBILE -eq 0 ]]; then
    step 3 "MAUI Android workload"
    if dotnet workload list 2>/dev/null | grep -q "maui-android"; then
        skip "maui-android workload already installed"
    else
        info "Installing maui-android workload (this may take several minutes)…"
        dotnet workload install maui-android --skip-sign-check 2>&1 \
            || warn "Workload install had issues — trying to continue"
        ok "MAUI Android workload installed"
    fi
fi

# ── Step 4 — Node.js (optional) ───────────────────────────────────────────────
step 4 "Node.js (optional — for nextjs-api development)"
if command -v node &>/dev/null && command -v npm &>/dev/null; then
    ok "Node $(node --version) / npm $(npm --version)"
else
    warn "Node.js not found — nextjs-api won't run locally"
    if [[ "$(uname)" == "Darwin" ]]; then
        info "Install: brew install node"
    else
        info "Install: https://nodejs.org  or  nvm install --lts"
    fi
fi

# ── Step 5 — Android SDK / ADB (optional) ─────────────────────────────────────
if [[ $SKIP_MOBILE -eq 0 ]]; then
    step 5 "Android SDK / ADB (optional — needed for APK sideload)"
    ADB_PATH=""
    for p in \
        "$ANDROID_HOME/platform-tools/adb" \
        "$HOME/Android/Sdk/platform-tools/adb" \
        "$HOME/Library/Android/sdk/platform-tools/adb" \
        "$(command -v adb 2>/dev/null)"; do
        [[ -x "$p" ]] && ADB_PATH="$p" && break
    done

    if [[ -n "$ADB_PATH" ]]; then
        ok "ADB found: $($ADB_PATH version 2>&1 | head -1)"
        info "  Path: $ADB_PATH"
    else
        warn "ADB not found — install Android Studio to deploy to device"
        if [[ "$(uname)" == "Darwin" ]]; then
            info "Install: brew install --cask android-studio"
        else
            info "Install: https://developer.android.com/studio"
        fi
    fi
fi

if [[ $SKIP_AI -eq 0 ]]; then

# ── Step 6 — Python venv ──────────────────────────────────────────────────────
step 6 "Python virtual environment (ai/.venv)"
VENV="$ROOT/ai/.venv"
if [[ -d "$VENV" ]]; then
    skip "venv already exists at ai/.venv"
else
    "$PY_CMD" -m venv "$VENV"
    ok "venv created at ai/.venv"
fi

PIP="$VENV/bin/pip"
PYTHON="$VENV/bin/python"

# ── Step 7 — pip / setuptools upgrade ─────────────────────────────────────────
step 7 "pip / setuptools upgrade"
"$PYTHON" -m pip install --upgrade pip setuptools wheel --quiet
ok "pip / setuptools up to date"

# ── Step 8 — PyTorch 2.2.0 CPU ────────────────────────────────────────────────
step 8 "PyTorch 2.2.0 (CPU)"
TORCH_OK=0
if [[ $FORCE -eq 0 ]]; then
    TORCH_VER=$("$PYTHON" -c "import torch; print(torch.__version__)" 2>/dev/null || echo "")
    if [[ "$TORCH_VER" =~ ^2\. ]]; then
        skip "PyTorch already installed ($TORCH_VER)"
        TORCH_OK=1
    fi
fi
if [[ $TORCH_OK -eq 0 ]]; then
    info "Downloading PyTorch 2.2.0 CPU wheels (≈500 MB)…"
    "$PIP" install torch==2.2.0 torchvision==0.17.0 torchaudio==2.2.0 \
        --index-url https://download.pytorch.org/whl/cpu --quiet
    ok "PyTorch 2.2.0 installed"
fi

# ── Step 9 — torch-geometric + other deps ─────────────────────────────────────
step 9 "Python dependencies (ai/requirements.txt)"
info "Installing torch-geometric wheels from PyG server…"
"$PIP" install torch_scatter torch_sparse torch_cluster torch_spline_conv \
    -f https://data.pyg.org/whl/torch-2.2.0+cpu.html --quiet \
    || warn "torch-geometric wheels failed — falling back to source (may be slow)"

info "Installing remaining requirements from ai/requirements.txt…"
"$PIP" install -r "$ROOT/ai/requirements.txt" --quiet
ok "All Python dependencies installed"

# ── Step 10 — Configure ai/.env ───────────────────────────────────────────────
step 10 "Environment configuration (ai/.env)"
ENV_FILE="$ROOT/ai/.env"
touch "$ENV_FILE"

if env_set_default "$ENV_FILE" "AGRIMITRA_PROXY" "https://1ved.cloud" \
        "Satellite + inference proxy — all Bhoonidhi calls route through here"; then
    ok "AGRIMITRA_PROXY set to https://1ved.cloud"
else
    skip "AGRIMITRA_PROXY already in .env"
fi

if [[ $CI -eq 0 ]]; then
    echo ""
    echo -e "  ${GRAY}Optional — press Enter to skip any key${NC}"
    env_prompt "$ENV_FILE" "DATA_GOV_KEY" \
        "data.gov.in API key (for AGMARKNET price data)" "" 0
else
    env_set_default "$ENV_FILE" "DATA_GOV_KEY" "" "data.gov.in API key (optional)" || true
fi

ok "ai/.env configured"

# ── Step 11 — Training data ───────────────────────────────────────────────────
step 11 "Training data"
DATA_DIR="$ROOT/ai/data"
CSV_COUNT=0
if [[ -d "$DATA_DIR" ]]; then
    CSV_COUNT=$(find "$DATA_DIR" -name "*.csv" -o -name "*.parquet" 2>/dev/null | wc -l | xargs)
fi

if [[ $CSV_COUNT -gt 0 && $FORCE -eq 0 && $SKIP_DATA -eq 0 ]]; then
    skip "$CSV_COUNT training file(s) already in ai/data/"
elif [[ $SKIP_DATA -eq 1 ]]; then
    skip "--skip-data flag set"
else
    if [[ $SYNTHETIC_ONLY -eq 1 || $NET_OK -eq 0 ]]; then
        [[ $NET_OK -eq 0 ]] && warn "No internet — using synthetic data"
        info "Generating synthetic dataset (200 records)…"
        "$PYTHON" "$ROOT/ai/scripts/generate_demo_data.py" --records 200
    else
        info "Fetching satellite data for Kankavli, Sindhudurg…"
        if ! "$PYTHON" "$ROOT/ai/scripts/fetch_bhoonidhi_data.py" \
                --bbox 73.6,16.4,73.9,16.7 --n-records 50 --env-file "$ENV_FILE"; then
            warn "Bhoonidhi fetch failed — falling back to synthetic data"
            "$PYTHON" "$ROOT/ai/scripts/generate_demo_data.py" --records 200
        fi
    fi
    ok "Training data ready"
fi

# ── Step 12 — Build ONNX model ────────────────────────────────────────────────
step 12 "ONNX model (FP32 → INT8)"
ONNX_SRC="$ROOT/ai/outputs/agrimitra_int8.onnx"

if [[ -f "$ONNX_SRC" && $FORCE -eq 0 ]]; then
    AGE=$(( ($(date +%s) - $(stat -c%Y "$ONNX_SRC" 2>/dev/null || stat -f%m "$ONNX_SRC" 2>/dev/null || echo 0)) / 86400 ))
    skip "agrimitra_int8.onnx exists (${AGE} day(s) old) — use --force to retrain"
else
    BUILD_ARGS="--demo-epochs 1"
    [[ $RESUME -eq 1 ]] && BUILD_ARGS="$BUILD_ARGS --resume"
    # shellcheck disable=SC2086
    "$PYTHON" "$ROOT/ai/scripts/build_demo_model.py" $BUILD_ARGS \
        || fail "Model build failed. Check the Python output above."
    ok "ONNX model built → ai/outputs/agrimitra_int8.onnx"
fi

# ── Step 13 — Copy ONNX → mobile + deploy ─────────────────────────────────────
step 13 "Copy model → mobile app + deploy service"
if [[ -f "$ONNX_SRC" ]]; then
    MOBILE_DST="$ROOT/mobile/AgriMitraMobile/Resources/Raw"
    DEPLOY_DST="$ROOT/deploy"
    mkdir -p "$MOBILE_DST" "$DEPLOY_DST"
    cp "$ONNX_SRC" "$MOBILE_DST/agrimitra_int8.onnx"
    cp "$ONNX_SRC" "$DEPLOY_DST/agrimitra_int8.onnx"
    ok "Copied to mobile/Resources/Raw/ and deploy/"
else
    warn "agrimitra_int8.onnx not found — mobile inference will use bundled demo model"
fi

fi  # end SKIP_AI

# ── Step 14 — MAUI restore + build ────────────────────────────────────────────
if [[ $SKIP_MOBILE -eq 0 ]]; then
    step 14 "MAUI Android build (net10.0-android)"
    MAUI_PROJ="$ROOT/mobile/AgriMitraMobile/AgriMitraMobile.csproj"
    APK_PATH="$ROOT/mobile/AgriMitraMobile/bin/Release/net10.0-android/com.agrimitra.mobile-Signed.apk"

    if [[ -f "$APK_PATH" && $FORCE -eq 0 ]]; then
        AGE_MIN=$(( ($(date +%s) - $(stat -c%Y "$APK_PATH" 2>/dev/null || stat -f%m "$APK_PATH" 2>/dev/null || echo 0)) / 60 ))
        skip "Signed APK exists (${AGE_MIN} min old) — use --force to rebuild"
        info "  APK: $APK_PATH"
    else
        info "Restoring NuGet packages…"
        dotnet restore "$MAUI_PROJ" --verbosity quiet \
            || fail "dotnet restore failed"

        info "Building APK (Release)…"
        dotnet build "$MAUI_PROJ" -f net10.0-android -c Release \
            -t:SignAndroidPackage --verbosity quiet --no-restore \
            || fail "dotnet build failed — check output above"

        if [[ -f "$APK_PATH" ]]; then
            SZ=$(du -sh "$APK_PATH" 2>/dev/null | cut -f1)
            ok "APK built ($SZ) → mobile/AgriMitraMobile/bin/Release/net10.0-android/"
        else
            warn "Build completed but APK not found at expected path"
        fi
    fi
fi

# ── Summary ────────────────────────────────────────────────────────────────────
echo ""
echo -e "${GRAY}─────────────────────────────────────────────────────────────${NC}"
echo -e "  Steps OK: ${GREEN}${STEPS_OK}${NC}   Skipped: ${GRAY}${STEPS_SKIPPED}${NC}   Warnings: ${YELLOW}${#WARNINGS[@]}${NC}"
if [[ ${#WARNINGS[@]} -gt 0 ]]; then
    echo -e "  ${YELLOW}Warnings:${NC}"
    for w in "${WARNINGS[@]}"; do echo -e "    ${YELLOW}⚠ $w${NC}"; done
fi

echo -e "${GREEN}
╔══════════════════════════════════════════════════════════════════╗
║                       SETUP COMPLETE                             ║
╚══════════════════════════════════════════════════════════════════╝

LOCAL DEVELOPMENT

1. Start inference service:
   cd deploy
   pip install fastapi uvicorn onnxruntime
   uvicorn inference_service:app --host 0.0.0.0 --port 8001
   → http://localhost:8001/docs

2. Start training UI:
   cd ai && source .venv/bin/activate
   uvicorn training_ui.app:app --reload --port 8000
   → http://localhost:8000

3. Run Next.js API locally:
   cd nextjs-api && npm install && npm run dev
   → http://localhost:3000

4. Build + deploy APK:
   dotnet build mobile/AgriMitraMobile/AgriMitraMobile.csproj \\
     -f net10.0-android -c Debug -t:SignAndroidPackage
   adb install -r mobile/AgriMitraMobile/bin/Debug/net10.0-android/com.agrimitra.mobile-Signed.apk

PRODUCTION DEPLOYMENT

Inference → 1ved.cloud:
   scp -r deploy/ user@1ved.cloud:~/agrimitra-infer/
   ssh user@1ved.cloud 'cd agrimitra-infer && docker compose up -d --build'

Next.js API → Vercel:
   cd nextjs-api && vercel deploy
   Set env vars: INFER_URL, DATABASE_URL, DATA_GOV_KEY, BHOONIDHI_USER, BHOONIDHI_PASS
${NC}"
