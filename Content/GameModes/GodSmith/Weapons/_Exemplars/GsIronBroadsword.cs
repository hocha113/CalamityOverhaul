using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Broadswords;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons._Exemplars
{
    /// <summary>
    /// 【范例·近战接管】铁宽剑重铸：三拍屠宰连击（举-滞-斩），两记交替重劈接一记更重的终结斩。<br/>
    /// 按框架规矩走：不碰 SetDefaults，由 GsCanUseItem 在 owner 侧生成手持弹幕并压掉原版挥舞，切模式即时恢复原版。
    /// 手持几何完全走阔剑族基类 <see cref="GsBroadswordHeldBase"/>（举-滞-斩-收、硬停顿、顿帧、重拍震屏），
    /// 本文件只剩拍表与色板；玩家位移不做。<br/>
    /// 材质：冷锻铁刃。签名行为：终结斩弧更大更重
    /// </summary>
    internal class GsIronBroadsword : GodSmithScheme
    {
        public override int TargetItemID => ItemID.IronBroadsword;

        public override string GsFamily => "Exemplars";

        protected override string GsDescFallback =>
            "Reforged: a three-beat butcher combo; the third strike lands a heavier, wider arc";
        internal static readonly Color IronBright = new(222, 226, 232);  //钢灰亮
        internal static readonly Color IronMain = new(158, 164, 176);    //铁身
        internal static readonly Color IronHot = new(255, 168, 92);      //摩擦灼橙

        /// <summary>连段计数，取模三拍；只在本地玩家路径消费（方案单例跨玩家共享）</summary>
        private int comboCounter;
        /// <summary>断手回第一拍的倒计时，只在本地玩家路径消费</summary>
        private int comboResetTimer;

        public override bool? GsCanUseItem(Item item, Player player) {
            //手持弹幕在场即攻击冷却（真实冷却 = max(useTime, 弹幕总帧)，两者都吃攻速）
            if (HeldAlive<GsIronBroadswordHeld>(player)) {
                return false;
            }
            if (player.whoAmI == Main.myPlayer) {
                int beat = comboCounter % 3;
                float swingSign = comboCounter % 2 == 0 ? 1f : -1f;
                comboCounter++;
                comboResetTimer = 55;
                Projectile.NewProjectile(player.GetSource_ItemUse(item), player.Center, GsAimUnit(player),
                    ModContent.ProjectileType<GsIronBroadswordHeld>(),
                    player.GetWeaponDamage(item), item.knockBack, player.whoAmI, beat, swingSign);
            }
            //全端返回 false 压掉原版挥舞；远端靠弹幕同步看到动作
            return false;
        }

        public override void GsHoldItem(Item item, Player player) {
            if (player.whoAmI != Main.myPlayer) {
                return;
            }
            if (comboResetTimer > 0 && --comboResetTimer == 0) {
                comboCounter = 0;
            }
        }

        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.10f;//弱势前期武器，重铸补一成底伤，综合 DPS 落在原版 105%~115%

        /// <summary>
        /// 压掉原版挥舞的物理尾巴：held 每帧强撑 itemAnimation&gt;0 而铁宽剑 noMelee=false（SetDefaults 密封禁改），
        /// 不压则 Player.ItemCheck 的隐形挥舞碰撞箱在 owner 端逐帧结算，与 held 扫击双吃。
        /// noHitbox=true 令 GetMeleeHitbox 置 dontAttack，整段近战尾巴跳过。近战接管范式必带此压制
        /// </summary>
        public override void GsUseItemHitbox(Item item, Player player, ref Rectangle hitbox, ref bool noHitbox)
            => noHitbox = true;
    }

    /// <summary>
    /// 铁宽剑手持挥砍：族基类的标准拍表原样使用（0/1 交替重劈 = Standard，2 终结斩 = Finisher）。
    /// ai[0]=拍号，ai[1]=交替符号
    /// </summary>
    internal class GsIronBroadswordHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.IronBroadsword;
        protected override Color EdgeBright => GsIronBroadsword.IronBright;
        protected override Color BodyMain => GsIronBroadsword.IronMain;
        protected override Color HotAccent => GsIronBroadsword.IronHot;

        protected override GsBroadBeat GetBeat(int stage)
            => stage >= 2 ? GsBroadBeat.Finisher : GsBroadBeat.Standard;
    }
}
