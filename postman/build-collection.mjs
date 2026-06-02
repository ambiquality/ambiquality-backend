#!/usr/bin/env node
// Assembles the git-native Postman collection (one YAML file per request, under
// collections/ambiquality-api/) into a single Postman v2.1 collection.json that
// `newman` can run, plus a newman environment JSON converted from an environment
// YAML. The git-native YAML is the version-controlled source of truth; this build
// step exists only because newman cannot run the directory format directly.
//
//   node postman/build-collection.mjs [environmentName]
//
// Defaults to the `ambiquality-dev` environment. Outputs:
//   /tmp/ambiquality.collection.json
//   /tmp/ambiquality.env.json
import { readFileSync, readdirSync, writeFileSync, existsSync } from "node:fs";
import { join, dirname, basename } from "node:path";
import { fileURLToPath } from "node:url";
import yaml from "js-yaml";

const here = dirname(fileURLToPath(import.meta.url));
const collectionDir = join(here, "collections", "ambiquality-api");
const envName = process.argv[2] || "ambiquality-dev";

// Collection-level bearer auth, baked onto each inheriting request (set below).
let collectionAuth;

const load = (p) => yaml.load(readFileSync(p, "utf8"));
const orderOf = (dir) => {
  const def = join(dir, ".resources", "definition.yaml");
  return existsSync(def) ? load(def)?.order ?? 0 : 0;
};

// --- map one *.request.yaml file to a Postman v2.1 item --------------------
function toItem(filePath) {
  const r = load(filePath);
  const name = basename(filePath).replace(/\.request\.yaml$/, "");

  const request = { method: r.method, header: [], url: r.url };
  for (const [key, value] of Object.entries(r.headers ?? {}))
    request.header.push({ key, value: String(value) });

  if (r.body?.type === "json")
    request.body = {
      mode: "raw",
      raw: r.body.content,
      options: { raw: { language: "json" } },
    };

  // Auth: an explicit `noauth` stays no-auth; anything else inherits the
  // collection bearer. newman does not auto-apply collection-level auth to
  // inheriting requests, so we bake it onto each request here.
  if (r.auth?.type === "noauth") request.auth = { type: "noauth" };
  else if (collectionAuth) request.auth = collectionAuth;

  const event = [];
  for (const s of r.scripts ?? []) {
    const listen = s.type.includes("afterResponse") ? "test" : "prerequest";
    event.push({ listen, script: { type: "text/javascript", exec: String(s.code).split("\n") } });
  }

  return { name, request, event, _order: r.order ?? 0 };
}

// --- map one folder directory to a Postman v2.1 folder item ----------------
function toFolder(dir) {
  const items = readdirSync(dir)
    .filter((f) => f.endsWith(".request.yaml"))
    .map((f) => toItem(join(dir, f)))
    .sort((a, b) => a._order - b._order)
    .map(({ _order, ...item }) => item);
  return { name: basename(dir), item: items, _order: orderOf(dir) };
}

// --- collection root -------------------------------------------------------
const def = load(join(collectionDir, ".resources", "definition.yaml"));

const variable = Object.entries(def.variables ?? {}).map(([key, value]) => ({
  key,
  value: String(value),
}));

const event = (def.scripts ?? []).map((s) => ({
  listen: s.type.includes("afterResponse") ? "test" : "prerequest",
  script: { type: "text/javascript", exec: String(s.code).split("\n") },
}));

const bearer = (def.auth ?? []).find((a) => a.type === "bearer");
if (bearer)
  collectionAuth = { type: "bearer", bearer: [{ key: "token", value: bearer.credentials.token, type: "string" }] };

const folders = readdirSync(collectionDir, { withFileTypes: true })
  .filter((d) => d.isDirectory() && !d.name.startsWith("."))
  .map((d) => toFolder(join(collectionDir, d.name)))
  .sort((a, b) => a._order - b._order)
  .map(({ _order, ...folder }) => folder);

const collection = {
  info: {
    name: def.name,
    description: def.description,
    schema: "https://schema.getpostman.com/json/collection/v2.1.0/collection.json",
  },
  item: folders,
  variable,
  ...(event.length ? { event } : {}),
  ...(collectionAuth ? { auth: collectionAuth } : {}),
};

// --- environment -----------------------------------------------------------
// Ship only env keys the collection does NOT already define (e.g. mailpitBaseUrl).
// In newman an environment variable SHADOWS the collection variable of the same
// name, so carrying the collection's own keys here would override the values the
// test scripts set via pm.collectionVariables.set (e.g. {{accessToken}} after
// Login, or the per-run unique {{email}} from Register). Collection-defined keys
// therefore resolve from collection scope — their defaults in definition.yaml
// (baseUrl, email, password) plus whatever the scripts overwrite at run time.
const envYaml = load(join(here, "environments", `${envName}.environment.yaml`));
const collectionVarNames = new Set(Object.keys(def.variables ?? {}));
const environment = {
  name: envYaml.name ?? envName,
  values: (envYaml.values ?? [])
    .filter((v) => !collectionVarNames.has(v.key))
    .map((v) => ({
      key: v.key,
      value: v.value,
      enabled: v.enabled !== false,
      type: "default",
    })),
};

const collectionOut = "/tmp/ambiquality.collection.json";
const envOut = "/tmp/ambiquality.env.json";
writeFileSync(collectionOut, JSON.stringify(collection, null, 2));
writeFileSync(envOut, JSON.stringify(environment, null, 2));

const count = folders.reduce((n, f) => n + f.item.length, 0);
console.log(`Built ${count} requests in ${folders.length} folders.`);
console.log(`  collection -> ${collectionOut}`);
console.log(`  environment (${environment.name}) -> ${envOut}`);
console.log(`Run: newman run ${collectionOut} -e ${envOut}`);
