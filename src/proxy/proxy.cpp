#include <windows.h>
#include <iterator>
#include <cwchar>
#include <cstdio>
#include <cstring>

static HMODULE g_self = nullptr;

static bool Contains(const wchar_t* text, const wchar_t* needle) {
    if (!text || !needle) return false;
    return wcsstr(text, needle) != nullptr;
}

static DWORD GetEnvPath(const wchar_t* name, wchar_t* out, DWORD outCount) {
    if (!out || outCount == 0) return 0;
    out[0] = L'\0';
    return GetEnvironmentVariableW(name, out, outCount);
}

static void AppendPath(wchar_t* base, DWORD baseCount, const wchar_t* suffix) {
    if (!base || !suffix) return;
    const size_t used = wcslen(base);
    if (used == 0 || used + wcslen(suffix) + 1 >= baseCount) return;
    wcscat_s(base, baseCount, suffix);
}

static void CreateDirIfNeeded(const wchar_t* path) {
    if (path && path[0]) {
        CreateDirectoryW(path, nullptr);
    }
}

static void WriteLog(const wchar_t* msg) {
    wchar_t localAppData[MAX_PATH]{};
    if (!GetEnvPath(L"LOCALAPPDATA", localAppData, MAX_PATH)) return;

    wchar_t dir[MAX_PATH]{};
    wcscpy_s(dir, localAppData);
    AppendPath(dir, MAX_PATH, L"\\MinecraftBedrockCTF");
    CreateDirIfNeeded(dir);

    wchar_t logPath[MAX_PATH]{};
    wcscpy_s(logPath, dir);
    AppendPath(logPath, MAX_PATH, L"\\loader.log");

    HANDLE h = CreateFileW(logPath, FILE_APPEND_DATA, FILE_SHARE_READ, nullptr,
                           OPEN_ALWAYS, FILE_ATTRIBUTE_NORMAL, nullptr);
    if (h == INVALID_HANDLE_VALUE) return;

    SYSTEMTIME st{};
    GetLocalTime(&st);

    wchar_t line[1024]{};
    swprintf_s(line, L"[%04u-%02u-%02u %02u:%02u:%02u] %s\r\n",
               st.wYear, st.wMonth, st.wDay, st.wHour, st.wMinute, st.wSecond,
               msg ? msg : L"(null)");

    char utf8[2048]{};
    int bytes = WideCharToMultiByte(CP_UTF8, 0, line, -1, utf8, sizeof(utf8), nullptr, nullptr);
    if (bytes > 1) {
        DWORD written = 0;
        WriteFile(h, utf8, static_cast<DWORD>(bytes - 1), &written, nullptr);
    }

    CloseHandle(h);
}

static bool ReadTextFileSmall(const wchar_t* path, char* out, DWORD outCount) {
    if (!path || !out || outCount == 0) return false;
    out[0] = '\0';

    HANDLE h = CreateFileW(path, GENERIC_READ, FILE_SHARE_READ, nullptr,
                           OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, nullptr);
    if (h == INVALID_HANDLE_VALUE) return false;

    DWORD read = 0;
    BOOL ok = ReadFile(h, out, outCount - 1, &read, nullptr);
    CloseHandle(h);

    if (!ok) return false;
    out[read] = '\0';
    return true;
}

static bool HasCtfMarker() {
    wchar_t localAppData[MAX_PATH]{};
    if (!GetEnvPath(L"LOCALAPPDATA", localAppData, MAX_PATH)) return false;

    wchar_t marker[MAX_PATH]{};
    wcscpy_s(marker, localAppData);
    AppendPath(marker, MAX_PATH, L"\\MinecraftBedrockCTF\\ALLOW_CTF.txt");

    char text[4096]{};
    if (!ReadTextFileSmall(marker, text, sizeof(text))) return false;

    return strstr(text, "CTF-ID=MINECRAFT-BEDROCK-CUSTOM-BUILD-CTF") != nullptr &&
           strstr(text, "CTF-SHA256=06ca408f52e98204f93da63aee16bb6b751b0e3256bdcb6095d3dada1ba55c0e") != nullptr;
}

static bool LooksLikeAllowedCtfProcess() {
    wchar_t exePath[MAX_PATH * 2]{};
    GetModuleFileNameW(nullptr, exePath, static_cast<DWORD>(std::size(exePath)));

    // Keep this narrow. Edit only for your authorized CTF build.
    return Contains(exePath, L"MICROSOFT.MINECRAFTUWP_1.26.2101.0_x64__8wekyb3d8bbwe") ||
           Contains(exePath, L"Microsoft.MinecraftUWP_1.26.2101.0_x64__8wekyb3d8bbwe");
}

static bool BuildSameDirectoryPath(const wchar_t* fileName, wchar_t* out, DWORD outCount) {
    if (!g_self || !fileName || !out || outCount == 0) return false;

    out[0] = L'\0';
    DWORD len = GetModuleFileNameW(g_self, out, outCount);
    if (len == 0 || len >= outCount) return false;

    for (DWORD i = len; i > 0; --i) {
        if (out[i - 1] == L'\\' || out[i - 1] == L'/') {
            out[i] = L'\0';
            break;
        }
    }

    AppendPath(out, outCount, fileName);
    return true;
}

static bool BuildAppDataModPath(const wchar_t* fileName, wchar_t* out, DWORD outCount) {
    wchar_t appData[MAX_PATH]{};
    if (!GetEnvPath(L"APPDATA", appData, MAX_PATH)) return false;

    wcscpy_s(out, outCount, appData);
    AppendPath(out, outCount, L"\\Minecraft Bedrock\\mods\\");
    AppendPath(out, outCount, fileName);
    return true;
}

static DWORD WINAPI LoaderThread(LPVOID) {
    Sleep(1500);

    if (!HasCtfMarker()) {
        WriteLog(L"Proxy loaded, but CTF marker missing. Not loading module.");
        return 0;
    }

    if (!LooksLikeAllowedCtfProcess()) {
        WriteLog(L"Proxy loaded outside the allowed CTF package/version. Not loading module.");
        return 0;
    }

    wchar_t modulePath[MAX_PATH * 2]{};

    if (BuildAppDataModPath(L"ctf_patch_module.dll", modulePath, static_cast<DWORD>(std::size(modulePath)))) {
        HMODULE h = LoadLibraryW(modulePath);
        if (h) {
            WriteLog(L"Loaded ctf_patch_module.dll from APPDATA mods directory.");
            return 0;
        }
    }

    if (BuildSameDirectoryPath(L"ctf_patch_module.dll", modulePath, static_cast<DWORD>(std::size(modulePath)))) {
        HMODULE h = LoadLibraryW(modulePath);
        if (h) {
            WriteLog(L"Loaded ctf_patch_module.dll from app directory.");
            return 0;
        }
    }

    WriteLog(L"Could not load ctf_patch_module.dll from allowed local paths.");
    return 0;
}

BOOL WINAPI DllMain(HINSTANCE instance, DWORD reason, LPVOID) {
    if (reason == DLL_PROCESS_ATTACH) {
        g_self = instance;
        DisableThreadLibraryCalls(instance);

        HANDLE hThread = CreateThread(nullptr, 0, LoaderThread, nullptr, 0, nullptr);
        if (hThread) {
            CloseHandle(hThread);
        }
    }

    return TRUE;
}
