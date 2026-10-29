// InputPlus: GH3+ plugin (successor of WhammyBoost) that sits between GH3.exe and XInput.
//  - [Whammy]   post-processes the whammy axis (sThumbRX) of guitar-type pads (subtype 6/7/11):
//               idle-reset fix, deadzone, sensitivity, motion amplify, turbo oscillation.
//  - [Keyboard] a virtual XInput pad driven by the keyboard, Clone Hero style: every action
//               takes up to 8 keys. SubType 7 = a guitar (GH3 wants strumming); SubType 1 = a
//               plain gamepad, which GH3 plays without strumming, with its frets laid out as
//               PadLayout says (GH3's gamepad layout). Keys bound to it can be hidden from GH3's
//               own DirectInput keyboard so a key never does two things.
//  - [PadN]     AutoStrum: fret presses strum by themselves (a gamepad played without strumming).
// Hooks GH3.exe's imports of XINPUT1_3 ordinals 2 (XInputGetState) and 4 (XInputGetCapabilities),
// and DINPUT8!DirectInput8Create. Settings hot-reload from InputPlus.ini (the launcher writes it).
#define WIN32_LEAN_AND_MEAN
#define _CRT_SECURE_NO_WARNINGS
#define DIRECTINPUT_VERSION 0x0800
#include <windows.h>
#include <math.h>
#include <stdarg.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#define MAX_PADS 4
#define PARK_BAND 0.012        // +/- around 50%: the Wii RB guitar reports 0x7F after idling
#define PARK_JUMP 0.10         // minimum jump into the band to treat it as the idle reset
#define PARK_STABLE_MS 400.0   // previous value must have been stable this long before the jump
#define PARK_TIMEOUT_MS 1200.0 // value stuck inside the band this long also counts as idle
#define CAPS_REFRESH_MS 2000.0
#define CONFIG_POLL_MS 1000.0
#define DEBUG_LOG_MS 100.0
#define PI 3.14159265358979323846
#define MAX_KEYS 8

#define XB_DPAD_UP 0x0001
#define XB_DPAD_DOWN 0x0002
#define XB_DPAD_LEFT 0x0004
#define XB_DPAD_RIGHT 0x0008
#define XB_START 0x0010
#define XB_BACK 0x0020
#define XB_LB 0x0100
#define XB_RB 0x0200
#define XB_A 0x1000
#define XB_B 0x2000
#define XB_X 0x4000
#define XB_Y 0x8000
#define FRET_MASK (XB_A | XB_B | XB_X | XB_Y | XB_LB)

typedef struct { WORD wButtons; BYTE bLeftTrigger, bRightTrigger; SHORT sThumbLX, sThumbLY, sThumbRX, sThumbRY; } XGamepad;
typedef struct { DWORD dwPacketNumber; XGamepad Gamepad; } XState;
typedef struct { BYTE Type, SubType; WORD Flags; XGamepad Gamepad; WORD wLeftMotorSpeed, wRightMotorSpeed; } XCaps;

typedef DWORD (WINAPI *GetStateFn)(DWORD, XState *);
typedef DWORD (WINAPI *GetCapsFn)(DWORD, DWORD, XCaps *);
typedef HRESULT (WINAPI *DI8CreateFn)(HINSTANCE, DWORD, REFIID, LPVOID *, void *);
typedef HRESULT (STDMETHODCALLTYPE *CreateDeviceFn)(void *self, REFGUID guid, void **dev, void *outer);
typedef HRESULT (STDMETHODCALLTYPE *GetDeviceStateFn)(void *self, DWORD size, void *data);
typedef HRESULT (STDMETHODCALLTYPE *GetDeviceDataFn)(void *self, DWORD objSize, void *data, DWORD *inOut, DWORD flags);

// DIDEVICEOBJECTDATA (DirectInput 8), declared here so dinput.h is not needed
typedef struct { DWORD dwOfs, dwData, dwTimeStamp, dwSequence; UINT_PTR uAppData; } DIObjData;
static const GUID kGuidSysKeyboard = { 0x6F1D2B61, 0xD5A0, 0x11CF, { 0xBF, 0xC7, 0x44, 0x45, 0x53, 0x54, 0x00, 0x00 } };

