import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

/** Unicode «Extended_Pictographic» (emoji); excludes normal CJK letters. */
const emojiRe = /\p{Extended_Pictographic}/u;

const here = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(here, "..", "src");

function walk(dir, out) {
  for (const ent of fs.readdirSync(dir, { withFileTypes: true })) {
    const p = path.join(dir, ent.name);
    if (ent.isDirectory()) {
      if (ent.name === "node_modules" || ent.name === "dist") continue;
      walk(p, out);
    } else if (/\.(tsx?|jsx?)$/.test(ent.name) && !p.includes(path.join("api", "sdk"))) {
      const text = fs.readFileSync(p, "utf8");
      if (emojiRe.test(text)) out.push(p);
    }
  }
}

const hits = [];
if (fs.existsSync(root)) walk(root, hits);

if (hits.length > 0) {
  console.error("Emoji / pictographic characters are not allowed in platform-admin src. Use SVG icons instead.");
  for (const f of hits) console.error(`  - ${f}`);
  process.exit(1);
}

console.log("No emoji in platform-admin src (check passed).");
