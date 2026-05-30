cmake_minimum_required(VERSION 3.20)

project(MinecraftBedrockCTFCleanModule LANGUAGES CXX)

# Build x64 Windows DLLs with the static MSVC runtime.
# This avoids the proxy depending on the same vcruntime DLL it is replacing.
if(MSVC)
  cmake_policy(SET CMP0091 NEW)
  set(CMAKE_MSVC_RUNTIME_LIBRARY "MultiThreaded$<$<CONFIG:Debug>:Debug>")
endif()

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

add_library(ctf_patch_module SHARED
  src/module/ctf_patch_module.cpp
  src/module/user_patches.h
)

target_compile_definitions(ctf_patch_module PRIVATE
  WIN32_LEAN_AND_MEAN
  NOMINMAX
  UNICODE
  _UNICODE
)

if(MSVC)
  target_compile_options(ctf_patch_module PRIVATE /W4 /permissive- /guard:cf)
  target_link_options(ctf_patch_module PRIVATE /guard:cf)
endif()

add_library(vcruntime_proxy SHARED
  src/proxy/proxy.cpp
  src/proxy/vcruntime140_1.def
)

set_target_properties(vcruntime_proxy PROPERTIES
  OUTPUT_NAME "vcruntime140_1"
)

target_compile_definitions(vcruntime_proxy PRIVATE
  WIN32_LEAN_AND_MEAN
  NOMINMAX
  UNICODE
  _UNICODE
)

if(MSVC)
  target_compile_options(vcruntime_proxy PRIVATE /W4 /permissive- /guard:cf)
  target_link_options(vcruntime_proxy PRIVATE /guard:cf)
endif()
