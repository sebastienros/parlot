import { spawnSync } from 'node:child_process';
import { mkdirSync, copyFileSync, chmodSync, writeFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = dirname(fileURLToPath(import.meta.url));
const platforms = { win32: 'win', darwin: 'osx', linux: 'linux' };
const rid = `${platforms[process.platform]}-${process.arch}`;
const supported = ['win-x64', 'osx-x64', 'osx-arm64', 'linux-x64'];
if (!supported.includes(rid)) throw new Error(`No packaged desktop shell for ${rid}; use --browser with a supported tool build.`);
const built = spawnSync('cargo', ['build', '--release', '--locked', '--manifest-path', join(root, 'Cargo.toml')], { stdio: 'inherit' });
if (built.status !== 0) process.exit(built.status || 1);
const output = resolve(process.argv[2] || join(root, 'dist'));
const name = 'parlot-explorer-desktop' + (process.platform === 'win32' ? '.exe' : '');
const folder = join(output, rid);
const executable = process.platform === 'darwin' ? join(folder, 'Parlot Explorer.app', 'Contents', 'MacOS', name) : join(folder, name);
mkdirSync(dirname(executable), { recursive: true });
copyFileSync(join(root, 'target', 'release', name), executable);
if (process.platform !== 'win32') chmodSync(executable, 0o755);
if (process.platform === 'darwin') {
  writeFileSync(join(folder, 'Parlot Explorer.app', 'Contents', 'Info.plist'), `<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict>
<key>CFBundleName</key><string>Parlot Explorer</string>
<key>CFBundleDisplayName</key><string>Parlot Explorer</string>
<key>CFBundleIdentifier</key><string>org.parlot.explorer</string>
<key>CFBundleExecutable</key><string>parlot-explorer-desktop</string>
<key>CFBundlePackageType</key><string>APPL</string>
<key>CFBundleShortVersionString</key><string>0.1.0</string>
<key>CFBundleVersion</key><string>1</string>
<key>NSHighResolutionCapable</key><true/>
<key>NSAppTransportSecurity</key><dict><key>NSAllowsLocalNetworking</key><true/></dict>
</dict></plist>`);
  const signed = spawnSync('codesign', ['--force', '--sign', '-', join(folder, 'Parlot Explorer.app')], { stdio: 'inherit' });
  if (signed.status !== 0) process.exit(signed.status || 1);
}
console.log(`Desktop shell: ${executable}`);
