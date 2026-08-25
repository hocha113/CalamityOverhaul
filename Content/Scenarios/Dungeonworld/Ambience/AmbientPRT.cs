using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Ambience
{
    //====================================================================
    //深牢七层空气签名的 8 个环境粒（WAVE2-ATMOSPHERE E-1）：
    //全部 CanPool=true（自定义字段在 Reset 复位、无构造期缓存），
    //贴图身份逐个对过 VFX.md 2026-08-17 全夹审计表——
    //黑底图(SoftGlow/Sparkle/DiffusionCircle4)只进 AdditiveBlend 且染色 A 随强度走，
    //真透明图(Fog/Extra_98)走 AlphaBlend 做哑光/暗片。纯客户端表现，零同步。
    //====================================================================

    //共享安全采样：粒子漂移可能贴近世界边，光照查询前先钳制
    internal static class AmbientPRTUtil
    {
        internal static float SafeBright(Vector2 worldPx) {
            int x = (int)MathHelper.Clamp(worldPx.X / 16f, 1, Main.maxTilesX - 2);
            int y = (int)MathHelper.Clamp(worldPx.Y / 16f, 1, Main.maxTilesY - 2);
            return Lighting.Brightness(x, y);
        }

        internal static Color SafeLight(Vector2 worldPx) {
            int x = (int)MathHelper.Clamp(worldPx.X / 16f, 1, Main.maxTilesX - 2);
            int y = (int)MathHelper.Clamp(worldPx.Y / 16f, 1, Main.maxTilesY - 2);
            return Lighting.GetColor(x, y);
        }

        internal static Tile SafeTile(Vector2 worldPx) {
            int x = (int)MathHelper.Clamp(worldPx.X / 16f, 1, Main.maxTilesX - 2);
            int y = (int)MathHelper.Clamp(worldPx.Y / 16f, 1, Main.maxTilesY - 2);
            return Framing.GetTileSafely(x, y);
        }
    }

    /// <summary>
    /// 悬浮微尘：布朗微漂 + 缓沉 + 光照门控明灭（亮处才可见）。
    /// SoftGlow 黑底图，加色批，染色 A 随强度走
    /// </summary>
    internal class PRT_DwMote : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "SoftGlow";
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 160;

        private float sinkSpeed;
        private float driftPhase;
        private float lightCache;

        public PRT_DwMote Configure(int lifetime, float sink) {
            Lifetime = lifetime;
            sinkSpeed = sink;
            driftPhase = Main.rand.NextFloat(MathHelper.TwoPi);
            return this;
        }

        public override void SetProperty() => PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;

        public override void Reset() {
            base.Reset();
            sinkSpeed = 0f;
            driftPhase = 0f;
            lightCache = 0f;
        }

        public override void AI() {
            //布朗微漂：横向抖 + 纵向缓趋向沉降速度（L7 反向即逆升）
            Velocity += new Vector2(Main.rand.NextFloat(-0.014f, 0.014f), Main.rand.NextFloat(-0.010f, 0.010f));
            Velocity = new Vector2(
                MathHelper.Clamp(Velocity.X, -0.32f, 0.32f),
                MathHelper.Lerp(Velocity.Y, sinkSpeed, 0.02f));

            if (Time % 4 == 0) {
                lightCache = AmbientPRTUtil.SafeBright(Position);
            }
            float t = LifetimeCompletion;
            float env = Math.Min(t / 0.2f, 1f) * MathHelper.Clamp((1f - t) / 0.25f, 0f, 1f);
            //尘埃只在光里显形：黑暗处自然隐没（材质三律之光照签名）
            Opacity = env * MathHelper.Clamp(lightCache * 1.15f, 0f, 1f);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            float shimmer = 0.85f + 0.15f * MathF.Sin(Time * 0.11f + driftPhase);
            //加色批：Color * 强度，A 一起缩（A=0 在 SrcAlpha 加色批=整层隐形，禁）
            spriteBatch.Draw(TexValue, Position - Main.screenPosition, null,
                Color * (Opacity * shimmer), 0f, TexValue.Size() * 0.5f,
                Scale * (0.9f + 0.1f * shimmer), SpriteEffects.None, 0f);
            return false;
        }
    }

    /// <summary>
    /// 纸屑/锈屑：摇摆下落（正弦横漂+随摆翻角）+ 落地前淡出。
    /// 魔法像素拉成 2~3px 小片，AlphaBlend 哑光乘光照
    /// </summary>
    internal class PRT_DwScrap : BasePRT
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 40;

        private float swayPhase;
        private float swayFreq;
        private float pxW;
        private float pxH;
        private bool landing;
        private Color lightCache;

        public PRT_DwScrap Configure(int lifetime, float w, float h) {
            Lifetime = lifetime;
            pxW = w;
            pxH = h;
            swayPhase = Main.rand.NextFloat(MathHelper.TwoPi);
            swayFreq = Main.rand.NextFloat(0.055f, 0.085f);
            return this;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            lightCache = Color.White;
        }

        public override void Reset() {
            base.Reset();
            swayPhase = 0f;
            swayFreq = 0f;
            pxW = 0f;
            pxH = 0f;
            landing = false;
            lightCache = default;
        }

        public override void AI() {
            float sway = MathF.Sin(Time * swayFreq + swayPhase);
            Velocity = new Vector2(sway * 0.42f, MathHelper.Lerp(Velocity.Y, 1.05f, 0.03f));
            //纸片随摆倾角，翻转感来自摆相位而非匀速自旋
            Rotation = sway * 0.85f;

            if (Time % 4 == 0) {
                lightCache = AmbientPRTUtil.SafeLight(Position);
                Tile below = AmbientPRTUtil.SafeTile(Position + new Vector2(0f, 8f));
                if (below.HasTile && Main.tileSolid[below.TileType]) {
                    landing = true;
                }
            }
            float t = LifetimeCompletion;
            float env = Math.Min(t / 0.15f, 1f) * MathHelper.Clamp((1f - t) / 0.2f, 0f, 1f);
            Opacity = landing ? MathF.Max(Opacity - 0.09f, 0f) : env;
            if (landing && Opacity <= 0f) {
                Kill();
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            //哑光：乘局部光照，黑暗里就是暗片不发光
            Color matte = Color.MultiplyRGB(lightCache);
            spriteBatch.Draw(TexValue, Position - Main.screenPosition, null,
                matte * Opacity, Rotation, TexValue.Size() * 0.5f,
                new Vector2(pxW, pxH) * Scale, SpriteEffects.None, 0f);
            return false;
        }
    }

    /// <summary>
    /// 水珠：重力加速 + 速度拉伸 + 入水即死（逐帧探液面，死时溅涟漪/星芒/轻响）。
    /// Extra_98 真透明窄梭，AlphaBlend
    /// </summary>
    internal class PRT_DwDrip : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";
        public override bool CanPool => true;
        //同屏在途水珠硬帽（计划值 12），超帽 AddParticle 直接丢弃
        public override int InGame_World_MaxCount => 12;

        private Color lightCache;

        public PRT_DwDrip Configure(int lifetime) {
            Lifetime = lifetime;
            return this;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            lightCache = Color.White;
        }

        public override void Reset() {
            base.Reset();
            lightCache = default;
        }

        public override void AI() {
            Velocity = new Vector2(Velocity.X * 0.99f, MathF.Min(Velocity.Y + 0.24f, 9f));
            Rotation = Velocity.ToRotation() - MathHelper.PiOver2;
            if (Time % 4 == 0) {
                lightCache = AmbientPRTUtil.SafeLight(Position);
            }

            Tile here = AmbientPRTUtil.SafeTile(Position);
            if (here.LiquidAmount > 32) {
                //入水：一圈亮环 + 碎星 + 一声轻响，然后立刻死
                AmbientEmitters.SplashAt(Position, Color);
                Kill();
                return;
            }
            if (here.HasTile && Main.tileSolid[here.TileType]) {
                Kill();
                return;
            }
            Opacity = MathHelper.Clamp(Time / 6f, 0f, 1f);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            float stretch = MathHelper.Clamp(MathF.Abs(Velocity.Y) * 0.14f, 0.5f, 1.7f);
            Color matte = Color.MultiplyRGB(lightCache);
            spriteBatch.Draw(TexValue, Position - Main.screenPosition, null,
                matte * (Opacity * 0.9f), Rotation, TexValue.Size() * 0.5f,
                new Vector2(0.4f, stretch) * Scale, SpriteEffects.None, 0f);
            return false;
        }
    }

    /// <summary>
    /// 水面涟漪：贴液面横椭圆扩张 + 先亮后散。
    /// DiffusionCircle4 黑底薄锐缘环，加色批
    /// </summary>
    internal class PRT_DwRipple : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "DiffusionCircle4";
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 24;

        public PRT_DwRipple Configure(int lifetime) {
            Lifetime = lifetime;
            return this;
        }

        public override void SetProperty() => PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;

        public override void AI() {
            Velocity = Vector2.Zero;
            float t = LifetimeCompletion;
            Opacity = t < 0.15f ? t / 0.15f : 1f - (t - 0.15f) / 0.85f;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            float t = LifetimeCompletion;
            float grow = 0.25f + 0.75f * (1f - (1f - t) * (1f - t));
            //横椭圆：贴水透视，环不立起来
            Vector2 scale = new Vector2(1f, 0.22f) * (Scale * grow);
            spriteBatch.Draw(TexValue, Position - Main.screenPosition, null,
                Color * (Opacity * 0.8f), 0f, TexValue.Size() * 0.5f,
                scale, SpriteEffects.None, 0f);
            return false;
        }
    }

    /// <summary>
    /// 水面星芒反光：闪现-驻留-熄灭 + 轻微横漂随水流。
    /// Sparkle 黑底四芒，加色批
    /// </summary>
    internal class PRT_DwGlint : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Sparkle";
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 40;

        private float twinklePhase;

        public PRT_DwGlint Configure(int lifetime) {
            Lifetime = lifetime;
            twinklePhase = Main.rand.NextFloat(MathHelper.TwoPi);
            return this;
        }

        public override void SetProperty() => PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;

        public override void Reset() {
            base.Reset();
            twinklePhase = 0f;
        }

        public override void AI() {
            Velocity = new Vector2(Velocity.X * 0.995f, 0f);
            float t = LifetimeCompletion;
            float env = Math.Min(Time / 6f, 1f) * MathHelper.Clamp((1f - t) / 0.3f, 0f, 1f);
            Opacity = env * (0.7f + 0.3f * MathF.Sin(Time * 0.37f + twinklePhase));
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            spriteBatch.Draw(TexValue, Position - Main.screenPosition, null,
                Color * Opacity, 0f, TexValue.Size() * 0.5f,
                Scale, SpriteEffects.None, 0f);
            return false;
        }
    }

    /// <summary>
    /// 潮雾/蒸汽团：慢升 + 膨胀 + 出生随机旋转/镜像防贴纸（Fog 单帧规则）。
    /// Fog 真透明烟羽，AlphaBlend 可染色
    /// </summary>
    internal class PRT_DwMist : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Fog";
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 60;

        private float riseSpeed;
        private float expandRate;
        private float spinRate;
        private float peakAlpha;
        private SpriteEffects mirror;
        private Color lightCache;

        public PRT_DwMist Configure(int lifetime, float rise, float expand, float peak) {
            Lifetime = lifetime;
            riseSpeed = rise;
            expandRate = expand;
            peakAlpha = peak;
            return this;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            //多团同屏逐层镜像+随机旋转：单帧贴图不读成一张贴纸
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            mirror = (SpriteEffects)Main.rand.Next(4);
            spinRate = Main.rand.NextFloat(0.0012f, 0.0026f) * (Main.rand.NextBool() ? 1f : -1f);
            lightCache = Color.White;
        }

        public override void Reset() {
            base.Reset();
            riseSpeed = 0f;
            expandRate = 0f;
            spinRate = 0f;
            peakAlpha = 0f;
            mirror = SpriteEffects.None;
            lightCache = default;
        }

        public override void AI() {
            Velocity = new Vector2(Velocity.X * 0.985f, MathHelper.Lerp(Velocity.Y, -riseSpeed, 0.012f));
            Scale += expandRate;
            Rotation += spinRate;
            if (Time % 4 == 0) {
                lightCache = AmbientPRTUtil.SafeLight(Position);
            }
            float t = LifetimeCompletion;
            float env = Math.Min(t / 0.18f, 1f) * MathHelper.Clamp((1f - t) / 0.4f, 0f, 1f);
            Opacity = env * peakAlpha;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Color matte = Color.MultiplyRGB(lightCache);
            spriteBatch.Draw(TexValue, Position - Main.screenPosition, null,
                matte * Opacity, Rotation, TexValue.Size() * 0.5f,
                Scale, mirror, 0f);
            return false;
        }
    }

    /// <summary>
    /// 骨灰/灰烬片：低频摆降（L7 逆升）+ 自转 + 近地散化，骨白哑光不发光。
    /// Fog 缩小档，AlphaBlend 乘光照
    /// </summary>
    internal class PRT_DwAsh : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Fog";
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 120;

        private float swayPhase;
        private float swayFreq;
        private float fallTarget;
        private float spinRate;
        private bool dissolving;
        private Color lightCache;

        /// <summary>fall 为负=逆升（倒吊教堂的"反重力"视觉语言）</summary>
        public PRT_DwAsh Configure(int lifetime, float fall) {
            Lifetime = lifetime;
            fallTarget = fall;
            swayPhase = Main.rand.NextFloat(MathHelper.TwoPi);
            swayFreq = Main.rand.NextFloat(0.03f, 0.055f);
            spinRate = Main.rand.NextFloat(0.008f, 0.02f) * (Main.rand.NextBool() ? 1f : -1f);
            return this;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            lightCache = Color.White;
        }

        public override void Reset() {
            base.Reset();
            swayPhase = 0f;
            swayFreq = 0f;
            fallTarget = 0f;
            spinRate = 0f;
            dissolving = false;
            lightCache = default;
        }

        public override void AI() {
            Velocity = new Vector2(
                MathF.Sin(Time * swayFreq + swayPhase) * 0.22f,
                MathHelper.Lerp(Velocity.Y, fallTarget, 0.02f));
            Rotation += spinRate;
            if (Time % 4 == 0) {
                lightCache = AmbientPRTUtil.SafeLight(Position);
                if (fallTarget > 0f) {
                    //落在肩头之前就散了：近地即快速散化
                    Tile below = AmbientPRTUtil.SafeTile(Position + new Vector2(0f, 10f));
                    if (below.HasTile && Main.tileSolid[below.TileType]) {
                        dissolving = true;
                    }
                }
            }
            float t = LifetimeCompletion;
            float env = Math.Min(t / 0.12f, 1f) * MathHelper.Clamp((1f - t) / 0.3f, 0f, 1f);
            Opacity = dissolving ? MathF.Max(Opacity - 0.06f, 0f) : env * 0.85f;
            if (dissolving && Opacity <= 0f) {
                Kill();
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            //哑光暗片：乘光照，无发光描边（验收专项）
            Color matte = Color.MultiplyRGB(lightCache);
            spriteBatch.Draw(TexValue, Position - Main.screenPosition, null,
                matte * Opacity, Rotation, TexValue.Size() * 0.5f,
                Scale, SpriteEffects.None, 0f);
            return false;
        }
    }

    /// <summary>
    /// 焊火/烬火：重力抛物 + 速度拉伸 + 热白金到橙到暗三段冷却（无纯白常驻）。
    /// SoftGlow 黑底拉伸，加色批（调色镜像 PRT_OniMacheteGold）
    /// </summary>
    internal class PRT_DwSpark : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "SoftGlow";
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 60;

        private static readonly Color ColHot = new(255, 240, 200);
        private static readonly Color ColOrange = new(238, 138, 46);
        private static readonly Color ColEmber = new(96, 38, 16);

        public PRT_DwSpark Configure(int lifetime) {
            Lifetime = lifetime;
            return this;
        }

        public override void SetProperty() => PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;

        public override void AI() {
            Velocity = new Vector2(Velocity.X * 0.985f, MathF.Min(Velocity.Y + 0.26f, 8f));
            Rotation = Velocity.ToRotation() + MathHelper.PiOver2;
            float t = LifetimeCompletion;
            //亮-橙-熄三段冷却
            Color = t < 0.3f
                ? Color.Lerp(ColHot, ColOrange, t / 0.3f)
                : Color.Lerp(ColOrange, ColEmber, (t - 0.3f) / 0.7f);
            Opacity = 1f - MathF.Pow(t, 2.5f);
            if (t < 0.4f && Main.rand.NextBool(8)) {
                Lighting.AddLight(Position, 0.22f, 0.13f, 0.03f);
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            float stretch = MathHelper.Clamp(Velocity.Length() * 0.12f, 0.5f, 2.2f);
            spriteBatch.Draw(TexValue, Position - Main.screenPosition, null,
                Color * Opacity, Rotation, TexValue.Size() * 0.5f,
                new Vector2(0.4f, stretch) * Scale, SpriteEffects.None, 0f);
            return false;
        }
    }
}
