using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    /// <summary>兔鱼调色板：奶粉绒毛与暖橙火心共享取色</summary>
    internal static class FishBunnyPalette
    {
        /// <summary>奶白到淡粉之间随机取一撮绒毛色</summary>
        public static Color Fluff() => Color.Lerp(new Color(255, 234, 238), new Color(246, 200, 214), Main.rand.NextFloat());
        public static readonly Color HeartFlush = new(255, 118, 148);  //心跳潮红
        public static readonly Color EmberHot = new(255, 158, 62);     //火心亮橙
        public static readonly Color EmberDeep = new(235, 96, 30);     //火心深橙
    }

    /// <summary>
    /// 兔鱼绒毛簇：哑光三瓣蓬毛，轻重力慢落 + 羽毛式左右摇摆，吃环境光照不自发光。<br/>
    /// 跳跃/落地/心跳挤压掉毛与爆后纷飞共用，是兔鱼"毛绒玩偶"材质的主要载体
    /// </summary>
    internal class PRT_FishBunnyFluff : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "SmokeSheet01";
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
            ai[0] = Main.rand.Next(4);
            swaySeed = Main.rand.NextFloat(MathHelper.TwoPi);
            spin = Main.rand.NextFloat(-0.045f, 0.045f);
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            if (Lifetime <= 0) {
                Lifetime = Main.rand.Next(46, 70);
                baseColor = Color;
            }
        }

        public override void AI() {
            //轻重力 + 强空气阻力：绒毛落得慢
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
            int index = (int)ai[0];
            Vector2 pos = Position - Main.screenPosition;

            //哑光材质吃环境光：暗处的绒毛跟着变暗，保底三成免得纯黑
            Color env = Lighting.GetColor(Position.ToTileCoordinates());
            Color lit = Color.Lerp(baseColor.MultiplyRGB(env), baseColor, 0.30f) * Opacity;

            //三瓣蓬毛：主瓣 + 两片贴身副瓣拼出不规则簇形
            DrawLobe(spriteBatch, tex, index, pos, lit, Rotation, Scale);
            Vector2 off1 = (Rotation + 0.7f).ToRotationVector2() * (Scale * 185f);
            DrawLobe(spriteBatch, tex, index + 1, pos + off1, lit * 0.85f, Rotation + 0.9f, Scale * 0.62f);
            Vector2 off2 = (Rotation - 2.3f).ToRotationVector2() * (Scale * 150f);
            DrawLobe(spriteBatch, tex, index + 2, pos + off2, lit * 0.78f, Rotation - 1.2f, Scale * 0.52f);
            return false;
        }

        private static void DrawLobe(SpriteBatch sb, Texture2D tex, int index, Vector2 pos, Color color, float rot, float scale) {
            index %= 4;
            Rectangle frame = new(index % 2 * 512, index / 2 * 512, 512, 512);
            sb.Draw(tex, pos, frame, color, rot, frame.Size() * 0.5f, scale, SpriteEffects.None, 0f);
        }
    }

    /// <summary>兔鱼尘烟团：哑光染色烟，落地尘环与爆炸烟圈共用，扩张 + 快进慢出</summary>
    internal class PRT_FishBunnySmoke : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "SmokeSheet01";
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
            ai[0] = Main.rand.Next(4);
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
            //快进慢出、峰值压低：尘是配角，不许糊住兔子
            float tail = MathHelper.Clamp((t - 0.34f) / 0.60f, 0f, 1f);
            Opacity = MathF.Min(t / 0.10f, 1f) * (1f - tail * tail * (3f - 2f * tail)) * 0.46f;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            int index = (int)ai[0];
            Rectangle frame = new(index % 2 * 512, index / 2 * 512, 512, 512);
            spriteBatch.Draw(tex, Position - Main.screenPosition, frame, Color * Opacity, Rotation
                , frame.Size() * 0.5f, Scale, SpriteEffects.None, 0f);
            return false;
        }
    }

    /// <summary>卡通星点：四芒星弹性 pop-in 后自旋收缩消散，flash 配置兼任爆炸两帧过曝闪</summary>
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

            //过曝只准白两帧：随后强制塌向暖橙
            if (flash && Time > 1f) {
                Color = Color.Lerp(Color, new Color(255, 150, 70), 0.45f);
            }

            Opacity = 1f - MathF.Pow(LifetimeCompletion, 2.2f);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 pos = Position - Main.screenPosition;
            Vector2 origin = tex.Size() * 0.5f;

            //弹性 pop-in：前四分之一过冲，之后缓缓收缩
            float t = LifetimeCompletion;
            float pop = t < 0.25f ? EaseOutBack(t / 0.25f) : MathHelper.Lerp(1f, 0.42f, (t - 0.25f) / 0.75f);
            float s = Scale * pop;

            Color col = Color * Opacity;
            //宽晕 + 窄芯双层：同贴图但构成晕/芯结构
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
