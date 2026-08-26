using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Mournfog
{
    /// <summary>
    /// 霭祭漫飘鬼火：SoulFire 真 alpha 五帧火体做本体，SoftGlow 黑底图只当 A=0 加色垫晕。
    /// grudge（怨聚度）把火体从暗绿压成近黑怨烬、点亮白芯为红——
    /// 玩家久待不动时，四周的漫飘鬼火先于怨聚环渐渐变红，充当预告的预告
    /// </summary>
    internal class PRT_MournfogWisp : BasePRT
    {
        public override string Texture => CWRConstant.Other + "SoulFire";
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 36;

        /// <summary>怨聚度 0~1：0 暗绿常态，1 怨红</summary>
        private float grudge;
        private float seed;
        private float drift;

        public PRT_MournfogWisp Configure(int lifetime, float grudgeShift) {
            Lifetime = lifetime;
            grudge = MathHelper.Clamp(grudgeShift, 0f, 1f);
            return this;
        }

        public override void Reset() {
            base.Reset();
            grudge = 0f;
            seed = 0f;
            drift = 0f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            seed = Main.rand.NextFloat(0f, 10f);
            drift = Main.rand.NextFloat(0f, MathHelper.TwoPi);
            if (Lifetime <= 0) {
                Lifetime = 280;
            }
        }

        public override void AI() {
            drift += 0.016f;
            //漫游：目标方向慢摆 + 轻微上浮，速度阻尼跟随
            Vector2 want = new(
                MathF.Sin(drift * 1.7f + seed * 9.3f) * 0.42f,
                -0.13f + MathF.Sin(drift * 1.1f + seed * 5.1f) * 0.18f);
            Velocity = Vector2.Lerp(Velocity, want, 0.03f);

            //五帧火焰循环
            if (++ai[0] > 6f) {
                ai[0] = 0f;
                if (++ai[1] > 4f) {
                    ai[1] = 0f;
                }
            }
            //火苗轻摆
            Rotation = MathF.Sin(drift * 2.3f + seed * 4f) * 0.09f;

            float env = Envelope();
            Vector3 light = Vector3.Lerp(
                new Vector3(0.05f, 0.13f, 0.07f), new Vector3(0.16f, 0.04f, 0.03f), grudge);
            Lighting.AddLight(Position, light * env);
        }

        private float Envelope() {
            float t = LifetimeCompletion;
            return Math.Min(t / 0.18f, 1f) * MathHelper.Clamp((1f - t) / 0.24f, 0f, 1f);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            float env = Envelope();
            if (env <= 0.01f) {
                return false;
            }
            float flick = 0.82f + 0.18f * MathF.Sin(drift * 7f + seed * 20f);
            Rectangle frameRect = TexValue.GetRectangle((int)ai[1], 5);
            Vector2 pos = Position - Main.screenPosition;

            //垫晕（黑底图，A=0 只加光）
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Color halo = Color.Lerp(new Color(66, 150, 86, 0), new Color(190, 56, 38, 0), grudge)
                * (0.30f * env * flick);
            spriteBatch.Draw(glow, pos, null, halo, 0f, glow.Size() * 0.5f,
                Scale * 0.85f, SpriteEffects.None, 0f);

            //火体：贴图本色青绿，绿 tint 得暗绿鬼火；转红时通道压暗成怨烬剪影
            Color body = Color.Lerp(new Color(150, 255, 170), new Color(118, 44, 40), grudge)
                * (0.85f * env * flick);
            spriteBatch.Draw(TexValue, pos, frameRect, body, Rotation,
                frameRect.Size() * 0.5f, Scale, SpriteEffects.None, 0f);

            //怨红芯（A=0 加色，只点亮贴图白芯）
            if (grudge > 0.02f) {
                Color core = new Color(255, 84, 52, 0) * (0.75f * env * grudge * flick);
                spriteBatch.Draw(TexValue, pos, frameRect, core, Rotation,
                    frameRect.Size() * 0.5f, Scale, SpriteEffects.None, 0f);
            }
            return false;
        }
    }

    /// <summary>
    /// 碑语幽光：贴在墓碑面上的一次呼吸辉光（加色批，只加光不承形），
    /// 中段自己吐几粒上升荧尘。声音由调度方在点燃帧播，纯氛围无判定
    /// </summary>
    internal class PRT_MournfogStoneGlow : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "SoftGlow";
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 8;

        public PRT_MournfogStoneGlow Configure(int lifetime) {
            Lifetime = lifetime;
            return this;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            Velocity = Vector2.Zero;
            if (Lifetime <= 0) {
                Lifetime = 140;
            }
        }

        public override void AI() {
            float env = Envelope();
            //中段荧尘上升（≤1 粒/9 帧）
            if (env > 0.3f && ++ai[0] >= 9f) {
                ai[0] = 0f;
                Dust dust = Dust.NewDustPerfect(
                    Position + new Vector2(Main.rand.NextFloat(-12f, 12f), Main.rand.NextFloat(-6f, 10f)),
                    DustID.CursedTorch, new Vector2(0f, -Main.rand.NextFloat(0.25f, 0.55f)),
                    150, default, Main.rand.NextFloat(0.5f, 0.75f));
                dust.noGravity = true;
            }
            Lighting.AddLight(Position, new Vector3(0.09f, 0.18f, 0.11f) * env);
        }

        private float Envelope() {
            float t = LifetimeCompletion;
            return MathF.Sin(MathHelper.Pi * t);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            float env = Envelope();
            if (env <= 0.01f) {
                return false;
            }
            float breath = 0.9f + 0.1f * MathF.Sin(LifetimeCompletion * 14f);
            Vector2 pos = Position - Main.screenPosition;
            Vector2 origin = TexValue.Size() * 0.5f;
            //加色批源因子是 SourceAlpha：A 必须随强度走，禁 A=0
            Color wide = new Color(120, 190, 140) * (0.42f * env * breath);
            spriteBatch.Draw(TexValue, pos, null, wide, 0f, origin,
                new Vector2(Scale * 0.95f, Scale * 0.62f), SpriteEffects.None, 0f);
            Color core = new Color(190, 245, 205) * (0.30f * env);
            spriteBatch.Draw(TexValue, pos, null, core, 0f, origin,
                new Vector2(Scale * 0.42f, Scale * 0.30f), SpriteEffects.None, 0f);
            return false;
        }
    }
}
