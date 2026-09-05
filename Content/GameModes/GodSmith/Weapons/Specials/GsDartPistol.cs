using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Specials
{
    /// <summary>
    /// 镖枪重铸（L1 弹口层）：镖种特效（诅咒/灵液/水晶镖）完整保留。<br/>
    /// [双针点射] 两连快镖压缩节拍（第 2 支 70% 伤），随后强制间歇，每发真实耗镖
    /// </summary>
    internal class GsDartPistol : GodSmithScheme
    {
        public override int TargetItemID => ItemID.DartPistol;

        public override string GsFamily => "Specials";

        protected override string GsDescFallback =>
            "Reforged: quick two-round bursts, second dart at 70%\nDart ammo effects are fully preserved";
        /// <summary>双针打完后的强制间歇</summary>
        private const int BurstGap = 12;

        //以下瞬时字段全部只在本地玩家路径消费（方案单例的 owner 契约）
        private int burstStep;
        private int burstGapTimer;
        private uint lastShotTick;

        public override bool? GsCanUseItem(Item item, Player player) {
            //两发打完后的节拍器强制间歇
            if (player.whoAmI == Main.myPlayer && burstGapTimer > 0) {
                return false;
            }
            return null;
        }

        //只对本地玩家加速；远端动画差异由弹幕生成自然呈现
        public override float GsUseSpeedMultiplier(Item item, Player player)
            => player.whoAmI == Main.myPlayer ? 1.75f : 1f;

        public override void GsHoldItem(Item item, Player player) {
            if (player.whoAmI != Main.myPlayer) {
                return;
            }
            if (burstGapTimer > 0) {
                burstGapTimer--;
            }
            //断手回拍：第二针迟迟不来就取消半截连发
            if (burstStep == 1 && Main.GameUpdateCount - lastShotTick > 30) {
                burstStep = 0;
            }
        }

        public override void GsModifyShootStats(Item item, Player player, ref Vector2 position,
            ref Vector2 velocity, ref int type, ref int damage, ref float knockback) {
            if (burstStep == 1) {
                damage = (int)(damage * 0.7f);
            }
        }

        public override bool? GsShoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            lastShotTick = Main.GameUpdateCount;
            burstStep++;
            if (burstStep >= 2) {
                burstStep = 0;
                burstGapTimer = BurstGap;
            }
            return null;//原版镖直通
        }

        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.08f;//基线补偿，综合 DPS 落在原版 108%~112%
    }
}
