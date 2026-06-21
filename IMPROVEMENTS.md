# te1000-mcp — Improvement List

Running list of papercuts and enhancement ideas discovered while using the server
on real TwinCAT projects. Newest first.

---

## 2026-06-20 — Renaming IO tree items is a token sink (no `rename`, `set_xml` echoes full XML)

### What I was doing
Bulk-renaming EtherCAT IO boxes/terminals in the CabSort project to the
`R{rack}.{area}.N{node} (partno)` convention — 15 items across three coupler chains
(EOAT boxes, OMAL terminals, LDR terminals). Conceptually this is 15 string
assignments. In practice it cost an estimated **~225k+ tokens** and forced me to
offload the bulk to a subagent just to keep the main context from being buried.

### The pitfalls

1. **There is no first-class rename.** The obvious affordance — the `newName`
   parameter on `tc_tree` — is silently inert for this purpose. It is wired only to
   the `import` action (`importAsName`); for an existing item it does nothing. See
   `index.js` `tc_tree` handler: `newName` only appears in `case "import"`.
   - `action:"rename"` fails Zod enum validation (the enum is
     `get|children|exists|get_xml|set_xml|create|delete|import|export|focus`).
   - `set_xml` with `newName` (no `xml`) errors with `'xml' is required`.
   - So the caller has to *discover by probing* that the only working path is
     `set_xml` (ConsumeXml) with a hand-built `<TreeItem><ItemName>...</ItemName></TreeItem>`.
     Each failed probe is a wasted round-trip.

2. **`set_xml` returns the entire `ProduceXml()` of the item.** See
   `te1000-bridge.ps1` `twincat_set_tree_item_xml`: after `ConsumeXml`, it returns
   `xml = $item.ProduceXml()`. For an EtherCAT slave that blob includes the full
   TxPdo/RxPdo map, the entire CoE/EEPROM/FMMU/SM init-command sequence, DC opmodes,
   **and an embedded bitmap (`TreeImageData16x14`)** — roughly **12–16k tokens per
   item**. A rename only needs to set one string, but you pay the full blob back on
   every single call.

3. **Inspecting the tree to plan the rename is also expensive.** `get_xml` is the
   same ~15k-token blob, so even *looking* at one item to confirm structure before
   renaming is costly. There's no cheap "identity only" view.

4. **Net effect.** 15 renames × ~15k token echo ≈ 225k tokens of pure return payload
   that is immediately discarded, plus the discovery probes and an exploratory
   `get_xml`. The operation is semantically trivial but blows a context budget and
   pushed me into spawning a subagent purely for damage control.

### Improvement ideas (roughly priority-ordered)

1. **Add a dedicated `rename` action** to `tc_tree`, wiring the already-present
   `newName` param to it. Implement a bridge verb `twincat_rename_tree_item` that
   sets the name and returns a **compact** result only:
   `{ ok, treePath, newName, newPath }` — no `ProduceXml`.
   - Implementation that is already proven to work and keeps IO links intact:
     ConsumeXml a minimal `<TreeItem><ItemName>$newName</ItemName></TreeItem>`
     (this updates *both* the tree `ItemName` and the internal EtherCAT
     `Info/Name` CDATA), then read back `$item.PathName` for `newPath`.
   - (`$item.Name = $newName` may also work via the Automation Interface, but the
     minimal-ConsumeXml route is the one verified on EtherCAT slaves here.)

2. **Stop echoing the full XML from `set_xml` by default.** Return a compact
   `{ ok, treePath }` (or `{ ok, treePath, newPath }`). Add an opt-in
   `returnXml: boolean` (default `false`) for callers who genuinely want the
   produced XML. This single change removes the per-call blow-up for *all*
   ConsumeXml uses, not just renames.

3. **Always strip `TreeImageData16x14`** (the embedded bitmap) from any XML the
   server returns. It's never useful to a model and is pure token cost in both
   `get_xml` and any `returnXml` path.

4. **Add a cheap identity view to `get_xml`** — e.g. `summary:true` (or a `fields`
   selector) that returns just `ItemName / PathName / ItemSubTypeName / ChildCount`
   without the PDO/CoE payload, so the tree can be inspected before edits without
   paying the full blob.

5. **Document the rename recipe** in `README.md` and the `tc_tree` tool description
   so future callers don't have to probe to find it.

6. **(Optional) Bulk rename helper** — racks are renamed in sequential runs
   (`Nxx` incrementing along a coupler chain), so a batch form that takes
   `[{path, newName}, ...]` and returns one compact array would fit the real usage
   pattern and cut round-trips.

The two highest-leverage, lowest-risk fixes are **#1 (dedicated `rename`)** and
**#2 (compact `set_xml` by default)**.
