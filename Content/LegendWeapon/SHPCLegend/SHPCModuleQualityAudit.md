# SHPC 改件（模具）质量审计

> **用途**：供后续 AI 或开发者快速了解 SHPC 改件体系中「粗制滥造 / 需优先改版」的模具清单与已知问题。  
> **审计日期**：2026-06-09  
> **代码范围**：`Content/LegendWeapon/SHPCLegend/Modules/`（91 个 `*Module.cs`）  
> **剧情来源**：`Content/ADV/Scenarios/Shepel/Gifts/`（23 个 Boss 礼物场景）

---

## 分类标准

改件基类为 `SHPCModuleItem`，行为通过 `Apply(ref ShootContext ctx)` 与生命周期钩子（`OnBeam*` / `OnLaser*` / `OnOrb*` / `OnPlayerUpdate`）实现。

| 等级 | 定义 | 判定条件 |
|------|------|----------|
| **白板（劣质）** | 仅数值增减，无玩法机制 | `Apply` 只改浮点倍率 / `CritAdd`；无生命周期钩子；未开启行为标志 |
| **Apply 行为** | 有机制但无自定义代码 | `Apply` 设置 `LaserMode`、`MergeBeams`、`BeamCountAdd`、`BeamExplodeOnHit` 等；无钩子覆写 |
| **有钩子（合格）** | 有独立机制实现 | 覆写了 `OnBeamAI`、`OnBeamHitNPC`、`OnLaserHitNPC`、`OnOrbDetonation` 等 |

**数值字段（白板常见）**：`AttackSpeedMul`、`DamageMul`、`SpreadMul`、`BeamSpeedMul`、`HomingMul`、`ManaCostMul`、`ChargeTimeMul`、`OrbSpeedMul`、`BeamLifeMul`、`OrbExplosionRadiusMul`、`CritAdd`

**行为标志（非白板）**：`MergeBeams`、`LaserMode`、`BeamExplodeOnHit`、`OrbDrainAura`、`OrbExplosionPropels`、`LaserScorchOnHit`、`OrbFlyingAttract`

**行为整型（非白板）**：`BeamCountAdd`、`BeamExtraPierce`、`BeamChainCount`、`BeamSplitOnDeath`、`OrbDetonationMinions`、`LaserPulseInterval`

---

## 总体统计

| 类别 | 数量 | 占比 |
|------|------|------|
| 纯数值白板 | **24** | 26% |
| 仅有 Apply 行为标志（无自定义钩子） | 15 | 16% |
| 有生命周期钩子 | 52 | 57% |
| **合计** | **91** | 100% |

---

## 已知 Bug：假机制（死属性）

以下改件在 tooltip / `GetStatLines` 中显示「聚束伤害」加成，但 **`MergedDamageBonus` 仅在 `MergeBeams == true` 时参与伤害计算**。这两件改件本身不开启 `MergeBeams`，因此聚束加成为 **完全无效**。

**相关代码**（`SHPCOverride.cs`）：

```csharp
int finalDamage = (int)(damage * ctx.DamageMul * (ctx.MergeBeams ? ctx.MergedDamageBonus : 1f));
```

| 类名 | 中文名 | 剧情来源 | `Apply` 内容 | 问题 |
|------|--------|----------|--------------|------|
| `HighVoltageCoreModule` | 高压核心 | 星神使（Exo Mechs） | `DamageMul +0.08`, `MergedDamageBonus +0.8`, `ManaCostMul +0.72` | +80% 聚束伤害无效 |
| `PlasmaInjectorModule` | 等离子注入器 | 至尊灾厄（SCal） | `OrbSpeedMul +0.6`, `MergedDamageBonus +0.4`, `ChargeTimeMul +0.36` | +40% 聚束伤害无效 |

**修复方向（供参考）**：二选一或组合——(1) 为这两件补上 `MergeBeams` 或独立机制；(2) 将 `MergedDamageBonus` 改为其他有效字段；(3) 修正 tooltip 与 `BuildStatLines` 显示逻辑。

---

## 剧情礼物模具质量（23 件）

