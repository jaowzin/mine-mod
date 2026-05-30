#pragma once

#include <cstddef>
#include <cstdint>

// Static analysis of KG-UP-GOAT.dll found one concrete in-memory patch:
//
//   target module: xgameruntime.dll
//   search bytes : "isTrial\0"
//   write bytes  : "xzNope\0"
//
// KG does this by loading xgameruntime.dll, scanning the loaded image, making the
// matched memory writable with VirtualProtect, writing the replacement, then
// restoring the original page protection.
//
// This clone keeps that behavior narrow:
// - it only runs after the CTF marker and ENABLE_PATCHES=1 are present;
// - it only runs in the allowlisted CTF package/version;
// - the pattern must match exactly once;
// - it has no network/update/telemetry code.

// Original KG patch target string, including the trailing NUL used for uniqueness.
static constexpr uint8_t kXGameRuntimeTrialPattern[] = {
    'i', 's', 'T', 'r', 'i', 'a', 'l', 0x00
};

// KG replacement. It is intentionally 7 bytes, matching the observed write size:
// "xzNope\0". The original 8th byte is already NUL in "isTrial\0".
static constexpr uint8_t kXGameRuntimeTrialReplacement[] = {
    'x', 'z', 'N', 'o', 'p', 'e', 0x00
};

inline const PatchSpec* GetUserPatches(std::size_t& count) {
    static const PatchSpec patches[] = {
        {
            "kg-clone-xgameruntime-isTrial-to-xzNope",
            L"xgameruntime.dll",
            kXGameRuntimeTrialPattern,
            "xxxxxxxx",
            sizeof(kXGameRuntimeTrialPattern),
            0,
            kXGameRuntimeTrialReplacement,
            sizeof(kXGameRuntimeTrialReplacement)
        },
    };

    count = sizeof(patches) / sizeof(patches[0]);
    return patches;
}
