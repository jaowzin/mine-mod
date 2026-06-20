#include <windows.h>
#include <cstdint>
#include <cstddef>
#include <iterator>
#include <cwchar>
#include <cstdio>
#include <cstring>

struct PatchSpec {
    const char* name;
    const wchar_t* module_name;       // nullptr = main executable
    const uint8_t* pattern;
    const char* mask;                 // 'x' exact, '?' wildcard
    std::size_t pattern_len;
    std::ptrdiff_t patch_offset;
    const uint8_t* replacement;
    std::size_t replacement_len;
};

#include "user_patches.h"

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

static bool ContainsW(const wchar_t* text, const wchar_t* needle) {
    if (!text || !needle) return false;
    return wcsstr(text, needle) != nullptr;
}

static void CreateDirIfNeeded(const wchar_t* path) {
    if (path && path[0]) {
        CreateDirectoryW(path, nullptr);
    }
}

static void WriteLogW(const wchar_t* msg) {
    wchar_t localAppData[MAX_PATH]{};
    if (!GetEnvPath(L"LOCALAPPDATA", localAppData, MAX_PATH)) return;

    wchar_t dir[MAX_PATH]{};
    wcscpy_s(dir, localAppData);
    AppendPath(dir, MAX_PATH, L"\\MinecraftBedrockCTF");
    CreateDirIfNeeded(dir);

    wchar_t logPath[MAX_PATH]{};
    wcscpy_s(logPath, dir);
    AppendPath(logPath, MAX_PATH, L"\\module.log");

    HANDLE h = CreateFileW(logPath, FILE_APPEND_DATA, FILE_SHARE_READ, nullptr,
                           OPEN_ALWAYS, FILE_ATTRIBUTE_NORMAL, nullptr);
    if (h == INVALID_HANDLE_VALUE) return;

    SYSTEMTIME st{};
    GetLocalTime(&st);

    wchar_t line[1536]{};
    swprintf_s(line, L"[%04u-%02u-%02u %02u:%02u:%02u] %s\r\n",
               st.wYear, st.wMonth, st.wDay, st.wHour, st.wMinute, st.wSecond,
               msg ? msg : L"(null)");

    char utf8[3072]{};
    int bytes = WideCharToMultiByte(CP_UTF8, 0, line, -1, utf8, sizeof(utf8), nullptr, nullptr);
    if (bytes > 1) {
        DWORD written = 0;
        WriteFile(h, utf8, static_cast<DWORD>(bytes - 1), &written, nullptr);
    }

    CloseHandle(h);
}

