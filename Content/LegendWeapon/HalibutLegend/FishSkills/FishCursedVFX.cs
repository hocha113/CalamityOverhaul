using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    /// <summary>冥焰迸发域内 shader 资源（域内加载器，不经 EffectLoader）</summary>
    internal class FishCursedAssets
    {
        /// <summary>诅咒绿火拖尾条带</summary>
        [VaultLoaden(CWRConstant.Effects)]
        public static Effect FishCursedFlame { get; private set; }
    }

    /// <summary>
    /// 冥焰迸发共享演出。<br/>
    /// 色彩脚本：墨绿烟压底 / 暗绿外圈 / 饱和中绿主体 / 亮黄绿焰心（极小面积）；
    /// 禁荧光绿糊屏、禁纯白常驻，诅咒绿火语系为本技能独占
    /// </summary>
    internal static class FishCursedVFX
    {
        /// <summary>墨绿烟（压底）</summary>
        public static readonly Color SmokeDark = new(12, 26, 14);
        /// <summary>暗绿（外圈）</summary>
        public static readonly Color GreenDeep = new(26, 88, 34);
        /// <summary>饱和中绿（主体）</summary>
        public static readonly Color GreenMid = new(64, 168, 58);
        /// <summary>亮黄绿焰心（极小面积热芯）</summary>
        public static readonly Color GreenCore = new(170, 216, 88);

        /// <summary>定向震屏，尊重服务器配置；散射小弹体，幅度克制</summary>
        public static void Punch(Vector2 pos, Vector2 dir, float strength, float vibrationsPerSec, int frames, float falloff = 600f) {
            if (Main.dedServ || !CWRServerConfig.Instance.ScreenVibration) {
                return;
            }
            Main.instance.CameraModifiers.Add(new PunchCameraModifier(
                pos, dir.SafeNormalize(Vector2.UnitY), strength, vibrationsPerSec, frames, falloff, "FishCursed"));
        }

        /// <summary>Fire 4×4 帧序列取帧</summary>
        public static Rectangle FireFrame(Texture2D fire, int idx) {
            int w = fire.Width / 4;
            int h = fire.Height / 4;
            idx = (idx % 16 + 16) % 16;
            return new Rectangle(w * (idx % 4), h * (idx / 4), w, h);
        }

        /// <summary>FishCursedFlame 条带标准参数；phase 传弹幕 whoAmI 派生量防多带同相</summary>
        public static void ApplyFlameTrail(Effect fx, float phase, float fade) {
            fx.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly * 0.8f);
            fx.Parameters["uSeed"]?.SetValue(phase % 1f);
            fx.Parameters["uFade"]?.SetValue(fade);
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (noise != null) {
                fx.Parameters["uNoiseTex"]?.SetValue(noise);
            }
        }

        /// <summary>焰舌小簇：root 处向上撕出 count 条上飘焰舌</summary>
        public static void TongueBurst(Vector2 pos, int count, float baseScale, float upSpeed) {
            for (int i = 0; i < count; i++) {
                Vector2 vel = new(Main.rand.NextFloat(-1.2f, 1.2f), -Main.rand.NextFloat(0.5f, 1f) * upSpeed);
                Color col = Color.Lerp(GreenCore, GreenMid, Main.rand.NextFloat(0.25f, 0.85f));
                PRTLoader.NewParticle<PRT_FishCursedTongue>(pos + Main.rand.NextVector2Circular(7f, 5f), vel
                    , col, baseScale * Main.rand.NextFloat(0.8f, 1.2f))
                    ?.Configure(Main.rand.Next(20, 32), -Main.rand.NextFloat(1.3f, 2f), Main.rand.NextFloat(0.4f, 0.8f));
            }
        }

        /// <summary>
        /// 落点/命中爆发：暗烟垫底 + 小冲击环 + 火星锥 + 焰舌上撕 + 诅咒尘填充，
        /// 并点一处余燃残迹（活得比弹体久）。host 非空时残迹贴体跟随
        /// </summary>
        public static void ImpactBurst(Vector2 pos, Vector2 incoming, float scale, Entity host = null) {
            if (Main.dedServ) {
                return;
            }
            Vector2 dir = incoming.SafeNormalize(Vector2.UnitY);
            //先压暗再放亮：两口墨绿烟垫底（AlphaBlend 真遮挡）
            for (int i = 0; i < 2; i++) {
                PRTLoader.NewParticle<PRT_FishCursedSmog>(pos, -dir.RotatedByRandom(0.7f) * Main.rand.NextFloat(0.8f, 2f)
                    , SmokeDark, Main.rand.NextFloat(0.26f, 0.34f) * scale)?.Configure(26, 0.42f, 0.015f);
            }
            //小冲击环：暗绿一闪即散，沿入射轴略压扁贴合撞击面
            PRTLoader.NewParticle<PRT_DWave>(pos, Vector2.Zero, GreenDeep, 0.08f * scale)
                ?.Configure(new Vector2(1f, 0.72f), dir.ToRotation(), 0.4f * scale, 10);
            //锐线火星锥：反射向迸出
            int sparkCount = (int)(5 * scale) + 2;
            for (int i = 0; i < sparkCount; i++) {
                Vector2 vel = (-dir).RotatedByRandom(0.85f) * Main.rand.NextFloat(2.5f, 6.5f) * scale;
                Color col = Color.Lerp(GreenMid, GreenDeep, Main.rand.NextFloat(0.6f));
                PRTLoader.NewParticle<PRT_Spark>(pos, vel, col, Main.rand.NextFloat(0.4f, 0.75f) * scale)
                    ?.Configure(false, Main.rand.Next(12, 20));
            }
            TongueBurst(pos, 2 + (int)scale, 0.3f * scale, 1.6f);
            //原版诅咒尘做粒状填充（量收紧防荧光绿糊屏）
            for (int i = 0; i < 4; i++) {
                Dust d = Dust.NewDustPerfect(pos, DustID.CursedTorch
                    , (-dir).RotatedByRandom(1f) * Main.rand.NextFloat(2f, 5f), 100, default, Main.rand.NextFloat(1f, 1.6f));
                d.noGravity = true;
            }
            //余燃残迹：短命实体，弹体死后仍烧一阵
            Vector2 offset = Vector2.Zero;
            if (host != null) {
                offset = pos - host.Center + Main.rand.NextVector2Circular(host.width * 0.2f, host.height * 0.2f);
            }
            PRTLoader.NewParticle<PRT_FishCursedResidue>(pos, Vector2.Zero, GreenMid, Main.rand.NextFloat(0.5f, 0.66f) * scale)
                ?.Configure(Main.rand.Next(52, 68), host, offset);
        }
    }

    /// <summary>
    /// 诅咒焰舌：根部锚定生成点、Fire 帧动画撕尖、脱体后被浮力接管上飘颤动。
    /// 出生色即根部热色，随生命沉入墨绿烟
    /// </summary>
    internal class PRT_FishCursedTongue : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Fire";
        public override bool CanPool => true;

        private Color initialColor;
        private float seed;
        private int frameIdx;
        private float buoyancy;   //上浮目标速度，负值向上
        private float swayAmp;    //横向摇曳幅度

        public PRT_FishCursedTongue Configure(int lifetime, float buoyancy = -1.7f, float swayAmp = 0.6f) {
            Lifetime = lifetime;
            this.buoyancy = buoyancy;
            this.swayAmp = swayAmp;
            initialColor = Color;
            //出生即顺速度立焰尖，随后被 AngleLerp 缓缓掰正
            Rotation = Velocity.SafeNormalize(-Vector2.UnitY).ToRotation() + MathHelper.PiOver2;
            return this;
        }

        public override void Reset() {
            base.Reset();
            initialColor = default;
            seed = 0f;
            frameIdx = 0;
            buoyancy = -1.7f;
            swayAmp = 0.6f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            seed = Main.rand.NextFloat(MathHelper.TwoPi);
            frameIdx = Main.rand.Next(16);
            initialColor = Color;
            if (Lifetime <= 0) {
                Lifetime = Main.rand.Next(20, 30);
            }
            Rotation = 0f;
        }

        public override void AI() {
            //脱体上飘：横向被正弦摇曳接管，纵向被浮力接管
            Velocity.X = MathHelper.Lerp(Velocity.X, MathF.Sin(Time * 0.23f + seed) * swayAmp, 0.1f);
            Velocity.Y = MathHelper.Lerp(Velocity.Y, buoyancy, 0.08f);

            if (Time % 3 == 0) {
                frameIdx++;
            }

            float lc = LifetimeCompletion;
            //根部热色 → 沉入墨绿烟
            Color = Color.Lerp(initialColor, FishCursedVFX.SmokeDark, MathF.Pow(lc, 1.5f));
            //先胀后撕尖收缩
            Scale *= lc < 0.28f ? 1.025f : 0.962f;
            //上飘颤动：明度低频抖
            Opacity = (1f - lc * lc) * (0.78f + 0.22f * MathF.Sin(Time * 0.85f + seed));
            //焰尖朝运动方向立起
            Rotation = Utils.AngleLerp(Rotation, Velocity.ToRotation() + MathHelper.PiOver2, 0.2f);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D fire = TexValue;
            Rectangle frame = FishCursedVFX.FireFrame(fire, frameIdx);
            //根部锚定：origin 压在焰底，焰尖向上撕
            Vector2 origin = new(frame.Width * 0.5f, frame.Height * 0.86f);
            Vector2 pos = Position - Main.screenPosition;
            float stretch = 1f + MathF.Abs(Velocity.Y) * 0.07f;

            //暗绿外鞘（异质大半档）
            spriteBatch.Draw(fire, pos, frame, FishCursedVFX.GreenDeep * (0.4f * Opacity), Rotation
                , origin, new Vector2(Scale * 1.4f, Scale * 1.5f * stretch), SpriteEffects.None, 0f);
            //主焰帧
            spriteBatch.Draw(fire, pos, frame, Color * Opacity, Rotation
                , origin, new Vector2(Scale, Scale * stretch), SpriteEffects.None, 0f);
            //根点热芯：只在前 40% 生命，极小
            float lc = LifetimeCompletion;
            if (lc < 0.4f) {
                Texture2D glow = CWRAsset.SoftGlow?.Value;
                if (glow != null) {
                    spriteBatch.Draw(glow, pos, null, FishCursedVFX.GreenCore * (0.5f * (1f - lc / 0.4f) * Opacity)
                        , 0f, glow.Size() * 0.5f, 0.1f * Scale, SpriteEffects.None, 0f);
                }
            }
            return false;
        }
    }

    /// <summary>
    /// 诅咒余烬：反重力小火星。急减速后被浮力接管缓缓上浮，横向正弦摇曳，
    /// 顺速度拉丝 + 明灭闪烁；无纯白，双层同色芯
    /// </summary>
    internal class PRT_FishCursedEmber : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";
        public override bool CanPool => true;

        private float seed;
        private float buoyancy;

        public PRT_FishCursedEmber Configure(int lifetime, float buoyancy = -1.3f) {
            Lifetime = lifetime;
            this.buoyancy = buoyancy;
            return this;
        }

        public override void Reset() {
            base.Reset();
            seed = 0f;
            buoyancy = -1.3f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            seed = Main.rand.NextFloat(MathHelper.TwoPi);
            if (Lifetime <= 0) {
                Lifetime = Main.rand.Next(22, 36);
            }
        }

        public override void AI() {
            Velocity *= 0.93f;
            //反重力：缓缓上浮 + 横向摇曳
            Velocity.Y = MathHelper.Lerp(Velocity.Y, buoyancy, 0.05f);
            Velocity.X = MathHelper.Lerp(Velocity.X, MathF.Sin(Time * 0.27f + seed) * 0.45f, 0.08f);

            float lc = LifetimeCompletion;
            float flicker = 0.72f + 0.28f * MathF.Sin(Time * 1.1f + seed);
            Opacity = MathF.Min(lc * 7f, 1f) * (1f - lc * lc) * flicker;
            Scale *= 0.972f;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D streak = TexValue;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Vector2 pos = Position - Main.screenPosition;
            //加色批次 srcBlend=SourceAlpha，alpha 必须随强度走，置 0 会整体不可见
            Color col = Color;

            //顺速度拉丝
            float speed = Velocity.Length();
            if (speed > 0.8f) {
                float stretch = MathHelper.Clamp(speed * 0.16f, 0.25f, 1.2f);
                spriteBatch.Draw(streak, pos, null, col * (0.7f * Opacity)
                    , Velocity.ToRotation() + MathHelper.PiOver2, streak.Size() * 0.5f
                    , new Vector2(0.2f, stretch) * Scale, SpriteEffects.None, 0f);
            }
            if (glow != null) {
                Vector2 origin = glow.Size() * 0.5f;
                spriteBatch.Draw(glow, pos, null, col * (0.5f * Opacity), 0f, origin, 0.26f * Scale, SpriteEffects.None, 0f);
                spriteBatch.Draw(glow, pos, null, col * (0.9f * Opacity), 0f, origin, 0.11f * Scale, SpriteEffects.None, 0f);
            }
            return false;
        }
    }

    /// <summary>
    /// 墨绿暗烟：AlphaBlend 真遮挡烟团（SmokeSheet01 真 alpha 帧），
    /// 缓慢上浮扩张，给加色亮部铺暗底；加色暗烟加不出暗，必须走本类
    /// </summary>
    internal class PRT_FishCursedSmog : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "SmokeSheet01";
        public override bool CanPool => true;

        private int frame;
        private float spin;
        private float baseOpacity;

        public PRT_FishCursedSmog Configure(int lifetime, float opacity, float spin = 0f) {
            Lifetime = lifetime;
            baseOpacity = opacity;
            this.spin = spin;
            return this;
        }

        public override void Reset() {
            base.Reset();
            frame = 0;
            spin = 0f;
            baseOpacity = 0f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            frame = Main.rand.Next(4);
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            if (Lifetime <= 0) {
                Lifetime = Main.rand.Next(26, 40);
            }
            if (baseOpacity <= 0f) {
                baseOpacity = 0.4f;
            }
        }

        public override void AI() {
            //缓慢上浮扩张
            Velocity *= 0.94f;
            Velocity.Y = MathHelper.Lerp(Velocity.Y, -0.7f, 0.04f);
            Rotation += spin * (Velocity.X >= 0f ? 1f : -1f);

            float lc = LifetimeCompletion;
            Scale *= lc < 0.3f ? 1.016f : 1.004f;
            Opacity = MathF.Min(lc * 6f, 1f) * (1f - MathF.Pow(lc, 1.7f)) * baseOpacity;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = TexValue;
            int w = tex.Width / 2;
            int h = tex.Height / 2;
            Rectangle rect = new(w * (frame % 2), h * (frame / 2), w, h);
            //帧 512px，×0.5 对齐 256px 级烟团的直觉尺寸
            spriteBatch.Draw(tex, Position - Main.screenPosition, rect, Color * Opacity
                , Rotation, rect.Size() / 2f, Scale * 0.5f, SpriteEffects.None, 0f);
            return false;
        }
    }

    /// <summary>
    /// 绿色余燃残迹：落点/命中处的短命火苗实体，活得比弹体久。
    /// 迸燃 → 稳燃（冒上飘余烬）→ 焰尖先蚀塌缩成根部余火。host 非空时贴体跟随
    /// </summary>
    internal class PRT_FishCursedResidue : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Fire";
        public override bool CanPool => true;

        private Entity host;
        private Vector2 hostOffset;
        private float seed;
        private int frameIdx;

        public PRT_FishCursedResidue Configure(int lifetime, Entity host = null, Vector2 hostOffset = default) {
            Lifetime = lifetime;
            this.host = host;
            this.hostOffset = hostOffset;
            return this;
        }

        public override void Reset() {
            base.Reset();
            host = null;
            hostOffset = default;
            seed = 0f;
            frameIdx = 0;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            seed = Main.rand.NextFloat(MathHelper.TwoPi);
            frameIdx = Main.rand.Next(16);
            if (Lifetime <= 0) {
                Lifetime = Main.rand.Next(52, 68);
            }
        }

        public override void AI() {
            //贴体跟随；宿主消失则原地续燃
            if (host != null) {
                if (host.active) {
                    Position = host.Center + hostOffset;
                }
                else {
                    host = null;
                }
            }
            else {
                Velocity = Vector2.Zero;
            }

            if (Time % 4 == 0) {
                frameIdx++;
            }

            float lc = LifetimeCompletion;
            //迸燃 → 稳燃 → 燃尽，后段明灭加剧
            float gutter = lc > 0.6f ? 0.24f * MathF.Sin(Time * 1.3f + seed) : 0.1f * MathF.Sin(Time * 0.6f + seed);
            float burnout = MathHelper.SmoothStep(0f, 1f, MathF.Max(0f, (lc - 0.55f) / 0.45f));
            Opacity = MathF.Min(lc * 8f, 1f) * (1f - burnout) * (0.85f + gutter);
            //色温冷却：中绿沉入墨绿
            Color = Color.Lerp(FishCursedVFX.GreenMid, FishCursedVFX.SmokeDark
                , MathHelper.SmoothStep(0f, 1f, MathF.Max(0f, (lc - 0.4f) / 0.6f)));

            //稳燃期冒上飘余烬（弹体已死，这里是余韵）
            if (lc < 0.7f && Time % 9 == 0) {
                PRTLoader.NewParticle<PRT_FishCursedEmber>(Position + new Vector2(Main.rand.NextFloat(-8f, 8f) * Scale, -4f)
                    , new Vector2(0f, -Main.rand.NextFloat(0.4f, 0.9f))
                    , Color.Lerp(FishCursedVFX.GreenMid, FishCursedVFX.GreenDeep, Main.rand.NextFloat())
                    , Main.rand.NextFloat(0.32f, 0.5f))?.Configure(Main.rand.Next(20, 30), -1f);
            }
            //偶发一粒原版诅咒尘
            if (lc < 0.6f && Time % 14 == 0) {
                Dust d = Dust.NewDustPerfect(Position + Main.rand.NextVector2Circular(6f, 3f), DustID.CursedTorch
                    , new Vector2(0f, -Main.rand.NextFloat(0.3f, 0.8f)), 120, default, Main.rand.NextFloat(0.8f, 1.2f));
                d.noGravity = true;
            }

            Lighting.AddLight(Position, FishCursedVFX.GreenMid.ToVector3() * 0.22f * Opacity);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D fire = TexValue;
            Rectangle frame = FishCursedVFX.FireFrame(fire, frameIdx);
            Vector2 origin = new(frame.Width * 0.5f, frame.Height * 0.9f);
            Vector2 pos = Position - Main.screenPosition;

            float lc = LifetimeCompletion;
            //焰尖先蚀：后段纵向塌缩，根部余火最后熄
            float tipErode = MathHelper.SmoothStep(0f, 1f, MathF.Max(0f, (lc - 0.55f) / 0.45f));
            float yScale = 1f - 0.62f * tipErode;
            float breath = 1f + 0.08f * MathF.Sin(Time * 0.35f + seed);

            //暗绿外鞘
            spriteBatch.Draw(fire, pos, frame, FishCursedVFX.GreenDeep * (0.42f * Opacity), 0f
                , origin, new Vector2(Scale * 1.35f * breath, Scale * 1.4f * yScale), SpriteEffects.None, 0f);
            //主焰
            spriteBatch.Draw(fire, pos, frame, Color * Opacity, 0f
                , origin, new Vector2(Scale * breath, Scale * yScale), SpriteEffects.None, 0f);
            //根部热芯：随蚀减
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow != null) {
                spriteBatch.Draw(glow, pos - new Vector2(0f, 3f), null
                    , FishCursedVFX.GreenCore * (0.4f * Opacity * (1f - tipErode * 0.8f)), 0f
                    , glow.Size() * 0.5f, 0.16f * Scale, SpriteEffects.None, 0f);
            }
            return false;
        }
    }
}
