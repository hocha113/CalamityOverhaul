using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    /// <summary>
    /// 麻布焚灰片，哑光小灰屑，浮力上升 + 侧向摆动翻飞；
    /// 出生带余温暖色，数帧内冷却为烟灰冷灰（AlphaBlend 实体屑，非光效）
    /// </summary>
    internal class PRT_FishVoodooAsh : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";
        public override bool CanPool => true;

        private float swayPhase;
        private float swayAmp;
        private float spin;
        private float emberHeat; //出生余温 0-1，逐帧冷却
        private float baseScale;

        public PRT_FishVoodooAsh Configure(int lifetime, float heat = 1f) {
            Lifetime = lifetime;
            emberHeat = heat;
            return this;
        }

        public override void Reset() {
            base.Reset();
            swayPhase = 0f;
            swayAmp = 0f;
            spin = 0f;
            emberHeat = 0f;
            baseScale = 0f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            swayPhase = Main.rand.NextFloat(MathHelper.TwoPi);
            swayAmp = Main.rand.NextFloat(0.14f, 0.3f);
            spin = Main.rand.NextFloat(0.03f, 0.09f) * (Main.rand.NextBool() ? 1f : -1f);
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            baseScale = Scale * Main.rand.NextFloat(0.85f, 1.2f);
            if (Lifetime <= 0) {
                Lifetime = Main.rand.Next(45, 70);
            }
        }

        public override void AI() {
            //浮力升腾:纵向缓慢加速到 -0.7,横向正弦摆动翻飞
            Velocity.Y = MathHelper.Lerp(Velocity.Y, -0.7f, 0.03f);
            Velocity.X = Velocity.X * 0.96f + MathF.Sin(Time * 0.11f + swayPhase) * swayAmp * 0.16f;
            Rotation += spin + MathF.Sin(Time * 0.09f + swayPhase) * 0.02f;
            emberHeat *= 0.9f;

            float lc = LifetimeCompletion;
            float fadeIn = MathHelper.Clamp(Time / 4f, 0f, 1f);
            Opacity = fadeIn * (1f - MathF.Pow(lc, 1.7f));
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            if (Opacity < 0.02f) {
                return false;
            }
            Texture2D tex = TexValue;
            Vector2 pos = Position - Main.screenPosition;
            Vector2 origin = tex.Size() / 2f;
            //冷灰基色被余温向暗橙拉,余温几帧内耗尽
            Color cold = new Color(64, 58, 54);
            Color warm = new Color(150, 74, 34);
            Color body = Color.Lerp(cold, warm, emberHeat * 0.8f);
            Vector2 scale = new Vector2(0.085f, 0.06f) * baseScale;

            spriteBatch.Draw(tex, pos, null, body * (0.85f * Opacity), Rotation, origin, scale, SpriteEffects.None, 0f);
            //余温期一个极小加色芯(A=0 借 AlphaBlend 预乘走加色),冷却后消失
            if (emberHeat > 0.12f) {
                Color emberTip = new Color(226, 132, 52) with { A = 0 };
                spriteBatch.Draw(tex, pos, null, emberTip * (emberHeat * 0.7f * Opacity), Rotation, origin, scale * 0.45f, SpriteEffects.None, 0f);
            }
            return false;
        }
    }

    /// <summary>
    /// 麻线断纤，细短的干麻纤维束，受重力下坠带空气阻尼
    /// 摆动式飘落 + 自旋（哑光麻色/暗红，针刺点与娃娃散架时用）
    /// </summary>
    internal class PRT_FishVoodooFiber : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";
        public override bool CanPool => true;

        private float spin;
        private float swayPhase;
        private bool crimson; //true 用缝线暗红,false 用麻布色

        public PRT_FishVoodooFiber Configure(int lifetime, bool threadColor = false) {
            Lifetime = lifetime;
            crimson = threadColor;
            return this;
        }

        public override void Reset() {
            base.Reset();
            spin = 0f;
            swayPhase = 0f;
            crimson = false;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            spin = Main.rand.NextFloat(0.08f, 0.2f) * (Main.rand.NextBool() ? 1f : -1f);
            swayPhase = Main.rand.NextFloat(MathHelper.TwoPi);
            if (Lifetime <= 0) {
                Lifetime = Main.rand.Next(26, 40);
            }
        }

        public override void AI() {
            //羽毛式飘落:重力被空气阻尼压住,横向摆动
            if (Velocity.Y < 2.4f) {
                Velocity.Y += 0.09f;
            }
            Velocity.X = Velocity.X * 0.95f + MathF.Sin(Time * 0.17f + swayPhase) * 0.06f;
            Rotation += spin;
            spin *= 0.97f;

            float lc = LifetimeCompletion;
            Opacity = MathHelper.Clamp(Time / 3f, 0f, 1f) * (1f - MathF.Pow(lc, 2f));
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            if (Opacity < 0.02f) {
                return false;
            }
            Texture2D tex = TexValue;
            Vector2 pos = Position - Main.screenPosition;
            Vector2 origin = tex.Size() / 2f;
            Color body = crimson ? new Color(126, 30, 38) : new Color(122, 100, 70);
            //细长纤维条,末端一段更暗(断口)
            Vector2 scale = new Vector2(0.035f, 0.26f) * Scale;
            spriteBatch.Draw(tex, pos, null, body * (0.9f * Opacity), Rotation, origin, scale, SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, pos + Rotation.ToRotationVector2() * 4f, null, body * 0.5f * Opacity, Rotation, origin, scale * new Vector2(0.8f, 0.4f), SpriteEffects.None, 0f);
            return false;
        }
    }
}