Shepel 礼物系列文件：`Content/ADV/Scenarios/Shepel/Gifts/Shepel*Gift.cs`  
奖励通过 `ADVRewardPopup.ShowReward(ModContent.ItemType<XXXModule>(), ...)` 发放。

| 质量 | 数量 | 占比 |
|------|------|------|
| 白板 | **14** | 61% |
| Apply 行为（无钩子） | 5 | 22% |
| 有钩子 | 4 | 17% |

### 剧情白板 — 优先改版清单（按曝光 + 劣质程度排序）

| 优先级 | 类名 | 中文名 | 槽位 | 剧情 Boss | `Apply` 数值摘要 | 主要问题 |
|--------|------|--------|------|-----------|------------------|----------|
| ★★★ | `HarmonyGripModule` | 谐振握把 | Grip | 血肉墙 | `ManaCostMul -0.30` | 唯一属性，最单薄 |
| ★★★ | `BalancedGripModule` | 平衡握把 | Grip | 史莱姆之神 | 散布 -16%、射速 +4%、伤害 +2% | 三角微调，几乎无存在感 |
| ★★★ | `HighVoltageCoreModule` | 高压核心 | Power | 星神使 | 伤害 +8%、聚束 +80%、法力 +72% | **死属性** + 后期高曝光 |
| ★★★ | `PlasmaInjectorModule` | 等离子注入器 | Power | 至尊灾厄 | 球速 +60%、聚束 +40%、蓄力 +36% | **死属性** + 终局礼物 |
| ★★ | `QuantumFrameModule` | 量子机匣 | Frame | 神明吞噬者 | 追踪 +32%、球速 +32%、法力 +20% | 纯数值 |
| ★★ | `OverloadCoreModule` | 超载核心 | Power | 普罗维登斯 | 球速 +36%、蓄力 -20% | 仅右键数值 |
| ★★ | `AssaultStockModule` | 突击枪托 | Stock | 骷髅王 Prime | 伤害 +5%、射速 +10%、法力 +50% | 模板化 trade-off |
| ★★ | `LightStockModule` | 轻量枪托 | Stock | 世界吞噬者 | 射速 +35%、伤害 -20%、散布 +30% | 模板化快枪托 |
| ★★ | `CrystalGripModule` | 水晶握把 | Grip | 克苏鲁之脑 | 法力 -16%、暴击 +4、蓄力 +12% | 无机制 |
| ★★ | `AdaptiveOpticModule` | 自适应瞄具 | Optic | 世纪之花 | 追踪 +32%、射速 +3%、暴击 +4 | 纯数值 |
| ★★ | `HoloOpticModule` | 全息瞄具 | Optic | 海瘟兽 | 散布 -55%、射速 +10%、法力 +18% | 纯数值 |
| ★★ | `KineticDamperModule` | 动能阻尼托 | Stock | 石巨人 | 散布 -50%、射速 -8%、暴击 +3 | 纯数值 |
| ★ | `PrecisionOpticModule` | 精密瞄具 | Optic | 双子魔眼 | 散布归零、暴击 +6 | 数值强但仍为白板 |
| ★ | `HypersonicBarrelModule` | 超音速枪管 | Barrel | 亚龙 | 弹速 +88%、射速 +16%、伤害 -12%、追踪 -84% | 极端 trade-off，无独特玩法 |

### 剧情礼物 — 有机制（暂不列入劣质）

| 类名 | 中文名 | Boss | 机制类型 |
|------|--------|------|----------|
| `LaserBarrelModule` | 棱镜激光枪管 | 克苏鲁之眼 | `LaserMode` 攻击模式替换 |
| `OscillatorBarrelModule` | 振荡枪管 | .hive 脑 | 激光 + `LaserPulseInterval` 脉冲爆炸 |
| `ScattershotBarrelModule` | 霰射枪管 | 穿孔者 | `BeamCountAdd +2` 霰弹化 |
| `SingularityCoreModule` | 奇点核心 | 月亮领主 | `OrbFlyingAttract` 飞行追踪 |
| `RecoilStockModule` | 反冲枪托 | 毁灭者 | `OrbExplosionPropels` 爆炸反推 |
| `MagmaVentBarrelModule` | 熔岩喷口枪管 | 硫磺火元素 | `OnBeamHitNPC` / `OnBeamKill` 钩子 |
| `ScorchBarrelModule` | 灼烧枪管 | 灾厄克隆体 | `LaserScorchOnHit` + `OnLaserAI` |
| `PhantomFrameModule` | 幻影机匣 | 拜月教邪教徒 | `OnBeamAI` 钩子 |
| `RecursiveFrameModule` | 递归机匣 | 幻鬼 | `OnBeamKill` 递归分裂 |

