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
    /// 极光帘幕：立于空间中的竖直光帘，缓缓横漂并波动；
    /// ai[0]=波动相位 ai[1]=横漂速度px/f(带符号) ai[2]=寿命帧数
    /// 判定只认帘心亮带，且入场2秒宽限；帘缘羽化纯装饰
    /// </summary>
    internal class EmpressAuroraVeil : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        private const float VeilHalfHeight = 1500f;
        private const float VisualHalfWidth = 190f;
        private const float CoreHalfWidth = 34f;
        private const int GraceTime = 110;
        private const int FadeOut = 55;

        private ref float Timer => ref Projectile.localAI[0];
        private float Phase => Projectile.ai[0];
        private float DriftSpeed => Projectile.ai[1];
        private int LifeFrames => Math.Max((int)Projectile.ai[2], GraceTime + FadeOut + 30);

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2600;

        public override void SetDefaults() {
            Projectile.width = 60;
            Projectile.height = 60;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 900;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        /// <summary>帘幕总强度包络</summary>
        private float Envelope() {
            float fadeIn = MathHelper.Clamp(Timer / GraceTime, 0f, 1f);
            float fadeOut = MathHelper.Clamp((LifeFrames - Timer) / (float)FadeOut, 0f, 1f);
            return VaultUtils.EaseOutQuad(fadeIn) * fadeOut;
        }

        /// <summary>帘心当前横向偏摆（Time确定函数，各端一致）</summary>
        private float SwayOffset(float yNorm) {
            float t = Timer * 0.016f + Phase;
            return (float)(Math.Sin(t + yNorm * 2.6f) * 46f + Math.Sin(t * 1.7f + yNorm * 5.2f) * 22f);
        }

        public override void AI() {
            if (Timer == 0f) {
                Projectile.timeLeft = LifeFrames;
                //零伤帘幕（死亡演出用）不参与敌对判定
                if (Projectile.damage <= 0) {
                    Projectile.hostile = false;
                }
            }
            Timer++;

            //横漂
            Projectile.velocity = new Vector2(DriftSpeed, 0f);
            Projectile.Opacity = Envelope();

            //沿帘照明与光尘
            float env = Envelope();
            if (env > 0.2f) {
                for (int i = -3; i <= 3; i++) {
                    float yNorm = i / 3f;
                    Vector2 pos = Projectile.Center + new Vector2(SwayOffset(yNorm), yNorm * VeilHalfHeight * 0.8f);
                    Lighting.AddLight(pos, Main.hslToRgb((Phase * 0.2f + yNorm * 0.24f + 1f) % 1f, 0.9f, 0.5f).ToVector3() * 0.5f * env);
                }
                if (!VaultUtils.isServer && Main.rand.NextBool(3)) {
                    float yNorm = Main.rand.NextFloat(-1f, 1f);
                    Vector2 pos = Projectile.Center + new Vector2(SwayOffset(yNorm) + Main.rand.NextFloat(-40f, 40f), yNorm * VeilHalfHeight);
                    PRTLoader.NewParticle<PRT_EmpressPetalDust>(pos, new Vector2(DriftSpeed * 0.4f, Main.rand.NextFloat(-0.7f, -0.2f)),
                        Main.hslToRgb(Main.rand.NextFloat(), 0.85f, 0.62f), Main.rand.NextFloat(0.4f, 0.75f))?.Configure(36, Phase % 1f);
                }
            }
        }

        //宽限期与消退期无伤
        public override bool? CanDamage() => Timer > GraceTime && Timer < LifeFrames - FadeOut ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            //帘心亮带：按目标高度取当前偏摆，做窄带判定
            float yNorm = MathHelper.Clamp((targetHitbox.Center.Y - Projectile.Center.Y) / VeilHalfHeight, -1f, 1f);
            float coreX = Projectile.Center.X + SwayOffset(yNorm);
            //纵向判定收到视觉实区（羽化端0.78以外纯装饰无判定）
            return Math.Abs(targetHitbox.Center.X - coreX) < CoreHalfWidth + targetHitbox.Width * 0.5f
                && Math.Abs(targetHitbox.Center.Y - Projectile.Center.Y) < VeilHalfHeight * 0.78f;
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(BuffID.Confused, 60);
        }

        public override bool PreDraw(ref Color lightColor) => false;

        void IPrimitiveDrawable.DrawPrimitives() {
            float env = Envelope();
            if (env <= 0.01f) {
                return;
            }
            Effect effect = EffectLoader.EmpressAurora?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (effect == null || noise == null) {
                return;
            }

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            BlendState origBlend = device.BlendState;
            RasterizerState origRaster = device.RasterizerState;
            device.BlendState = BlendState.Additive;
            device.RasterizerState = RasterizerState.CullNone;

            effect.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uIntensity"]?.SetValue(env);
            effect.Parameters["uPhase"]?.SetValue(Phase);
            //帘心偏摆相位：与判定同源，亮心画在真实危险区
            effect.Parameters["uSwayTime"]?.SetValue(Timer * 0.016f + Phase);
            effect.Parameters["uNoiseTex"]?.SetValue(noise);
            //判定亮带的相对宽度
            effect.Parameters["uCoreRatio"]?.SetValue(CoreHalfWidth / VisualHalfWidth);

            Vector2 top = Projectile.Center - new Vector2(0f, VeilHalfHeight);
            Vector2 bottom = Projectile.Center + new Vector2(0f, VeilHalfHeight);

            //uv.x=横截（0左1右），uv.y=纵向（0顶1底）
            VertexPositionColorTexture[] verts = new VertexPositionColorTexture[4];
            verts[0] = new VertexPositionColorTexture((top - new Vector2(VisualHalfWidth, 0f)).ToVector3(), Color.White, new Vector2(0f, 0f));
            verts[1] = new VertexPositionColorTexture((top + new Vector2(VisualHalfWidth, 0f)).ToVector3(), Color.White, new Vector2(1f, 0f));
            verts[2] = new VertexPositionColorTexture((bottom - new Vector2(VisualHalfWidth, 0f)).ToVector3(), Color.White, new Vector2(0f, 1f));
            verts[3] = new VertexPositionColorTexture((bottom + new Vector2(VisualHalfWidth, 0f)).ToVector3(), Color.White, new Vector2(1f, 1f));
            foreach (EffectPass pass in effect.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, 2);
            }

            device.BlendState = origBlend;
            device.RasterizerState = origRaster;
        }
    }
}
