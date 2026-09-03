import { readFileSync } from "node:fs";
import { gzipSync } from "node:zlib";

const manifest = JSON.parse(readFileSync(new URL("../dist/.vite/manifest.json", import.meta.url), "utf8"));
const entryRecord = Object.entries(manifest).find(([, asset]) => asset?.isEntry);
const [entryKey, entry] = entryRecord ?? [];

if (!entryKey || !entry?.isEntry) {
  throw new Error("Vite manifest does not contain an application entry.");
}

const initialChunks = new Set();
const visit = (key) => {
  if (initialChunks.has(key)) return;
  initialChunks.add(key);
  for (const dependency of manifest[key]?.imports ?? []) visit(dependency);
};
visit(entryKey);

let rawBytes = 0;
let gzipBytes = 0;
for (const key of initialChunks) {
  const asset = manifest[key];
  if (!asset?.file?.endsWith(".js")) continue;
  const content = readFileSync(new URL(`../dist/${asset.file}`, import.meta.url));
  rawBytes += content.byteLength;
  gzipBytes += gzipSync(content).byteLength;
  if (content.includes(Buffer.from("monaco-editor"))) {
    throw new Error(`Initial JavaScript unexpectedly contains Monaco: ${asset.file}`);
  }
}

const rawLimit = 1024 * 1024;
const gzipLimit = 350 * 1024;
console.log(`Initial JavaScript: ${(rawBytes / 1024).toFixed(1)} KiB raw, ${(gzipBytes / 1024).toFixed(1)} KiB gzip`);

if (rawBytes > rawLimit || gzipBytes > gzipLimit) {
  throw new Error(`Initial JavaScript exceeds budget (${rawLimit / 1024} KiB raw / ${gzipLimit / 1024} KiB gzip).`);
}
