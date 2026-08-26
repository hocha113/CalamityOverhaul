using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicMorph
{
    /// <summary>
    /// 水晶蛇重铸：标准二形态。<br/>
    /// A 形态爆裂碎晶多一次弹跳（承签穿透 +1）；
    /// B 形态（右键蓄 45t）「晶蟒」：正弦蛇行的大晶弹，爆裂时追加更多碎晶
    /// </summary>
    internal class GsCrystalSerpent : GsMorphScheme
    {
        public override int TargetItemID => ItemID.CrystalSerpent;

        protected override string GsDescFallback =>
            "Reforged: burst shards bounce once more.\nHold right click to charge; release a great crystal python that slithers in a sine wave and bursts into a richer shard shower";

        protected override int ChargeTicksB => 45;
        protected override float ChargeManaMult => 1.9f;
        protected override Color ChargeColor => new(255, 170, 220);
        protected override float BaseDamageMult => 1.08f;

        /// <summary>蛇行相位（每弹幕本地状态，寿命计数各端同步起点一致）</summary>
        private class SerpentAge
        {
            public int T;
        }

        protected override void FireMorphB(Item item, Player player) {
            SoundEngine.PlaySound(SoundID.Item109 with { Volume = 0.9f, Pitch = -0.25f }, player.Center);
            Vector2 dir = GsAimUnit(player);
            int dmg = (int)(player.GetWeaponDamage(item) * 1.6f);
            SpawnMorph(player, item, player.Center + dir * 16f, dir * MathHelper.Max(item.shootSpeed, 10f),
                ProjectileID.CrystalPulse, dmg, item.knockBack, KindB);
        }

        public override void GsProjOnSpawnInherited(Projectile proj, GodSmithProjRouter router,
            Projectile parent, GodSmithProjRouter parentRouter) {
            if (proj.type != ProjectileID.CrystalPulse2) {
                return;
            }
            //碎晶多一次弹跳（弹跳消耗 penetrate，守卫防 -1 无限穿被写坏）
            if (proj.penetrate > 0) {
                proj.penetrate++;
            }
        }

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            if (proj.type != ProjectileID.CrystalPulse || KindOf(router) != KindB) {
                return;
            }
            //晶蟒体型：幂等放大（scale 不入生成包，各端每帧对齐）
            if (proj.scale < 1.55f) {
                proj.scale = 1.6f;
            }
            //正弦蛇行：位移差分由自计寿命驱动，各端确定同式；原版判定（碰撞/爆裂）保留
            SerpentAge state = router.GetOrCreateState<SerpentAge>();
            int age = ++state.T;
            Vector2 perp = proj.velocity.SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2);
            float wave = MathF.Sin(age * 0.22f) - MathF.Sin((age - 1) * 0.22f);
            proj.position += perp * wave * 22f;
            if (!VaultUtils.isServer && proj.timeLeft % 2 == 0) {
                Color c = Main.rand.NextBool() ? new Color(255, 170, 220) : new Color(190, 150, 255);
                PRTLoader.NewParticle<PRT_DefCrystalShard>(proj.Center + Main.rand.NextVector2Circular(8f, 8f),
                    -proj.velocity * 0.06f, c, Main.rand.NextFloat(0.3f, 0.5f))
                    ?.Configure(Main.rand.Next(12, 20), Main.rand.NextFloat(-0.15f, 0.15f), 0.02f);
            }
        }

        public override void GsProjOnKill(Projectile proj, int timeLeft, GodSmithProjRouter router) {
            if (proj.type != ProjectileID.CrystalPulse || KindOf(router) != KindB
                || !proj.IsOwnedByLocalPlayer()) {
                return;
            }
            //晶蟒爆裂：在原版爆裂之上追加 3 枚碎晶（Parent 源承签）
            for (int i = 0; i < 3; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(7f, 7f);
                Projectile.NewProjectile(proj.GetSource_FromAI(), proj.Center, vel,
                    ProjectileID.CrystalPulse2, (int)(proj.damage * 0.22f), 0.5f, proj.owner);
            }
        }
    }
}
