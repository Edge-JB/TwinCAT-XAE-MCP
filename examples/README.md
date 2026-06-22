# Examples

Copy-pasteable recipes for driving TwinCAT through `te1000-mcp`. They assume an XAE Shell is
**already open** on your solution and the server is wired into your MCP client (see
[`mcp-config.json`](mcp-config.json)).

Each recipe shows the tool name followed by its arguments. Tree paths use `^` separators — see
the [tool reference](../docs/tools.md) for path roots and batch semantics.

> 🔒 Actions marked here as guarded require a `confirm` token and touch the live target. Read
> [Safety & guards](../README.md#safety--guards) before running them on a real cell.

---

## 1. Sanity check the session

```text
xae   action: "status"
```

Returns whether the server is attached and a solution is open. Then inspect the IO tree:

```text
tc_tree   action: "children"   path: "TIID^Device 2 (EtherCAT)"
```

## 2. Build an EtherCAT rack from ESI

Add a coupler's worth of terminals in one call. Modules are created left-to-right in array
order, fully populated from each device's ESI, and the solution is saved once at the end.

```text
tc_ethercat
  racks: [
    {
      parent: "TIID^Device 2 (EtherCAT)^R01.Main.N01 (EK1200)",
      modules: [
        { type: "EL1008", name: "R01.Main.N15 (EL1008)" },
        { type: "EL2008" },
        { type: "EL3064" },
        { type: "EL4004" },
        { type: "EL6224" }
      ]
    }
  ]
  save: true
```

Pin an older ESI revision per module with `revision: "0000-0017"` (decimal product-variant and
revision) or the full string `revision: "EL1008-0000-0017"`.

## 3. Bulk-link PLC variables to IO channels

One DTE attach, continue-on-error. Each pair reports the resolved `^`-paths it linked through.

```text
tc_link
  action: "link_batch"
  save: true
  links: [
    { a: "TIPC^Cabsort Lite^Cabsort Lite Instance^PlcTask Inputs^MAIN.bStart",
      b: "TIID^Device 2 (EtherCAT)^Term 1^Channel 1^Input" },
    { a: "TIPC^Cabsort Lite^Cabsort Lite Instance^PlcTask Outputs^MAIN.bRun",
      b: "TIID^Device 2 (EtherCAT)^Term 2^Channel 1^Output" }
  ]
```

Verify a link afterwards (discover → act → **verify**):

```text
tc_link
  action: "links"
  a: "TIID^Device 2 (EtherCAT)^Term 1^Channel 1^Input"
```

If a path is rejected, resolve the valid alternatives:

```text
tc_link   action: "resolve"   a: "TIPC^MyPlc^MyPlc Instance^PlcTask Inputs^MAIN.stSlot02_DI^In00"
```

## 4. Edit terminal parameters via ProduceXml / ConsumeXml

The general read-modify-write loop for any tree item's settings:

```text
# 1. read current XML (or summary:true for just identity + slot modules)
tc_tree   action: "get_xml"   path: "TIID^Device 2 (EtherCAT)^Box 1^Term 5^Channel 1^PAI Settings"

# 2. ...edit the returned XML in your agent...

# 3. push it back (use set_xml_batch for several items in one attach)
tc_tree
  action: "set_xml"
  path: "TIID^Device 2 (EtherCAT)^Box 1^Term 5^Channel 1^PAI Settings"
  xml:  "<TreeItem>…edited…</TreeItem>"

# 4. save
xae   action: "save_all"
```

## 5. Author and surgically edit a POU (offline)

`plc_pou` edits land in memory — they reach the cell only via a later guarded `plc_download`.

```text
# create a program POU
plc_pou
  action: "create"
  parent: "TIPC^Cabsort Lite^Cabsort Lite Project^POUs"
  name: "pConveyor"
  pouType: "program"
  language: "ST"

# read only the lines you need
plc_pou   action: "get_impl"   path: "…^POUs^pConveyor"   grep: { pattern: "bRun", context: 2 }

# surgical, anchored replace — fails without writing if the anchor isn't unique
plc_pou
  action: "replace"
  path: "…^POUs^pConveyor"
  find: "bRun := FALSE;"
  replace: "bRun := bStart AND NOT bFault;"
  expectCount: 1
  validate: true
```

## 6. Safe build → activate → download flow

```text
xae_build   action: "build"        # compile only — no effect on the live cell yet

# inspect results before going further
xae         action: "error_list"

# the next three touch the LIVE target and are each guarded:
twincat_activate_configuration   confirm: "ALLOW_TWINCAT_ACTIVATE"   # 🔒
plc_download                     confirm: "ALLOW_PLC_DOWNLOAD"       # 🔒  (auto-logs-out first)
twincat_restart_runtime          confirm: "ALLOW_TWINCAT_RESTART"    # 🔒
```

> Leave work at "build green, not activated" unless you intend to interrupt the running machine.
> A green build does **not** change the live cell.

## 7. Preview before you delete

Destructive batches are guarded. Preview first, then commit:

```text
# dryRun reports which children exist — deletes nothing
tc_tree
  action: "delete_batch"
  dryRun: true
  deletes: [ { parent: "TIID^Device 2 (EtherCAT)", name: "Box 7" } ]

# commit
tc_tree
  action: "delete_batch"
  confirm: "ALLOW_TWINCAT_DELETE"
  deletes: [ { parent: "TIID^Device 2 (EtherCAT)", name: "Box 7" } ]
```
