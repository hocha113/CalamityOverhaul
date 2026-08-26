using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.ChargeBows
{
    /// <summary>
    /// 地狱之翼弓：高射速定位，蓄力时长全档 ×0.85。原版「木箭化穿墙火蝠」保留。
    /// T2 齐出 3 蝠 V 编队（出生 ±18px 平行位、同速自然保持约 20 帧后被原版摆动散开）；
    /// T3 狱蝠风暴：6 蝠以相位差先散后拢再放飞（确定性编舞，各端同式推演，标位随生成包过线）。
    /// 非木箭弹药退化为普通质变单箭
    /// </summary>
    internal class GsHellwingBow : GsChargeBowScheme
    {
        public override int TargetItemID => ItemID.HellwingBow;
        protected override string GsDescFallback =>
            "Reforged: quick three-stage draw (charges 15% faster). A full draw of wooden arrows looses a V of three hellwings; an overdrawn draw unleashes a spiraling storm of six";
        internal override float DpsTarget => 1.02f;
        internal override float ChargeScale => 0.85f;
        internal override Color TrailMain => new(255, 110, 40);
        internal override Color TrailHot => new(255, 200, 110);
        internal override Color TrailDeep => new(110, 40, 22);

        internal override int TransformShootType(int pickedType, int tier)
            => pickedType == ProjectileID.WoodenArrowFriendly ? ProjectileID.Hellwing : pickedType;

        internal override bool CustomLoose(int tier, int shootType)
            => tier >= 2 && shootType == ProjectileID.Hellwing;

        internal override void OnLoose(Player player, Item item, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback, int tier) {
            if (tier < 2 || type != ProjectileID.Hellwing) {
                return;
            }
            Vector2 perp = velocity.SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2);
            if (tier == 2) {
                //V 编队：中蝠领航 + 两翼平行蝠，同速出巢自然保持队形
                int mid = Math.Max(1, (int)(damage * 0.7f));
                int wing = Math.Max(1, (int)(damage * 0.45f));
                StampNext(tier, KindMain);
                Projectile.NewProjectile(source, position, velocity, type, mid, knockback, player.whoAmI);
                for (int i = 0; i < 2; i++) {
                    float side = i == 0 ? 1f : -1f;
                    StampNext(tier, KindBatWing + (i + 1) * 1000);
                    Projectile.NewProjectile(source, position + perp * side * 18f - velocity.SafeNormalize(Vector2.UnitX) * 10f,
                        velocity, type, wing, knockback * 0.6f, player.whoAmI);
                }
                return;
            }
            //狱蝠风暴：6 蝠相位差编舞，先散后拢
            int swarm = Math.Max(1, (int)(damage * 0.33f));
            for (int i = 0; i < 6; i++) {
                StampNext(tier, KindBatSpiral + i * 1000);
                Projectile.NewProjectile(source, position, velocity, type, swarm, knockback * 0.5f, player.whoAmI);
            }
        }

        internal override void ArrowPostAI(Projectile proj, GodSmithProjRouter router, int tier, int kind) {
            if (kind != KindBatSpiral) {
                return;
            }
            //编舞帧计数放每弹幕状态包：各端 PostAI 同步自增，公式确定性，无随机
            SpiralState state = router.GetOrCreateState<SpiralState>();
            state.T++;
            int index = DecodeIndex(router.MarkData2);
            float sign = index % 2 == 0 ? 1f : -1f;
            float mag = (index / 2 + 1) * 0.035f;
            if (state.T < 12) {
                //散开
                proj.velocity = proj.velocity.RotatedBy(sign * mag);
            }
            else if (state.T < 24) {
                //收拢（与散开等帧对称，不过转）
                proj.velocity = proj.velocity.RotatedBy(-sign * mag);
            }
            if (!VaultUtils.isServer && state.T < 30 && Main.rand.NextBool(3)) {
                PRTLoader.NewParticle<PRT_HellFire>(proj.Center - proj.velocity * 0.5f,
                    -proj.velocity * 0.05f, Color.White, Main.rand.NextFloat(0.5f, 0.8f));
            }
        }

        private class SpiralState
        {
            public int T;
        }
    }
}