enum { K_GREEN, K_RED, K_YELLOW, K_BLUE, K_ORANGE, K_STRUMUP, K_STRUMDOWN, K_LEFT, K_RIGHT,
       K_START, K_SELECT, K_WHAMMY, K_TILT, K_COUNT };
static const char *kKeyNames[K_COUNT] = { "Green", "Red", "Yellow", "Blue", "Orange", "StrumUp", "StrumDown",
                                          "Left", "Right", "Start", "Select", "Whammy", "Tilt" };
static const WORD kKeyButtons[K_COUNT] = { XB_A, XB_B, XB_Y, XB_X, XB_LB, XB_DPAD_UP, XB_DPAD_DOWN,
                                           XB_DPAD_LEFT, XB_DPAD_RIGHT, XB_START, XB_BACK, 0, 0 };

typedef struct {
    int enabled, parkFix, amplify, turbo, debugLog;
    double restDeadzone, sensitivity, motionGain, motionSmoothMs, turboHz, turboDepth, turboThreshold;
    // keyboard guitar
    int kbEnabled, kbSlot, kbFocusOnly, kbBlockGame, kbSubType, kbAutoStrum;
    int keys[K_COUNT][MAX_KEYS], keyCount[K_COUNT];
    WORD padFretButton[5];          // gamepad subtype: each fret's button, or 0 for a trigger
    BYTE padFretTrigger[5];         // 1 = left trigger, 2 = right trigger
    // per pad
    int autoStrum[MAX_PADS];
    double strumWindowMs, strumPulseMs;
} Config;

typedef struct {
    int capsValid, isGuitar;
    double capsTime;
    int started, parked;
    double lastRaw, lastChangeTime;
    double avg, lastProcTime;
    int turboActive;
    double turboStart;
    int haveOut;
    XGamepad lastOut;
    DWORD lastInPacket, packet;
    double lastLogTime;
    // auto strum
    WORD lastFrets;
    double strumAt, strumEnd;
} PadState;

static GetStateFn g_origGetState;
static GetCapsFn g_origGetCaps;
static DI8CreateFn g_origDI8Create;
static CreateDeviceFn g_origCreateDevice;
static GetDeviceStateFn g_origGetDeviceState;
static GetDeviceDataFn g_origGetDeviceData;
static void *g_kbdDevice;
static DWORD *g_slotState, *g_slotCaps, *g_slotDI;
static Config g_cfg;
static PadState g_pads[MAX_PADS];
static CRITICAL_SECTION g_lock;
static char g_iniPath[MAX_PATH], g_logPath[MAX_PATH];
static FILETIME g_iniTime;
static double g_lastConfigCheck;
static LARGE_INTEGER g_qpf;
static BYTE g_blockDik[256];   // DirectInput key codes hidden from GH3's keyboard

static double NowMs(void)
{
    LARGE_INTEGER t;
    QueryPerformanceCounter(&t);
    return (double)t.QuadPart * 1000.0 / (double)g_qpf.QuadPart;
}

static double Clamp(double v, double lo, double hi) { return v < lo ? lo : (v > hi ? hi : v); }

static void Log(const char *fmt, ...)
{
    FILE *f = fopen(g_logPath, "a");
    SYSTEMTIME st;
    va_list ap;
    if (!f) return;
    GetLocalTime(&st);
    fprintf(f, "%02d:%02d:%02d.%03d ", st.wHour, st.wMinute, st.wSecond, st.wMilliseconds);
    va_start(ap, fmt);
    vfprintf(f, fmt, ap);
    va_end(ap);
    fputc('\n', f);
    fclose(f);
}

static double IniDouble(const char *sec, const char *key, double def)
{
    char buf[64], defs[64];
    snprintf(defs, sizeof defs, "%g", def);
    GetPrivateProfileStringA(sec, key, defs, buf, sizeof buf, g_iniPath);
    return atof(buf);
}

static int IniInt(const char *sec, const char *key, int def) { return (int)GetPrivateProfileIntA(sec, key, def, g_iniPath); }

static int GetIniTime(FILETIME *ft)
{
    WIN32_FILE_ATTRIBUTE_DATA d;
    if (!GetFileAttributesExA(g_iniPath, GetFileExInfoStandard, &d)) return 0;
    *ft = d.ftLastWriteTime;
    return 1;
}

