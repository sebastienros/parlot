# Parlot Explorer desktop shell

This shared Tauri shell hosts the explorer's local UI in a native window: WKWebView on macOS,
WebView2 on Windows, and WebKitGTK 4.1 on Linux. It has no filesystem, shell, or other Tauri IPC
capabilities; parser discovery and execution stay in the authenticated .NET backend. Navigation
is restricted to that backend's exact origin, and new windows are blocked.

`parlot-explorer` launches the shell for the current OS and process architecture. Closing the window
ends the .NET host. Stopping the host closes the shell through its stdin lifetime pipe, including
when the host crashes. The shell is an implementation detail, not a separate user command.

## Build

Install Rust and the [Tauri prerequisites](https://v2.tauri.app/start/prerequisites/), then run:

```sh
node tools/Parlot.Explorer.Desktop/build.mjs
```

The explorer .NET project runs this step automatically. The lockfile pins native dependencies.
Output is placed under `dist/<rid>`; macOS output is an application bundle signed ad hoc for local
execution. Distribution signing/notarization can be applied to the bundle before packaging.
No Rust toolchain is needed on a machine running the installed .NET tool.

Native build targets are `win-x64`, `osx-x64`, `osx-arm64`, and `linux-x64`. Build each on its matching
OS/architecture. Linux builds use Ubuntu 22.04 to avoid introducing a newer glibc requirement;
target systems need WebKitGTK 4.1 and a graphical desktop. Windows needs the WebView2 Evergreen runtime.
macOS uses the system WebKit framework.

The Explorer workflow builds and smoke-tests the native webview and parent-pipe shutdown on each
platform, combines the `dist` directories, and packs one .NET tool
containing all four shells. Packing checks for all four by default. A development package for only
the current machine can opt out with `-p:RequireAllDesktopRids=false`. Set `DesktopShellRoot` to use a
previously assembled distribution directory and `SkipDesktopBuild=true` to avoid rebuilding it.
A headless build may use `SkipDesktopBuild=true`; running it requires `--browser` or `--no-browser`
unless the native assets were supplied separately.
