# External QuestLogs / Entrust API · 任务书与委托卷宗对外接口

Outbound contract for other mods. Zero compile reference required.
给其他模组的对外契约，不需要编译引用本模组。

```csharp
Mod cwr = ModLoader.GetMod("CalamityOverhaul");
if (cwr?.Call("QuestLogs.SupportsExternal") is true) { /* ... */ }
```

Optional compile-ref facades: `CalamityOverhaul.API.QuestLogsAPI`, `CalamityOverhaul.API.EntrustAPI`. `Mod.Call` is a thin string router over the same code.
愿意编译引用的模组可以直接用同名门面类，`Mod.Call` 只是字符串路由。

CWR does not `weakReferences` any consumer and does not hardcode foreign nodes.
本模组不弱引用任何消费方，也不写死任何外模节点。

## Commands · 命令表

| Command | Args | Returns |
|---------|------|---------|
| `QuestLogs.SupportsExternal` | — | `true` |
| `QuestLogs.RegisterNode` | `Dictionary<string, object>` | `bool` |
| `QuestLogs.AddReward` | dict `{id, item, stack}` or `(id, itemId, stack)` | `bool` |
| `QuestLogs.IsCompleted` | dict `{player?, id}` or `(player, id)` / `(id)` | `bool` |
| `QuestLogs.SetObjectiveProgress` | dict `{player?, id, progress}` or `(player, id, progress01)` / `(id, progress01)` | `bool` |
| `Entrust.SupportsExternal` | — | `true` |
| `Entrust.Register` | `Dictionary<string, object>` | `bool` |
| `Entrust.SetProgress` | dict `{key, progress, status?}` or `(key, progress01, status?)` | `bool` |
| `Entrust.SetStatus` | dict `{key, status, progress?}` or `(key, status, progress?)` | `bool` |
| `Entrust.Unregister` | `(key)` | `bool` |
| `Entrust.Get` | `(key)` | `Dictionary` or `null` |

Invalid args, unreadiness, or duplicate node id: `false` (`null` for `Entrust.Get`). Never throws into the caller. Failures that mean a caller bug get one warn log inside CWR; expected states (entrust key absent after a world unload) are silent.
参数错误、未就绪、节点 ID 重复：返回 `false`（`Entrust.Get` 返回 `null`），不会把异常抛回调用方。属于调用方 bug 的失败在本模组记一条 Warn；预期状态（出世界后委托 Key 不存在）不记日志。

## When to call · 调用时机

- `QuestLogs.RegisterNode`: from your `PostSetupContent` **or later**. Calling from `Load()` is rejected with `false` when CWR has not autoloaded its own nodes yet (an unprefixed id registered that early would silently shadow a CWR node).
  从你的 `PostSetupContent` 起再调。CWR 自有节点尚未入表时（例如你在 `Load()` 里调且比 CWR 先加载）会直接返回 `false`。
- Nodes live for the CWR load session. All mods reload together, so re-register from the same hook next time.
  节点随 CWR 加载会话存活，所有模组一起重载，下次仍从同一钩子注册即可。
- `Entrust.*`: client only, in-world. Entries are wiped on world unload, re-register after entering a world (see Lifecycle).
  委托只在客户端、进世界后调；出世界会清空，进世界后要再注册。
- Do not call `RegisterNode` from inside a `complete` / `unlock` predicate: predicates run while CWR is iterating its node table.
  不要在 `complete` / `unlock` 谓词里再调 `RegisterNode`，谓词执行时 CWR 正在遍历节点表。

## `QuestLogs.RegisterNode` keys · 参数