### 剧情礼物完整对照表

| 礼物场景文件 | Boss | 奖励类名 |
|--------------|------|----------|
| `ShepelEoCGift.cs` | 克苏鲁之眼 | `LaserBarrelModule` |
| `ShepelEoWGift.cs` | 世界吞噬者 | `LightStockModule` |
| `ShepelBoCGift.cs` | 克苏鲁之脑 | `CrystalGripModule` |
| `ShepelHiveMindGift.cs` | .hive 脑 | `OscillatorBarrelModule` |
| `ShepelPerforatorGift.cs` | 穿孔者 | `ScattershotBarrelModule` |
| `ShepelSlimeGodGift.cs` | 史莱姆之神 | `BalancedGripModule` |
| `ShepelWoFGift.cs` | 血肉墙 | `HarmonyGripModule` |
| `ShepelAquaticScourgeGift.cs` | 海瘟兽 | `HoloOpticModule` |
| `ShepelBrimstoneElementalGift.cs` | 硫磺火元素 | `MagmaVentBarrelModule` |
| `ShepelDestroyerGift.cs` | 毁灭者 | `RecoilStockModule` |
| `ShepelTwinsGift.cs` | 双子魔眼 | `PrecisionOpticModule` |
| `ShepelSkeletronPrimeGift.cs` | 骷髅王 Prime | `AssaultStockModule` |
| `ShepelCalamitasCloneGift.cs` | 灾厄克隆体 | `ScorchBarrelModule` |
| `ShepelPlanteraGift.cs` | 世纪之花 | `AdaptiveOpticModule` |
| `ShepelGolemGift.cs` | 石巨人 | `KineticDamperModule` |
| `ShepelCultistGift.cs` | 拜月教邪教徒 | `PhantomFrameModule` |
| `ShepelMoonLordGift.cs` | 月亮领主 | `SingularityCoreModule` |
| `ShepelProvidenceGift.cs` | 普罗维登斯 | `OverloadCoreModule` |
| `ShepelPolterghastGift.cs` | 幻鬼 | `RecursiveFrameModule` |
| `ShepelDevourerofGodsGift.cs` | 神明吞噬者 | `QuantumFrameModule` |
| `ShepelYharonGift.cs` | 亚龙 | `HypersonicBarrelModule` |
| `ShepelExoMechsGift.cs` | 星神使 | `HighVoltageCoreModule` |
| `ShepelSupremeCalamitasGift.cs` | 至尊灾厄 | `PlasmaInjectorModule` |

---

## 非剧情白板（实验室 / 掉落池，10 件）

不在 Shepel 礼物中，但同属纯数值白板，改版优先级低于剧情礼物。

| 类名 | 中文名 | 槽位 | `Apply` 数值摘要 |
|------|--------|------|------------------|
| `RapidBarrelModule` | 速射枪管 | Barrel | 射速 +40%、伤害 -24%、散布 +36% |
| `HeavyBarrelModule` | 重型枪管 | Barrel | 伤害 +36%、射速 -75%、散布 -40% |
| `EfficientGripModule` | 高效握把 | Grip | 法力 -12%、射速 +6% |
| `SteadyStockModule` | 稳压枪托 | Stock | 射速 -20%、伤害 +12% |
| `CapacitorBankModule` | 储能阵列 | Power | 蓄力 -32%、球速 -12%、射速 -6% |
| `ErgonomicStockModule` | 人体工学枪托 | Stock | 法力 -30%、射速 +6%、散布 -10% |
| `ExtenderStockModule` | 延伸枪托 | Stock | 弹寿 +65%、弹速 +30%、伤害 -5% |
| `BraceStockModule` | 支撑枪托 | Stock | 散布归零、弹速/弹寿 +50%、射速 -20% |
| `ThermalOpticModule` | 热成像瞄具 | Optic | 追踪 +120%、暴击 +4、散布 -20% |
| `SniperOpticModule` | 狙击瞄具 | Optic | 弹速/弹寿拉满、伤害 +24%，大幅射速/追踪/散布惩罚 |

