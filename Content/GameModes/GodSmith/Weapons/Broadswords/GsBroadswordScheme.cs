using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using Terraria;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Broadswords
{
    /// <summary>
    /// 阔剑族方案基类：GsCanUseItem 在 owner 侧生成手持弹幕并全端返回 false 压掉原版挥舞，
    /// 远端靠弹幕同步看到动作；连段计数与断手回拍在这里统一记账。<br/>
    /// 子类填 <see cref="HeldProjID"/> 与 <see cref="ComboBeats"/>，
    /// 需要改拍号/符号的签名（居合蓄拍、指定拍插入）重写 <see cref="ModifyLocalSwing"/>；
    /// 重写 <see cref="GodSmithScheme.GsHoldItem"/> 时必须调 base 保住连段衰减。<br/>
    /// 联机纪律：方案单例跨玩家共享，连段字段只在 myPlayer 守门路径消费
    /// </summary>
    internal abstract class GsBroadswordScheme : GodSmithScheme
    {
        public sealed override string GsFamily => "Broadswords";

        /// <summary>手持弹幕类型（ModContent.ProjectileType&lt;XxxHeld&gt;()）</summary>
        protected abstract int HeldProjID { get; }

        /// <summary>连段拍数（与手持侧 BeatCount 一致）</summary>
        protected virtual int ComboBeats => 3;

        /// <summary>断手回第一拍的帧数</summary>
        protected virtual int ComboResetFrames => 55;

        /// <summary>连段计数；跨玩家共享单例，只在 myPlayer 路径消费</summary>
        protected int comboCounter;
        /// <summary>断手倒计时，只在 myPlayer 路径消费</summary>
        protected int comboResetTimer;

        public override bool? GsCanUseItem(Item item, Player player) {
            //手持弹幕在场即攻击冷却（真实冷却 = max(useTime, 弹幕总帧)，两者都吃攻速）
            if (player.ownedProjectileCounts[HeldProjID] > 0) {
                return false;
            }
            if (player.whoAmI == Main.myPlayer) {
                int beat = comboCounter % ComboBeats;
                float swingSign = comboCounter % 2 == 0 ? 1f : -1f;
                ModifyLocalSwing(item, player, ref beat, ref swingSign);
                comboCounter++;
                comboResetTimer = ComboResetFrames;
                Projectile.NewProjectile(player.GetSource_ItemUse(item), player.Center, GsAimUnit(player),
                    HeldProjID, player.GetWeaponDamage(item), item.knockBack, player.whoAmI, beat, swingSign);
            }
            //全端返回 false 压掉原版挥舞；远端靠弹幕同步看到动作
            return false;
        }

        /// <summary>出手前改拍号/交替符号（只在 myPlayer 路径被调；读 comboCounter 做条件拍合法）</summary>
        protected virtual void ModifyLocalSwing(Item item, Player player, ref int beat, ref float swingSign) { }

        /// <summary>
        /// 压掉原版挥舞的物理尾巴：held 每帧强撑 itemAnimation&gt;0 而原版阔剑 noMelee=false（SetDefaults 密封禁改），
        /// 不压则 Player.ItemCheck 的隐形挥舞碰撞箱在 owner 端逐帧结算，与 held 扫击双吃。
        /// noHitbox=true 令 GetMeleeHitbox 置 dontAttack，整段近战尾巴（命中/切块/挥舞粒子）跳过；全族生效
        /// </summary>
        public override void GsUseItemHitbox(Item item, Player player, ref Rectangle hitbox, ref bool noHitbox)
            => noHitbox = true;

        public override void GsHoldItem(Item item, Player player) {
            if (player.whoAmI != Main.myPlayer) {
                return;
            }
            if (comboResetTimer > 0 && --comboResetTimer == 0) {
                comboCounter = 0;
            }
        }
    }
}