// "49,112,0x26" -> virtual-key codes
static int ParseKeys(const char *s, int *out)
{
    int n = 0;
    while (*s && n < MAX_KEYS) {
        char *end;
        long v = strtol(s, &end, 0);
        if (end == s) { s++; continue; }
        if (v > 0 && v < 256) out[n++] = (int)v;
        s = end;
    }
    return n;
}

// virtual key -> DirectInput key code (the keyboard's scan code, +0x80 when extended)
static int VkToDik(int vk)
{
    UINT sc = MapVirtualKeyA((UINT)vk, 4 /* MAPVK_VK_TO_VSC_EX */);
    if (!sc) return 0;
    switch (vk) { // keys MapVirtualKey reports without their E0 prefix
    case VK_UP: case VK_DOWN: case VK_LEFT: case VK_RIGHT: case VK_INSERT: case VK_DELETE:
    case VK_HOME: case VK_END: case VK_PRIOR: case VK_NEXT: case VK_RCONTROL: case VK_RMENU:
    case VK_DIVIDE: case VK_NUMLOCK: case VK_LWIN: case VK_RWIN: case VK_APPS:
        sc |= 0xE000;
    }
    return (int)((sc & 0xFF) | ((sc & 0xFF00) ? 0x80 : 0));
}

static void LoadConfig(void)
{
    Config c;
    char buf[256], sec[16];
    int i, k;
    ZeroMemory(&c, sizeof c);
    c.enabled = IniInt("Whammy", "Enabled", 1);
    c.parkFix = IniInt("Whammy", "ParkFix", 1);
    c.restDeadzone = Clamp(IniDouble("Whammy", "RestDeadzone", 0.10), 0.0, 0.5);
    c.sensitivity = Clamp(IniDouble("Whammy", "Sensitivity", 1.5), 0.1, 5.0);
    c.amplify = IniInt("Whammy", "Amplify", 1);
    c.motionGain = Clamp(IniDouble("Whammy", "MotionGain", 2.5), 1.0, 10.0);
    c.motionSmoothMs = Clamp(IniDouble("Whammy", "MotionSmoothMs", 120.0), 10.0, 2000.0);
    c.turbo = IniInt("Whammy", "Turbo", 1);
    c.turboHz = Clamp(IniDouble("Whammy", "TurboHz", 9.0), 0.5, 30.0);
    c.turboDepth = Clamp(IniDouble("Whammy", "TurboDepth", 0.7), 0.0, 1.0);
    c.turboThreshold = Clamp(IniDouble("Whammy", "TurboThreshold", 0.05), 0.0, 1.0);
    c.debugLog = IniInt("Whammy", "DebugLog", 0);

    c.kbEnabled = IniInt("Keyboard", "Enabled", 0);
    c.kbSlot = IniInt("Keyboard", "Slot", 0);
    c.kbFocusOnly = IniInt("Keyboard", "FocusOnly", 1);
    c.kbBlockGame = IniInt("Keyboard", "BlockGameKeys", 1);
    c.kbSubType = IniInt("Keyboard", "SubType", 7);
    c.kbAutoStrum = IniInt("Keyboard", "AutoStrum", 0);
    for (k = 0; k < K_COUNT; k++) {
        GetPrivateProfileStringA("Keyboard", kKeyNames[k], "", buf, sizeof buf, g_iniPath);
        c.keyCount[k] = ParseKeys(buf, c.keys[k]);
    }
    // GH3's gamepad frets, green to orange: LT LB RB RT A unless the ini says otherwise
    GetPrivateProfileStringA("Keyboard", "PadLayout", "LT,LB,RB,RT,A", buf, sizeof buf, g_iniPath);
    {
        char *tok = strtok(buf, ", ");
        for (k = 0; k < 5; k++) {
            const char *t = tok ? tok : "";
            c.padFretButton[k] = 0; c.padFretTrigger[k] = 0;
            if (!_stricmp(t, "LT")) c.padFretTrigger[k] = 1;
            else if (!_stricmp(t, "RT")) c.padFretTrigger[k] = 2;
            else if (!_stricmp(t, "LB")) c.padFretButton[k] = XB_LB;
            else if (!_stricmp(t, "RB")) c.padFretButton[k] = XB_RB;
            else if (!_stricmp(t, "A")) c.padFretButton[k] = XB_A;
            else if (!_stricmp(t, "B")) c.padFretButton[k] = XB_B;
            else if (!_stricmp(t, "X")) c.padFretButton[k] = XB_X;
            else if (!_stricmp(t, "Y")) c.padFretButton[k] = XB_Y;
            else c.padFretButton[k] = kKeyButtons[k];
            if (tok) tok = strtok(NULL, ", ");
        }
    }
    for (i = 0; i < MAX_PADS; i++) {
        snprintf(sec, sizeof sec, "Pad%d", i + 1);
        c.autoStrum[i] = IniInt(sec, "AutoStrum", 0);
    }
    c.strumWindowMs = Clamp(IniDouble("AutoStrum", "ChordWindowMs", 12.0), 0.0, 60.0);
    c.strumPulseMs = Clamp(IniDouble("AutoStrum", "PulseMs", 35.0), 10.0, 120.0);
    if (c.kbSlot < 0 || c.kbSlot > MAX_PADS) c.kbSlot = 0;

    ZeroMemory(g_blockDik, sizeof g_blockDik);
    if (c.kbEnabled && c.kbBlockGame)
        for (k = 0; k < K_COUNT; k++)
            for (i = 0; i < c.keyCount[k]; i++) {
                int d = VkToDik(c.keys[k][i]);
                if (d) g_blockDik[d] = 1;
            }

    g_cfg = c;
    Log("config: Whammy Enabled=%d ParkFix=%d RestDeadzone=%.3f Sensitivity=%.2f Amplify=%d MotionGain=%.2f "
        "MotionSmoothMs=%.0f Turbo=%d TurboHz=%.2f TurboDepth=%.2f TurboThreshold=%.3f",
        c.enabled, c.parkFix, c.restDeadzone, c.sensitivity, c.amplify, c.motionGain, c.motionSmoothMs,
        c.turbo, c.turboHz, c.turboDepth, c.turboThreshold);
    Log("config: Keyboard Enabled=%d Slot=%d FocusOnly=%d BlockGameKeys=%d AutoStrum=%d; AutoStrum pads=%d%d%d%d",
        c.kbEnabled, c.kbSlot, c.kbFocusOnly, c.kbBlockGame, c.kbAutoStrum,
        c.autoStrum[0], c.autoStrum[1], c.autoStrum[2], c.autoStrum[3]);
}

