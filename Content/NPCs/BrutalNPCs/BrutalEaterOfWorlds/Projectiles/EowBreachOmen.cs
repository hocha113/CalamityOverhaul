using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEaterOfWorlds.Rendering;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEaterOfWorlds.Projectiles
{
    /// <summary>破土预兆盘(无伤害纯预警)；ai[0]=持续帧 ai[1]=半径档 0常规 1大型；锚地表点</summary>
    internal class EowBreachOmen : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private int Duration => Math.Max((int)Projectile.ai[0], 10);
        private float RadiusPx => Projectile.ai[1] == 1f ? 300f : 150f;

        private int Age => (int)Projectile.localAI[0];
        private float ChargeT => MathHelper.Clamp(Age / (float)Duration, 0f, 1f);

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 700;

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 12;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 300;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (Projectile.localAI[1] == 0f) {
                Projectile.localAI[1] = 1f;
                Projectile.timeLeft = Duration;
            }
            Projectile.localAI[0]++;

            if (VaultUtils.isServer || !EowMotionFX.OnScreen(Projectile.Center, 500f)) {
                return;
            }

            //向心汇聚的蚀土屑，密度随充能爬升，末25%静默(收势)
            float t = ChargeT;
            if (t < 0.75f) {
                int count = 1 + (int)(t * 3f);
                for (int i = 0; i < count; i++) {
                    Vector2 dustPos = Projectile.Center
                        + new Vector2(Main.rand.NextFloat(-1f, 1f) * RadiusPx, Main.rand.NextFloat(-6f, 4f));
                    Dust dust = Dust.NewDustDirect(dustPos, 4, 4,
                        Main.rand.NextBool(3) ? DustID.CorruptGibs : DustID.Dirt,
                        0, 0, 110, default, Main.rand.NextFloat(1.0f, 1.7f));
                    dust.velocity = (Projectile.Center - dustPos).SafeNormalize(Vector2.Zero) * (1.4f + t * 3.4f)
                        - Vector2.UnitY * Main.rand.NextFloat(0.5f, 1.6f);
                    dust.noGravity = true;
                }
            }

            //震颤随充能加深
            if (Age % 9 == 0 && t > 0.3f) {
                EowMotionFX.CameraPunch(Projectile.Center, 1f + t * 2.6f, 10, "EowOmenRumble");
            }
            Lighting.AddLight(Projectile.Center, EowMotionFX.AcidGreen.ToVector3() * (0.25f + t * 0.8f));
        }

        public override bool PreDraw(ref Color lightColor) {
            Effect effect = EffectLoader.EowGeyser?.Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            if (effect != null) {
                effect.CurrentTechnique = effect.Techniques["TechOmen"];
                effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
                effect.Parameters["uSeed"]?.SetValue(Projectile.whoAmI % 71 * 0.149f);
                effect.Parameters["uProgress"]?.SetValue(ChargeT);
                effect.Parameters["uFade"]?.SetValue(0f);
                effect.Parameters["uAspect"]?.SetValue(1f);
                effect.Parameters["uDirtColor"]?.SetValue(EowMotionFX.DirtBrown.ToVector3());
                effect.Parameters["uAcidColor"]?.SetValue(EowMotionFX.AcidGreen.ToVector3());

                SpriteBatch sb = Main.spriteBatch;
                sb.End();
                sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, Main.DefaultSamplerState,
                    DepthStencilState.None, RasterizerState.CullNone, effect, Main.GameViewMatrix.TransformationMatrix);
                effect.CurrentTechnique.Passes[0].Apply();

                Texture2D pixel = VaultAsset.placeholder2.Value;
                //压扁的地面椭圆盘
                Vector2 scale = new Vector2(RadiusPx * 2f / pixel.Width, RadiusPx * 0.62f / pixel.Height);
                sb.Draw(pixel, drawPos, null, Color.White, 0f, pixel.Size() / 2f, scale, SpriteEffects.None, 0f);

                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                    DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
                return false;
            }

            //回退：软光压扁双层呼吸
            Texture2D softGlow = CWRAsset.SoftGlow.Value;
            float pulse = 0.6f + 0.4f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * (6f + ChargeT * 12f));
            Color warn = EowMotionFX.AcidGreen with { A = 0 } * (ChargeT * 0.6f * pulse);
            Main.EntitySpriteDraw(softGlow, drawPos, null, warn, 0f, softGlow.Size() / 2f,
                new Vector2(RadiusPx / softGlow.Width * 2.2f, 0.4f), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(softGlow, drawPos, null, warn * 0.6f, 0f, softGlow.Size() / 2f,
                new Vector2(RadiusPx / softGlow.Width * 1.3f, 0.26f), SpriteEffects.None, 0);
            return false;
        }

        public override void DrawBehind(int index, System.Collections.Generic.List<int> behindNPCsAndTiles,
            System.Collections.Generic.List<int> behindNPCs, System.Collections.Generic.List<int> behindProjectiles,
            System.Collections.Generic.List<int> overPlayers, System.Collections.Generic.List<int> overWiresUI) {
            behindNPCs.Add(index);
        }
    }
}