static void WriteLogA(const char* msg) {
    wchar_t wide[1024]{};
    MultiByteToWideChar(CP_UTF8, 0, msg ? msg : "(null)", -1, wide, static_cast<int>(std::size(wide)));
    WriteLogW(wide);
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

static bool MarkerHas(const char* token) {
    wchar_t localAppData[MAX_PATH]{};
    if (!GetEnvPath(L"LOCALAPPDATA", localAppData, MAX_PATH)) return false;

    wchar_t marker[MAX_PATH]{};
    wcscpy_s(marker, localAppData);
    AppendPath(marker, MAX_PATH, L"\\MinecraftBedrockCTF\\ALLOW_CTF.txt");

    char text[4096]{};
    if (!ReadTextFileSmall(marker, text, sizeof(text))) return false;

    return strstr(text, token) != nullptr;
}

static bool HasCtfMarker() {
    return MarkerHas("CTF-ID=MINECRAFT-BEDROCK-CUSTOM-BUILD-CTF") &&
           MarkerHas("CTF-SHA256=06ca408f52e98204f93da63aee16bb6b751b0e3256bdcb6095d3dada1ba55c0e");
}

static bool PatchesEnabled() {
    return MarkerHas("ENABLE_PATCHES=1");
}

static bool LooksLikeAllowedCtfProcess() {
    wchar_t exePath[MAX_PATH * 2]{};
    GetModuleFileNameW(nullptr, exePath, static_cast<DWORD>(std::size(exePath)));

    // Version-agnostic CTF guard: accept the installed Microsoft.MinecraftUWP x64 package
    // family, but do not pin the middle version segment. This supports updated CTF builds
    // such as 21.31 while keeping the check scoped to the expected package family.
    const bool inWindowsApps = ContainsW(exePath, L"\\WindowsApps\\");
    const bool packageName = ContainsW(exePath, L"Microsoft.MinecraftUWP_") ||
                             ContainsW(exePath, L"MICROSOFT.MINECRAFTUWP_");
    const bool packageArchAndFamily = ContainsW(exePath, L"_x64__8wekyb3d8bbwe\\") ||
                                      ContainsW(exePath, L"_x64__8wekyb3d8bbwe/");

    return inWindowsApps && packageName && packageArchAndFamily;
}

static bool MatchMask(const uint8_t* data, const uint8_t* pattern, const char* mask, std::size_t len) {
    if (!data || !pattern || !mask) return false;
    for (std::size_t i = 0; i < len; ++i) {
        if (mask[i] == 'x' && data[i] != pattern[i]) return false;
        if (mask[i] != 'x' && mask[i] != '?') return false;
    }
    return true;
}

static const IMAGE_NT_HEADERS* GetNtHeaders(HMODULE module) {
    if (!module) return nullptr;

    const auto base = reinterpret_cast<const uint8_t*>(module);
    const auto dos = reinterpret_cast<const IMAGE_DOS_HEADER*>(base);
    if (dos->e_magic != IMAGE_DOS_SIGNATURE) return nullptr;

    const auto nt = reinterpret_cast<const IMAGE_NT_HEADERS*>(base + dos->e_lfanew);
    if (nt->Signature != IMAGE_NT_SIGNATURE) return nullptr;

    return nt;
}

static bool ScanReadableImageSections(HMODULE module,
                                      const uint8_t* pattern,
                                      const char* mask,
                                      std::size_t patternLen,
                                      uintptr_t* uniqueMatch) {
    if (!module || !pattern || !mask || patternLen == 0 || !uniqueMatch) return false;

    *uniqueMatch = 0;
    std::size_t matchCount = 0;

    auto base = reinterpret_cast<uintptr_t>(module);
    const IMAGE_NT_HEADERS* nt = GetNtHeaders(module);
    if (!nt) return false;

    const uintptr_t imageEnd = base + static_cast<uintptr_t>(nt->OptionalHeader.SizeOfImage);
    const IMAGE_SECTION_HEADER* sections = IMAGE_FIRST_SECTION(nt);

    for (WORD i = 0; i < nt->FileHeader.NumberOfSections; ++i) {
        const IMAGE_SECTION_HEADER& sec = sections[i];

        // KG-UP-GOAT does a raw in-image scan. This safer clone scans readable,
        // non-discardable sections so data strings are covered without walking
        // unrelated process memory.
        const bool readable = (sec.Characteristics & IMAGE_SCN_MEM_READ) != 0;
        const bool discardable = (sec.Characteristics & IMAGE_SCN_MEM_DISCARDABLE) != 0;
        if (!readable || discardable) continue;

        uintptr_t secBase = base + static_cast<uintptr_t>(sec.VirtualAddress);
        std::size_t secSize = static_cast<std::size_t>(
            sec.Misc.VirtualSize ? sec.Misc.VirtualSize : sec.SizeOfRawData
        );

        if (secSize < patternLen) continue;
        if (secBase < base || secBase >= imageEnd) continue;
        if (secBase + secSize > imageEnd) {
            secSize = static_cast<std::size_t>(imageEnd - secBase);
        }

        const auto secStart = reinterpret_cast<const uint8_t*>(secBase);
        for (std::size_t off = 0; off <= secSize - patternLen; ++off) {
            if (MatchMask(secStart + off, pattern, mask, patternLen)) {
                ++matchCount;
                *uniqueMatch = reinterpret_cast<uintptr_t>(secStart + off);

                if (matchCount > 1) {
                    WriteLogW(L"Patch skipped: pattern matched more than once.");
                    return false;
                }
            }
        }
    }

    if (matchCount != 1) {
        WriteLogW(L"Patch skipped: pattern did not match exactly once.");
        return false;
    }

    return true;
}

static bool ApplyOnePatch(const PatchSpec& spec) {
    if (!spec.name || !spec.pattern || !spec.mask || spec.pattern_len == 0 ||
        !spec.replacement || spec.replacement_len == 0) {
        WriteLogW(L"Patch skipped: invalid patch spec.");
        return false;
    }

    HMODULE module = spec.module_name ? GetModuleHandleW(spec.module_name) : GetModuleHandleW(nullptr);
    if (!module && spec.module_name) {
        // Load only the module named by the CTF patch specification and record it in logs.
        module = LoadLibraryW(spec.module_name);
        if (module) {
            WriteLogW(L"Target module was loaded by ctf_patch_module.");
        }
    }

    if (!module) {
        WriteLogA(spec.name);
        WriteLogW(L"Patch skipped: target module is not loaded and could not be loaded.");
        return false;
    }

    uintptr_t match = 0;
    if (!ScanReadableImageSections(module, spec.pattern, spec.mask, spec.pattern_len, &match)) {
        WriteLogA(spec.name);
        return false;
    }

    if (spec.patch_offset < 0 ||
        static_cast<std::size_t>(spec.patch_offset) + spec.replacement_len > spec.pattern_len) {
        WriteLogW(L"Patch skipped: replacement must stay inside the matched signature.");
        return false;
    }

    const uintptr_t patchAddress = match + static_cast<uintptr_t>(spec.patch_offset);
    if (patchAddress < match || patchAddress + spec.replacement_len < patchAddress) {
        WriteLogW(L"Patch skipped: patch address overflow.");
        return false;
    }

    DWORD oldProtect = 0;
    if (!VirtualProtect(reinterpret_cast<void*>(patchAddress), spec.replacement_len,
                        PAGE_EXECUTE_READWRITE, &oldProtect)) {
        WriteLogW(L"Patch skipped: VirtualProtect failed.");
        return false;
    }

    CopyMemory(reinterpret_cast<void*>(patchAddress), spec.replacement, spec.replacement_len);
    FlushInstructionCache(GetCurrentProcess(), reinterpret_cast<void*>(patchAddress), spec.replacement_len);

    DWORD ignored = 0;
    VirtualProtect(reinterpret_cast<void*>(patchAddress), spec.replacement_len, oldProtect, &ignored);

    WriteLogA(spec.name);
    WriteLogW(L"Patch applied.");
    return true;
}

static DWORD WINAPI PatchThread(LPVOID) {
    Sleep(2500);
    WriteLogW(L"ctf_patch_module loaded.");

    if (!HasCtfMarker()) {
        WriteLogW(L"CTF marker missing or invalid. Exiting without patching.");
        return 0;
    }

    if (!LooksLikeAllowedCtfProcess()) {
        WriteLogW(L"Process path does not match allowed CTF package family/architecture. Exiting without patching.");
        return 0;
    }

    if (!PatchesEnabled()) {
        WriteLogW(L"Patching disabled. Set ENABLE_PATCHES=1 in ALLOW_CTF.txt after adding CTF-only signatures.");
        return 0;
    }

    std::size_t count = 0;
    const PatchSpec* patches = GetUserPatches(count);

    if (!patches || count == 0) {
        WriteLogW(L"No user patches defined.");
        return 0;
    }

    for (std::size_t i = 0; i < count; ++i) {
        ApplyOnePatch(patches[i]);
    }

    return 0;
}

BOOL WINAPI DllMain(HINSTANCE instance, DWORD reason, LPVOID) {
    if (reason == DLL_PROCESS_ATTACH) {
        DisableThreadLibraryCalls(instance);

        HANDLE hThread = CreateThread(nullptr, 0, PatchThread, nullptr, 0, nullptr);
        if (hThread) {
            CloseHandle(hThread);
        }
    }

    return TRUE;
}