static int GameFocused(void)
{
    DWORD pid = 0;
    HWND w = GetForegroundWindow();
    if (!w) return 0;
    GetWindowThreadProcessId(w, &pid);
    return pid == GetCurrentProcessId();
}

static int KeyDown(int action)
{
    int i;
    for (i = 0; i < g_cfg.keyCount[action]; i++)
        if (GetAsyncKeyState(g_cfg.keys[action][i]) & 0x8000) return 1;
    return 0;
}

// The keyboard guitar's pad, or an idle one when the game is not in front.
static void KeyboardPad(XGamepad *g)
{
    int k;
    ZeroMemory(g, sizeof *g);
    g->sThumbRX = -32768;
    if (g_cfg.kbFocusOnly && !GameFocused()) return;
    for (k = 0; k < K_COUNT; k++) {
        if (!KeyDown(k)) continue;
        if (g_cfg.kbSubType == 1 && k <= K_ORANGE) {
            // a plain gamepad: GH3's gamepad frets, no strumming needed
            g->wButtons |= g_cfg.padFretButton[k];
            if (g_cfg.padFretTrigger[k] == 1) g->bLeftTrigger = 255;
            if (g_cfg.padFretTrigger[k] == 2) g->bRightTrigger = 255;
        } else if (kKeyButtons[k]) g->wButtons |= kKeyButtons[k];
    }
    if (KeyDown(K_WHAMMY)) g->sThumbRX = 32767;
    if (KeyDown(K_TILT)) g->sThumbRY = 32767;
}

static int KeyboardSlot(void) { return g_cfg.kbEnabled ? (g_cfg.kbSlot > 0 ? g_cfg.kbSlot - 1 : MAX_PADS - 1) : -1; }

