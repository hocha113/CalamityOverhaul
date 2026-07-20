using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    /// <summary>
    /// 群蝠幻流化形入场/退场的英雄时刻弹幕：<br/>
    /// 入场（ai0=0）在施放点冻结玩家姿态，三片暗影剪影加速撕散并蚀空本体；<br/>
    /// 退场（ai0=1）追踪玩家实时位置，剪影自外收拢重组，前段压住真身显形避免瞬现<br/>
    /// 零伤害纯视觉，仅拥有者客户端生成、走常规弹幕同步，粒子装饰在各端 AI 内自发
    /// </summary>
    internal class FishBatMorphProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>撕散阶段时长</summary>
        public const int TearLife = 20;
        /// <summary>重组阶段时长</summary>
        public const int AssembleLife = 22;

        private Player Owner => Main.player[Projectile.owner];
        private bool Assemble => Projectile.ai[0] == 1f;
        private ref float Timer => ref Projectile.localAI[0];

        //共享绘制傀儡：仅在单次 PreDraw 内写入后立即使用，非跨帧状态（镜像 MimicPhantom）
        private static Player ghostDrawPlayer;
        private bool poseCaptured;
        private Rectangle poseBody;
        private Rectangle poseLeg;
        private int poseDirection;

        //三片剪影的分离方向、距离与基础透明度
        private static readonly Vector2[] ShredDirs = [new(-0.68f, -0.74f), new(0.96f, 0.2f), new(-0.22f, 0.92f)];
        private static readonly float[] ShredDists = [30f, 38f, 26f];
        private static readonly float[] ShredAlphas = [0.62f, 0.5f, 0.42f];

        internal static readonly Color SmokeDark = new(24, 18, 34);
        internal static readonly Color WingViolet = new(58, 38, 86);
        internal static readonly Color SonarLine = new(186, 162, 240);
        internal static readonly Color GhostTint = new(34, 24, 52);

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = AssembleLife + 4;
            Projectile.alpha = 255;
        }

        public override void AI() {
            if (!Owner.active || Owner.dead) {
                Projectile.Kill();
                return;
            }

            int life = Assemble ? AssembleLife : TearLife;
            if (Timer >= life) {
                Projectile.Kill();
                return;
            }

            if (Assemble) {
                //收拢目标点跟着真身走，落点与显形逐帧对齐
                Projectile.Center = Owner.Center;
                if (Timer < 14) {
                    //压住显形直到剪影凝到峰值：真身在暗影全覆盖下现身，再由剪影衰减揭开
                    Owner.GetOverride<HalibutPlayer>().HidePlayerTime = 2;
                }
                if (!VaultUtils.isServer) {
                    SpawnAssembleDress();
                }
            }
            else if (!VaultUtils.isServer) {
                SpawnTearDress();
            }

            Timer++;
        }

        /// <summary>撕散逐帧装饰：起手双声呐环一快一慢、暗烟自躯体渗出、翼影自剪影边撕出</summary>
        private void SpawnTearDress() {
            Vector2 basePos = Projectile.Center;
            if (Timer == 0) {
                PRTLoader.NewParticle<PRT_FishBatSonar>(basePos, Vector2.Zero, SonarLine, 1f)
                    .Configure(0.26f, 2.4f, 26);
            }
            if (Timer == 6) {
                PRTLoader.NewParticle<PRT_FishBatSonar>(basePos, Vector2.Zero, SonarLine * 0.75f, 1f)
                    .Configure(0.2f, 1.5f, 30);
            }
            if (Timer % 2 == 0 && Timer < 15) {
                Vector2 puffPos = basePos + new Vector2(Main.rand.NextFloat(-13f, 13f), Main.rand.NextFloat(-25f, 23f));
                Vector2 puffVel = (puffPos - basePos).SafeNormalize(Vector2.UnitY) * Main.rand.NextFloat(0.8f, 2f) + new Vector2(0f, -0.5f);
                PRTLoader.NewParticle<PRT_FishBatSmoke>(puffPos, puffVel, SmokeDark, Main.rand.NextFloat(0.15f, 0.24f))
                    .Configure(Main.rand.Next(22, 32), 0.5f);
            }
            if (Timer % 3 == 0 && Timer < 12) {
                float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 edge = basePos + ang.ToRotationVector2() * Main.rand.NextFloat(10f, 20f);
                var wing = PRTLoader.NewParticle<PRT_FishBatCrescent>(edge, ang.ToRotationVector2() * Main.rand.NextFloat(2.5f, 4.5f)
                    , WingViolet, Main.rand.NextFloat(0.3f, 0.44f));
                wing.Rotation = ang + MathHelper.PiOver2;
                wing.Configure(Main.rand.Next(11, 16), Main.rand.NextFloat(-0.06f, 0.06f));
            }
        }

        /// <summary>重组逐帧装饰：塌缩声呐环定位重组点、暗烟自外圈向心收拢、落定轻响环</summary>
        private void SpawnAssembleDress() {
            Vector2 center = Projectile.Center;
            if (Timer == 0) {
                //反向环：由外向内塌缩，宣告蝠群的归巢点
                PRTLoader.NewParticle<PRT_FishBatSonar>(center, Vector2.Zero, SonarLine, 1f)
                    .Configure(1.8f, 0.18f, 18);
            }
            if (Timer % 2 == 0 && Timer < 14) {
                float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 puffPos = center + ang.ToRotationVector2() * Main.rand.NextFloat(48f, 86f);
                Vector2 puffVel = (center - puffPos).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(3.2f, 5f);
                PRTLoader.NewParticle<PRT_FishBatSmoke>(puffPos, puffVel, SmokeDark, Main.rand.NextFloat(0.13f, 0.2f))
                    .Configure(Main.rand.Next(16, 24), 0.48f);
            }
            if (Timer == 15) {
                PRTLoader.NewParticle<PRT_FishBatSonar>(center, Vector2.Zero, new Color(170, 148, 226), 1f)
                    .Configure(0.14f, 0.7f, 14);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Player owner = Owner;
            if (owner == null || !owner.active) {
                return false;
            }

            float life = Assemble ? AssembleLife : TearLife;
            float t = MathHelper.Clamp(Timer / life, 0f, 1f);

            if (!poseCaptured || Assemble) {
                //撕散冻结施放瞬间姿态，重组追当前姿态
                poseCaptured = true;
                poseBody = owner.bodyFrame;
                poseLeg = owner.legFrame;
                poseDirection = owner.direction;
            }

            //结束当前批次以使用 PlayerRenderer 绘制剪影傀儡
            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend
                , SamplerState.PointClamp, null, Main.Rasterizer, null, Main.GameViewMatrix.ZoomMatrix);

            ghostDrawPlayer ??= new Player();
            Player gp = ghostDrawPlayer;
            //标记为陈列体：tileRangeX/Y 是全局静态，镜像 MimicPhantom 防止抹掉接触距离加成
            gp.isDisplayDollOrInanimate = true;
            gp.CopyVisuals(owner);
            gp.ResetEffects();
            gp.velocity = Vector2.Zero;
            gp.direction = poseDirection;
            gp.bodyFrame = poseBody;
            gp.legFrame = poseLeg;
            gp.fullRotationOrigin = owner.Size * 0.5f;
            gp.skinVariant = owner.skinVariant;
            gp.heldProj = -1;

            Vector2 basePos = Projectile.Center - owner.Size * 0.5f;

            //中心本体：撕散时最先蚀空；重组时快速凝到峰值盖住真身现身帧，尾段衰减揭开
            float coreAlpha = Assemble
                ? MathF.Min(1f, MathF.Max(0f, (t - 0.22f) / 0.36f)) * 0.72f * (1f - SmoothStep((t - 0.68f) / 0.32f))
                : MathF.Max(0f, 1f - t * 1.9f) * 0.78f;

            for (int k = 2; k >= 0; k--) {
                float shredT = Assemble ? MathF.Pow(1f - t, 1.4f) : MathF.Pow(t, 1.55f);
                Vector2 offset = ShredDirs[k] * ShredDists[k] * shredT + new Vector2(0f, -9f) * (Assemble ? shredT : t);
                //撕散片渐入接替本体，避免开头四层剪影叠成黑块
                float alpha = Assemble
                    ? ShredAlphas[k] * MathF.Min(t * 4f, 1f) * (1f - SmoothStep((t - 0.6f) / 0.34f))
                    : ShredAlphas[k] * MathF.Min(t * 3f, 1f) * MathF.Pow(1f - t, 1.5f);
                float rot = (Assemble ? shredT : t) * 0.15f * (k % 2 == 0 ? -1f : 1f);
                DrawGhost(gp, basePos + offset, rot, alpha);
            }
            if (coreAlpha > 0.01f) {
                DrawGhost(gp, basePos, 0f, coreAlpha);
            }

            //恢复主世界的批次状态
            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend
                , Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            return false;
        }

        private static float SmoothStep(float x) {
            x = MathHelper.Clamp(x, 0f, 1f);
            return x * x * (3f - 2f * x);
        }

        private static void DrawGhost(Player gp, Vector2 pos, float rotation, float alpha) {
            //基色固定暗紫，整体透明度交给 shadow 形参：盔甲/时装层一起淡出，读作剪影而非亮甲假人
            gp.skinColor = GhostTint;
            gp.shirtColor = GhostTint;
            gp.underShirtColor = GhostTint;
            gp.pantsColor = GhostTint;
            gp.shoeColor = GhostTint;
            gp.hairColor = GhostTint;
            gp.eyeColor = GhostTint;
            gp.position = pos;
            gp.fullRotation = rotation;
            Main.PlayerRenderer.DrawPlayer(Main.Camera, gp, gp.position, gp.fullRotation
                , gp.fullRotationOrigin, 1f - MathHelper.Clamp(alpha, 0f, 1f));
        }
    }

    /// <summary>
    /// 新月扑翼残影：蝙蝠下拍瞬间甩出的皮翼暗影，AlphaBlend 暗紫压色非加色，<br/>
    /// 顺拍翼方向短促漂移、微展开后蚀散，双层窄叠读作皮膜厚度
    /// </summary>
    internal class PRT_FishBatCrescent : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "CrescentSoft02";
        public override bool CanPool => true;

        private float spin;
        private float baseScale;

        public PRT_FishBatCrescent Configure(int lifetime, float spinSpeed) {
            Lifetime = lifetime;
            spin = spinSpeed;
            baseScale = Scale;
            return this;
        }

        public override void Reset() {
            base.Reset();
            spin = 0f;
            baseScale = 0f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            //漏掉 Configure 时的兜底，防 Lifetime<=0 永驻
            if (Lifetime <= 0) {
                Lifetime = 12;
            }
        }

        public override void AI() {
            if (baseScale == 0f) {
                baseScale = Scale;
            }
            Velocity *= 0.9f;
            Rotation += spin;
            float t = LifetimeCompletion;
            Scale = baseScale * (1f + t * 0.35f);
            Opacity = MathF.Min(t * 6f, 1f) * MathF.Pow(1f - t, 1.6f);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 pos = Position - Main.screenPosition;
            Vector2 origin = tex.Size() * 0.5f;
            spriteBatch.Draw(tex, pos, null, Color * Opacity, Rotation, origin, Scale, SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, pos, null, new Color(16, 12, 26) * (Opacity * 0.7f), Rotation, origin
                , Scale * new Vector2(0.8f, 0.62f), SpriteEffects.None, 0f);
            return false;
        }
    }

    /// <summary>
    /// 声呐脉冲细环：回声定位可视化，Ring01 细线环加色绘制、主环+滞后回声双线；<br/>
    /// from&gt;to 时为塌缩环（退场定位用）。全技能唯一亮部，峰值透明度压在 0.55 以下
    /// </summary>
    internal class PRT_FishBatSonar : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Ring01";
        public override bool CanPool => true;

        private float fromScale;
        private float toScale;

        public PRT_FishBatSonar Configure(float from, float to, int lifetime) {
            fromScale = from;
            toScale = to;
            Scale = from;
            Lifetime = lifetime;
            return this;
        }

        public override void Reset() {
            base.Reset();
            fromScale = 0f;
            toScale = 0f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            if (Lifetime <= 0) {
                Lifetime = 18;
            }
        }

        public override void AI() {
            float t = LifetimeCompletion;
            //扩散前快后慢，声波衰减
            float ease = 1f - MathF.Pow(1f - t, 2.2f);
            Scale = MathHelper.Lerp(fromScale, toScale, ease);
            Opacity = MathF.Min(t * 7f, 1f) * MathF.Pow(1f - t, 1.7f) * 0.55f;
            Velocity *= 0.9f;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 pos = Position - Main.screenPosition;
            Vector2 origin = tex.Size() * 0.5f;
            Color col = Color with { A = 0 };
            spriteBatch.Draw(tex, pos, null, col * Opacity, 0f, origin, Scale, SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, pos, null, col * (Opacity * 0.45f), 0f, origin, Scale * 0.84f, SpriteEffects.None, 0f);
            return false;
        }
    }

    /// <summary>
    /// 化形暗烟：聚散的载体，SmokeSheet01 随机帧 AlphaBlend 炭黑染色，<br/>
    /// 缓胀缓散带微升，暗色压底永不发光
    /// </summary>
    internal class PRT_FishBatSmoke : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "SmokeSheet01";
        public override bool CanPool => true;

        private float spin;
        private float baseScale;
        private float maxOpacity;

        public PRT_FishBatSmoke Configure(int lifetime, float opacity = 0.42f) {
            Lifetime = lifetime;
            maxOpacity = opacity;
            baseScale = Scale;
            return this;
        }

        public override void Reset() {
            base.Reset();
            spin = 0f;
            baseScale = 0f;
            maxOpacity = 0f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            ai[0] = Main.rand.Next(4);
            spin = Main.rand.NextFloat(-0.03f, 0.03f);
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            if (Lifetime <= 0) {
                Lifetime = 24;
                maxOpacity = 0.42f;
            }
        }

        public override void AI() {
            if (baseScale == 0f) {
                baseScale = Scale;
            }
            Velocity *= 0.93f;
            Velocity.Y -= 0.012f;//暗烟微升
            Rotation += spin;
            float t = LifetimeCompletion;
            Scale = baseScale * (1f + t * 0.55f);
            Opacity = MathF.Min(t * 5f, 1f) * MathF.Pow(1f - t, 1.35f) * maxOpacity;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            int index = (int)ai[0];
            int frameSize = tex.Width / 2;
            Rectangle frame = new Rectangle(index % 2 * frameSize, index / 2 * frameSize, frameSize, frameSize);
            spriteBatch.Draw(tex, Position - Main.screenPosition, frame, Color * Opacity, Rotation
                , frame.Size() / 2f, Scale / 6f, SpriteEffects.None, 0f);
            return false;
        }
    }
}
