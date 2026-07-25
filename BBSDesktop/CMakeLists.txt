cmake_minimum_required(VERSION 3.20)
project(BBSStudio LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

if(NOT CMAKE_BUILD_TYPE AND NOT CMAKE_CONFIGURATION_TYPES)
    set(CMAKE_BUILD_TYPE Release CACHE STRING "" FORCE)
endif()

if(MSVC)
    add_compile_options(/W3 /permissive- /utf-8 /EHsc)
    add_compile_definitions(_CRT_SECURE_NO_WARNINGS UNICODE _UNICODE
                            WINVER=0x0A00 _WIN32_WINNT=0x0A00)
endif()

set(CORE_SOURCES
    src/core/Engine.cpp
    src/core/Json.cpp
    src/core/Project.cpp
    src/core/Export.cpp
    src/core/Parse.cpp)

set(UI_SOURCES
    src/ui/Theme.cpp
    src/ui/Draw.cpp
    src/ui/Widgets.cpp
    src/ui/Dialogs.cpp
    src/ui/Page.cpp
    src/ui/ElementPage.cpp
    src/ui/Pages.cpp
    src/ui/Compute.cpp
    src/ui/MainWindow.cpp)

add_executable(BBSStudio WIN32
    src/App.cpp
    ${CORE_SOURCES}
    ${UI_SOURCES}
    app.manifest)  # CMake embeds this as the single application manifest

target_link_libraries(BBSStudio PRIVATE
    gdiplus dwmapi uxtheme comctl32 comdlg32 user32 gdi32 shell32 ole32)

# Engine smoke tests (console).
add_executable(bbs_tests tests/test_engine.cpp src/core/Engine.cpp src/core/Parse.cpp)