static void ProcessGuitar(DWORD idx, PadState *ps, XState *st, double now)
{
    double raw = ((double)st->Gamepad.sThumbRX + 32768.0) / 65535.0;
    int inBand = fabs(raw - 0.5) <= PARK_BAND;
    double dt, p, out;
    long v;

    if (!ps->started) {
        // A first sample already sitting at 50% is the idle reset, not a held bar.
        ps->started = 1;
        ps->parked = inBand;
        ps->lastRaw = raw;
        ps->lastChangeTime = now;
        ps->lastProcTime = now;
        ps->avg = 0.0;
    }
    dt = Clamp(now - ps->lastProcTime, 0.0, 100.0);
    ps->lastProcTime = now;

    if (!g_cfg.parkFix) {
        ps->parked = 0;
    } else if (raw != ps->lastRaw) {
        if (!inBand)
            ps->parked = 0;
        else if (now - ps->lastChangeTime >= PARK_STABLE_MS && fabs(raw - ps->lastRaw) >= PARK_JUMP)
            ps->parked = 1;
        ps->lastRaw = raw;
        ps->lastChangeTime = now;
    } else if (inBand && now - ps->lastChangeTime >= PARK_TIMEOUT_MS) {
        ps->parked = 1;
    }

    p = ps->parked ? 0.0 : raw;
    p = p <= g_cfg.restDeadzone ? 0.0 : (p - g_cfg.restDeadzone) / (1.0 - g_cfg.restDeadzone);
    p = Clamp(p * g_cfg.sensitivity, 0.0, 1.0);

    out = p;
    if (g_cfg.amplify) {
        // Exaggerate every up/down movement around a smoothed position.
        ps->avg += (p - ps->avg) * (1.0 - exp(-dt / g_cfg.motionSmoothMs));
        out = Clamp(ps->avg + g_cfg.motionGain * (p - ps->avg), 0.0, 1.0);
    } else {
        ps->avg = p;
    }

    if (g_cfg.turbo && p > g_cfg.turboThreshold) {
        double wave;
        if (!ps->turboActive) {
            ps->turboActive = 1;
            ps->turboStart = now;
        }
        wave = 0.5 - 0.5 * cos(2.0 * PI * (now - ps->turboStart) * g_cfg.turboHz / 1000.0); // 0 -> 1 -> 0
        out *= 1.0 - g_cfg.turboDepth * wave;
    } else {
        ps->turboActive = 0;
    }

    v = (long)floor(out * 65535.0 + 0.5) - 32768;
    v = v < -32768 ? -32768 : (v > 32767 ? 32767 : v);

    if (g_cfg.debugLog && now - ps->lastLogTime >= DEBUG_LOG_MS) {
        ps->lastLogTime = now;
        Log("pad%lu rawRX=%6d (%.3f) parked=%d level=%.3f outRX=%6d",
            idx + 1, st->Gamepad.sThumbRX, raw, ps->parked, p, (int)v);
    }
    st->Gamepad.sThumbRX = (SHORT)v;
}

// A fret going down schedules one strum (after a short window that gathers a chord's frets);
// the strum is a short press of D-pad down, with a gap before the next one.
static void AutoStrum(PadState *ps, XState *st, double now)
{
    WORD frets = st->Gamepad.wButtons & FRET_MASK;
    WORD pressed = frets & (WORD)~ps->lastFrets;
    ps->lastFrets = frets;
    if (pressed && ps->strumAt <= 0.0) {
        double at = now + g_cfg.strumWindowMs;
        if (at < ps->strumEnd + 16.0) at = ps->strumEnd + 16.0;
        ps->strumAt = at;
    }
    if (ps->strumAt > 0.0 && now >= ps->strumAt) {
        ps->strumEnd = ps->strumAt + g_cfg.strumPulseMs;
        ps->strumAt = 0.0;
    }
    if (now < ps->strumEnd) st->Gamepad.wButtons |= XB_DPAD_DOWN;
}

static int IsGuitarCaps(DWORD idx, PadState *ps, double now)
{
    if (!ps->capsValid || now - ps->capsTime >= CAPS_REFRESH_MS) {
        XCaps c;
        int guitar;
        ZeroMemory(&c, sizeof c);
        guitar = g_origGetCaps && g_origGetCaps(idx, 0, &c) == ERROR_SUCCESS &&
                 (c.SubType == 6 || c.SubType == 7 || c.SubType == 11);
        if (!ps->capsValid || guitar != ps->isGuitar)
            Log("pad%lu subtype=%u -> %s", idx + 1, c.SubType, guitar ? "guitar (whammy boost)" : "untouched");
        ps->isGuitar = guitar;
        ps->capsValid = 1;
        ps->capsTime = now;
    }
    return ps->isGuitar;
}

