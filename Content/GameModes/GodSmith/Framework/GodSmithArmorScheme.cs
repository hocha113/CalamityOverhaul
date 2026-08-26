using System.Collections.Generic;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Framework
{
    /// <summary>
    /// 神匠盔甲神赋方案基类。不是 ModType：由 <see cref="GodSmithLoader"/> 反射扫描实例化，
    /// 手动实现 <see cref="ILocalizedModType"/> 解决 GetLocalization 载体问题
    /// （键 = Mods.CalamityOverhaul.GodSmith{GsFamily}.{类名}.{后缀}）。<br/>
    /// 路由：<see cref="GodSmithArmorPlayer"/> 在 PostUpdateEquips 比对三件套并派发钩子；
    /// 原版套装奖励保留不删，神赋是叠加层。<br/>
    /// 神赋必须是机制表现（弹幕/姿态/领域/联动），禁止纯 +X% 数值行；
    /// 玩家态一律放 ModPlayer（可用 <see cref="GodSmithArmorPlayer"/> 的暂存寄存器），
    /// 方案单例上不许放每玩家字段
    /// </summary>
    internal abstract class GodSmithArmorScheme : ILocalizedModType
    {
        //==================== 注册表（GodSmithLoader 填充与清理） ====================

        /// <summary>全部盔甲方案，按类型全名排序保证确定性</summary>
        public static List<GodSmithArmorScheme> Schemes { get; internal set; } = [];

        /// <summary>胸甲 ID → 方案列表（胸甲最不易重复，作主键；同胸多方案按头盔细分）</summary>
        public static Dictionary<int, List<GodSmithArmorScheme>> SchemesByBody { get; internal set; } = [];

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

        //==================== 生命周期 ====================

        /// <summary>神赋行文本（追加进 player.setBonus，前缀由 GameModeText.GodSmithEndowPrefix 提供）</summary>
        public LocalizedText EndowLine { get; private set; }

        /// <summary>神赋行代码默认值（en 文案；正典 zh 写进族 loc 文件）</summary>
        protected virtual string EndowLineFallback => "";

        /// <summary>加载期初始化，由 GodSmithLoader 调用；先注册本地化再走子类静态初始化</summary>
        internal void Load() {
            EndowLine = this.GetLocalization("EndowLine", () => EndowLineFallback);
            GsSetStaticDefaults();
        }

        /// <summary>子类静态初始化（缓存额外本地化键等）</summary>
        public virtual void GsSetStaticDefaults() { }

        /// <summary>三件是否命中本方案（vanity 不算，只看 armor[0..2]）</summary>
        public bool Matches(Player player) {
            if (player.armor[1].type != BodyID || player.armor[2].type != LegsID) {
                return false;
            }
            int head = player.armor[0].type;
            int[] heads = HeadIDs;
            for (int i = 0; i < heads.Length; i++) {
                if (heads[i] == head) {
                    return true;
                }
            }
            return false;
        }

        //==================== 神赋钩子（GodSmithArmorPlayer 派发，模式开启且整套命中时） ====================

        /// <summary>每帧驻留效果（各端都会执行；粒子守 !VaultUtils.isServer，个人量守 whoAmI == Main.myPlayer）</summary>
        public virtual void UpdateEndowment(Player player, GodSmithArmorPlayer state) { }

        /// <summary>
        /// 穿戴者命中 NPC（物品直击与弹幕统一入口；只在攻击方端执行，proc 弹幕在此 owner 侧生成）。
        /// <paramref name="sourceProj"/> 为造成命中的弹幕，物品直击时为 null；用它排除自家 proc 弹自喂
        /// </summary>
        public virtual void OnEndowHitNPC(Player player, GodSmithArmorPlayer state, NPC target, in NPC.HitInfo hit, int damageDone, Projectile sourceProj) { }

        /// <summary>穿戴者受击（受击方端执行）</summary>
        public virtual void OnEndowHurt(Player player, GodSmithArmorPlayer state, in Player.HurtInfo info) { }

        /// <summary>穿戴者击杀 NPC（由命中钩子里 life&lt;=0 判定，攻击方端执行）</summary>
        public virtual void OnEndowKillNPC(Player player, GodSmithArmorPlayer state, NPC target) { }

        /// <summary>方案从命中态切走（换装/关模式）时清理，默认清空暂存寄存器</summary>
        public virtual void OnEndowLost(Player player, GodSmithArmorPlayer state) => state.ClearScratch();
    }
}
