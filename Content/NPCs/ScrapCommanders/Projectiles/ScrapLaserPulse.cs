using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.ScrapCommanders.Projectiles
{
    /// <summary>
    /// 镭射短脉冲：细快的锈红曳光弹，速度拉丝 + 焊橙芯，撞地一嘬火星
    /// </summary>
    internal class ScrapLaserPulse : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        public override void SetDefaults() {
            Projectile.width = 8;
            Projectile.height = 8;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.penetrate = 1;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.aiStyle = -1;
            Projectile.timeLeft = 180;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();
            Lighting.AddLight(Projectile.Center, 0.24f, 0.1f, 0.04f);
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            //撞点：脉冲环 + 反溅火花
            PRTLoader.NewParticle<PRT_HeartcarverPulseRing>(Projectile.Center, Vector2.Zero,
                new Color(255, 150, 58) * 0.8f, 1f)?.Configure(0.05f, 0.26f, 10);
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                    -Projectile.velocity.SafeNormalize(Vector2.UnitX)
                        .RotatedBy(Main.rand.NextFloat(-0.6f, 0.6f)) * Main.rand.NextFloat(2f, 5f),
                    new Color(255, 150, 58) * 0.8f, Main.rand.NextFloat(0.4f, 0.7f))
                    ?.Configure(true, Main.rand.Next(8, 14));
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            //曳光弹体：BeamLine 材质——白热弹头 + 锈红热流尾迹
            SpriteBatch sb = Main.spriteBatch;
            Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            float tailLen = MathHelper.Clamp(Projectile.velocity.Length() * 2.6f, 44f, 96f);
            Vector2 tail = Projectile.Center - dir * tailLen;
            Vector2 head = Projectile.Center + dir * 10f;

            ScrapVfx.BeginBeamBatch(sb);
            ScrapVfx.DrawBeam(sb, tail, head, 22f, 0.95f, 0f,
                Projectile.identity * 0.71f, ScrapVfx.BeamCoreWarm, ScrapVfx.BeamEdgeRust,
                0.55f, 0.04f);
            ScrapVfx.EndBeamBatch(sb);
            return false;
        }
    }
}