static DWORD WINAPI HookGetState(DWORD idx, XState *st)
{
    DWORD r = g_origGetState(idx, st);
    PadState *ps;
    double now;
    int guitar, kb;

    if (idx >= MAX_PADS || !st) return r;

    EnterCriticalSection(&g_lock);
    now = NowMs();

    if (now - g_lastConfigCheck >= CONFIG_POLL_MS) {
        FILETIME ft;
        g_lastConfigCheck = now;
        if (GetIniTime(&ft) && CompareFileTime(&ft, &g_iniTime) != 0) {
            g_iniTime = ft;
            LoadConfig();
        }
    }

    ps = &g_pads[idx];
    kb = (int)idx == KeyboardSlot();
    if (kb) {
        // The keyboard guitar: its own pad on a free slot, or added onto whatever is there.
        XGamepad g;
        KeyboardPad(&g);
        if (r != ERROR_SUCCESS) {
            ZeroMemory(st, sizeof *st);
            st->Gamepad = g;
            r = ERROR_SUCCESS;
            guitar = g_cfg.kbSubType == 6 || g_cfg.kbSubType == 7 || g_cfg.kbSubType == 11;
        } else {
            st->Gamepad.wButtons |= g.wButtons;
            if (g.bLeftTrigger > st->Gamepad.bLeftTrigger) st->Gamepad.bLeftTrigger = g.bLeftTrigger;
            if (g.bRightTrigger > st->Gamepad.bRightTrigger) st->Gamepad.bRightTrigger = g.bRightTrigger;
            if (g.sThumbRX > st->Gamepad.sThumbRX) st->Gamepad.sThumbRX = g.sThumbRX;
            if (g.sThumbRY > st->Gamepad.sThumbRY) st->Gamepad.sThumbRY = g.sThumbRY;
            guitar = IsGuitarCaps(idx, ps, now);
        }
    } else {
        if (r != ERROR_SUCCESS) { LeaveCriticalSection(&g_lock); return r; }
        guitar = IsGuitarCaps(idx, ps, now);
    }

    if ((kb && g_cfg.kbAutoStrum) || (!kb && g_cfg.autoStrum[idx])) AutoStrum(ps, st, now);

    if (guitar && g_cfg.enabled) ProcessGuitar(idx, ps, st, now);

    // Report a fresh packet whenever the output changes, so the game never skips a turbo step.
    if (!ps->haveOut || st->dwPacketNumber != ps->lastInPacket ||
        memcmp(&ps->lastOut, &st->Gamepad, sizeof(XGamepad)) != 0) {
        ps->packet++;
        ps->haveOut = 1;
        ps->lastInPacket = st->dwPacketNumber;
        ps->lastOut = st->Gamepad;
    }
    st->dwPacketNumber = ps->packet;

    LeaveCriticalSection(&g_lock);
    return r;
}

static DWORD WINAPI HookGetCaps(DWORD idx, DWORD flags, XCaps *caps)
{
    DWORD r = g_origGetCaps ? g_origGetCaps(idx, flags, caps) : ERROR_DEVICE_NOT_CONNECTED;
    if (r != ERROR_SUCCESS && caps && (int)idx == KeyboardSlot()) {
        ZeroMemory(caps, sizeof *caps);
        caps->Type = 1; // XINPUT_DEVTYPE_GAMEPAD
        caps->SubType = (BYTE)g_cfg.kbSubType;
        caps->Gamepad.wButtons = 0xF3FF;
        caps->Gamepad.sThumbRX = caps->Gamepad.sThumbRY = -64;
        r = ERROR_SUCCESS;
    }
    return r;
}

/* ---- GH3's DirectInput keyboard: bound keys are hidden from it ---- */
static int HideKeys(void) { return g_cfg.kbEnabled && g_cfg.kbBlockGame && (!g_cfg.kbFocusOnly || GameFocused()); }

