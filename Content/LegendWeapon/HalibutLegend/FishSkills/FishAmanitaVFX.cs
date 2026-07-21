using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    /// <summary>毒孢鱼泡域内资源（域内加载器，不经 EffectLoader）</summary>
    internal class FishAmanitaAssets
    {
        /// <summary>浓稠孢子云</summary>
        [VaultLoaden(CWRConstant.Effects)]
        public static Effect FishAmanitaMist { get; private set; }

        /// <summary>孢子四帧序列贴图（与 <see cref="Items.Ranged.PRT_SporeBobo"/> 同源），弹体核仁用</summary>
        [VaultLoaden(CWRConstant.Other + "SporeBobo")]
        internal static Asset<Texture2D> SporeSheet = null;
    }

    /// <summary>毒孢鱼泡</summary>
    internal static class FishAmanitaVFX
    {
        /// <summary>暗菌紫（外圈/压底）</summary>
        public static readonly Color SporeDusk = new(46, 28, 74);
        /// <summary>菌紫（语系主强调）</summary>
        public static readonly Color SporeViolet = new(124, 78, 202);
        /// <summary>菌蓝（过渡）</summary>
        public static readonly Color SporeBlue = new(98, 146, 234);
        /// <summary>孢光青白，仅限小面积热芯/瞬时爆点</summary>
        public static readonly Color SporeGlow = new(186, 226, 252);
        /// <summary>毒蕈伞红（爆炸蘑菇伞盖）</summary>
        public static readonly Color CapCrimson = new(206, 66, 84);
        /// <summary>伞红暗部</summary>
        public static readonly Color CapDeep = new(110, 30, 54);
        /// <summary>追孢青（追踪孢子）</summary>
        public static readonly Color HomingCyan = new(108, 210, 214);
        /// <summary>瘴紫（毒雾主色，区别于通用荧光绿毒雾）</summary>
        public static readonly Color MistOrchid = new(150, 96, 214);
        /// <summary>瘴紫暗部</summary>
        public static readonly Color MistDeep = new(66, 36, 104);
        /// <summary>菌电紫白（闪电孢子电弧）</summary>
        public static readonly Color ArcVolt = new(196, 176, 255);

        /// <summary>形态 0-3 → 弹药主色</summary>
        public static Color PhaseColor(int phase) => phase switch {
            0 => CapCrimson,
            1 => HomingCyan,
            2 => MistOrchid,
            3 => ArcVolt,
            _ => SporeViolet
        };


        /// <summary>湿润孢子噗</summary>
        public static void SporePuffSound(Vector2 pos, float pitch, float volume) {
            SoundEngine.PlaySound(SoundID.NPCDeath1 with { Pitch = pitch, Volume = volume, MaxInstances = 4 }, pos);
            SoundEngine.PlaySound(SoundID.Item85 with { Pitch = pitch + 0.25f, Volume = volume * 0.45f, MaxInstances = 4 }, pos);
        }


        /// <summary>菌盖爆开成孢子环</summary>
        public static void SporeRing(Vector2 pos, Color color, int glowCount, float speed, float scale = 1f) {
            if (Main.dedServ) {
                return;
            }
            float phase = Main.rand.NextFloat(MathHelper.TwoPi);
            for (int i = 0; i < glowCount; i++) {
                float angle = MathHelper.TwoPi * i / glowCount + phase;
                Vector2 vel = angle.ToRotationVector2() * speed * Main.rand.NextFloat(0.85f, 1.15f);
                PRTLoader.NewParticle<PRT_FishAmanitaSpore>(pos, vel, color, Main.rand.NextFloat(0.7f, 1.1f) * scale)
                    ?.Configure(Main.rand.Next(34, 52), 0.05f, 0.008f);
            }
            //实体孢子颗粒
            int grainCount = glowCount / 3;
            for (int i = 0; i < grainCount; i++) {
                var prt = PRTLoader.NewParticle<PRT_SporeBobo>(pos, Main.rand.NextVector2Unit() * speed * 0.55f);
                if (prt != null) {
                    prt.Color = color;
                    prt.Scale = Main.rand.NextFloat(0.6f, 1f) * scale;
                }
            }
            //暗紫外环压底 + 弹药色内环
            PRTLoader.NewParticle<PRT_DWave>(pos, Vector2.Zero, SporeDusk, 0.16f * scale)
                ?.Configure(Vector2.One, 0f, 0.95f * scale, 14);
            PRTLoader.NewParticle<PRT_DWave>(pos, Vector2.Zero, color, 0.1f * scale)
                ?.Configure(Vector2.One, 0f, 0.6f * scale, 10);
        }

        /// <summary>单颗漂移孢子</summary>
        public static void SporeDrift(Vector2 pos, Vector2 vel, Color color, float scale = 1f, int lifetime = 0) {
            if (Main.dedServ) {
                return;
            }
            PRTLoader.NewParticle<PRT_FishAmanitaSpore>(pos, vel, color
                , scale * Main.rand.NextFloat(0.75f, 1.1f))
                ?.Configure(lifetime > 0 ? lifetime : Main.rand.Next(28, 44), 0.04f, 0.006f);
        }

        /// <summary>菌丝电弧</summary>
        public static void MyceliumArc(Vector2 from, Vector2 to, Color color, float width, int lifetime, int branches = 1, float jitterScale = 1f) {
            if (Main.dedServ) {
                return;
            }
            PRTLoader.NewParticle<PRT_FishAmanitaArc>(from, Vector2.Zero, color, 1f)
                ?.Configure(from, to, width, lifetime, branches, jitterScale);
        }
    }

    /// <summary>发光真菌孢子</summary>
    internal class PRT_FishAmanitaSpore : BasePRT
    {
        public override string Texture => CWRConstant.Other + "SporeBobo";
        public override bool CanPool => true;

        private float breathSeed;
        private float drift;     //布朗扰动强度
        private float riseBias;  //上浮偏置，孢子比空气轻

        public PRT_FishAmanitaSpore Configure(int lifetime, float driftStrength = 0.05f, float rise = 0.006f) {
            Lifetime = lifetime;
            drift = driftStrength;
            riseBias = rise;
            return this;
        }

        public override void Reset() {
            base.Reset();
            breathSeed = 0f;
            drift = 0f;
            riseBias = 0f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            breathSeed = Main.rand.NextFloat(MathHelper.TwoPi);
            Frame = TexValue.GetRectangle(Main.rand.Next(4), 4);
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            if (Lifetime <= 0) {
                Lifetime = Main.rand.Next(30, 46);
            }
            if (drift == 0f) {
                drift = 0.05f;
            }
        }

        public override void AI() {
            //撒出减速 → 布朗游走接管
            Velocity *= 0.93f;
            Velocity += Main.rand.NextVector2Circular(drift, drift);
            Velocity.Y -= riseBias;
            Rotation += Velocity.X * 0.03f;

            float lc = LifetimeCompletion;
            float breath = 0.72f + 0.28f * MathF.Sin(Time * 0.23f + breathSeed);
            Opacity = MathF.Min(lc * 6f, 1f) * (1f - lc * lc) * breath;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Vector2 pos = Position - Main.screenPosition;
            Color col = Color with { A = 0 };
            //底光垫层，小半径微光，权重封顶
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow != null) {
                spriteBatch.Draw(glow, pos, null, col * (0.35f * Opacity), 0f
                    , glow.Size() * 0.5f, 0.14f * Scale, SpriteEffects.None, 0f);
            }
            //孢子实体帧，剪影载体
            spriteBatch.Draw(TexValue, pos, Frame, col * (0.9f * Opacity), Rotation
                , Frame.Size() / 2f, 0.85f * Scale, SpriteEffects.None, 0f);
            return false;
        }
    }

    /// <summary>菌丝电弧</summary>
    internal class PRT_FishAmanitaArc : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Line";
        public override bool CanPool => true;

        private const int TrunkPoints = 9;   //主径折点数（8 段）
        private const int BranchPoints = 4;  //每条分叉折点数（3 段）
        private const int MaxBranches = 2;

        private readonly Vector2[] trunk = new Vector2[TrunkPoints];
        private readonly Vector2[,] branchPts = new Vector2[MaxBranches, BranchPoints];
        private Vector2 endPos;
        private float width;
        private int branchCount;
        private float jitter;
        private float flickerSeed;

        public PRT_FishAmanitaArc Configure(Vector2 from, Vector2 to, float arcWidth, int lifetime, int branches, float jitterScale = 1f) {
            Position = from;
            endPos = to;
            width = arcWidth;
            Lifetime = lifetime;
            branchCount = Math.Clamp(branches, 0, MaxBranches);
            jitter = jitterScale;
            BuildArc();
            return this;
        }

        public override void Reset() {
            base.Reset();
            endPos = default;
            width = 0f;
            branchCount = 0;
            jitter = 1f;
            flickerSeed = 0f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            flickerSeed = Main.rand.NextFloat(MathHelper.TwoPi);
            if (Lifetime <= 0) {
                Lifetime = 9;
            }
        }

        /// <summary>中点位移细分</summary>
        private void BuildArc() {
            Vector2 dir = endPos - Position;
            float len = dir.Length();
            if (len < 1f) {
                len = 1f;
            }
            Vector2 normal = (dir / len).RotatedBy(MathHelper.PiOver2);
            for (int i = 0; i < TrunkPoints; i++) {
                float t = i / (float)(TrunkPoints - 1);
                //端点锚定，中段位移
                float amp = MathF.Sin(t * MathHelper.Pi) * len * 0.14f * jitter;
                float offset = Main.rand.NextFloat(-1f, 1f) * amp;
                trunk[i] = Vector2.Lerp(Position, endPos, t) + normal * offset;
            }
            for (int b = 0; b < branchCount; b++) {
                int rootIdx = Main.rand.Next(2, TrunkPoints - 3);
                Vector2 root = trunk[rootIdx];
                Vector2 bDir = (dir / len).RotatedBy(Main.rand.NextFloat(0.5f, 1.1f) * (Main.rand.NextBool() ? 1f : -1f));
                float bLen = len * Main.rand.NextFloat(0.18f, 0.3f);
                for (int i = 0; i < BranchPoints; i++) {
                    float t = i / (float)(BranchPoints - 1);
                    branchPts[b, i] = root + bDir * bLen * t
                        + bDir.RotatedBy(MathHelper.PiOver2) * Main.rand.NextFloat(-1f, 1f) * bLen * 0.16f * t;
                }
            }
        }

        public override void AI() {
            //周期重抖动
            if (Time % 3 == 0 && Time > 0) {
                BuildArc();
            }
            float lc = LifetimeCompletion;
            float flicker = 0.7f + 0.3f * MathF.Sin(Time * 2.7f + flickerSeed);
            Opacity = (1f - lc * lc) * flicker;
        }

        private void DrawSegments(SpriteBatch sb, Texture2D tex, Vector2[] pts, int count, float widthMul, Color col) {
            for (int i = 0; i < count - 1; i++) {
                Vector2 a = pts[i];
                Vector2 b = pts[i + 1];
                Vector2 seg = b - a;
                float t = i / (float)(count - 1);
                //粗细不均，头尾收细
                float w = width * widthMul * (0.55f + 0.45f * MathF.Sin(t * MathHelper.Pi)) / tex.Width;
                sb.Draw(tex, (a + b) * 0.5f - Main.screenPosition, null, col
                    , seg.ToRotation() + MathHelper.PiOver2, tex.Size() * 0.5f
                    , new Vector2(w, seg.Length() / tex.Height * 1.12f), SpriteEffects.None, 0f);
            }
        }

        private void DrawBranch(SpriteBatch sb, Texture2D tex, int b, float widthMul, Color col) {
            for (int i = 0; i < BranchPoints - 1; i++) {
                Vector2 a = branchPts[b, i];
                Vector2 c = branchPts[b, i + 1];
                Vector2 seg = c - a;
                float w = width * widthMul * (1f - i / (float)BranchPoints) / tex.Width;
                sb.Draw(tex, (a + c) * 0.5f - Main.screenPosition, null, col
                    , seg.ToRotation() + MathHelper.PiOver2, tex.Size() * 0.5f
                    , new Vector2(w, seg.Length() / tex.Height * 1.12f), SpriteEffects.None, 0f);
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = TexValue;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Color body = Color with { A = 0 };
            Color dim = FishAmanitaVFX.SporeViolet with { A = 0 };

            //暗紫底描边（宽）+ 电紫白亮芯（窄）
            DrawSegments(spriteBatch, tex, trunk, TrunkPoints, 2.1f, dim * (0.4f * Opacity));
            DrawSegments(spriteBatch, tex, trunk, TrunkPoints, 1f, body * Opacity);
            for (int b = 0; b < branchCount; b++) {
                DrawBranch(spriteBatch, tex, b, 0.6f, body * (0.7f * Opacity));
            }

            //折点孢子亮斑，菌丝网络节点发光
            for (int i = 1; i < TrunkPoints - 1; i += 2) {
                spriteBatch.Draw(glow, trunk[i] - Main.screenPosition, null, body * (0.5f * Opacity)
                    , 0f, glow.Size() * 0.5f, 0.1f + 0.03f * MathF.Sin(Time + i), SpriteEffects.None, 0f);
            }
            return false;
        }
    }

    /// <summary>菌盖碎片</summary>
    internal class PRT_FishAmanitaCapShard : BasePRT
    {
        public override string Texture => CWRConstant.Projectile + "Glomushroom";
        public override bool CanPool => true;

        private float spin;

        public override void Reset() {
            base.Reset();
            spin = 0f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            Lifetime = Main.rand.Next(26, 40);
            spin = Main.rand.NextFloat(0.12f, 0.3f) * (Main.rand.NextBool() ? 1f : -1f);
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            //随机源块，伞盖撕成不规则小片
            int w = Math.Max(TexValue.Width / 3, 4);
            int h = Math.Max(TexValue.Height / 3, 4);
            Frame = new Rectangle(Main.rand.Next(TexValue.Width - w), Main.rand.Next(TexValue.Height - h), w, h);
        }

        public override void AI() {
            Velocity.X *= 0.97f;
            Velocity.Y += 0.24f;
            Rotation += spin * (0.4f + Velocity.Length() * 0.08f);
            Opacity = 1f - MathF.Pow(LifetimeCompletion, 3f);
            Scale *= 0.985f;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            //环境光调制，哑光碎片不发光
            Color lit = Lighting.GetColor(Position.ToTileCoordinates());
            Color col = Color.Lerp(lit, Color, 0.35f) * Opacity;
            spriteBatch.Draw(TexValue, Position - Main.screenPosition, Frame, col
                , Rotation, Frame.Size() * 0.5f, Scale, SpriteEffects.None, 0f);
            return false;
        }
    }
}
