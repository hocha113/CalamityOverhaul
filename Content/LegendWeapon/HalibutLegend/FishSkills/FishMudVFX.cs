using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    internal class FishMudAssets
    {
        /// <summary>根部泥堆，破土隆起 / 常驻泥丘 / 塌陷回吸三相</summary>
        [VaultLoaden(CWRConstant.Effects)]
        public static Effect FishMudMound { get; private set; }

        /// <summary>泥球液团，速度拉伸 + 软体蠕动 + 尾侧噪声撕裂</summary>
        [VaultLoaden(CWRConstant.Effects)]
        public static Effect FishMudGlob { get; private set; }

        /// <summary>真alpha液滴基元，shader缺失时的液团后备贴图</summary>
        [VaultLoaden(CWRConstant.Masking + "Extra_98")]
        public static Asset<Texture2D> DropTex { get; private set; }
    }

    /// <summary>湿泥调色板，深褐哑光，异于 FishDirt 干土 / FishScorpio 风沙</summary>
    internal static class FishMudPalette
    {
        public static readonly Color Murk = new(40, 30, 24);     //最深湿泥，底层剪影与干涸终色
        public static readonly Color Deep = new(60, 44, 33);     //暗湿泥，外圈
        public static readonly Color Base = new(94, 70, 49);     //主体褐
        public static readonly Color Wet = new(128, 100, 68);    //湿亮面
        public static readonly Color Sheen = new(160, 164, 148); //水光，窄条低幅度专用

        /// <summary>暗湿泥到湿亮面之间取色</summary>
        public static Color Mud(float t) => Color.Lerp(Deep, Wet, t);
    }

    /// <summary>
    /// 泥珠，受重力下坠、随速度拉伸的哑光湿泥液滴，触固体即钉住压扁快速干涸<br/>
    /// 贴图用带真 alpha 的 Extra_98，默认 AlphaBlend 直绘，不发光
    /// </summary>
    internal class PRT_FishMudDroplet : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";
        public override bool CanPool => true;

        private Color initialColor;
        private float gravity;
        private bool landed;

        public PRT_FishMudDroplet Configure(int lifetime, float gravityPerFrame = 0.4f) {
            Lifetime = lifetime;
            initialColor = Color;
            gravity = gravityPerFrame;
            return this;
        }

        public override void Reset() {
            base.Reset();
            initialColor = default;
            gravity = 0f;
            landed = false;
        }

        public override void SetProperty() {
            //防漏 Configure 兜底
            if (Lifetime <= 0) {
                Lifetime = Main.rand.Next(18, 30);
            }
            if (gravity == 0f) {
                gravity = 0.4f;
            }
            if (initialColor == default) {
                initialColor = Color;
            }
        }

        public override void AI() {
            if (!landed) {
                Velocity.X *= 0.975f;
                Velocity.Y += gravity;
                if (Velocity.Y > 15f) {
                    Velocity.Y = 15f;
                }
                //湿泥粘壁不弹跳，触固体即钉住
                Tile tile = Framing.GetTileSafely(Position.ToTileCoordinates());
                if (tile.HasUnactuatedTile && Main.tileSolid[tile.TileType]) {
                    landed = true;
                    Velocity = Vector2.Zero;
                    if (Lifetime - Time > 14) {
                        Lifetime = Time + 14;
                    }
                }
            }

            float t = LifetimeCompletion;
            Color = Color.Lerp(initialColor, FishMudPalette.Murk, t * 0.5f);
            Opacity = 1f - MathF.Pow(t, 3f);
            if (!landed) {
                Rotation = Velocity.ToRotation() + MathHelper.PiOver2;
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = TexValue;
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 pos = Position - Main.screenPosition;

            if (landed) {
                //钉壁压扁成渍
                spriteBatch.Draw(tex, pos, null, Color * Opacity, 0f, origin
                    , new Vector2(0.62f, 0.3f) * Scale, SpriteEffects.None, 0f);
                return false;
            }

            //随速度纵向拉伸，快则成条、慢则成珠
            float stretch = MathHelper.Clamp(Velocity.Length() * 0.05f, 0f, 1f);
            Vector2 scale = new Vector2(0.4f * (1f - stretch * 0.35f), 0.55f * (1f + stretch * 1.9f)) * Scale;
            //外圈暗一层、中心实一层
            spriteBatch.Draw(tex, pos, null, Color.Lerp(Color, FishMudPalette.Murk, 0.45f) * (Opacity * 0.75f)
                , Rotation, origin, scale * 1.35f, SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, pos, null, Color * Opacity, Rotation, origin, scale, SpriteEffects.None, 0f);
            return false;
        }
    }

    /// <summary>
    /// 泥瓣，破土与塌陷甩出的大块湿泥，翻滚下坠带转影，触固体即溃碎成滴珠
    /// </summary>
    internal class PRT_FishMudClod : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";
        public override bool CanPool => true;

        private Color initialColor;
        private float gravity;
        private float spin;

        public PRT_FishMudClod Configure(int lifetime, float gravityPerFrame = 0.34f) {
            Lifetime = lifetime;
            initialColor = Color;
            gravity = gravityPerFrame;
            spin = Main.rand.NextFloat(0.1f, 0.24f) * (Main.rand.NextBool() ? 1f : -1f);
            return this;
        }

        public override void Reset() {
            base.Reset();
            initialColor = default;
            gravity = 0f;
            spin = 0f;
        }

        public override void SetProperty() {
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            if (Lifetime <= 0) {
                Lifetime = Main.rand.Next(26, 40);
            }
            if (gravity == 0f) {
                gravity = 0.34f;
            }
            if (spin == 0f) {
                spin = Main.rand.NextFloat(0.1f, 0.24f) * (Main.rand.NextBool() ? 1f : -1f);
            }
            if (initialColor == default) {
                initialColor = Color;
            }
        }

        public override void AI() {
            Velocity.X *= 0.985f;
            Velocity.Y += gravity;
            if (Velocity.Y > 14f) {
                Velocity.Y = 14f;
            }
            Rotation += spin;

            //触固体溃碎，留两滴小珠续演落点
            Tile tile = Framing.GetTileSafely(Position.ToTileCoordinates());
            if (Time > 4 && tile.HasUnactuatedTile && Main.tileSolid[tile.TileType]) {
                for (int i = 0; i < 2; i++) {
                    PRTLoader.NewParticle<PRT_FishMudDroplet>(Position
                        , new Vector2(Main.rand.NextFloat(-1.4f, 1.4f), Main.rand.NextFloat(-1.6f, -0.4f))
                        , Color, Scale * 0.6f)?.Configure(Main.rand.Next(10, 18));
                }
                active = false;
                return;
            }

            float t = LifetimeCompletion;
            Color = Color.Lerp(initialColor, FishMudPalette.Murk, t * 0.4f);
            Opacity = 1f - MathF.Pow(t, 4f);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = TexValue;
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 pos = Position - Main.screenPosition;
            Vector2 lobeA = new Vector2(0.55f, 0.36f) * Scale;
            Vector2 lobeB = new Vector2(0.34f, 0.5f) * Scale;

            //转影，位置残影表达不了自旋
            Color ghost = Color.Lerp(Color, FishMudPalette.Murk, 0.5f) * (Opacity * 0.3f);
            spriteBatch.Draw(tex, pos - Velocity * 0.9f, null, ghost, Rotation - spin * 2.6f, origin, lobeA, SpriteEffects.None, 0f);

            //双瓣交叉拼合成不规则湿泥块
            spriteBatch.Draw(tex, pos, null, Color.Lerp(Color, FishMudPalette.Deep, 0.35f) * Opacity
                , Rotation + MathHelper.PiOver2 * 0.9f, origin, lobeB, SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, pos, null, Color * Opacity, Rotation, origin, lobeA, SpriteEffects.None, 0f);
            return false;
        }
    }

    /// <summary>
    /// 泥斑，命中点 / 落点 / 退场点残留的湿泥渍
    /// 数瓣错位叠成一滩，钉在世界位置上，是活得比弹体与哨兵都久的残迹层
    /// </summary>
    internal class PRT_FishMudStain : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";
        public override bool CanPool => true;

        private Color initialColor;
        private float aspect;
        private int dripBudget;
        private float baseScale;

        /// <param name="aspectRatio">横纵比，贴地滩用 2.4 左右，挂壁渍用 1.2</param>
        public PRT_FishMudStain Configure(int lifetime, float aspectRatio = 2.4f, int drips = 2) {
            Lifetime = lifetime;
            initialColor = Color;
            aspect = aspectRatio;
            dripBudget = drips;
            baseScale = Scale;
            return this;
        }

        public override void Reset() {
            base.Reset();
            initialColor = default;
            aspect = 0f;
            dripBudget = 0;
            baseScale = 0f;
        }

        public override void SetProperty() {
            ai[0] = Main.rand.NextFloat(1000f);
            Velocity = Vector2.Zero;
            if (Lifetime <= 0) {
                Lifetime = Main.rand.Next(60, 90);
            }
            if (aspect == 0f) {
                aspect = 2.4f;
            }
            if (initialColor == default) {
                initialColor = Color;
            }
            if (baseScale == 0f) {
                baseScale = Scale;
            }
        }

        public override void AI() {
            float t = LifetimeCompletion;

            //出生 12%，湿泥摊开
            float spread = t < 0.12f ? 1f - MathF.Pow(1f - t / 0.12f, 3f) : 1f;
            Scale = baseScale * spread * (1f - MathF.Max(t - 0.6f, 0f) * 0.25f);

            //中期缓慢下渗，析出滴珠往下淌
            if (t is > 0.12f and < 0.62f) {
                Position.Y += 0.045f;
                if (dripBudget > 0 && Time % 24 == 0 && Main.rand.NextBool(2)) {
                    dripBudget--;
                    PRTLoader.NewParticle<PRT_FishMudDroplet>(
                        Position + new Vector2(Main.rand.NextFloat(-8f, 8f) * Scale, 3f)
                        , new Vector2(0f, Main.rand.NextFloat(0.4f, 0.9f))
                        , Color, Scale * Main.rand.NextFloat(0.45f, 0.7f))?.Configure(Main.rand.Next(14, 22), 0.16f);
                }
            }

            //干涸，变暗收拢
            Color = Color.Lerp(initialColor, FishMudPalette.Murk, MathF.Pow(t, 1.6f));
            Opacity = (1f - MathF.Pow(t, 2.6f)) * MathF.Min(Time / 4f, 1f);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = TexValue;
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 pos = Position - Main.screenPosition;
            float seed = ai[0];
            float t = LifetimeCompletion;

            //三瓣错位叠成不规则滩形，各瓣角度尺寸由 seed 定死不闪
            for (int i = 0; i < 3; i++) {
                float u = seed + i * 71.3f;
                Vector2 off = new(MathF.Sin(u) * 7.5f * Scale, MathF.Cos(u * 1.7f) * 2.4f * Scale);
                float rot = MathF.Sin(u * 2.3f) * 0.5f;
                float lobe = 0.72f + 0.34f * MathF.Sin(u * 3.1f);
                Color c = Color.Lerp(Color, FishMudPalette.Murk, 0.22f + 0.2f * i) * (Opacity * (0.85f - i * 0.18f));
                spriteBatch.Draw(tex, pos + off, null, c, rot, origin
                    , new Vector2(aspect * 0.42f, 0.3f) * (Scale * lobe), SpriteEffects.None, 0f);
            }

            //湿期上缘窄水光
            if (t < 0.4f) {
                float wet = 1f - t / 0.4f;
                spriteBatch.Draw(tex, pos + new Vector2(0f, -2.2f * Scale), null
                    , FishMudPalette.Sheen * (Opacity * 0.22f * wet), 0f, origin
                    , new Vector2(aspect * 0.26f, 0.12f) * Scale, SpriteEffects.None, 0f);
            }
            return false;
        }
    }
}
