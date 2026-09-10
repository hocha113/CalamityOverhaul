# External QuestLogs / Entrust API

Outbound contract for other mods. Zero compile reference required.

```csharp
Mod cwr = ModLoader.GetMod("CalamityOverhaul");
if (cwr?.Call("QuestLogs.SupportsExternal") is true) { /* ... */ }
```

Optional compile-ref facades: `CalamityOverhaul.API.QuestLogsAPI`, `CalamityOverhaul.API.EntrustAPI`. `Mod.Call` is a thin string router.

CWR does not `weakReferences` any consumer and does not hardcode foreign nodes.

## Commands

| Command | Args | Returns |
|---------|------|---------|
| `QuestLogs.SupportsExternal` | — | `true` |
| `QuestLogs.RegisterNode` | `Dictionary<string, object>` | `bool` |
| `QuestLogs.AddReward` | dict `{id, item, stack}` or `(id, itemId, stack)` | `bool` |
| `QuestLogs.IsCompleted` | dict `{player?, id}` or `(player, id)` / `(id)` | `bool` |
| `QuestLogs.SetObjectiveProgress` | dict `{player?, id, progress}` or `(player, id, progress01)` | `bool` |
| `Entrust.SupportsExternal` | — | `true` |
| `Entrust.Register` | `Dictionary<string, object>` | `bool` |
| `Entrust.SetProgress` | dict `{key, progress, status?}` or `(key, progress01, status?)` | `bool` |
| `Entrust.SetStatus` | dict `{key, status, progress?}` or `(key, status, progress?)` | `bool` |
| `Entrust.Unregister` | `(key)` | `bool` |
| `Entrust.Get` | `(key)` | `Dictionary` or `null` |

Invalid args, unreadiness, or duplicate id/key: `false` / `null`. Never throws into the caller's `Load`. One warn log inside CWR.

## `QuestLogs.RegisterNode` keys

| Key | Type | Notes |
|-----|------|-------|
| `id` | string | Required. Prefix with your mod name (`CalamityEntropy.Cruiser`). Collision returns `false`. |
| `title` / `summary` / `detailed` | `LocalizedText` or string | Caller-owned. Strings are a runtime shell, not `QuestLogs.hjson`. |
| `position` or `x`/`y` | `Vector2` / floats | Relative to the first parent. Missing parent → absolute. |
| `parents` / `parent` | string or `string[]` | e.g. `"FirstQuest"`. |
| `icon` | asset path | Or `iconItem` / `iconNpc`. |
| `type` | `QuestType` / name / int | Default `Side`. |
| `complete` | `Func<Player, bool>` or `Func<Player, float>` | Read already-synced world flags. |
| `unlock` | `Func<Player, bool>` | Extra unlock gate. Optional. |
| `hiddenUntilUnlocked` | bool | Optional. |
| `countsTowardCompletionist` | bool | **Default `false`.** |
| `rewards` | `{item, stack}` list | `itemId <= 0` ignored. |

Player save is `QLPlayer.QuestProgress[id]`. No extra tag.

Late register writes `_quests` + `Instances` (does not need Autoload / `VaultTypeRegistry`). CWR unload clears the table; Call again after the next Load.

## `Entrust.Register` keys

| Key | Type | Notes |
|-----|------|-------|
| `key` / `id` | string | Required. Prefix with your mod name. |
| `title` / `summary` / `category` | `LocalizedText` or string | Caller-owned. |
| `progress` | float 0–1 | Optional. |
| `status` | string | `Active` `Tracked` `Suspended` `Completed` `Failed`. |
| `priority` | int | Higher sorts first. |
| `provider` / `providerName` | `LocalizedText` or string | One-off provider. Not added to the official seven-person table. |
| `providerColor` / `color` | `Color` or `{r,g,b}` | Optional. |

v1 is display + progress + status. No contribution net, no rewards through the dossier.

## Lifecycle

- Quest nodes last for the CWR load session. Re-Call after a CWR reload.
- Entrust is wiped on world unload (`ClearAll`). **Re-register after entering a world.** CWR will not re-pump foreign entries.
- Dedicated server: `Entrust.*` writes are client-only no-ops (`false`). Quest node register may run on the server; `QLPlayer` updates are client-side.

## Completionist

CWR autoload nodes still count. External nodes default to not counting.

## Out of scope (v1)

Entrust reward grants, contribution / damage net, new packets, hunting-chronicle auto-ingest.
