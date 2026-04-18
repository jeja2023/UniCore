/* global console, process */
/**
 * 在 Windows/Linux/macOS 下由 npm 调用 PowerShell 脚本。
 * 优先使用 pwsh（PowerShell 7+）；在 Windows 上若没有 pwsh，
 * 则回退到 Windows PowerShell 5.1（npm 常在 cmd.exe 下运行）。
 */
import { spawnSync } from "node:child_process";
import { platform } from "node:os";

const passthrough = process.argv.slice(2);
const candidates = platform() === "win32" ? ["pwsh", "powershell"] : ["pwsh"];

for (const exe of candidates) {
  const result = spawnSync(exe, passthrough, {
    encoding: "utf8",
    stdio: ["inherit", "pipe", "pipe"],
  });
  if (result.error) {
    if (result.error.code === "ENOENT") continue;
    console.error(result.error.message);
    process.exit(1);
  }
  if (result.stdout) process.stdout.write(result.stdout);
  if (result.stderr) process.stderr.write(result.stderr);
  process.exit(result.status ?? 0);
}

console.error(
  "未找到 PowerShell。请先安装 PowerShell 7（https://aka.ms/powershell），或确保 Windows PowerShell 可通过 `powershell` 命令在 PATH 中访问。",
);
process.exit(1);