| Key | Type | Notes |
|-----|------|-------|
| `id` | string | Required. Prefix with your mod name (`CalamityEntropy.Cruiser`). Collision returns `false`. 必填，带模组名前缀。 |
| `title` / `summary` / `detailed` | `LocalizedText` or string | Caller-owned. Prefer `LocalizedText`. String literals are wrapped at runtime and never written into any hjson. 文案归调用方，优先传 `LocalizedText`。 |
| `objective` | `LocalizedText` or string | Objective line in the detail page. Defaults to `summary`. 目标行文案，缺省用 `summary`。 |
| `position` or `x`/`y` | `Vector2` / floats | Relative to the first parent. No parent → absolute. **No parent and no position puts the node on (0,0), on top of `FirstQuest`** (one warn). 相对首父；无父无坐标会压在起点上。 |
| `parents` / `parent` | string or `string[]` | e.g. `"FirstQuest"`, `"MoonLordQuest"`, or your own node ids. Missing parent is tolerated. |
| `chapterHub` | bool | Register this node as a chapter in the left rail. External nodes are **never** treated as the "start" root even with no parents. A hub is still an ordinary node: give it a parent (`"FirstQuest"`) and a `complete` (`_ => true` for a pure entry hub), otherwise its children never unlock. 登记为章目枢纽；外部节点无父也不会被当成起点。枢纽仍是普通节点，要给父节点和 `complete`，否则子节点永不解锁。 |
| `chapterOrder` | int > 0 | Chapter sort key. Defaults to `100` (after CWR chapters) when `chapterHub` is set. 章目排序，缺省 100。 |
| `icon` | asset path | Or `iconItem` / `iconNpc`. Cross-mod paths work (`YourMod/Assets/...`). |
| `type` | `QuestType` / name / int | `Main` `Side` `Daily` `Achievement`. Default `Side`. |
| `difficulty` | `QuestDifficulty` / name / int | `Easy` `Normal` `Hard` `Expert` `Master`. Default `Normal`. |
| `complete` | `Func<Player, bool>` or `Func<Player, float>` | Read already-synced world flags. `bool` → objective shows as a single step (`RequiredProgress = 1`). `float` (0–1) → shown as `n / progressMax`. 读已同步的世界旗。 |
| `progressMax` | int | Scale for `float` predicates and manual progress. Default `100`. E.g. `5` for "kills 3/5". |
| `unlock` | `Func<Player, bool>` | Extra unlock gate on top of parents. Optional. |
| `hiddenUntilUnlocked` | bool | Hidden quest: invisible until unlocked. |
| `countsTowardCompletionist` | bool | **Default `false`.** Whether the "完典" milestone counts this node. |
| `rewards` | `{item, stack}` list | `itemId <= 0` ignored. Same item only once per node. **Keep reward order stable across sessions**: claimed flags are stored by index. 奖励顺序跨会话必须稳定，领取标记按索引存。 |

Predicates that throw are retried twice, then disabled with one error log (`complete` freezes progress, `unlock` keeps the gate closed).
谓词抛异常两次后停用并记一条 Error：`complete` 冻住进度，`unlock` 保持关门。

Player save is `QLPlayer.QuestProgress[id]`. No extra tag. Reading `IsCompleted` for an unknown id does not create a save entry.
玩家档仍是 `QLPlayer.QuestProgress[id]`，查询未知 id 不会往档里插键。

### `QuestLogs.SetObjectiveProgress`

Only for **external** nodes that have **no** `complete` predicate (a predicate rewrites progress every frame). Writes 0–1 scaled by `progressMax`; reaching 1 lets the node complete through its normal update (notification, child unlock). It does not flip the completion flag directly.
只对没有 `complete` 谓词的外部节点有效。写入 0–1，按 `progressMax` 换算；到 1 后由节点自身更新走完成流程（弹窗、解锁子节点），不会直接改完成位。

### Progress bars vs Completionist · 进度条与完典

The book header / footer progress bars count every visible node, external nodes included. The "完典" milestone only counts nodes with `countsTowardCompletionist = true`. These two numbers can legitimately differ.
书页头尾的进度条计入所有可见节点（含外部）；完典里程碑只计 `countsTowardCompletionist = true` 的节点。两个数字不同是正常的。

