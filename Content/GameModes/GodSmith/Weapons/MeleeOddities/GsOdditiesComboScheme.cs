using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MeleeOddities
{
    /// <summary>
    /// 近战异形族·连段方案基类：镜像 GsBroadswordScheme 的连段记账
    /// （该基类 GsFamily 密封为 Broadswords，本族不能直用，否则 loc 键落错文件）。<br/>
    /// GsCanUseItem 在 owner 侧生成手持弹幕并全端返回 false 压掉原版挥舞，
    /// 远端靠弹幕同步看到动作。<br/>
    /// 联机纪律：方案单例跨玩家共享，连段字段只在 myPlayer 守门路径消费
    /// </summary>
    internal abstract class GsOdditiesComboScheme : GodSmithScheme
    {
        public sealed override string GsFamily => "MeleeOddities";

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
