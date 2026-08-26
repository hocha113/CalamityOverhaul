using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    //提灯巡守（Dungeonworld C1）专属粒子三件套：灯油余烬 / 熄灯烟 / 溅油火舌。
    //材质身份：灯油火=有浮力的暖火（升温上飘、降温转红熄灭）；烟=真透明度遮蔽物（AlphaBlend）。

    /// <summary>灯油余烬：受浮力上飘的暖火星，速度方向拉伸，金→红→炭三段降温</summary>
    internal class PRT_LWardenEmber : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "SoftGlow";
        public override bool CanPool => true;

        private static readonly Color EmberGold = new(255, 186, 92);
        private static readonly Color EmberRed = new(232, 96, 40);
        private static readonly Color EmberChar = new(112, 50, 30);

        private float buoyancy;
        private float flickerSeed;

        public PRT_LWardenEmber Configure(int lifetime, float buoyancyIn = 0.045f) {
            Lifetime = lifetime;
            buoyancy = buoyancyIn;
            return this;
        }

        public override void Reset() {
            base.Reset();
            buoyancy = 0.045f;
            flickerSeed = 0f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            flickerSeed = Main.rand.NextFloat(100f);
            if (Lifetime <= 0) {
                Lifetime = Main.rand.Next(22, 40);
            }
        }

        public override void AI() {
            float lc = LifetimeCompletion;
            //火的物性：先随迸发方向飞，阻尼收速后浮力接管上飘
            Velocity *= 0.94f;
            Velocity.Y -= buoyancy * (1f - lc * 0.6f);
            Color = lc < 0.45f
                ? Color.Lerp(EmberGold, EmberRed, lc / 0.45f)
                : Color.Lerp(EmberRed, EmberChar, (lc - 0.45f) / 0.55f);
            Opacity = (1f - lc * lc) * (0.8f + 0.2f * MathF.Sin((Time + flickerSeed) * 1.9f));
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = TexValue;
            Vector2 pos = Position - Main.screenPosition;
            Vector2 origin = tex.Size() * 0.5f;
            Color col = Color with { A = 0 };
            //速度拉伸：动得快的火星是短划,不是圆点
            float speed = Velocity.Length();
            float stretch = 1f + MathHelper.Clamp(speed * 0.16f, 0f, 1.4f);
            float rot = speed > 0.35f ? Velocity.ToRotation() : 0f;
            spriteBatch.Draw(tex, pos, null, col * Opacity, rot, origin,
                new Vector2(0.062f * stretch, 0.05f) * Scale, SpriteEffects.None, 0f);
            //亮芯
            spriteBatch.Draw(tex, pos, null, new Color(255, 240, 205, 0) * (Opacity * 0.55f),
                rot, origin, new Vector2(0.032f * stretch, 0.026f) * Scale, SpriteEffects.None, 0f);
            return false;
        }
    }

    /// <summary>熄灯烟：真透明度的灰褐烟团（AlphaBlend 遮蔽而非发光），缓升、膨胀、钟形透明度</summary>
    internal class PRT_LWardenSmoke : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Fog";
        public override bool CanPool => true;

        private float spinDir;
        private SpriteEffects mirror;

        public PRT_LWardenSmoke Configure(int lifetime, float scaleIn) {
            Lifetime = lifetime;
            Scale = scaleIn;
            return this;
        }

        public override void Reset() {
            base.Reset();
            spinDir = 0f;
            mirror = SpriteEffects.None;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            //Fog 蒙版复用纪律：随机初相 + 随机镜像破除同图辨识
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            spinDir = Main.rand.NextBool() ? 1f : -1f;
            mirror = Main.rand.NextBool() ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            if (Lifetime <= 0) {
                Lifetime = Main.rand.Next(38, 64);
            }
            Color = new Color(96, 86, 78);
        }

        public override void AI() {
            float lc = LifetimeCompletion;
            //烟的物性：初速衰减,热浮力恒在,团体越老越大越淡
            Velocity *= 0.955f;
            Velocity.Y -= 0.022f;
            Rotation += spinDir * 0.012f;
            Scale += 0.011f;
            Opacity = MathF.Sin(MathF.Min(lc * 1.25f, 1f) * MathHelper.PiOver2)
                * (1f - lc) * 0.85f;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = TexValue;
            Vector2 pos = Position - Main.screenPosition;
            //AlphaBlend 批内 Color*x 同缩 RGBA,天然正确的预乘淡出
            spriteBatch.Draw(tex, pos, null, Color * Opacity, Rotation,
                tex.Size() * 0.5f, Scale, mirror, 0f);
            return false;
        }
    }

    /// <summary>溅油火舌：死亡溅油着火用，根锚地面向上舔，先窜高再转红塌熄</summary>
    internal class PRT_LWardenOilTongue : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "TearFlame01";
        public override bool CanPool => true;

        private static readonly Color OilGold = new(255, 178, 70);
        private static readonly Color OilRed = new(226, 84, 34);

        private float tongueRot;
        private float lengthMul;
        private float jitterSeed;

        public PRT_LWardenOilTongue Configure(Vector2 outwardDir, float length, int lifetime) {
            tongueRot = outwardDir.ToRotation() + MathHelper.PiOver2;
            lengthMul = length;
            Lifetime = lifetime;
            return this;
        }

        public override void Reset() {
            base.Reset();
            tongueRot = 0f;
            lengthMul = 1f;
            jitterSeed = 0f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            jitterSeed = Main.rand.NextFloat(100f);
            if (Lifetime <= 0) {
                Lifetime = Main.rand.Next(26, 46);
            }
        }

        public override void AI() {
            Velocity = Vector2.Zero;//根锚:油着地烧,火不位移
            float lc = LifetimeCompletion;
            Opacity = (1f - lc * lc) * (0.7f + 0.3f * MathF.Sin((Time + jitterSeed) * 2.3f));
            Color = Color.Lerp(OilGold, OilRed, MathF.Pow(lc, 1.4f));
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = TexValue;
            Vector2 pos = Position - Main.screenPosition;
            Color col = Color with { A = 0 };
            float lc = LifetimeCompletion;
            //生长包络:前 22% 窜高,尾段塌矮;逐帧长度抖动是火的时域签名
            float grow = MathF.Min(lc / 0.22f, 1f) * (1f - lc * lc * 0.55f);
            float jitter = 0.82f + 0.32f * MathF.Sin((Time * 1.9f + jitterSeed) * 3.1f);
            var stretch = new Vector2(0.42f, lengthMul * grow * jitter) * Scale;
            var origin = new Vector2(tex.Width * 0.5f, tex.Height);
            spriteBatch.Draw(tex, pos, null, col * Opacity, tongueRot, origin,
                stretch, SpriteEffects.None, 0f);
            return false;
        }
    }
}
