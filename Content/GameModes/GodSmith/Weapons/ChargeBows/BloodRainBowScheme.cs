using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.ChargeBows.Projectiles;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.ChargeBows
{
    /// <summary>
    /// 血雨弓：原版「任意箭化为天降血雨矢」保留（held 接管后在 OnLoose 复刻天降轨迹）。
    /// T2 释放追加 4 道天矢锁定准星（各 60%，高度阶梯自然错帧）；
    /// T3 血洪 8 道，且主矢命中点留 2 秒血泊（踩踏伤 + 减速，判定与可见体同源）
    /// </summary>
    internal class GsBloodRainBow : GsChargeBowScheme
    {
        public override int TargetItemID => ItemID.BloodRainBow;
        protected override string GsDescFallback =>
            "Reforged: three-stage draw. Arrows still fall as blood rain; a full draw calls four extra bolts onto the cursor, an overdrawn flood calls eight and pools blood where the main bolt lands";
        internal override float DpsTarget => 1.0f;
        internal override Color TrailMain => new(214, 42, 54);
        internal override Color TrailHot => new(255, 120, 120);
        internal override Color TrailDeep => new(96, 14, 26);

        //任意档任意弹药都走天降复刻（原版转化语义）
        internal override bool CustomLoose(int tier, int shootType) => true;

        internal override void OnLoose(Player player, Item item, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback, int tier) {
            //owner 端：准星即落点锚
            Vector2 anchor = Main.MouseWorld;
            float fallSpeed = Math.Max(9f, velocity.Length() * 0.9f);

            //主矢：准星正上方落下，全额伤害
            StampNext(tier, KindMain);
            SpawnRainBolt(source, anchor, 0f, 560f, fallSpeed, damage, knockback, player.whoAmI);

            //追加天矢：T2 四道、T3 八道，60% 伤，x 散布 + 高度阶梯错时落地
            int extra = tier >= 3 ? 8 : tier >= 2 ? 4 : 0;
            int extraDamage = Math.Max(1, (int)(damage * 0.6f));
            for (int i = 0; i < extra; i++) {
                float xOff = Main.rand.NextFloat(-46f, 46f);
                float height = 500f + i * 34f + Main.rand.NextFloat(0f, 18f);
                StampNext(tier, KindBloodRain);
                SpawnRainBolt(source, anchor, xOff, height, fallSpeed, extraDamage, knockback * 0.5f, player.whoAmI);
            }
        }

        private static void SpawnRainBolt(EntitySource_ItemUse_WithAmmo source, Vector2 anchor,
            float xOff, float height, float fallSpeed, int damage, float knockback, int owner) {
            Vector2 pos = new(anchor.X + xOff, anchor.Y - height);
            Projectile.NewProjectile(source, pos, new Vector2(0f, fallSpeed),
                ProjectileID.BloodArrow, damage, knockback, owner);
        }

        internal override void OnQualityHit(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router, int tier) {
            if (tier < 3 || !ValidRiderTarget(target)) {
                return;
            }
            //血泊：owner 端先探地再生成，位置随生成包定死；悬空目标直接以敌为心
            Vector2 anchor = FindGroundBelow(target.Center, 10);
            int puddleDamage = Math.Max(3, (int)(damageDone * 0.2f));
            Projectile.NewProjectile(Main.player[proj.owner].GetSource_Misc("GsBloodPuddle"),
                anchor, Vector2.Zero, ModContent.ProjectileType<GsBloodPuddleProj>(),
                puddleDamage, 0f, proj.owner);
        }

        /// <summary>自 center 向下逐格探实心地面，找到则贴地，找不到原地返回</summary>
        internal static Vector2 FindGroundBelow(Vector2 center, int maxTiles) {
            int tileX = (int)(center.X / 16f);
            int tileY = (int)(center.Y / 16f);
            for (int i = 0; i < maxTiles; i++) {
                int y = tileY + i;
                if (y >= Main.maxTilesY - 10) {
                    break;
                }
                if (WorldGen.SolidTile(tileX, y)) {
                    return new Vector2(center.X, y * 16f - 10f);
                }
            }
            return center;
        }
    }
}
