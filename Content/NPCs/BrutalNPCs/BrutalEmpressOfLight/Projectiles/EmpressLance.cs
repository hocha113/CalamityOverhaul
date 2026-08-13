using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Rendering;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Projectiles
{
    /// <summary>
    /// 以太枪骑：预告线→光矛贯穿；
    /// ai[0]=飞行角度 ai[1]=色相 ai[2]=预告帧数（错拍执行的关键参数）
    /// 全程确定性，无需追加同步
    /// </summary>
    internal class EmpressLance : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        private const float FlySpeed = 40f;
        private const float SpearLength = 320f;
        private const float TelegraphLength = 2800f;
        private const int FlyLife = 86;

        private ref float Timer => ref Projectile.localAI[0];
        private float Angle => Projectile.ai[0];
        private float Hue => Projectile.ai[1];
        private int TelegraphTime => Math.Max((int)Projectile.ai[2], 12);

        private bool Launched => Timer >= TelegraphTime;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2400;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 24;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;//SetDefaults 时读不到 ai，入场首帧改为真实寿命
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        public override void AI() {
            if (Timer == 0f) {
                //首帧对表：总寿命=预告+飞行
                Projectile.timeLeft = TelegraphTime + FlyLife;
                Projectile.rotation = Angle;
                Projectile.velocity = Vector2.Zero;
            }
            Timer++;

            if (!Launched) {
                //预告期：静止，预告线亮度爬升
                Projectile.velocity = Vector2.Zero;
                Projectile.Opacity = MathHelper.Clamp(Timer / 14f, 0f, 1f);

                //即将发射前3帧，收束火花（屏息拍）
                if (!VaultUtils.isServer && Timer > TelegraphTime - 4 && Main.rand.NextBool(2)) {
                    Vector2 gather = Projectile.Center + Main.rand.NextVector2CircularEdge(60f, 60f);
                    PRTLoader.NewParticle<PRT_EmpressSpark>(gather, (Projectile.Center - gather) * 0.16f,
                        Main.hslToRgb(Hue, 1f, 0.7f), Main.rand.NextFloat(0.6f, 1f))?.Configure(10, Hue);
                }
            }
            else {
                if (Timer == TelegraphTime + 1) {
                    //一帧内点火，冲出
                    Projectile.velocity = Angle.ToRotationVector2() * FlySpeed;
                    if (!VaultUtils.isServer) {
                        PRTLoader.NewParticle<PRT_EmpressRipple>(Projectile.Center, Vector2.Zero, Color.White, 0.5f)?
                            .Configure(14, Hue);
                    }
                }
                Projectile.Opacity = MathHelper.Clamp(Projectile.timeLeft / 12f, 0f, 1f);

                //飞行拖尾闪尘
                if (!VaultUtils.isServer && Main.rand.NextBool(3)) {
                    PRTLoader.NewParticle<PRT_EmpressSpark>(
                        Projectile.Center - Angle.ToRotationVector2() * Main.rand.NextFloat(SpearLength * 0.6f),
                        Main.rand.NextVector2Circular(1.2f, 1.2f), Main.hslToRgb(Hue, 1f, 0.62f),
                        Main.rand.NextFloat(0.5f, 0.9f))?.Configure(14, Hue);
                }
            }

            Lighting.AddLight(Projectile.Center, Main.hslToRgb(Hue, 1f, 0.55f).ToVector3() * 0.5f * Projectile.Opacity);
        }

        //预告期无伤，伤害窗与可见冲刺对齐
        public override bool? CanDamage() => Launched ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!Launched) {
                return false;
            }
            float p = 0f;
            Vector2 dir = Angle.ToRotationVector2();
            //矛体从弹头向后延伸，判定略窄于视觉
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                Projectile.Center, Projectile.Center - dir * SpearLength, 22f, ref p);
        }

        public override bool PreDraw(ref Color lightColor) {
            //着色器缺失时走贴图后备（此处有活动batch，图元层没有）
            if (EffectLoader.EmpressLanceBeam?.Value == null) {
                DrawFallback();
            }
            return false;
        }

        void IPrimitiveDrawable.DrawPrimitives() {
            Effect effect = EffectLoader.EmpressLanceBeam?.Value;
            if (effect == null) {
                return;
            }

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            BlendState origBlend = device.BlendState;
            RasterizerState origRaster = device.RasterizerState;
            device.BlendState = BlendState.Additive;
            device.RasterizerState = RasterizerState.CullNone;

            effect.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uHue"]?.SetValue(Hue);

            Vector2 dir = Angle.ToRotationVector2();
            Vector2 perp = dir.RotatedBy(MathHelper.PiOver2);

            if (!Launched) {
                //预告线：细折光线沿整条路径，进度推亮
                float progress = Timer / (float)TelegraphTime;
                effect.Parameters["uProgress"]?.SetValue(progress);
                effect.Parameters["uOpacity"]?.SetValue(Projectile.Opacity);
                float laneHalf = 26f;
                Vector2 start = Projectile.Center - dir * 200f;
                Vector2 end = Projectile.Center + dir * TelegraphLength;
                DrawQuad(device, effect, "TelegraphTech", start, end, perp, laneHalf);
            }
            else {
                //光矛本体
                effect.Parameters["uProgress"]?.SetValue(1f);
                effect.Parameters["uOpacity"]?.SetValue(Projectile.Opacity);
                float spearHalf = 46f;
                Vector2 tip = Projectile.Center + dir * 30f;
                Vector2 tail = Projectile.Center - dir * SpearLength;
                DrawQuad(device, effect, "LanceTech", tail, tip, perp, spearHalf);
            }

            device.BlendState = origBlend;
            device.RasterizerState = origRaster;
        }

        private static void DrawQuad(GraphicsDevice device, Effect effect, string technique,
            Vector2 tail, Vector2 head, Vector2 perp, float halfWidth) {
            EffectTechnique tech = effect.Techniques[technique];
            if (tech == null) {
                return;
            }
            effect.CurrentTechnique = tech;
            VertexPositionColorTexture[] verts = new VertexPositionColorTexture[4];
            verts[0] = new VertexPositionColorTexture((tail + perp * halfWidth).ToVector3(), Color.White, new Vector2(0f, 0f));
            verts[1] = new VertexPositionColorTexture((tail - perp * halfWidth).ToVector3(), Color.White, new Vector2(0f, 1f));
            verts[2] = new VertexPositionColorTexture((head + perp * halfWidth).ToVector3(), Color.White, new Vector2(1f, 0f));
            verts[3] = new VertexPositionColorTexture((head - perp * halfWidth).ToVector3(), Color.White, new Vector2(1f, 1f));
            foreach (EffectPass pass in tech.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, 2);
            }
        }

        /// <summary>着色器缺失时的贴图后备</summary>
        private void DrawFallback() {
            Texture2D line = CWRAsset.MaskLaserLine.Value;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Vector2 dir = Angle.ToRotationVector2();
            Color prism = Main.hslToRgb(Hue, 1f, 0.62f) with { A = 0 };
            Main.spriteBatch.Draw(line, Projectile.Center - Main.screenPosition, null,
                prism * (Launched ? 0.9f : 0.35f) * Projectile.Opacity, Angle,
                new Vector2(0f, line.Height / 2f),
                new Vector2((Launched ? SpearLength : TelegraphLength) / line.Width, Launched ? 0.5f : 0.1f),
                SpriteEffects.None, 0);
            Main.spriteBatch.Draw(glow, Projectile.Center - Main.screenPosition, null, prism * Projectile.Opacity,
                0f, glow.Size() / 2f, 0.6f, SpriteEffects.None, 0);
        }
    }
}
