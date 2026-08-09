using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    /// <summary>斑驳油渍专属 shader 资源（域内加载器，不动 EffectLoader）</summary>
    internal class FishLardAssets
    {
        /// <summary>程序化油体（飞行液滴/附着油渍双态一体）</summary>
        [VaultLoaden(CWRConstant.Effects)]
        public static Effect FishLardBlob { get; private set; }
    }

    /// <summary>斑驳油渍配色，暗褐油色为主，虹彩只在 shader 内点缀</summary>
    internal static class FishLardPalette
    {
        /// <summary>暗褐近黑油底</summary>
        public static readonly Color OilDeep = new(26, 22, 14);
        /// <summary>油褐中间调</summary>
        public static readonly Color OilBrown = new(56, 45, 24);
        /// <summary>油黄反光，低亮非白</summary>
        public static readonly Color OilAmber = new(128, 100, 46);
        /// <summary>燃烧热橙</summary>
        public static readonly Color HeatOrange = new(250, 90, 18);
        /// <summary>油烟暗灰褐</summary>
        public static readonly Color SmokeDark = new(38, 32, 26);

        /// <summary>油滴随机色，深浅油褐之间取值</summary>
        public static Color Droplet() => Color.Lerp(OilDeep, OilBrown, Main.rand.NextFloat());
    }

    /// <summary>
    /// 油面滋滋气泡，贴着油渍表面缓慢鼓起的小圆圈，尾段 2 帧琥珀爆点后消失；<br/>
    /// 暗色 AlphaBlend 细环，读作油里挤出的气泡而非光点
    /// </summary>
    internal class PRT_FishLardBubble : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Ring01";
        public override bool CanPool => true;

        private float wobbleSeed;
        private float riseSpeed;

        public PRT_FishLardBubble Configure(int lifetime, float rise = 0.16f) {
            Lifetime = lifetime;
            riseSpeed = rise;
            return this;
        }

        public override void Reset() {
            base.Reset();
            wobbleSeed = 0f;
            riseSpeed = 0f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            wobbleSeed = Main.rand.NextFloat(MathHelper.TwoPi);
            if (Lifetime <= 0) {
                Lifetime = Main.rand.Next(26, 44);
            }
        }

        public override void AI() {
            //粘稠介质里的迟缓上浮
            Velocity.Y = -riseSpeed;
            Velocity.X = MathF.Sin(Time * 0.11f + wobbleSeed) * 0.05f;

            float t = LifetimeCompletion;
            //鼓起 → 顶到膜面停住 → 破
            Scale = MathHelper.Lerp(0.55f, 1.15f, MathF.Min(t * 1.6f, 1f));
            Opacity = MathF.Min(Time / 8f, 1f) * (t > 0.92f ? (1f - t) / 0.08f : 1f);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 pos = Position - Main.screenPosition;
            Vector2 origin = tex.Size() * 0.5f;
            float t = LifetimeCompletion;
            //直径 3.5-7px 的小气泡，破裂前最后一成再鼓大
            float px = MathHelper.Lerp(3.5f, 7f, (Scale - 0.55f) / 0.6f) * (1f + (t > 0.86f ? (t - 0.86f) * 3.2f : 0f));
            float s = px / tex.Width;

            //气泡壁，比油面略浅的暗环
            spriteBatch.Draw(tex, pos, null, new Color(74, 60, 32) * (Opacity * 0.55f)
                , 0f, origin, s, SpriteEffects.None, 0f);
            //破裂瞬间，≤2 帧琥珀小爆点（A=0 加色，唯一亮部）
            if (t > 0.90f && t < 0.97f) {
                spriteBatch.Draw(tex, pos, null, FishLardPalette.OilAmber with { A = 0 } * (Opacity * 0.8f)
                    , 0f, origin, s * 1.25f, SpriteEffects.None, 0f);
            }
            return false;
        }
    }

    /// <summary>
    /// 油烟，燃油特有的浓黑烟团，AlphaBlend 暗色压底、缓慢上升膨胀，<br/>
    /// 活得比油渍久，是燃烧余波的主体
    /// </summary>
    internal class PRT_FishLardSmoke : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Fog";
        public override bool CanPool => true;

        private float spin;
        private float driftSeed;

        public PRT_FishLardSmoke Configure(int lifetime) {
            Lifetime = lifetime;
            return this;
        }

        public override void Reset() {
            base.Reset();
            spin = 0f;
            driftSeed = 0f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            spin = Main.rand.NextFloat(-0.012f, 0.012f);
            driftSeed = Main.rand.NextFloat(MathHelper.TwoPi);
            if (Lifetime <= 0) {
                Lifetime = Main.rand.Next(44, 72);
            }
        }

        public override void AI() {
            //热浮力上升 + 微风横漂，越升越散
            Velocity.Y = MathHelper.Lerp(Velocity.Y, -0.85f, 0.05f);
            Velocity.X = Velocity.X * 0.96f + MathF.Sin(Time * 0.05f + driftSeed) * 0.05f;
            Rotation += spin;

            float t = LifetimeCompletion;
            Scale *= 1.012f;
            Opacity = MathF.Min(Time / 10f, 1f) * MathF.Pow(1f - t, 1.4f);
            if (Opacity < 0.02f) {
                active = false;
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 pos = Position - Main.screenPosition;
            Vector2 origin = tex.Size() / 2f;
            //双层异径
            spriteBatch.Draw(tex, pos, null, FishLardPalette.SmokeDark * (Opacity * 0.42f)
                , Rotation * 0.7f, origin, Scale * 0.81f, SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, pos, null, new Color(52, 44, 34) * (Opacity * 0.6f)
                , Rotation, origin, Scale * 0.6f, SpriteEffects.None, 0f);
            return false;
        }
    }
}
