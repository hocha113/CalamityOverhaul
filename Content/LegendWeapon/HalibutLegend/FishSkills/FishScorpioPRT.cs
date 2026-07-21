using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    /// <summary>
    /// 风载干沙粒，沙蝎技能专属，风载段被升力托着沿正弦流线盘旋上卷
    /// 风力耗尽后立即恢复重力坠落；随速度拉丝，哑光零发光<br/>
    /// 贴图用带真 alpha 的 Extra_98，AlphaBlend 直绘读作沙粒而非光点
    /// </summary>
    internal class PRT_FishScorpioSand : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";
        public override bool CanPool => true;

        private float gravity;
        private float windLift;   //0=纯坠沙，1=完全风载
        private float liftEnd;    //风载段在生命周期中的占比
        private float swayPhase;

        public PRT_FishScorpioSand Configure(int lifetime, float windLift = 0f, float gravity = 0.26f, float liftEnd = 0.55f) {
            Lifetime = lifetime;
            this.windLift = windLift;
            this.gravity = gravity;
            this.liftEnd = liftEnd;
            return this;
        }

        public override void Reset() {
            base.Reset();
            gravity = 0f;
            windLift = 0f;
            liftEnd = 0f;
            swayPhase = 0f;
        }

        public override void SetProperty() {
            swayPhase = Main.rand.NextFloat(MathHelper.TwoPi);
            //防漏 Configure 兜底
            if (Lifetime <= 0) {
                Lifetime = Main.rand.Next(18, 28);
                gravity = 0.26f;
                liftEnd = 0.55f;
            }
        }

        public override void AI() {
            float lc = LifetimeCompletion;
            if (windLift > 0f && lc < liftEnd) {
                //风载段，升力抵消重力
                Velocity *= 0.982f;
                Velocity.Y -= windLift * 0.055f;
                Vector2 perp = Velocity.SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2);
                Velocity += perp * MathF.Sin(Time * 0.24f + swayPhase) * windLift * 0.16f;
            }
            else {
                //失能段
                Velocity.X *= 0.955f;
                if (Velocity.Y < 11f) {
                    Velocity.Y += gravity;
                }
            }

            Opacity = MathF.Min(lc * 8f, 1f) * MathHelper.Clamp((1f - lc) * 2.6f, 0f, 1f);
            Rotation = Velocity.ToRotation() + MathHelper.PiOver2;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = TexValue;
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 pos = Position - Main.screenPosition;
            //随速度纵向拉丝
            float stretch = MathHelper.Clamp(Velocity.Length() * 0.05f, 0f, 1f);
            Vector2 scale = new Vector2(0.30f * (1f - stretch * 0.3f), 0.5f * (1f + stretch * 1.9f)) * Scale;

            //暗沙衬底 + 本体
            Color dark = Color.Lerp(Color, new Color(96, 74, 46), 0.55f);
            spriteBatch.Draw(tex, pos + new Vector2(1f, 2f), null, dark * (Opacity * 0.6f), Rotation, origin, scale * 1.05f, SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, pos, null, Color * Opacity, Rotation, origin, scale, SpriteEffects.None, 0f);
            return false;
        }
    }

    /// <summary>
    /// 哑光沙尘雾团，Fog 真 alpha 贴图 AlphaBlend 直绘，暗衬底+主色双层
    /// 微浮升缓涨后消散；蝎子出入土的土浪、龙卷底裙与尾迹用
    /// </summary>
    internal class PRT_FishScorpioDust : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Fog";
        public override bool CanPool => true;

        private float strength;   //峰值不透明度
        private float grow;       //每帧膨胀量
        private float buoy;       //浮升力
        private float spin;

        public PRT_FishScorpioDust Configure(int lifetime, float strength = 0.35f, float grow = 0.004f, float buoy = 0.01f) {
            Lifetime = lifetime;
            this.strength = strength;
            this.grow = grow;
            this.buoy = buoy;
            return this;
        }

        public override void Reset() {
            base.Reset();
            strength = 0f;
            grow = 0f;
            buoy = 0f;
            spin = 0f;
        }

        public override void SetProperty() {
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            spin = Main.rand.NextFloat(0.004f, 0.014f) * (Main.rand.NextBool() ? 1f : -1f);
            if (Lifetime <= 0) {
                Lifetime = Main.rand.Next(26, 40);
                strength = 0.3f;
                grow = 0.004f;
                buoy = 0.01f;
            }
        }

        public override void AI() {
            Velocity *= 0.9f;
            Velocity.Y -= buoy;
            Scale += grow;
            Rotation += spin;

            float lc = LifetimeCompletion;
            Opacity = strength * MathF.Min(lc * 6f, 1f) * MathF.Pow(1f - lc, 1.4f);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = TexValue;
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 pos = Position - Main.screenPosition;
            Color dark = Color.Lerp(Color, new Color(96, 74, 46), 0.6f);

            spriteBatch.Draw(tex, pos + new Vector2(2f, 3f), null, dark * (Opacity * 0.8f), Rotation, origin, Scale * 1.1f, SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, pos, null, Color * Opacity, Rotation, origin, Scale, SpriteEffects.None, 0f);
            return false;
        }
    }

    /// <summary>
    /// 短命沙丘 decal，位置钉死在地面，隆起成形后缓慢塌陷摊平并消散；
    /// 龙卷失能落沙、蝎子出入土的 aftermath 残迹
    /// </summary>
    internal class PRT_FishScorpioMound : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Fog";
        public override bool CanPool => true;

        private float widthPx;

        public PRT_FishScorpioMound Configure(int lifetime, float widthPx = 56f) {
            Lifetime = lifetime;
            this.widthPx = widthPx;
            return this;
        }

        public override void Reset() {
            base.Reset();
            widthPx = 0f;
        }

        public override void SetProperty() {
            Velocity = Vector2.Zero;
            if (Lifetime <= 0) {
                Lifetime = 55;
            }
            if (widthPx <= 0f) {
                widthPx = 56f;
            }
        }

        public override void AI() {
            Velocity = Vector2.Zero;
            float lc = LifetimeCompletion;
            Opacity = lc < 0.55f ? 1f : (1f - lc) / 0.45f;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = TexValue;
            Vector2 origin = tex.Size() * 0.5f;
            float lc = LifetimeCompletion;

            //隆起-塌陷
            float form = MathF.Min(lc / 0.18f, 1f);
            float heightK = (1f - MathF.Pow(1f - form, 3f)) * MathHelper.Lerp(1f, 0.5f, MathF.Max(lc - 0.3f, 0f) / 0.7f);
            float widthK = 1f + MathF.Max(lc - 0.3f, 0f) * 0.22f;

            float sx = widthPx / tex.Width * widthK;
            float sy = widthPx / tex.Width * 0.34f * heightK;
            //锚定地面，贴图中心上移半个可视高度
            Vector2 pos = Position - Main.screenPosition - new Vector2(0f, tex.Height * sy * 0.34f);

            Color dark = Color.Lerp(Color, new Color(96, 74, 46), 0.62f);
            Color crest = Color.Lerp(Color, new Color(229, 202, 148), 0.4f);
            spriteBatch.Draw(tex, pos, null, dark * (Opacity * 0.85f), 0f, origin, new Vector2(sx, sy), SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, pos - new Vector2(0f, tex.Height * sy * 0.1f), null, crest * (Opacity * 0.5f), 0f, origin, new Vector2(sx * 0.62f, sy * 0.6f), SpriteEffects.None, 0f);
            return false;
        }
    }
}
