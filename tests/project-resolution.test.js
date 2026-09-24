"use strict";

const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");

const root = path.resolve(__dirname, "..");
const daemonRoot = path.join(root, "daemon");

function read(relativePath) {
  return fs.readFileSync(path.join(root, relativePath), "utf8");
}

function csharpFiles(directory) {
  return fs.readdirSync(directory, { withFileTypes: true }).flatMap((entry) => {
    const fullPath = path.join(directory, entry.name);
    if (entry.isDirectory()) {
      return csharpFiles(fullPath);
    }
    return entry.isFile() && entry.name.endsWith(".cs") ? [fullPath] : [];
  });
}

const helper = read("daemon/PlcProjectHelper.cs");
const pouActions = read("daemon/Actions/PlcPouActions.cs");
const taskActions = read("daemon/Actions/TaskActions.cs");
const downloadActions = read("daemon/Actions/SessionDownloadActions.cs");

assert.match(
  helper,
  /\(\(ITcProjectRoot\)\(object\)root\)\.NestedProject/,
  "the resolver must obtain the IEC project through ITcProjectRoot.NestedProject"
);
assert.match(
  helper,
  /projectPath = nestedItem\.PathName/,
  "the resolver must prefer the authoritative nested tree-item path"
);
assert.match(
  helper,
  /projectPath = plcPath \+ "\^" \+ projectName/,
  "the resolver fallback must use the actual nested project name"
);

for (const projectName of ["Example Project", "Example Projekt", "Arbitrary IEC Name"]) {
  const plcPath = "TIPC^Example";
  assert.equal(
    plcPath + "^" + projectName,
    ["TIPC^Example", projectName].join("^"),
    "nested project path composition must preserve the actual name"
  );
}

for (const file of csharpFiles(daemonRoot)) {
  const source = fs.readFileSync(file, "utf8");
  assert.doesNotMatch(
    source,
    /(?:\+\s*" Project"|EndsWith\(" Project"|\\sProject\$)/,
    path.relative(root, file) + " must not infer IEC project identity from an English suffix"
  );
}

assert.match(pouActions, /CheckObjects[\s\S]*PlcProjectHelper\.Resolve/);
assert.match(pouActions, /GetPlcProjectNodePath[\s\S]*PlcProjectHelper\.Resolve/);
assert.match(pouActions, /AddValidateResult[\s\S]*PlcProjectHelper\.Resolve/);
assert.match(taskActions, /ResolvePlcTaskRefCandidates[\s\S]*PlcProjectHelper\.Resolve/);
assert.match(downloadActions, /SelectPlcProjectInSolutionExplorer[\s\S]*PlcProjectHelper\.Resolve/);
assert.match(pouActions, /data\["projectPath"\] = project\.ProjectPath/);
assert.match(pouActions, /data\["instancePath"\] = project\.ProjectPath/);

console.log("project-resolution regression checks passed");