static HRESULT STDMETHODCALLTYPE HookGetDeviceState(void *self, DWORD size, void *data)
{
    HRESULT hr = g_origGetDeviceState(self, size, data);
    if (SUCCEEDED(hr) && self == g_kbdDevice && size == 256 && data && HideKeys()) {
        BYTE *keys = (BYTE *)data;
        int i;
        for (i = 0; i < 256; i++) if (g_blockDik[i]) keys[i] = 0;
    }
    return hr;
}

static HRESULT STDMETHODCALLTYPE HookGetDeviceData(void *self, DWORD objSize, void *data, DWORD *inOut, DWORD flags)
{
    HRESULT hr = g_origGetDeviceData(self, objSize, data, inOut, flags);
    if (SUCCEEDED(hr) && self == g_kbdDevice && data && inOut && objSize >= sizeof(DWORD) && HideKeys()) {
        BYTE *base = (BYTE *)data;
        DWORD i, kept = 0;
        for (i = 0; i < *inOut; i++) {
            DIObjData *e = (DIObjData *)(base + i * objSize);
            if (e->dwOfs < 256 && g_blockDik[e->dwOfs]) continue;
            if (kept != i) memmove(base + kept * objSize, e, objSize);
            kept++;
        }
        *inOut = kept;
    }
    return hr;
}

static void PatchVtable(void **slot, void *hook, void **orig)
{
    DWORD old;
    if (*slot == hook) return;
    if (!VirtualProtect(slot, sizeof(void *), PAGE_EXECUTE_READWRITE, &old)) return;
    if (!*orig) *orig = *slot;
    *slot = hook;
    VirtualProtect(slot, sizeof(void *), old, &old);
}

static HRESULT STDMETHODCALLTYPE HookCreateDevice(void *self, REFGUID guid, void **dev, void *outer)
{
    HRESULT hr = g_origCreateDevice(self, guid, dev, outer);
    if (SUCCEEDED(hr) && dev && *dev && IsEqualGUID(guid, &kGuidSysKeyboard)) {
        void **vt = *(void ***)*dev;
        g_kbdDevice = *dev;
        PatchVtable(&vt[9], (void *)HookGetDeviceState, (void **)&g_origGetDeviceState);   // IDirectInputDevice8::GetDeviceState
        PatchVtable(&vt[10], (void *)HookGetDeviceData, (void **)&g_origGetDeviceData);    // IDirectInputDevice8::GetDeviceData
        Log("keyboard device %p hooked", *dev);
    }
    return hr;
}

static HRESULT WINAPI HookDI8Create(HINSTANCE inst, DWORD ver, REFIID riid, LPVOID *out, void *outer)
{
    HRESULT hr = g_origDI8Create(inst, ver, riid, out, outer);
    if (SUCCEEDED(hr) && out && *out) {
        void **vt = *(void ***)*out;
        PatchVtable(&vt[3], (void *)HookCreateDevice, (void **)&g_origCreateDevice);         // IDirectInput8::CreateDevice
    }
    return hr;
}

static void PatchSlot(DWORD *slot, void *hook)
{
    DWORD old;
    if (!slot || !VirtualProtect(slot, sizeof *slot, PAGE_READWRITE, &old)) return;
    *slot = (DWORD)(ULONG_PTR)hook;
    VirtualProtect(slot, sizeof *slot, old, &old);
}