## `Entrust.Register` keys · 参数

| Key | Type | Notes |
|-----|------|-------|
| `key` / `id` | string | Required. Prefix with your mod name. 必填，带前缀。 |
| `title` / `summary` / `category` | `LocalizedText` or string | Caller-owned. `category` is a display label, not a filter. `category` 只是显示用标签。 |
| `progress` | float 0–1 | Optional. |
| `progressLabel` | `LocalizedText` or string | Optional progress caption ("3/5"). |
| `status` | string | `Active` `Tracked` `Suspended` `Completed` `Failed`. `Active` / `Tracked` / `Suspended` are player-managed in the dossier (right / middle click); requesting `Active` on an entry that is already in any of those three keeps the player's choice. Drive `Completed` / `Failed` from your side. 进行中三态归玩家管，外模传 `Active` 不会打掉玩家的关注 / 挂起；外模负责 `Completed` / `Failed`。 |
| `notify` | bool | Default `true`. A **new** `Active` entry pops a "new entrust" toast and becomes `Tracked`. Pass `false` for entries the player has already seen (no toast, lands as `Tracked`). 已首发过的委托传 `false`，不再弹窗。 |
| `priority` | int | Higher sorts first. |
| `provider` / `providerName` | `LocalizedText` or string | One-off provider. Not added to the official seven-person table. |
| `providerColor` / `color` | `Color` or `{r,g,b}` | Optional. |
| `providerItem` | int | Item type used as the provider avatar. 委托人物品头像。 |
| `providerTexture` | asset path | Texture avatar, used when `providerItem` is 0. |
| `providerGlyph` | string | SVG path in [-1,1] (M/L/H/V/C/Q, no arcs) drawn inside the stamp. Empty = blank stamp. 戳内纹样。 |

`Entrust.Register` is an **upsert**: if the key already exists it only updates `progress` / `status` / `priority` and returns `true`, with no toast. You can call it every N frames as an "ensure".
`Entrust.Register` 是 upsert：Key 已存在时只更新进度 / 状态 / 优先级并返回 `true`，不弹窗，可以每隔若干帧当 Ensure 反复调。

`Entrust.SetProgress` / `Entrust.SetStatus` on an absent key return `false` silently.
对不存在的 Key 调 `SetProgress` / `SetStatus` 静默返回 `false`。

v1 is display + progress + status. No contribution net, no rewards through the dossier.
v1 只做展示、进度、状态。不做贡献度网络，不通过卷宗发奖。

## Lifecycle · 生命周期

- Quest nodes last for the CWR load session. Re-Call after a reload.
- Entrust is wiped on world unload (`ClearAll`). **Re-register after entering a world.** CWR will not re-pump foreign entries. Per-frame `SetProgress` before you re-register just returns `false`.
  委托出世界即清空，进世界后必须自己再注册；重注册前的 `SetProgress` 只会返回 `false`。
- Dedicated server: `Entrust.*` writes are client-only no-ops (`false`, `Get` → `null`). `QuestLogs.RegisterNode` may run on the server, but all quest evaluation is client-side; `QuestLogs.IsCompleted` on the server is always `false`.
  听服上委托写入是 no-op；任务判定全在客户端，服务器查 `IsCompleted` 恒 `false`。
- World decision: when the player has declined quest tracking for the current world (or the decision card is still open), an in-world `RegisterNode` still succeeds but skips the immediate unlock check, same as CWR's own nodes.
  玩家在本世界拒绝了任务检测（或决策卡未答）时，世界内注册仍成功，只是不立刻做解锁检查。

## Completionist · 完典

CWR autoload nodes still count. External nodes default to not counting.

## Out of scope (v1) · 未做

Entrust reward grants, contribution / damage net, new packets, hunting-chronicle auto-ingest, multiple objectives per external node.
委托发奖、贡献度网络、新增网络包、讨伐编年史自动收录、外部节点多目标。
