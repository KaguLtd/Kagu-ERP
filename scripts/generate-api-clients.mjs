import { existsSync, readFileSync, readdirSync, rmSync, writeFileSync } from "node:fs";
import { dirname, extname, isAbsolute, relative, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { spawnSync } from "node:child_process";

const repositoryRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const outputDirectories = [
  resolve(repositoryRoot, "packages/api-client-ts"),
  resolve(repositoryRoot, "apps/android/generated/api-client"),
];
const generatedTextExtensions = new Set([
  "",
  ".gradle",
  ".json",
  ".kt",
  ".md",
  ".properties",
  ".ts",
  ".xml",
  ".yaml",
  ".yml",
]);

function normalizeGeneratedText(directory) {
  for (const entry of readdirSync(directory, { withFileTypes: true })) {
    const entryPath = resolve(directory, entry.name);
    if (entry.isDirectory()) {
      normalizeGeneratedText(entryPath);
      continue;
    }

    if (!entry.isFile()) {
      continue;
    }

    if (!generatedTextExtensions.has(extname(entry.name))) {
      continue;
    }

    const source = readFileSync(entryPath, "utf8");
    const normalized = `${source.replace(/[ \t]+$/gm, "").trimEnd()}\n`;
    if (normalized !== source) {
      writeFileSync(entryPath, normalized, "utf8");
    }
  }
}

const generatorEntrypoint = resolve(
  repositoryRoot,
  "node_modules/@openapitools/openapi-generator-cli/main.js",
);
const preflight = spawnSync(process.execPath, [generatorEntrypoint, "version"], {
  cwd: repositoryRoot,
  stdio: "inherit",
});
if (preflight.error) {
  throw preflight.error;
}
if (preflight.status !== 0) {
  process.exitCode = preflight.status ?? 1;
  process.exit();
}

for (const outputDirectory of outputDirectories) {
  const repositoryRelativePath = relative(repositoryRoot, outputDirectory);
  if (
    repositoryRelativePath.length === 0 ||
    repositoryRelativePath.startsWith("..") ||
    isAbsolute(repositoryRelativePath)
  ) {
    throw new Error(`Refusing to clean unsafe generated-client path: ${outputDirectory}`);
  }

  if (!existsSync(outputDirectory)) {
    continue;
  }

  for (const entry of readdirSync(outputDirectory)) {
    if (entry === "node_modules") {
      continue;
    }

    rmSync(resolve(outputDirectory, entry), { recursive: true, force: true });
  }
}

const result = spawnSync(process.execPath, [generatorEntrypoint, "generate"], {
  cwd: repositoryRoot,
  stdio: "inherit",
});
if (result.error) {
  throw result.error;
}
if (result.status !== 0) {
  process.exitCode = result.status ?? 1;
  process.exit();
}

for (const outputDirectory of outputDirectories) {
  normalizeGeneratedText(outputDirectory);
}

const typeScriptPackage = {
  name: "@kaguerp/api-client",
  version: "0.1.0",
  private: true,
  type: "module",
  sideEffects: false,
  exports: {
    ".": {
      types: "./dist/index.d.ts",
      default: "./dist/index.js",
    },
  },
  scripts: {
    build: "tsc -p tsconfig.json",
    typecheck: "tsc -p tsconfig.json --noEmit",
  },
  devDependencies: {
    typescript: "catalog:",
  },
};
writeFileSync(
  resolve(repositoryRoot, "packages/api-client-ts/package.json"),
  `${JSON.stringify(typeScriptPackage, null, 2)}\n`,
  "utf8",
);

const typeScriptConfiguration = {
  compilerOptions: {
    declaration: true,
    lib: ["DOM", "ES2022"],
    module: "ESNext",
    moduleResolution: "Bundler",
    outDir: "dist",
    rootDir: "src",
    strict: true,
    target: "ES2022",
  },
  include: ["src"],
};
writeFileSync(
  resolve(repositoryRoot, "packages/api-client-ts/tsconfig.json"),
  `${JSON.stringify(typeScriptConfiguration, null, 2)}\n`,
  "utf8",
);
rmSync(resolve(repositoryRoot, "packages/api-client-ts/tsconfig.esm.json"), { force: true });
