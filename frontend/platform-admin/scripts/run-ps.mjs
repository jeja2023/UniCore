/**
 * Run a PowerShell script from npm on Windows/Linux/macOS.
 * Prefers pwsh (PowerShell 7+); on Windows falls back to Windows PowerShell 5.1
 * when pwsh is not installed (npm often runs under cmd.exe).
 */
import { spawnSync } from "node:child_process";
import { platform } from "node:os";

const passthrough = process.argv.slice(2);
const candidates = platform() === "win32" ? ["pwsh", "powershell"] : ["pwsh"];

for (const exe of candidates) {
  const result = spawnSync(exe, passthrough, { stdio: "inherit" });
  if (result.error) {
    if (result.error.code === "ENOENT") continue;
    console.error(result.error.message);
    process.exit(1);
  }
  process.exit(result.status ?? 0);
}

console.error(
  "PowerShell not found. Install PowerShell 7 from https://aka.ms/powershell or ensure Windows PowerShell is available as `powershell` on PATH.",
);
process.exit(1);
