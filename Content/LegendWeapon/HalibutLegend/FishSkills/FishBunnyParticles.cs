using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    /// <summary>兔鱼调色板</summary>
    internal static class FishBunnyPalette
    {
        /// <summary>奶白到淡粉之间随机取一撮绒毛色</summary>
        public static Color Fluff() => Color.Lerp(new Color(255, 234, 238), new Color(246, 200, 214), Main.rand.NextFloat());
        public static readonly Color HeartFlush = new(255, 118, 148);  //心跳潮红
        public static readonly Color EmberHot = new(255, 158, 62);     //火心亮橙
        public static readonly Color EmberDeep = new(235, 96, 30);     //火心深橙
    }

    /// <summary>兔鱼绒毛簇，哑光三瓣蓬毛</summary>
    internal class PRT_FishBunnyFluff : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Fog";
        public override bool CanPool => true;

        private float swaySeed;
        private float spin;
        private Color baseColor;

        public PRT_FishBunnyFluff Configure(int lifetime) {
            Lifetime = lifetime;
            baseColor = Color;
            return this;
        }

        public override void Reset() {
            base.Reset();
            swaySeed = 0f;
            spin = 0f;
            baseColor = default;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            swaySeed = Main.rand.NextFloat(MathHelper.TwoPi);
            spin = Main.rand.NextFloat(-0.045f, 0.045f);
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            if (Lifetime <= 0) {
                Lifetime = Main.rand.Next(46, 70);
                baseColor = Color;
            }
        }

        public override void AI() {
            //轻重力 + 强空气阻力
            Velocity.X *= 0.955f;
            Velocity.Y += 0.055f;
            if (Velocity.Y > 1.7f) {
                Velocity.Y = 1.7f;
            }

            //降到漂浮速度后左右摇摆飘落
            if (Velocity.Y > 0.4f) {
                Position.X += MathF.Sin(Time * 0.09f + swaySeed) * 0.42f;
            }
            Rotation += spin + MathF.Cos(Time * 0.09f + swaySeed) * 0.012f;

            float t = LifetimeCompletion;
            float tail = MathHelper.Clamp((t - 0.60f) / 0.40f, 0f, 1f);
            Opacity = MathF.Min(Time / 5f, 1f) * (1f - tail * tail * (3f - 2f * tail));
            Scale *= 0.9975f;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 pos = Position - Main.screenPosition;

            //哑光吃环境光
            Color env = Lighting.GetColor(Position.ToTileCoordinates());
            Color lit = Color.Lerp(baseColor.MultiplyRGB(env), baseColor, 0.30f) * Opacity;

            //三瓣蓬毛，各瓣镜像错开，免得三团同形
            DrawLobe(spriteBatch, tex, pos, lit, Rotation, Scale, SpriteEffects.None);
            Vector2 off1 = (Rotation + 0.7f).ToRotationVector2() * (Scale * 185f);
            DrawLobe(spriteBatch, tex, pos + off1, lit * 0.85f, Rotation + 0.9f, Scale * 0.62f, SpriteEffects.FlipHorizontally);
            Vector2 off2 = (Rotation - 2.3f).ToRotationVector2() * (Scale * 150f);
            DrawLobe(spriteBatch, tex, pos + off2, lit * 0.78f, Rotation - 1.2f, Scale * 0.52f, SpriteEffects.FlipVertically);
            return false;
        }

        private static void DrawLobe(SpriteBatch sb, Texture2D tex, Vector2 pos, Color color, float rot, float scale, SpriteEffects flip) {
            sb.Draw(tex, pos, null, color, rot, tex.Size() * 0.5f, scale * 0.6f, flip, 0f);
        }
    }

    /// <summary>兔鱼尘烟团</summary>
    internal class PRT_FishBunnySmoke : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Fog";
        public override bool CanPool => true;

        private float spin;
        private Color hotColor;
        private Color coldColor;
        private float expandRate;
        private float buoyancy;

        public PRT_FishBunnySmoke Configure(int lifetime, Color hot, Color cold, float expand = 1.010f, float rise = 0.008f) {
            Lifetime = lifetime;
            hotColor = hot;
            coldColor = cold;
            expandRate = expand;
            buoyancy = rise;
            return this;
        }

        public override void Reset() {
            base.Reset();
            spin = 0f;
            hotColor = coldColor = default;
            expandRate = 1.010f;
            buoyancy = 0.008f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            spin = Main.rand.NextFloat(0.008f, 0.02f) * (Main.rand.NextBool() ? 1f : -1f);
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            if (Lifetime <= 0) {
                Lifetime = Main.rand.Next(26, 40);
                hotColor = new Color(196, 186, 180);
                coldColor = new Color(126, 120, 116);
            }
        }

        public override void AI() {
            float t = LifetimeCompletion;
            Scale *= expandRate;
            Rotation += spin;
            Velocity *= 0.91f;
            Velocity.Y -= buoyancy;

            Color = Color.Lerp(hotColor, coldColor, MathF.Min(1f, t * 1.4f));
            //快进慢出、峰值压低
            float tail = MathHelper.Clamp((t - 0.34f) / 0.60f, 0f, 1f);
            Opacity = MathF.Min(t / 0.10f, 1f) * (1f - tail * tail * (3f - 2f * tail)) * 0.46f;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            spriteBatch.Draw(tex, Position - Main.screenPosition, null, Color * Opacity, Rotation
                , tex.Size() * 0.5f, Scale * 0.6f, SpriteEffects.None, 0f);
            return false;
        }
    }

    /// <summary>卡通星点</summary>
    internal class PRT_FishBunnyStar : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "StarGlow01";
        public override bool CanPool => true;

        private float spin;
        private bool flash;

        public PRT_FishBunnyStar Configure(int lifetime, bool isFlash = false) {
            Lifetime = lifetime;
            flash = isFlash;
            return this;
        }

        public override void Reset() {
            base.Reset();
            spin = 0f;
            flash = false;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            spin = Main.rand.NextFloat(0.03f, 0.08f) * (Main.rand.NextBool() ? 1f : -1f);
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            if (Lifetime <= 0) {
                Lifetime = 18;
            }
        }

        public override void AI() {
            Velocity *= 0.86f;
            Rotation += spin;

            //过曝只准白两帧
            if (flash && Time > 1f) {
                Color = Color.Lerp(Color, new Color(255, 150, 70), 0.45f);
            }

            Opacity = 1f - MathF.Pow(LifetimeCompletion, 2.2f);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 pos = Position - Main.screenPosition;
            Vector2 origin = tex.Size() * 0.5f;

            //弹性 pop-in
            float t = LifetimeCompletion;
            float pop = t < 0.25f ? EaseOutBack(t / 0.25f) : MathHelper.Lerp(1f, 0.42f, (t - 0.25f) / 0.75f);
            float s = Scale * pop;

            Color col = Color * Opacity;
            //宽晕 + 窄芯双层
            spriteBatch.Draw(tex, pos, null, col * 0.6f, Rotation, origin, s, SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, pos, null, col, Rotation, origin, s * 0.5f, SpriteEffects.None, 0f);
            return false;
        }

        private static float EaseOutBack(float x) {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float u = x - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }
    }
}