static int PatchImports(void)
{
    BYTE *base = (BYTE *)GetModuleHandleA(NULL);
    IMAGE_NT_HEADERS *nt = (IMAGE_NT_HEADERS *)(base + ((IMAGE_DOS_HEADER *)base)->e_lfanew);
    IMAGE_DATA_DIRECTORY dir = nt->OptionalHeader.DataDirectory[IMAGE_DIRECTORY_ENTRY_IMPORT];
    IMAGE_IMPORT_DESCRIPTOR *d;

    if (!dir.VirtualAddress) return 0;
    for (d = (IMAGE_IMPORT_DESCRIPTOR *)(base + dir.VirtualAddress); d->Name; d++) {
        IMAGE_THUNK_DATA32 *names, *iat;
        const char *dll = (const char *)(base + d->Name);
        int xinput = _stricmp(dll, "xinput1_3.dll") == 0, dinput = _stricmp(dll, "dinput8.dll") == 0;
        if (!xinput && !dinput) continue;
        names = (IMAGE_THUNK_DATA32 *)(base + (d->OriginalFirstThunk ? d->OriginalFirstThunk : d->FirstThunk));
        iat = (IMAGE_THUNK_DATA32 *)(base + d->FirstThunk);
        for (; names->u1.AddressOfData; names++, iat++) {
            WORD ord = 0;
            const char *fn = "";
            if (IMAGE_SNAP_BY_ORDINAL32(names->u1.Ordinal)) ord = (WORD)IMAGE_ORDINAL32(names->u1.Ordinal);
            else fn = (const char *)((IMAGE_IMPORT_BY_NAME *)(base + names->u1.AddressOfData))->Name;
            if (xinput && (ord == 2 || strcmp(fn, "XInputGetState") == 0)) g_slotState = &iat->u1.Function;
            if (xinput && (ord == 4 || strcmp(fn, "XInputGetCapabilities") == 0)) g_slotCaps = &iat->u1.Function;
            if (dinput && strcmp(fn, "DirectInput8Create") == 0) g_slotDI = &iat->u1.Function;
        }
    }
    if (!g_slotState || *g_slotState == (DWORD)(ULONG_PTR)HookGetState) return 0;

    g_origGetState = (GetStateFn)(ULONG_PTR)*g_slotState;
    if (g_slotCaps) g_origGetCaps = (GetCapsFn)(ULONG_PTR)*g_slotCaps;
    if (!g_origGetCaps) {
        HMODULE h;
        if (GetModuleHandleExA(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
                               (LPCSTR)g_origGetState, &h))
            g_origGetCaps = (GetCapsFn)GetProcAddress(h, MAKEINTRESOURCEA(4));
    }
    PatchSlot(g_slotState, (void *)HookGetState);
    if (g_slotCaps) PatchSlot(g_slotCaps, (void *)HookGetCaps);
    if (g_slotDI && *g_slotDI != (DWORD)(ULONG_PTR)HookDI8Create) {
        g_origDI8Create = (DI8CreateFn)(ULONG_PTR)*g_slotDI;
        PatchSlot(g_slotDI, (void *)HookDI8Create);
    }
    return 1;
}

static void Unpatch(DWORD *slot, void *orig)
{
    DWORD old;
    if (!slot || !orig || !VirtualProtect(slot, sizeof *slot, PAGE_READWRITE, &old)) return;
    *slot = (DWORD)(ULONG_PTR)orig;
    VirtualProtect(slot, sizeof *slot, old, &old);
}

BOOL WINAPI DllMain(HINSTANCE inst, DWORD reason, LPVOID reserved)
{
    if (reason == DLL_PROCESS_ATTACH) {
        char path[MAX_PATH], mod[MAX_PATH] = "?";
        char *dot;
        HMODULE h;
        FILE *f;
        int ok;

        DisableThreadLibraryCalls(inst);
        QueryPerformanceFrequency(&g_qpf);
        InitializeCriticalSection(&g_lock);

        GetModuleFileNameA(inst, path, MAX_PATH);
        dot = strrchr(path, '.');
        if (dot) *dot = '\0';
        snprintf(g_iniPath, MAX_PATH, "%s.ini", path);
        snprintf(g_logPath, MAX_PATH, "%s.log", path);
        if ((f = fopen(g_logPath, "w")) != NULL) fclose(f);

        GetIniTime(&g_iniTime);
        LoadConfig();
        ok = PatchImports();
        if (g_origGetState &&
            GetModuleHandleExA(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
                               (LPCSTR)g_origGetState, &h))
            GetModuleFileNameA(h, mod, MAX_PATH);
        Log("hook %s, XInputGetState -> %s, caps %s, DirectInput8Create %s", ok ? "OK" : "FAILED", mod,
            g_slotCaps ? "hooked" : (g_origGetCaps ? "found" : "missing"), g_origDI8Create ? "hooked" : "not imported");
    } else if (reason == DLL_PROCESS_DETACH && reserved == NULL) {
        Unpatch(g_slotState, (void *)g_origGetState);
        Unpatch(g_slotCaps, (void *)g_origGetCaps);
        Unpatch(g_slotDI, (void *)g_origDI8Create);
    }
    return TRUE;
}
