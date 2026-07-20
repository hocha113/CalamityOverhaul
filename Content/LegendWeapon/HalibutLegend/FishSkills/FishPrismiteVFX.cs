using CalamityOverhaul.Common;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    /// <summary>棱彩冲击波域内资源（域内加载器，不经 EffectLoader）</summary>
    internal class FishPrismiteAssets
    {
        /// <summary>波前弧线：冷白发丝线 + 色散边 + 暗干涉纹</summary>
        [VaultLoaden(CWRConstant.Effects)]
        public static Effect FishPrismWave { get; private set; }

        /// <summary>弧形波痕贴图（黑底，仅加色）</summary>
        [VaultLoaden(CWRConstant.Masking + "ArcWave")]
        public static Asset<Texture2D> ArcWaveTex { get; private set; }
    }

    /// <summary>
    /// 棱彩冲击波共享演出协作类。<br/>
    /// 色彩脚本：平时 = 冷白（波前发丝线）+ 前红后蓝极窄色散边（透镜色差）；
    /// 彩虹只在分裂事件出现：白光在分裂点展开成光谱扇，子波按角序继承红→紫色相切片。<br/>
    /// 与近邻差异：FishJewel 是离散宝石实体，FishUnicorn 是神话装饰彩虹，本技能是连续光学波 + 严格物理色散
    /// </summary>
    internal static class FishPrismiteVFX
    {
        //==== 色彩脚本 ====
        /// <summary>冷白（波前主体，非纯白）</summary>
        public static readonly Color ColdWhite = new(214, 232, 252);
        /// <summary>前缘色散（红侧，折射率低的一端）</summary>
        public static readonly Color LeadRed = new(255, 84, 48);
        /// <summary>后缘色散（蓝侧）</summary>
        public static readonly Color TrailBlue = new(96, 138, 255);

        /// <summary>光谱采样：t 0=红 → 1=紫，饱和压明度（防过曝：宁提饱和度不提明度）</summary>
        public static Color Spectrum(float t) {
            t = MathHelper.Clamp(t, 0f, 1f);
            return Main.hslToRgb(t * 0.75f, 0.96f, 0.52f);
        }

        /// <summary>
        /// 波的三色组：hueT &lt; 0 = 白光波（红/冷白/蓝色差），否则为谱色子波
        /// （色散边取其光谱邻位色相，像从光谱上剪下的一窄条）
        /// </summary>
        public static void WaveColors(float hueT, out Color lead, out Color core, out Color trail) {
            if (hueT < 0f) {
                lead = LeadRed;
                core = ColdWhite;
                trail = TrailBlue;
                return;
            }
            lead = Spectrum(hueT - 0.055f);
            core = Color.Lerp(Spectrum(hueT), ColdWhite, 0.30f);
            trail = Spectrum(hueT + 0.055f);
        }

        /// <summary>定向震屏，尊重服务器配置；幅度克制（技能高频触发）</summary>
        public static void Punch(Vector2 pos, Vector2 dir, float strength, int frames) {
            if (Main.dedServ || !CWRServerConfig.Instance.ScreenVibration) {
                return;
            }
            Main.instance.CameraModifiers.Add(new PunchCameraModifier(
                pos, dir.SafeNormalize(Vector2.UnitY), strength, 9f, frames, 620f, "FishPrismite"));
        }

        /// <summary>
        /// 分裂时刻的完整演出：光谱扇 + 玻璃闪点锥 + 小定向震屏 + 碎裂/晶莹双层音。
        /// spread 与 count 传分裂逻辑的同一组值，扇面射线与子波出射方向严格对位
        /// </summary>
        public static void PrismBurst(Vector2 pos, Vector2 dir, float spread, int count, float scaleMul) {
            SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.5f, Pitch = 0.35f, MaxInstances = 4 }, pos);
            SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.32f, Pitch = 0.5f, MaxInstances = 3 }, pos);
            if (Main.dedServ) {
                return;
            }
            float baseRot = dir.SafeNormalize(Vector2.UnitX).ToRotation();
            PRTLoader.NewParticle<PRT_FishPrismSpectrum>(pos, Vector2.Zero, ColdWhite, scaleMul)
                ?.Configure(count, baseRot, spread, Main.rand.NextFloat(100f));
            //玻璃闪点锥：色相与出射角对位，冷白少量混入
            for (int i = 0; i < 8; i++) {
                float rt = Main.rand.NextFloat();
                Vector2 vel = (baseRot - spread / 2f + spread * rt).ToRotationVector2() * Main.rand.NextFloat(2.5f, 7f);
                Color col = Main.rand.NextBool(4) ? ColdWhite : Spectrum(rt);
                PRTLoader.NewParticle<PRT_FishPrismGlint>(pos, vel, col, Main.rand.NextFloat(0.5f, 0.9f))
                    ?.Configure(Main.rand.Next(12, 20));
            }
            Punch(pos, dir, 2.4f, 8);
        }
    }

    /// <summary>
    /// 波痕残弧：波前身后脱落的相位残迹。原地缓慢扩散、变薄、消散；
    /// 三层色散绘制随生命推移彼此分离（退相干的可视化）。黑底 ArcWave，仅加色
    /// </summary>
    internal class PRT_FishPrismWavelet : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "ArcWave";
        public override bool CanPool => true;

        private float dirRot;
        private Color colLead;
        private Color colTrail;
        private float expandRate;

        public PRT_FishPrismWavelet Configure(float rotation, Color lead, Color trail, int lifetime, float expand = 1.012f) {
            dirRot = rotation;
            colLead = lead;
            colTrail = trail;
            Lifetime = lifetime;
            expandRate = expand;
            return this;
        }

        public override void Reset() {
            base.Reset();
            dirRot = 0f;
            colLead = default;
            colTrail = default;
            expandRate = 1.012f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            if (Lifetime <= 0) {
                Lifetime = 14;
            }
        }

        public override void AI() {
            Velocity *= 0.9f;
            Scale *= expandRate;
            float lc = LifetimeCompletion;
            Opacity = (1f - lc) * (1f - lc);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = TexValue;
            //弧顶在贴图右侧约 72% 处，把原点挪到弧顶附近让残弧贴着波前脱落位置
            Vector2 origin = new(tex.Width * 0.72f, tex.Height * 0.5f);
            Vector2 pos = Position - Main.screenPosition;
            Vector2 dirVec = dirRot.ToRotationVector2();
            //色散分离随衰老增大：残迹逐渐散成红蓝双边
            float disp = 2f + 5f * LifetimeCompletion;
            //变薄：跨波方向随生命收缩
            Vector2 texScale = new Vector2(0.6f, 0.8f * (1f - LifetimeCompletion * 0.3f)) * Scale;

            Color lead = colLead with { A = 0 };
            Color core = Color with { A = 0 };
            Color trail = colTrail with { A = 0 };
            spriteBatch.Draw(tex, pos + dirVec * disp, null, lead * (Opacity * 0.34f), dirRot, origin, texScale, SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, pos - dirVec * disp, null, trail * (Opacity * 0.34f), dirRot, origin, texScale, SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, pos, null, core * (Opacity * 0.5f), dirRot, origin, texScale, SpriteEffects.None, 0f);
            return false;
        }
    }

    /// <summary>
    /// 玻璃微闪点：单帧镜面反光的质感。前 2 帧冷白过冲后落回本色急衰；
    /// SoftGlow 小底晕 + 四芒星芯双层异质。黑底贴图，仅加色
    /// </summary>
    internal class PRT_FishPrismGlint : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "StarGlow01";
        public override bool CanPool => true;

        private float spin;

        public PRT_FishPrismGlint Configure(int lifetime) {
            Lifetime = lifetime;
            return this;
        }

        public override void Reset() {
            base.Reset();
            spin = 0f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            spin = Main.rand.NextFloat(-0.04f, 0.04f);
            if (Lifetime <= 0) {
                Lifetime = 16;
            }
        }

        public override void AI() {
            Velocity *= 0.88f;
            Rotation += spin;
            float lc = LifetimeCompletion;
            Opacity = MathF.Pow(1f - lc, 1.5f);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D star = TexValue;
            Texture2D soft = CWRAsset.SoftGlow?.Value;
            Vector2 pos = Position - Main.screenPosition;
            //出生 2 帧冷白过冲，随后落回本色（≤2 帧白，非常驻）
            bool flash = Time <= 2f;
            Color col = (flash ? FishPrismiteVFX.ColdWhite : Color) with { A = 0 };
            float s = Scale * (flash ? 1.5f : 1f);

            if (soft != null) {
                spriteBatch.Draw(soft, pos, null, col * (Opacity * 0.3f), 0f, soft.Size() * 0.5f, s * 0.5f, SpriteEffects.None, 0f);
            }
            spriteBatch.Draw(star, pos, null, col * Opacity, Rotation, star.Size() * 0.5f, s * 0.34f, SpriteEffects.None, 0f);
            return false;
        }
    }

    /// <summary>
    /// 光谱扇：分裂点的彩虹时刻。中心棱镜白闪（≤2 帧）+ N 道细谱色射线按子波出射角展开，
    /// 射线快速生长后自根部蚀退，尾段留色散残光 aftermath。黑底贴图，仅加色
    /// </summary>
    internal class PRT_FishPrismSpectrum : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";
        public override bool CanPool => true;

        private int rayCount;
        private float baseRot;
        private float spread;
        private float seed;

        public PRT_FishPrismSpectrum Configure(int count, float rotation, float spreadAngle, float randSeed) {
            rayCount = Math.Clamp(count, 2, 9);
            baseRot = rotation;
            spread = spreadAngle;
            seed = randSeed;
            return this;
        }

        public override void Reset() {
            base.Reset();
            rayCount = 0;
            baseRot = 0f;
            spread = 0f;
            seed = 0f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            Lifetime = 26;
        }

        public override void AI() {
            float lc = LifetimeCompletion;
            Opacity = MathF.Pow(1f - lc, 1.7f);
        }

        private static float Hash01(float x) {
            x = MathF.Sin(x * 12.9898f) * 43758.547f;
            return x - MathF.Floor(x);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            if (rayCount < 2) {
                return false;
            }
            Texture2D ray = TexValue;
            Vector2 center = Position - Main.screenPosition;
            float lc = LifetimeCompletion;
            float grow = VaultUtils.EaseOutCubic(MathF.Min(1f, Time / 5f));
            //根部先蚀：第 7 帧起残光从分裂点向外撤退，尾端最后熄灭
            float rootT = MathHelper.Clamp((Time - 7f) / (Lifetime - 7f), 0f, 1f);

            for (int i = 0; i < rayCount; i++) {
                float rayT = i / (float)(rayCount - 1);
                //与 SplitOnImpact 同构的角度公式 + 微抖动
                float ang = baseRot - spread / 2f + spread * rayT + (Hash01(seed + i * 3.7f) - 0.5f) * 0.05f;
                float maxLen = (96f + 54f * Hash01(seed + i * 7.9f)) * Scale;
                float len = maxLen * grow;
                float rootOff = len * rootT * 0.92f;
                float segLen = len - rootOff;
                if (segLen < 6f) {
                    continue;
                }

                Vector2 angVec = ang.ToRotationVector2();
                Color col = FishPrismiteVFX.Spectrum(rayT) with { A = 0 };
                Vector2 segPos = center + angVec * (rootOff + segLen * 0.5f);
                Vector2 segScale = new(0.34f * (1f - lc * 0.45f), segLen / ray.Height);
                spriteBatch.Draw(ray, segPos, null, col * (Opacity * 0.85f), ang + MathHelper.PiOver2
                    , ray.Size() * 0.5f, segScale, SpriteEffects.None, 0f);

                //射线端头的谱色小闪点：扇面外缘的一圈亮痕
                if (Time < 10f) {
                    Texture2D tip = CWRAsset.StarGlow01?.Value;
                    if (tip != null) {
                        spriteBatch.Draw(tip, center + angVec * len, null, col * (Opacity * 0.7f)
                            , ang, tip.Size() * 0.5f, 0.2f * Scale, SpriteEffects.None, 0f);
                    }
                }
            }

            //中心棱镜闪：≤2 帧白色十字过冲 → 冷白光斑衰减
            if (Time <= 2f) {
                Texture2D cross = CWRAsset.RayCross01?.Value;
                if (cross != null) {
                    spriteBatch.Draw(cross, center, null, Color.White with { A = 0 } * 0.85f
                        , seed, cross.Size() * 0.5f, 0.5f * Scale, SpriteEffects.None, 0f);
                }
            }
            Texture2D flare = CWRAsset.StarFlare02?.Value;
            if (flare != null) {
                float flareFade = MathF.Pow(1f - lc, 2.4f);
                spriteBatch.Draw(flare, center, null, FishPrismiteVFX.ColdWhite with { A = 0 } * (flareFade * 0.55f)
                    , -seed, flare.Size() * 0.5f, (0.36f + 0.2f * lc) * Scale, SpriteEffects.None, 0f);
            }
            return false;
        }
    }
}
