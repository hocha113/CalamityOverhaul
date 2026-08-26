using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Core
{
    /// <summary>
    /// 神匠词缀神赋基类：神匠模式下重铸出某词缀时，按权重从该词缀的神赋池 roll 一条，
    /// 以稳定 string 键存进物品实例（<see cref="GodSmithItem"/>）；模式关闭时数据保留但惰性。<br/>
    /// 不是 ModType：由 <see cref="GodSmithRegistry"/> 在 PostSetupContent 反射扫描实例化并按词缀建池，
    /// 手动实现 <see cref="ILocalizedModType"/>
    /// （键 = Mods.CalamityOverhaul.GodSmithEndow.{类名}.{后缀}）。<br/>
    /// 内容 agent 只建自己的子类文件即接入，禁止改任何共享文件。<br/>
    /// 联机纪律：神赋是单例，跨玩家跨物品共享，禁止放每玩家/每物品字段；
    /// 复杂状态在子类同文件里自建私有 ModPlayer
    /// </summary>
    internal abstract class GodSmithEndow : ILocalizedModType
    {
        //==================== 注册表（GodSmithRegistry 填充与清理） ====================

        /// <summary>稳定键 → 神赋</summary>
        public static Dictionary<string, GodSmithEndow> ByKey { get; internal set; } = [];

        /// <summary>词缀 ID → 神赋池，注册期对全部词缀求值 AppliesTo 建成</summary>
        public static Dictionary<int, List<GodSmithEndow>> PoolByPrefix { get; internal set; } = [];

        /// <summary>按稳定键查神赋；键未注册（内容被移除/改名）返回 false，数据层保留键不丢</summary>
        public static bool TryGet(string key, out GodSmithEndow endow) {
            if (!string.IsNullOrEmpty(key) && ByKey.TryGetValue(key, out endow)) {
                return true;
            }
            endow = null;
            return false;
        }

        /// <summary>
        /// 按词缀池加权 roll 一条；空池或权重全非正返回 null。
        /// 只应在交互端调用（PostReforge 本地执行），结果随物品数据同步，不要在多端各自 roll
        /// </summary>
        internal static GodSmithEndow RollFor(int prefixId) {
            if (!PoolByPrefix.TryGetValue(prefixId, out List<GodSmithEndow> pool) || pool.Count == 0) {
                return null;
            }
            float total = 0f;
            foreach (GodSmithEndow endow in pool) {
                total += Math.Max(0f, endow.RollWeight);
            }
            if (total <= 0f) {
                return null;
            }
            float pick = Main.rand.NextFloat() * total;
            foreach (GodSmithEndow endow in pool) {
                pick -= Math.Max(0f, endow.RollWeight);
                if (pick < 0f) {
                    return endow;
                }
            }
            return pool[^1];
        }

        //==================== ILocalizedModType 载体 ====================

        public Mod Mod => CWRMod.Instance;

        public string Name => GetType().Name;

        public string FullName => Mod.Name + "/" + Name;

        public string LocalizationCategory => "GodSmithEndow";

        /// <summary>稳定存档键 = 类名。改类名等于清洗现存物品身上的这条神赋，慎重</summary>
        public string Key => Name;

        //==================== 词缀覆盖与档位 ====================

        /// <summary>覆盖的词缀 ID 表（PrefixID 常量）；改用谓词覆盖时可留 null 并重写 AppliesTo</summary>
        public virtual int[] CoveredPrefixes => null;

        /// <summary>是否覆盖该词缀，默认查 <see cref="CoveredPrefixes"/>；注册期对全部词缀求值一次建池</summary>
        public virtual bool AppliesTo(int prefixId) {
            int[] covered = CoveredPrefixes;
            if (covered == null) {
                return false;
            }
            for (int i = 0; i < covered.Length; i++) {
                if (covered[i] == prefixId) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>roll 权重，同池内相对比较；&lt;= 0 视为不可 roll</summary>
        public virtual float RollWeight => 1f;

        /// <summary>
        /// 档位缩放约定：效果数值一律以「顶级词缀 = 1.0」为基准书写；
        /// 覆盖同主题多档词缀时按档位回缩（范例：守护 1.0 / 装甲 0.8 / 防卫 0.6 / 坚硬 0.4）。
        /// 分发端按物品当前词缀算好后随钩子参数 tierScale 传入，子类效果内不必自算
        /// </summary>
        public virtual float TierScaleFor(int prefixId) => 1f;

        //==================== 本地化 ====================

        /// <summary>神赋名（tooltip 行 = GameModeText.GodSmithEndowPrefix + 本名）</summary>
        public LocalizedText EndowName { get; private set; }

        /// <summary>神赋描述（\n 可多行，支持 Format 占位）</summary>
        public LocalizedText EndowDesc { get; private set; }

        /// <summary>神赋名英文默认值（正典 zh 后续统一写进 loc 文件）</summary>
        protected abstract string EndowNameFallback { get; }

        /// <summary>神赋描述英文默认值</summary>
        protected abstract string EndowDescFallback { get; }

        /// <summary>描述的运行时 Format 参数（随词缀档位变的数字走这里）；null = 原文显示</summary>
        public virtual object[] DescFormatArgs(Item item) => null;

        internal void LoadLocalization() {
            EndowName = this.GetLocalization("EndowName", () => EndowNameFallback);
            EndowDesc = this.GetLocalization("EndowDesc", () => EndowDescFallback);
        }

        //==================== 效果钩子（分发端已查 GodSmithActive，tierScale 已按物品词缀算好） ====================

        /// <summary>神赋武器：伤害修饰</summary>
        public virtual void ModifyWeaponDamage(Item item, Player player, ref StatModifier damage, float tierScale) { }

        /// <summary>神赋武器：暴击修饰</summary>
        public virtual void ModifyWeaponCrit(Item item, Player player, ref float crit, float tierScale) { }

        /// <summary>神赋武器：用速倍率（大于 1 更快，与其他模组的倍率相乘）</summary>
        public virtual float UseSpeedMultiply(Item item, Player player, float tierScale) => 1f;

        /// <summary>
        /// 神赋武器：每次使用动画开始（各端都会模拟这条链）。
        /// 生成弹幕等权威动作守 player.whoAmI == Main.myPlayer，随 NewProjectile 自动同步
        /// </summary>
        public virtual void OnUseAnimation(Item item, Player player, float tierScale) { }

        /// <summary>
        /// 神赋武器命中 NPC 的统一入口（只在攻击方端执行）。
        /// 直击：sourceItem = 武器，sourceProj = null；
        /// 弹幕：sourceItem = null，sourceProj = 弹幕（出生源打标回溯，含仆从/哨兵与近战接管手持弹幕）。
        /// 在此派生新弹幕时若不想让派生弹幕继承打标再次触发本钩子，
        /// 出生源用 sourceProj.GetSource_FromThis() 而不是物品源
        /// </summary>
        public virtual void OnHitNPC(Player player, Item sourceItem, Projectile sourceProj, NPC target,
            in NPC.HitInfo hit, int damageDone, float tierScale) { }

        /// <summary>神赋饰品：佩戴时每帧（被动数值写这里；各端执行，纪律同 GodSmithAccEffect.UpdateAccessory）</summary>
        public virtual void UpdateAccessory(Item item, Player player, bool hideVisual, GodSmithPlayer state, float tierScale) { }

        /// <summary>神赋饰品：佩戴者命中 NPC（攻击方端执行）。fromProjectile 是命中来源而非伤害类别</summary>
        public virtual void OnWearerHitNPC(Item accessory, Player player, NPC target,
            in NPC.HitInfo hit, int damageDone, bool fromProjectile, float tierScale) { }

        /// <summary>神赋饰品：佩戴者受击结算修改（受击方本地端权威）</summary>
        public virtual void ModifyHurt(Item accessory, Player player, ref Player.HurtModifiers modifiers, float tierScale) { }

        /// <summary>神赋饰品：佩戴者受击后</summary>
        public virtual void OnHurt(Item accessory, Player player, in Player.HurtInfo info, float tierScale) { }
    }
}
