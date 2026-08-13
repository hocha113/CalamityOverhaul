using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalPlantera.Rendering
{
    /// <summary>
    /// 花瓣碎片：蜕壳/绽放/受创的介质。哑光瓣面+侧摆翻面透视，
    /// 瓣缘带一线荧光余温，先飘后坠，末段褪淡。
    /// </summary>
    internal class PRT_PlanteraPetal : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 160;

        private Color initialColor;
        private Color glowColor;
        private float swayPhase;
        private float swayRate;
        private float spin;
        private float fallCap;

        /// <summary>lifetime 寿命；fallSpeed 落速帽；glow 瓣缘荧光色</summary>
        public PRT_PlanteraPetal Configure(int lifetime, float fallSpeed, Color glow) {
            Lifetime = lifetime;
            initialColor = Color;
            fallCap = fallSpeed;
            glowColor = glow;
            return this;
        }

        public override void Reset() {
            base.Reset();
            initialColor = default;
            glowColor = default;
            swayPhase = 0f;
            swayRate = 0f;
            spin = 0f;
            fallCap = 0f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            Opacity = 1f;
            swayPhase = Main.rand.NextFloat(MathHelper.TwoPi);
            swayRate = Main.rand.NextFloat(0.06f, 0.11f);
            spin = Main.rand.NextFloat(0.03f, 0.07f) * (Main.rand.NextBool() ? 1f : -1f);
            if (Lifetime <= 0) {
                Lifetime = Main.rand.Next(50, 90);
            }
            if (fallCap <= 0f) {
                fallCap = 0.9f;
            }
            if (initialColor == default) {
                initialColor = Color;
            }
            if (glowColor == default) {
                glowColor = new Color(150, 255, 110);
            }
        }

        public override void AI() {
            swayPhase += swayRate;
            //爆发初速被空气阻尼吃掉，然后进入飘落
            Velocity.X = Velocity.X * 0.94f + MathF.Sin(swayPhase) * 0.05f;
            Velocity.Y = Math.Min(Velocity.Y * 0.94f + 0.03f, fallCap);
            Rotation += spin + MathF.Sin(swayPhase * 0.8f) * 0.025f;

            float t = LifetimeCompletion;
            float fade = MathHelper.Clamp((t - 0.62f) / 0.38f, 0f, 1f);
            Color = Color.Lerp(initialColor, Color.Transparent, MathF.Pow(fade, 1.35f));
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 pos = Position - Main.screenPosition;
            //翻面透视：宽度随摆相呼吸
            float flutter = 0.5f + 0.5f * MathF.Sin(swayPhase * 1.3f);
            Vector2 scale = new Vector2(0.3f * MathHelper.Lerp(0.4f, 1f, flutter), 0.5f) * Scale;

            spriteBatch.Draw(tex, pos, null, Color, Rotation, origin, scale, SpriteEffects.None, 0f);

            //瓣缘荧光余温，随寿命冷却
            float glowFade = (1f - LifetimeCompletion) * (Color.A / 255f);
            Color rim = glowColor with { A = 0 } * (0.4f * glowFade);
            spriteBatch.Draw(tex, pos + Rotation.ToRotationVector2() * 1.4f, null, rim,
                Rotation, origin, scale * new Vector2(0.62f, 0.9f), SpriteEffects.None, 0f);
            return false;
        }
    }

    /// <summary>
    /// 孢子微光：漂浮的荧光尘，慢漂+呼吸脉动；
    /// 可设汇聚点(吸收演出)，向心加速被吞。加色绘制。
    /// </summary>
    internal class PRT_PlanteraSporeMote : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "SoftGlow";
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 260;

        private float pulsePhase;
        private Vector2 convergeTarget;
        private bool converging;
        private Color initialColor;

        /// <summary>汇聚模式：向 target 加速并被吞</summary>
        public PRT_PlanteraSporeMote Converge(Vector2 target) {
            convergeTarget = target;
            converging = true;
            return this;
        }

        public PRT_PlanteraSporeMote SetLife(int lifetime) {
            Lifetime = lifetime;
            return this;
        }

        public override void Reset() {
            base.Reset();
            pulsePhase = 0f;
            convergeTarget = default;
            converging = false;
            initialColor = default;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            Opacity = 1f;
            pulsePhase = Main.rand.NextFloat(MathHelper.TwoPi);
            if (Lifetime <= 0) {
                Lifetime = Main.rand.Next(60, 130);
            }
            if (initialColor == default) {
                initialColor = Color;
            }
        }

        public override void AI() {
            pulsePhase += 0.09f;

            if (converging) {
                //向心吞入：加速+近点消亡
                Vector2 to = convergeTarget - Position;
                float dist = to.Length();
                Velocity = Vector2.Lerp(Velocity, to.SafeNormalize(Vector2.Zero) * Math.Min(4f + (140f - Math.Min(dist, 140f)) * 0.12f, 18f), 0.08f);
                if (dist < 24f) {
                    Lifetime = 0;
                }
            }
            else {
                //自由漂浮：微布朗+缓升
                Velocity *= 0.97f;
                Velocity += new Vector2(Main.rand.NextFloat(-0.02f, 0.02f), -0.008f);
            }

            float t = LifetimeCompletion;
            float fade = MathHelper.Clamp((t - 0.6f) / 0.4f, 0f, 1f);
            Color = Color.Lerp(initialColor, Color.Transparent, fade);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 pos = Position - Main.screenPosition;
            float pulse = 0.75f + 0.25f * MathF.Sin(pulsePhase);
            float baseScale = Scale * 0.062f * pulse;

            //核心亮点+外圈弱晕，微粒不是光球本体故极小
            spriteBatch.Draw(tex, pos, null, Color, 0f, tex.Size() * 0.5f, baseScale, SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, pos, null, Color * 0.35f, 0f, tex.Size() * 0.5f, baseScale * 2.2f, SpriteEffects.None, 0f);
            return false;
        }
    }
}
