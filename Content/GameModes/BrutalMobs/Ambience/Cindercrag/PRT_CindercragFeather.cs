using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Cindercrag
{
    /// <summary>
    /// 硫火烬羽：羽毛状暗红烬片，向上浮升。
    /// 材质=烬片，三个签名行为：①落叶式摆动但方向朝上（浮力翻转的飘零）；
    /// ②余温随寿命冷却（亮橙红边线→暗红→熄灭）；③高频微闪 + 年轻期偶发火星剥落。
    /// 暗片本体走带 A 的 Extra_98 真 alpha 梭形（能遮挡才读作实体薄片），余温边线 A=0 加色敷在其上
    /// </summary>
    internal class PRT_CindercragFeather : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";
        public override bool CanPool => true;

        /// <summary>烬片暗体（带 A，承担轮廓与遮挡）</summary>
        private static readonly Color BodyDark = new(52, 16, 14);
        /// <summary>初生余温（A=0 加色）</summary>
        private static readonly Color EmberHot = new(255, 96, 38);
        /// <summary>冷却末期余温</summary>
        private static readonly Color EmberCool = new(132, 36, 22);

        private float flutterSeed;
        private float flutterAmp;
        private float spinBias;

        public override void Reset() {
            base.Reset();
            flutterSeed = 0f;
            flutterAmp = 0f;
            spinBias = 0f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            if (Lifetime <= 0) {
                Lifetime = Main.rand.Next(110, 170);
            }
            flutterSeed = Main.rand.NextFloat(MathHelper.TwoPi);
            flutterAmp = Main.rand.NextFloat(0.45f, 1.05f);
            spinBias = Main.rand.NextFloat(-0.35f, 0.35f);
        }

        public override void AI() {
            float lc = LifetimeCompletion;
            //横向摆动是即时速度：烬片轻，风一吹就走
            Velocity.X = MathF.Sin(Time * 0.055f + flutterSeed) * flutterAmp;
            //浮力缓升，封顶防越飘越快
            Velocity.Y = MathHelper.Clamp(Velocity.Y - 0.012f, -1.15f, 2f);
            //朝向跟随速度，再叠翻飞摆角
            Rotation = Velocity.ToRotation() + MathHelper.PiOver2
                + MathF.Sin(Time * 0.11f + flutterSeed) * 0.5f + spinBias;

            Opacity = MathF.Min(lc * 6f, 1f) * MathHelper.Clamp((1f - lc) / 0.25f, 0f, 1f);

            //年轻期偶发火星剥落（热烬才崩火星）
            if (lc < 0.3f && Main.rand.NextBool(26)) {
                Dust spark = Dust.NewDustPerfect(Position, DustID.RedTorch,
                    new Vector2(Velocity.X * 0.4f, Main.rand.NextFloat(-0.6f, 0.4f)), 120, default, 0.7f);
                spark.noGravity = true;
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = TexValue;
            Vector2 pos = Position - Main.screenPosition;
            Vector2 origin = tex.Size() * 0.5f;
            float lc = LifetimeCompletion;
            float flicker = 0.72f + 0.28f * MathF.Sin(Time * 0.9f + flutterSeed * 3f);

            //暗片本体：窄梭形读作羽状薄片
            var bodyScale = new Vector2(0.17f, 0.36f) * Scale;
            spriteBatch.Draw(tex, pos, null, BodyDark * (0.85f * Opacity),
                Rotation, origin, bodyScale, SpriteEffects.None, 0f);

            //余温边线：随寿命冷却，永不纯白
            Color hot = Color.Lerp(EmberHot, EmberCool, lc) with { A = 0 };
            float heat = (1f - lc * 0.65f) * flicker;
            spriteBatch.Draw(tex, pos, null, hot * (0.8f * Opacity * heat),
                Rotation, origin, bodyScale * 0.78f, SpriteEffects.None, 0f);
            return false;
        }
    }
}
