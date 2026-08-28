using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Boomerangs
{
    /// <summary>
    /// 烈焰回旋镖重铸。材质：狱岩燃刃。签名行为：①全程点燃命中目标
    /// ②悬停顶点炸开一圈烈焰环 ③回程沿路撒落余烬火线，火星寿命长过镖体
    /// </summary>
    internal class GsFlamarang : GsBoomerScheme
    {
        public override int TargetItemID => ItemID.Flamarang;

        internal override int BoomerProjType => ModContent.ProjectileType<GsFlamarangProj>();

        internal override float DamageMul => 1.0f;

        protected override string GsDescFallback =>
            "Every hit sets the target on fire\n" +
            "At the hover peak it bursts into a ring of flame; the return path drops smoldering embers\n" +
            "Right click while it flies: command it to dash toward your cursor";
    }

    /// <summary>燃刃镖体：引燃弧线</summary>
    internal class GsFlamarangProj : GsBoomerProjBase
    {
        internal override int SourceItemID => ItemID.Flamarang;

        protected override Color GlowColor => new(255, 140, 50);

        protected override Color TrailColor => new(255, 100, 40);

        protected override int HoverTime => 20;

        protected override SoundStyle HitSound => SoundID.Item20 with { Volume = 0.4f, Pitch = 0.2f };

        protected override void OnHitEffects(NPC target, NPC.HitInfo hit, int damageDone)
            => target.AddBuff(BuffID.OnFire, 240);

        protected override void OnEnterPhase(int phase, Player owner) {
            if (phase != PhaseHover || VaultUtils.isServer) {
                return;
            }
            //悬停顶点烈焰环：一圈定向火舌 + 爆燃声
            SoundEngine.PlaySound(SoundID.Item20 with { Volume = 0.7f, Pitch = -0.1f }, Projectile.Center);
            for (int i = 0; i < 10; i++) {
                Vector2 vel = (MathHelper.TwoPi / 10f * i).ToRotationVector2() * Main.rand.NextFloat(2.5f, 4f);
                PRTLoader.NewParticle<PRT_HellFire>(Projectile.Center, vel, GlowColor,
                    Main.rand.NextFloat(0.5f, 0.8f));
            }
            PRTLoader.NewParticle<PRT_Light>(Projectile.Center, Vector2.Zero, GlowColor, 0.5f)?.Configure(12, 0.9f);
        }

        protected override void FlightFX(Player owner) {
            //火镖不走默认点缀：全程火舌，回程加密成余烬火线
            int interval = Phase == PhaseReturn ? 2 : 4;
            if (PhaseTimer % interval == 0) {
                PRTLoader.NewParticle<PRT_HellFire>(
                    Projectile.Center + Main.rand.NextVector2Circular(6f, 6f),
                    (-Projectile.velocity * 0.06f) + new Vector2(0f, -Main.rand.NextFloat(0.3f, 0.9f)),
                    TrailColor, Main.rand.NextFloat(0.4f, 0.7f));
            }
            //回程余烬：带重力的火星滞留地面附近，寿命长过镖体
            if (Phase == PhaseReturn && PhaseTimer % 5 == 0) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                    new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), Main.rand.NextFloat(0.2f, 0.8f)),
                    GlowColor, Main.rand.NextFloat(0.35f, 0.5f))?.Configure(true, Main.rand.Next(30, 50));
            }
        }

        protected override void HitBurstFX(NPC target, NPC.HitInfo hit) {
            PRTLoader.NewParticle<PRT_Light>(target.Center, Vector2.Zero, GlowColor, 0.28f)?.Configure(10, 0.85f);
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_HellFire>(target.Center,
                    Main.rand.NextVector2Circular(3f, 3f), GlowColor,
                    Main.rand.NextFloat(0.45f, 0.7f));
            }
        }
    }
}
