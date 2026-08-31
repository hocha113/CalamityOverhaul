using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Core
{
    /// <summary>
    /// 神匠饰品重铸效果基类：每个原版饰品一个具体子类，原版效果保留，本类只做叠加层。<br/>
    /// 不是 ModType：由 <see cref="GodSmithRegistry"/> 在 PostSetupContent 反射扫描实例化并注册，
    /// 手动实现 <see cref="ILocalizedModType"/>
    /// （键 = Mods.CalamityOverhaul.GodSmithAcc.{类名}.{后缀}）。<br/>
    /// 内容 agent 只建自己的子类文件即接入，禁止改任何共享文件。<br/>
    /// 联机纪律：效果是单例，跨玩家共享，禁止放每玩家字段；
    /// 轻量每玩家状态用 <see cref="GodSmithPlayer"/> 的冷却表，
    /// 复杂状态在子类同文件里自建私有 ModPlayer
    /// </summary>
    internal abstract class GodSmithAccEffect : ILocalizedModType
    {
        //==================== 注册表（GodSmithRegistry 填充与清理） ====================

        /// <summary>物品 ID → 效果，加载期填充；家族多件（如音乐盒）各 ID 指向同一实例</summary>
        public static Dictionary<int, GodSmithAccEffect> ByItemID { get; internal set; } = [];

        /// <summary>按物品 ID 查效果（不含模式闸门，调用方自查 <see cref="GameModeSystem.GodSmithActive"/>）</summary>
        public static bool TryGet(int itemType, out GodSmithAccEffect effect)
            => ByItemID.TryGetValue(itemType, out effect);

        //==================== ILocalizedModType 载体 ====================

        public Mod Mod => CWRMod.Instance;

        public string Name => GetType().Name;

        public string FullName => Mod.Name + "/" + Name;

        public string LocalizationCategory => "GodSmithAcc";

        //==================== 子类必填 ====================

        /// <summary>
        /// 目标原版饰品 ID，支持单件与家族多件（如全部音乐盒）。
        /// 家族内不同两件同时佩戴时事件会各派发一次，效果自行决定是否去重
        /// </summary>
        public abstract int[] TargetItemIDs { get; }

        /// <summary>效果描述英文默认值（\n 可多行；正典 zh 后续统一写进 loc 文件）</summary>
        protected abstract string EffectDescFallback { get; }

        /// <summary>
        /// 佩戴时每帧（等价 GlobalItem.UpdateAccessory，各端都会执行）。
        /// 保底数值行写这里（player.GetDamage 之类）；
        /// 粒子守 !VaultUtils.isServer，个人量写入守 player.whoAmI == Main.myPlayer
        /// </summary>
        public abstract void UpdateAccessory(Item item, Player player, bool hideVisual, GodSmithPlayer state);

        //==================== 可选钩子（分发端已查 GodSmithActive） ====================

        /// <summary>装备结算后每帧（读玩家最终数值做联动用；死亡帧不会走到）</summary>
        public virtual void PostUpdateEquips(Item item, Player player, GodSmithPlayer state) { }

        /// <summary>
        /// 佩戴者命中 NPC 的统一入口（物品直击与弹幕合流，只在攻击方端执行）。
        /// fromProjectile 是命中来源而非伤害类别，判类别用 hit.DamageType；
        /// 近战接管型武器的命中一律从弹幕路径来
        /// </summary>
        public virtual void OnHitNPC(Item item, Player player, GodSmithPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, bool fromProjectile) { }

        /// <summary>佩戴者受击结算修改（受击方本地端权威）</summary>
        public virtual void ModifyHurt(Item item, Player player, GodSmithPlayer state, ref Player.HurtModifiers modifiers) { }

        /// <summary>
        /// 佩戴者受击后。全端执行——tML Player.Hurt 无条件派发 OnHurt（Player.cs:34654），
        /// 远端与服务端还会经 MessageBuffer case 117 收包重放同一次 Hurt。
        /// Heal/AddBuff(自身)/NewProjectile 等权威动作必须守 player.whoAmI == Main.myPlayer，否则多端多发
        /// </summary>
        public virtual void OnHurt(Item item, Player player, GodSmithPlayer state, in Player.HurtInfo info) { }

        /// <summary>
        /// 致死一击前，返回 false 取消死亡（护死类饰品用，配合冷却表防无限护死）。
        /// 首个取消者生效，之后的效果不再被询问
        /// </summary>
        public virtual bool PreKill(Item item, Player player, GodSmithPlayer state, double damage,
            int hitDirection, bool pvp, ref bool playSound, ref bool genGore, ref PlayerDeathReason damageSource) => true;

        //==================== 本地化 ====================

        /// <summary>tooltip 效果行（金色系，随标题行注入）</summary>
        public LocalizedText EffectDesc { get; private set; }

        internal void LoadLocalization() => EffectDesc = this.GetLocalization("EffectDesc", () => EffectDescFallback);
    }
}