---

## 仅有 Apply 行为、无钩子的改件（15 件）

有玩法变化但缺少自定义逻辑，质量介于白板与合格之间。剧情礼物中已标注的 5 件见上表；其余 10 件：

| 类名 | 中文名 | 槽位 | 关键行为 |
|------|--------|------|----------|
| `FocusBarrelModule` | 聚束枪管 | Barrel | `MergeBeams` + 高 `MergedDamageBonus` |
| `NovaBarrelModule` | 新星枪管 | Barrel | `BeamExplodeOnHit` + 衰减 |
| `GravityFrameModule` | 重力机匣 | Frame | `OrbDrainAura` |
| `MultiCellFrameModule` | 多格机匣 | Frame | `BeamCountAdd +2` |
| `ResonanceFrameModule` | 共振机匣 | Frame | `BeamCountAdd +1`（仅 +1 束，较单薄） |
| `VolatileFrameModule` | 不稳定机匣 | Frame | `BeamCountAdd +1` + 高暴击 |
| `SwarmGripModule` | 蜂群握把 | Grip | `OrbDetonationMinions +3` |
| `PrismOpticModule` | 棱镜瞄具 | Optic | `BeamSplitOnDeath +2` |
| `TeslaCoreModule` | 特斯拉核心 | Power | 链式跳跃 + 穿透 |
| `StormStockModule` | 风暴枪托 | Stock | `BeamCountAdd +1` + 射速 |

---

## 特殊案例

| 类名 | 说明 |
|------|------|
| `OverkillFrameModule` | `Apply` 为空 `{}`，机制完全依赖 `OnBeamHitNPC` + `OnPlayerUpdate`（超杀层数系统），**不属于白板** |

---

## 建议改版优先级（摘要）

1. **谐振握把** — 早期剧情礼物，单属性最单薄  
2. **平衡握把** — 早期剧情礼物，微调三角  
3. **高压核心 / 等离子注入器** — 修复死属性 bug + 终局高曝光  
4. **量子机匣 / 超载核心** — 中后期剧情节点纯数值  
5. 其余剧情白板按上表 ★★ → ★ 顺序处理  
6. 非剧情白板（实验室池）优先级最低  

---

## 相关源文件索引

| 路径 | 说明 |
|------|------|
| `Content/LegendWeapon/SHPCLegend/Modules/SHPCModuleItem.cs` | 改件基类、`GetStatLines`、钩子定义 |
| `Content/LegendWeapon/SHPCLegend/Modules/ShootContext.cs` | 所有可修改字段 |
| `Content/LegendWeapon/SHPCLegend/SHPCOverride.cs` | 伤害计算、`MergedDamageBonus` 消费处 |
| `Content/ADV/Scenarios/Shepel/Gifts/` | 剧情礼物发放 |
| `Localization/zh-Hans/Mods.CalamityOverhaul.Items.hjson` | 中文 DisplayName / Tooltip |

---

## 复现审计方法（供 AI 脚本化）

```powershell
# 在模组根目录执行：扫描无钩子且 Apply 未设置行为标志的 Module
$behaviorFlags = 'MergeBeams|LaserMode|BeamExplodeOnHit|OrbDrainAura|OrbExplosionPropels|LaserScorchOnHit|OrbFlyingAttract'
$behaviorInts = 'BeamCountAdd|BeamExtraPierce|BeamChainCount|BeamSplitOnDeath|OrbDetonationMinions|LaserPulseInterval'
# 白板条件：无 OnBeam/OnLaser/OnOrb/OnPlayer 钩子覆写，且 Apply 体不匹配上述标志
```

手动审查时：读取各 `*Module.cs` 的 `Apply` 方法体 + 搜索 `public override void On` 即可复现本报告分类。
