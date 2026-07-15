// KSPProfiler CSV 내보내기를 감시해 미리 입력한 라벨로 자동 개명하는 콘솔 도구 (수동 파일명 변경 대체)
// Usage: node scripts/label-profiler.js [KSP root]
//   (root defaults to the KSP_ROOT env var, then the dev install path)
// Type a label at the prompt BEFORE exporting in-game; every subsequent export is renamed to
// <original>_<label>.csv the moment it lands. Empty label = HHMMSS timestamp. Repeats get -2, -3.
"use strict";
const fs = require("fs");
const path = require("path");
const readline = require("readline");

const root = process.argv[2] || process.env.KSP_ROOT || "F:\\project\\2026\\KSP_win64";
if (!fs.existsSync(root)) {
  console.error("KSP root not found: " + root);
  console.error("Usage: node scripts/label-profiler.js [KSP root]");
  process.exit(1);
}

let label = "";
const mine = new Set();      // files this session produced — never re-rename our own output
const pending = new Map();   // debounce timers per filename (exports are written in chunks)

const rl = readline.createInterface({ input: process.stdin, output: process.stdout, prompt: "label> " });
console.log("감시 중: " + root);
console.log("라벨을 입력해 두면 다음 내보내기부터 그 라벨로 개명됩니다. 빈 입력 = 시각(HHMMSS) 라벨. Ctrl+C 종료.");
rl.prompt();
rl.on("line", function (l) {
  label = l.trim().replace(/[\\/:*?"<>|]/g, "-");   // 파일명 금지 문자만 정리
  console.log(label ? "  다음 내보내기 라벨: " + label : "  라벨 비움 — 시각 라벨 사용");
  rl.prompt();
});
rl.on("close", function () { process.exit(0); });

fs.watch(root, function (ev, name) {
  if (!name || !/^KSPProfiler.*\.csv$/i.test(name)) return;
  if (mine.has(name)) return;
  clearTimeout(pending.get(name));
  pending.set(name, setTimeout(function () { pending.delete(name); handle(name, 0); }, 400));
});

function stamp() {
  const d = new Date();
  return [d.getHours(), d.getMinutes(), d.getSeconds()].map(function (n) { return String(n).padStart(2, "0"); }).join("");
}

function handle(name, retries) {
  const src = path.join(root, name);
  if (!fs.existsSync(src)) return;                  // already renamed / removed
  const base = name.replace(/\.csv$/i, "");
  const lab = label || stamp();
  let target = base + "_" + lab + ".csv";
  let n = 2;
  while (fs.existsSync(path.join(root, target))) target = base + "_" + lab + "-" + (n++) + ".csv";
  try {
    fs.renameSync(src, path.join(root, target));
  } catch (e) {
    // 내보내기가 아직 파일을 잡고 있으면(EBUSY/EPERM) 잠깐 뒤 재시도
    if (retries < 5) { setTimeout(function () { handle(name, retries + 1); }, 500); return; }
    console.log("  rename failed (" + e.code + "): " + name);
    rl.prompt();
    return;
  }
  mine.add(target);
  console.log("[" + new Date().toLocaleTimeString() + "] " + name + " → " + target + summary(path.join(root, target)));
  rl.prompt();
}

// 개명 직후 즉석 요약: 방금 그 런이 캡처됐는지 눈으로 확인용
function summary(p) {
  try {
    const lines = fs.readFileSync(p, "utf8").split(/\r?\n/);
    const pick = function (key) {
      const l = lines.find(function (x) { return x.indexOf(key + ";") === 0; });
      return l ? l.split(";")[1] : null;
    };
    const fps = pick("FPS"), ft = pick("Frame time");
    return fps ? "   (FPS " + fps + " · " + ft + " ms)" : "";
  } catch (e) { return ""; }
}
