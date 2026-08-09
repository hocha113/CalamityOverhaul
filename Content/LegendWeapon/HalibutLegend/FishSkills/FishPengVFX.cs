using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    /// <summary>神风鹅 VFX，雾灰+雪白哑光，冷芯白仅 A=0 瞬闪；落点阴影预告独占（异于 FishFallenStar/FishHarpy）</summary>
    internal static class FishPengVFX
    {
        //==== 色彩脚本 ====
        /// <summary>落点阴影（近黑冷海军蓝）</summary>
        public static readonly Color ShadowInk = new(10, 14, 22);
        /// <summary>雾灰蓝（尾迹外圈、雪尘冷面）</summary>
        public static readonly Color Mist = new(150, 168, 192);
        /// <summary>雪白（尘团主体，哑光）</summary>
        public static readonly Color Snow = new(224, 231, 240);
        /// <summary>冷芯白（配 A=0 得加色观感，仅限瞬时小面积）</summary>
        public static readonly Color Core = new(206, 228, 252);
        /// <summary>冰环蓝（冲击环、预告圈）</summary>
        public static readonly Color IceRing = new(132, 178, 226);
        /// <summary>羽背黑</summary>
        public static readonly Color FeatherDark = new(30, 34, 42);
        /// <summary>羽腹白</summary>
        public static readonly Color FeatherLight = new(214, 221, 230);

        //==== 打击链 ====

        /// <summary>落点竖向震屏，尊重服务器配置；首砸强、回弹减半</summary>
        public static void Punch(Vector2 pos, float strength, int frames) {
            if (Main.dedServ || !CWRServerConfig.Instance.ScreenVibration) {
                return;
            }
            Main.instance.CameraModifiers.Add(new PunchCameraModifier(
                pos, Vector2.UnitY, strength, 9f, frames, 900f, "FishPeng"));
        }

        //==== 音效分层 ====

        /// <summary>俯冲呼啸，stage 0 姿态锁定入弯、stage 1 高速逼近（音高上抬制造升调呼啸）</summary>
        public static void DiveWhoosh(Vector2 pos, int stage) {
            if (stage == 0) {
                SoundEngine.PlaySound(SoundID.DD2_BetsyWindAttack with { Volume = 0.32f, Pitch = -0.15f, MaxInstances = 3 }, pos);
            }
            else {
                SoundEngine.PlaySound(SoundID.DD2_BetsyWindAttack with { Volume = 0.42f, Pitch = 0.3f, MaxInstances = 3 }, pos);
                SoundEngine.PlaySound(SoundID.DD2_MonkStaffSwing with { Volume = 0.28f, Pitch = 0.45f, MaxInstances = 3 }, pos);
            }
        }

        /// <summary>着陆三层混音，爆雪 + 肉感闷响 + 土层低频垫底</summary>
        public static void ImpactBoom(Vector2 pos, bool first) {
            float v = first ? 1f : 0.55f;
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.8f * v, Pitch = -0.3f, MaxInstances = 5 }, pos);
            SoundEngine.PlaySound(SoundID.NPCHit11 with { Volume = 0.6f * v, Pitch = 0.2f, MaxInstances = 5 }, pos);
            SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.55f * v, Pitch = -0.7f, MaxInstances = 5 }, pos);
        }

        //==== 粒子族 ====

        /// <summary>雪尘爆，哑光尘团球形略上偏喷出，随后微重力落定</summary>
        public static void SnowBurst(Vector2 pos, int count, float speed) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < count; i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(0.3f, 1f) * speed;
                vel.Y -= speed * 0.22f;
                Color col = Color.Lerp(Snow, Mist, Main.rand.NextFloat());
                PRTLoader.NewParticle<PRT_FishPengSnow>(pos + Main.rand.NextVector2Circular(10f, 6f), vel
                    , col, Main.rand.NextFloat(0.5f, 0.85f))?.Configure(Main.rand.Next(26, 40), 0.5f);
            }
        }

        /// <summary>羽毛四散，着陆爆点缀，先爆散旋转后转入摆动飘落</summary>
        public static void FeatherBurst(Vector2 pos, int count, float speed) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < count; i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(0.4f, 1f) * speed;
                vel.Y -= speed * 0.35f;
                Color col = Color.Lerp(FeatherDark, new Color(52, 58, 70), Main.rand.NextFloat(0.5f));
                PRTLoader.NewParticle<PRT_FishPengFeather>(pos + Main.rand.NextVector2Circular(8f, 8f), vel
                    , col, Main.rand.NextFloat(0.34f, 0.52f))?.Configure(Main.rand.Next(55, 85), Main.rand.NextFloat(1f, 1.7f));
            }
        }

        /// <summary>着陆残雪，贴地扁平雪斑，缓慢消散的 aftermath</summary>
        public static void GroundPatch(Vector2 groundPos) {
            if (Main.dedServ) {
                return;
            }
            PRTLoader.NewParticle<PRT_FishPengSnow>(groundPos + new Vector2(0f, -3f), new Vector2(0f, -0.02f)
                , Snow, 0.85f)?.Configure(85, 0.30f, new Vector2(3.1f, 0.5f), 0.002f);
            PRTLoader.NewParticle<PRT_FishPengSnow>(groundPos + new Vector2(Main.rand.NextFloat(-14f, 14f), -4f)
                , new Vector2(Main.rand.NextFloat(-0.1f, 0.1f), -0.03f), Mist, 0.62f)
                ?.Configure(100, 0.24f, new Vector2(2.2f, 0.42f), 0.002f);
        }

        /// <summary>风切线，高速俯冲时身侧甩出的短命白线</summary>
        public static void WindShear(Vector2 pos, Vector2 vel) {
            if (Main.dedServ) {
                return;
            }
            Vector2 perp = vel.SafeNormalize(Vector2.UnitY).RotatedBy(MathHelper.PiOver2);
            Vector2 offset = perp * (Main.rand.NextBool() ? 1f : -1f) * Main.rand.NextFloat(10f, 24f);
            PRTLoader.NewParticle<PRT_Spark>(pos + offset + vel * Main.rand.NextFloat(-0.2f, 0.3f)
                , vel * 0.35f, Core * 0.5f, Main.rand.NextFloat(0.5f, 0.9f))?.Configure(false, Main.rand.Next(7, 11));
        }

        /// <summary>冲击环对，主环 + 滞后回声环，贴地压扁，ke 0..1 动能系数</summary>
        public static void ImpactRings(Vector2 pos, float ke) {
            if (Main.dedServ) {
                return;
            }
            PRTLoader.NewParticle<PRT_DWave>(pos, Vector2.Zero, IceRing * 0.75f, 0.12f)
                ?.Configure(new Vector2(1f, 0.42f), 0f, 0.95f + 0.3f * ke, 12);
            PRTLoader.NewParticle<PRT_DWave>(pos, Vector2.Zero, IceRing * 0.4f, 0.08f)
                ?.Configure(new Vector2(1f, 0.42f), 0f, 0.68f, 17);
        }

        /// <summary>着陆瞬间把整条凝结尾迹截为独立 PRT，活得比企鹅的俯冲更久</summary>
        public static void SnapContrail(Projectile proj) {
            if (Main.dedServ) {
                return;
            }
            PRTLoader.NewParticle<PRT_FishPengContrail>(proj.Center, Vector2.Zero, Mist, 1f)?.Configure(proj, 30);
        }

        //==== 飞行期凝结尾迹（活体绘制，画在企鹅精灵之下） ====

        /// <summary>
        /// 凝结尾迹，头窄尾宽的真实凝结物理，机体后方 2 点起才凝出（贴体空隙）
        /// 外层雾灰哑光 + 头段 A=0 冷芯；strength 随俯冲速度淡入
        /// </summary>
        public static void DrawLiveContrail(SpriteBatch sb, Projectile proj, float strength) {
            if (strength <= 0.03f) {
                return;
            }
            Texture2D tex = CWRAsset.Extra_98?.Value;
            Vector2[] old = proj.oldPos;
            if (tex == null || old == null || old.Length < 5) {
                return;
            }
            Vector2 half = proj.Size * 0.5f;
            Vector2 origin = tex.Size() * 0.5f;
            int lastIdx = old.Length - 1;
            for (int i = 2; i < lastIdx; i++) {
                if (old[i] == Vector2.Zero || old[i + 1] == Vector2.Zero) {
                    break;
                }
                float t = i / (float)lastIdx;
                Vector2 a = old[i] + half;
                Vector2 b = old[i + 1] + half;
                Vector2 seg = b - a;
                float len = seg.Length();
                if (len < 0.5f) {
                    continue;
                }
                //尾迹越老横摆越大，扩散中的蒸汽
                Vector2 perp = seg.SafeNormalize(Vector2.UnitY).RotatedBy(MathHelper.PiOver2);
                float wob = MathF.Sin(i * 2.13f + proj.whoAmI * 3.7f) * t * 3.5f;
                Vector2 mid = (a + b) * 0.5f + perp * wob - Main.screenPosition;
                float w = MathHelper.Lerp(3.5f, 13f, t);
                float fade = MathF.Pow(1f - t, 0.65f) * strength;
                float rot = seg.ToRotation() + MathHelper.PiOver2;
                sb.Draw(tex, mid, null, Mist * (0.40f * fade), rot, origin
                    , new Vector2(w / tex.Width * 2f, len / tex.Height * 1.25f), SpriteEffects.None, 0f);
                if (t < 0.55f) {
                    sb.Draw(tex, mid, null, Core with { A = 0 } * (0.30f * fade * (1f - t / 0.55f)), rot, origin
                        , new Vector2(w / tex.Width * 0.7f, len / tex.Height * 1.15f), SpriteEffects.None, 0f);
                }
            }
        }

        //==== 数学 ====

        /// <summary>带过冲缓出，着陆压扁回弹的「果冻」曲线</summary>
        public static float EaseOutBack(float x) {
            x = MathHelper.Clamp(x, 0f, 1f);
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float xm = x - 1f;
            return 1f + c3 * xm * xm * xm + c1 * xm * xm;
        }
    }

    /// <summary>
    /// 企鹅雪尘，哑光 AlphaBlend 雪雾团，快进慢出、微重力落定
    /// squish 拉宽后可作贴地残雪斑
    /// </summary>
    internal class PRT_FishPengSnow : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Fog";
        public override bool CanPool => true;

        private float peak;
        private Vector2 squish;
        private float gravity;
        private float spin;
        private Color initialColor;

        public PRT_FishPengSnow Configure(int lifetime, float peakOpacity, Vector2? squishVec = null, float gravityPerFrame = 0.045f) {
            Lifetime = lifetime;
            peak = peakOpacity;
            squish = squishVec ?? Vector2.One;
            gravity = gravityPerFrame;
            initialColor = Color;
            spin = Main.rand.NextFloat(-0.02f, 0.02f);
            return this;
        }

        public override void Reset() {
            base.Reset();
            peak = 0f;
            squish = Vector2.One;
            gravity = 0f;
            spin = 0f;
            initialColor = default;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
        }

        public override void AI() {
            float t = LifetimeCompletion;
            Scale *= 1.006f;
            Rotation += spin;
            Velocity *= 0.90f;
            Velocity.Y += gravity;
            if (Velocity.Y > 2f) {
                Velocity.Y = 2f;
            }
            //雪尘冷面渐显，白→雾灰蓝
            Color = Color.Lerp(initialColor, FishPengVFX.Mist, t * 0.8f);
            Opacity = MathF.Min(t / 0.10f, 1f) * (1f - SmoothStep01((t - 0.35f) / 0.65f)) * peak;
        }

        private static float SmoothStep01(float x) {
            x = MathHelper.Clamp(x, 0f, 1f);
            return x * x * (3f - 2f * x);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            spriteBatch.Draw(tex, Position - Main.screenPosition, null, Color * Opacity, Rotation
                , tex.Size() * 0.5f, Scale * 0.3f * squish, SpriteEffects.None, 0);
            return false;
        }
    }

    /// <summary>
    /// 企鹅羽毛，黑背白腹双色哑光羽，爆散段旋转带拖影，随后转入摆动飘落
    /// 只作着陆爆点缀，量少命长撑 aftermath
    /// </summary>
    internal class PRT_FishPengFeather : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";
        public override bool CanPool => true;

        private float sway;
        private float spin;
        private float seed;

        public PRT_FishPengFeather Configure(int lifetime, float swayAmp) {
            Lifetime = lifetime;
            sway = swayAmp;
            spin = Main.rand.NextFloat(0.2f, 0.42f) * (Main.rand.NextBool() ? 1f : -1f);
            seed = Main.rand.NextFloat(MathHelper.TwoPi);
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            return this;
        }

        public override void Reset() {
            base.Reset();
            sway = 0f;
            spin = 0f;
            seed = 0f;
        }

        public override void SetProperty() => PRTDrawMode = PRTDrawModeEnum.AlphaBlend;

        public override void AI() {
            if (Time < 12) {
                //爆散段，高速甩出自旋
                Velocity *= 0.90f;
                Rotation += spin;
                spin *= 0.95f;
            }
            else {
                //摆落段，钟摆式左右飘，羽面随摆倾斜
                float ph = seed + Time * 0.085f;
                Velocity.X = MathHelper.Lerp(Velocity.X, MathF.Sin(ph) * sway, 0.12f);
                Velocity.Y = MathHelper.Lerp(Velocity.Y, 1.15f + MathF.Cos(ph * 0.5f) * 0.25f, 0.07f);
                Rotation = Rotation.AngleLerp(MathHelper.PiOver2 + Velocity.X * 0.32f, 0.14f);
                spin *= 0.9f;
            }
            float t = LifetimeCompletion;
            Opacity = t < 0.72f ? 1f : 1f - (t - 0.72f) / 0.28f;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 pos = Position - Main.screenPosition;
            Vector2 bodyScale = new Vector2(0.16f, 0.42f) * Scale;
            //自旋期旋转拖影，位置残影表达不了自旋
            if (MathF.Abs(spin) > 0.04f) {
                spriteBatch.Draw(tex, pos, null, Color * (Opacity * 0.30f), Rotation - spin * 2.6f
                    , origin, bodyScale, SpriteEffects.None, 0f);
            }
            //羽背黑
            spriteBatch.Draw(tex, pos, null, Color * Opacity, Rotation, origin, bodyScale, SpriteEffects.None, 0f);
            //羽腹白
            Vector2 axis = (Rotation + MathHelper.PiOver2).ToRotationVector2();
            spriteBatch.Draw(tex, pos + axis * tex.Height * bodyScale.Y * 0.18f, null
                , FishPengVFX.FeatherLight * (Opacity * 0.9f), Rotation, origin
                , new Vector2(0.10f, 0.24f) * Scale, SpriteEffects.None, 0f);
            return false;
        }
    }

    /// <summary>
    /// 着陆后残留天空的凝结尾迹，整条独立存活 30 帧
    /// 天空端先蚀、整体缓缓上浮扩散，指向坠机点的喜剧余韵
    /// </summary>
    internal class PRT_FishPengContrail : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";
        public override bool CanPool => true;

        private const int MaxPoints = 15;
        private readonly Vector2[] points = new Vector2[MaxPoints];
        private int count;
        private float seed;

        /// <summary>从弹幕 oldPos 截取尾迹路径（points[0]=坠机端）</summary>
        public PRT_FishPengContrail Configure(Projectile proj, int lifetime) {
            count = 0;
            seed = proj.whoAmI * 3.7f;
            if (proj.oldPos != null) {
                Vector2 half = proj.Size * 0.5f;
                for (int i = 2; i < proj.oldPos.Length && count < MaxPoints; i++) {
                    if (proj.oldPos[i] == Vector2.Zero) {
                        break;
                    }
                    points[count++] = proj.oldPos[i] + half;
                }
            }
            Lifetime = lifetime;
            Position = count > 0 ? points[0] : proj.Center;
            return this;
        }

        public override void Reset() {
            base.Reset();
            count = 0;
            seed = 0f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            Velocity = Vector2.Zero;
        }

        public override void AI() {
            Opacity = MathF.Pow(1f - LifetimeCompletion, 1.35f);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            if (count < 2) {
                return false;
            }
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 origin = tex.Size() * 0.5f;
            float lc = LifetimeCompletion;
            //失去机体的蒸汽整体上浮
            Vector2 drift = new(0f, -lc * 14f);

            for (int i = 0; i < count - 1; i++) {
                float t = i / (float)(count - 1);   //0=坠机端 1=天空端
                //天空端先蚀
                float aliveEdge = 1f - lc * 1.3f;
                float alive = MathHelper.Clamp((aliveEdge - t) / 0.16f, 0f, 1f);
                alive *= 0.75f + 0.25f * MathF.Sin(seed + i * 3.1f + lc * 26f);
                if (alive <= 0.02f) {
                    continue;
                }
                Vector2 perp = (points[i + 1] - points[i]).SafeNormalize(Vector2.UnitY).RotatedBy(MathHelper.PiOver2);
                float wob = MathF.Sin(i * 2.13f + seed) * (t + lc) * 3.5f;
                Vector2 a = points[i] + drift + perp * wob - Main.screenPosition;
                Vector2 b = points[i + 1] + drift + perp * wob - Main.screenPosition;
                Vector2 seg = b - a;
                float len = seg.Length();
                if (len < 0.5f) {
                    continue;
                }
                //死尾迹只剩雾体，随龄扩宽变淡
                float w = MathHelper.Lerp(4f, 14f, t) * (1f + lc * 0.9f);
                Color col = FishPengVFX.Mist * (0.34f * MathF.Pow(1f - t, 0.5f) * alive * Opacity);
                spriteBatch.Draw(tex, (a + b) * 0.5f, null, col, seg.ToRotation() + MathHelper.PiOver2, origin
                    , new Vector2(w / tex.Width * 2f, len / tex.Height * 1.25f), SpriteEffects.None, 0f);
            }
            return false;
        }
    }
}
