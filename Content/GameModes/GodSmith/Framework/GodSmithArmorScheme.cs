using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Framework
{
    /// <summary>
    /// 神匠盔甲方案基类。不是 ModType：由 <see cref="GodSmithLoader"/> 反射扫描实例化，
    /// 手动实现 <see cref="ILocalizedModType"/> 解决 GetLocalization 载体问题
    /// （键 = Mods.CalamityOverhaul.GodSmith{GsFamily}.{类名}.{后缀}）。<br/>
    /// 两种形态：<br/>
    /// ①神赋叠加（默认，<see cref="OverridesVanilla"/> = false）：原版单件属性与套装奖励保留，
    /// 方案只叠一层机制，<see cref="EndowLine"/> 以「神赋：」前缀追加进 player.setBonus；<br/>
    /// ②接管重置（<see cref="OverridesVanilla"/> = true）：三件的原版套装奖励被
    /// <see cref="GodSmithArmorReset"/> 压掉，<see cref="OverridesPieceStats"/> 为 true 时原版单件属性也一并压掉，
    /// 改由 <see cref="UpdateHead"/>/<see cref="UpdateBody"/>/<see cref="UpdateLegs"/> 与
    /// <see cref="UpdateSetBonus"/> 重新定义，<see cref="SetBonusLine"/> 直接写进 player.setBonus。
    /// 头盔镶嵌链（<see cref="GodSmithHelmetNestItem"/>）会把下级接管方案的套装奖励一并派发给穿戴者。<br/>
    /// 路由：<see cref="GodSmithArmorPlayer"/> 在装备结算期比对三件套并派发钩子；
    /// 玩家态一律放 ModPlayer（暂存寄存器或按方案键的冷却表），方案单例上不许放每玩家字段
    /// </summary>
    internal abstract class GodSmithArmorScheme : ILocalizedModType
    {
        //==================== 注册表（GodSmithLoader 填充与清理） ====================

        /// <summary>全部盔甲方案，按类型全名排序保证确定性</summary>
        public static List<GodSmithArmorScheme> Schemes { get; internal set; } = [];

        /// <summary>胸甲 ID → 方案列表（胸甲最不易重复，作主键；同胸多方案按头盔细分）</summary>
        public static Dictionary<int, List<GodSmithArmorScheme>> SchemesByBody { get; internal set; } = [];

        /// <summary>头盔 ID → 接管方案（镶嵌链继承查表；只登记 <see cref="OverridesVanilla"/> 的方案）</summary>
        public static Dictionary<int, GodSmithArmorScheme> SchemeByHead { get; internal set; } = [];

        /// <summary>单件 ID → 接管方案（三件全部登记；单件属性压制与 UpdateEquip 分发查表）</summary>
        public static Dictionary<int, GodSmithArmorScheme> SchemeByPiece { get; internal set; } = [];

        /// <summary>该单件是否属于某个接管原版单件属性的方案</summary>
        public static bool OverridesPiece(int itemType)
            => SchemeByPiece.TryGetValue(itemType, out GodSmithArmorScheme scheme) && scheme.OverridesPieceStats;

        //==================== ILocalizedModType 载体 ====================

        public Mod Mod => CWRMod.Instance;

        public string Name => GetType().Name;

        public string FullName => Mod.Name + "/" + Name;

        public string LocalizationCategory => "GodSmith" + GsFamily;

        //==================== 子类必填 ====================

        /// <summary>族名（决定本地化类目与 loc 文件名，如 Exemplars/ArmorsBatch1）</summary>
        public abstract string GsFamily { get; }

        /// <summary>可命中的头盔 ID 数组（神圣/叶绿这类多头盔套全部列出）</summary>
        public abstract int[] HeadIDs { get; }

        /// <summary>胸甲 ID</summary>
        public abstract int BodyID { get; }

        /// <summary>护腿 ID</summary>
        public abstract int LegsID { get; }

        /// <summary>可命中的胸甲 ID 数组（远古变体可混搭的套全部列出），默认只有 <see cref="BodyID"/></summary>
        public virtual int[] BodyIDs => [BodyID];

        /// <summary>可命中的护腿 ID 数组，默认只有 <see cref="LegsID"/></summary>
        public virtual int[] LegsIDs => [LegsID];

        //==================== 接管重置开关 ====================

        /// <summary>是否接管原版：true 时原版套装奖励被压掉，套装奖励改由 <see cref="UpdateSetBonus"/> 定义</summary>
        public virtual bool OverridesVanilla => false;

        /// <summary>
        /// 接管时是否连原版单件属性一并压掉（改由 UpdateHead/Body/Legs 定义）；
        /// false = 保留原版单件属性，只换套装奖励。仅 <see cref="OverridesVanilla"/> 为 true 时有意义
        /// </summary>
        public virtual bool OverridesPieceStats => true;

        /// <summary>
        /// 接管时是否放行原版套装奖励的机制部分（日耀护盾/星旋隐身/星云增幅/星尘守卫/蘑菇潜行这类
        /// 逻辑散落在原版 UpdateArmorSets 内、无法靠旗标复刻的套）：true 时原版 UpdateArmorSets 照常执行，
        /// 本方案的 <see cref="UpdateSetBonus"/> 叠在其上，套装奖励文本仍由 <see cref="SetBonusLine"/> 覆盖。
        /// 仅 <see cref="OverridesVanilla"/> 为 true 时有意义
        /// </summary>
        public virtual bool KeepsVanillaSetBonus => false;

        //==================== 生命周期 ====================

        /// <summary>神赋行文本（叠加形态：追加进 player.setBonus，前缀由 GameModeText.GodSmithEndowPrefix 提供）</summary>
        public LocalizedText EndowLine { get; private set; }

        /// <summary>套装奖励文本（接管形态：直接写进 player.setBonus）</summary>
        public LocalizedText SetBonusLine { get; private set; }

        /// <summary>头盔单件属性说明行（接管单件属性时替换原版 tooltip；null = 该件无属性行）</summary>
        public LocalizedText HeadLine { get; private set; }

        /// <summary>胸甲单件属性说明行</summary>
        public LocalizedText BodyLine { get; private set; }

        /// <summary>护腿单件属性说明行</summary>
        public LocalizedText LegsLine { get; private set; }

        /// <summary>神赋行代码默认值（en 文案；正典 zh 写进族 loc 文件）</summary>
        protected virtual string EndowLineFallback => "";

        /// <summary>套装奖励行代码默认值（接管形态）</summary>
        protected virtual string SetBonusLineFallback => "";

        /// <summary>头盔属性行代码默认值；null 或空 = 不注册该键</summary>
        protected virtual string HeadLineFallback => null;

        /// <summary>胸甲属性行代码默认值；null 或空 = 不注册该键</summary>
        protected virtual string BodyLineFallback => null;

        /// <summary>护腿属性行代码默认值；null 或空 = 不注册该键</summary>
        protected virtual string LegsLineFallback => null;

        /// <summary>加载期初始化，由 GodSmithLoader 调用；先注册本地化再走子类静态初始化</summary>
        internal void Load() {
            if (OverridesVanilla) {
                SetBonusLine = this.GetLocalization("SetBonusLine", () => SetBonusLineFallback);
                HeadLine = LoadOptionalLine("HeadLine", HeadLineFallback);
                BodyLine = LoadOptionalLine("BodyLine", BodyLineFallback);
                LegsLine = LoadOptionalLine("LegsLine", LegsLineFallback);
            }
            else {
                EndowLine = this.GetLocalization("EndowLine", () => EndowLineFallback);
            }
            GsSetStaticDefaults();
        }

        private LocalizedText LoadOptionalLine(string suffix, string fallback) {
            if (string.IsNullOrEmpty(fallback)) {
                return null;
            }
            return this.GetLocalization(suffix, () => fallback);
        }

        /// <summary>子类静态初始化（缓存额外本地化键等）</summary>
        public virtual void GsSetStaticDefaults() { }

        /// <summary>三件是否命中本方案（vanity 不算，只看 armor[0..2]）</summary>
        public bool Matches(Player player)
            => Contains(HeadIDs, player.armor[0].type)
            && Contains(BodyIDs, player.armor[1].type)
            && Contains(LegsIDs, player.armor[2].type);

        /// <summary>该头盔 ID 是否属于本方案</summary>
        public bool IsHead(int itemType) => Contains(HeadIDs, itemType);

        /// <summary>该胸甲 ID 是否属于本方案</summary>
        public bool IsBody(int itemType) => Contains(BodyIDs, itemType);

        /// <summary>该护腿 ID 是否属于本方案</summary>
        public bool IsLegs(int itemType) => Contains(LegsIDs, itemType);

        private static bool Contains(int[] ids, int type) {
            for (int i = 0; i < ids.Length; i++) {
                if (ids[i] == type) {
                    return true;
                }
            }
            return false;
        }

        //==================== 接管形态：单件属性与套装奖励数值层 ====================

        /// <summary>头盔单件属性（接管单件属性时替代原版 GrantArmorBenefits；装备结算期各端执行）</summary>
        public virtual void UpdateHead(Player player, Item item) { }

        /// <summary>胸甲单件属性</summary>
        public virtual void UpdateBody(Player player, Item item) { }

        /// <summary>护腿单件属性</summary>
        public virtual void UpdateLegs(Player player, Item item) { }

        /// <summary>
        /// 套装奖励数值层：整套命中，或被穿戴者头盔的镶嵌链继承时，每帧在装备结算期调用（各端执行）。
        /// 只写 player 数值与旗标；弹幕/音效放命中钩子或 <see cref="UpdateEndowment"/>
        /// </summary>
        public virtual void UpdateSetBonus(Player player, GodSmithArmorPlayer state) { }

        //==================== 神赋钩子（GodSmithArmorPlayer 派发，模式开启且整套命中或被继承时） ====================

        /// <summary>每帧驻留效果（各端都会执行；粒子守 !VaultUtils.isServer，个人量守 whoAmI == Main.myPlayer）</summary>
        public virtual void UpdateEndowment(Player player, GodSmithArmorPlayer state) { }

        /// <summary>
        /// 穿戴者命中 NPC（物品直击与弹幕统一入口；只在攻击方端执行，proc 弹幕在此 owner 侧生成）。
        /// <paramref name="sourceProj"/> 为造成命中的弹幕，物品直击时为 null；用它排除自家 proc 弹自喂
        /// </summary>
        public virtual void OnEndowHitNPC(Player player, GodSmithArmorPlayer state, NPC target, in NPC.HitInfo hit, int damageDone, Projectile sourceProj) { }

        /// <summary>穿戴者命中结算修改（攻击方端；固定伤害/暴击等写 modifiers）</summary>
        public virtual void ModifyEndowHitNPC(Player player, GodSmithArmorPlayer state, NPC target, ref NPC.HitModifiers modifiers, Projectile sourceProj) { }

        /// <summary>
        /// 穿戴者受击。全端执行——tML Player.Hurt 无条件派发 OnHurt（Player.cs:34654），
        /// 远端与服务端还会经 MessageBuffer case 117 收包重放同一次 Hurt。
        /// Heal/AddBuff(自身)/NewProjectile 等权威动作必须守 player.whoAmI == Main.myPlayer，否则多端多发
        /// </summary>
        public virtual void OnEndowHurt(Player player, GodSmithArmorPlayer state, in Player.HurtInfo info) { }

        /// <summary>穿戴者受击结算修改（受击方本地端权威）</summary>
        public virtual void ModifyEndowHurt(Player player, GodSmithArmorPlayer state, ref Player.HurtModifiers modifiers) { }

        /// <summary>
        /// 消耗型闪避（受击方本地端）：返回 true 完全闪掉这次伤害，tML 不再把这次受击发上网；
        /// 远端若需看到闪避表现请自建同步。首个返回 true 的方案生效
        /// </summary>
        public virtual bool EndowConsumableDodge(Player player, GodSmithArmorPlayer state, in Player.HurtInfo info) => false;

        /// <summary>穿戴者击杀 NPC（由命中钩子里 life&lt;=0 判定，攻击方端执行）</summary>
        public virtual void OnEndowKillNPC(Player player, GodSmithArmorPlayer state, NPC target) { }

        /// <summary>穿戴者实际扣除魔力时（各端执行；改写魔力守 myPlayer）</summary>
        public virtual void OnEndowConsumeMana(Player player, GodSmithArmorPlayer state, Item item, int manaConsumed) { }

        /// <summary>穿戴者武器伤害修饰（各端执行；投掷类加固定伤害等）</summary>
        public virtual void ModifyEndowWeaponDamage(Player player, Item item, ref StatModifier damage) { }

        /// <summary>穿戴者武器用速倍率（&gt;1 更快；各方案倍率相乘）</summary>
        public virtual float EndowUseSpeedMultiplier(Player player, Item item) => 1f;

        /// <summary>本次使用是否消耗该物品（返回 false 阻止消耗，null 不表态）</summary>
        public virtual bool? EndowConsumeItem(Player player, Item item) => null;

        /// <summary>本次射击是否消耗弹药（返回 false 阻止消耗，null 不表态）</summary>
        public virtual bool? EndowCanConsumeAmmo(Player player, Item weapon, Item ammo) => null;

        /// <summary>穿戴者射击（owner 端）；返回 false 压掉原版弹幕，true 放行</summary>
        public virtual bool EndowShoot(Player player, Item item, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) => true;

        /// <summary>穿戴者钓鱼判定（本地端）；改写 itemDrop 即改变渔获</summary>
        public virtual void EndowCatchFish(Player player, GodSmithArmorPlayer state, FishingAttempt attempt,
            ref int itemDrop, ref int npcSpawn) { }

        /// <summary>穿戴者渔获物品修改（本地端；可改堆叠）</summary>
        public virtual void EndowModifyCaughtFish(Player player, GodSmithArmorPlayer state, Item fish) { }

        /// <summary>是否本方案自产的 proc 弹幕（命中分发时跳过，防自喂循环）</summary>
        public virtual bool IsOwnEndowProj(Projectile proj) => false;

        /// <summary>方案从命中态切走（换装/关模式）时清理，默认清空暂存寄存器</summary>
        public virtual void OnEndowLost(Player player, GodSmithArmorPlayer state) => state.ClearScratch();
    }
}
