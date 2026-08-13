using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalBrainOfCthulhu.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalBrainOfCthulhu.Rendering;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalBrainOfCthulhu.States;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalBrainOfCthulhu.Projectiles
{
    /// <summary>
    /// 镜像假体：位置奴役于真身同步位（点对称）或脚本轨道，各端所见必然一致
    /// ai[0]=模式*100+槽位，ai[1]/ai[2]=锚点世界坐标
    /// 破绽：不发光、无心跳搏动、色泽冷偏、无出手眼芒；判定：仅冲刺窗口有伤且伤害折减
    /// </summary>
    internal class BrainMirrorImage : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>入场旋聚（纯演出）</summary>
        internal const int ModeIntroConverge = 0;
        /// <summary>点对称镜像奴役（同步进攻）</summary>
        internal const int ModePointMirror = 1;
        /// <summary>镜阵轨道+排程冲刺</summary>
        internal const int ModeMazeOrbit = 2;
        /// <summary>骤停闪现冲刺（生成即带速度）</summary>
        internal const int ModeGuidedDash = 3;
        /// <summary>摄心镜狱·收环（纯预告无伤，半径由本地寿命确定性推演）</summary>
        internal const int ModeSeizeRing = 4;
        /// <summary>摄心镜狱·穿刺（捕获后由服务端改写模式，按槽位排程刺向环心）</summary>
        internal const int ModeSeizePierce = 5;

        /// <summary>镜阵每槽冲刺间隔（帧）</summary>
        internal const int MazeDashInterval = 46;
        /// <summary>镜阵起始等待（帧）</summary>
        internal const int MazeFirstDashDelay = 76;
        /// <summary>镜阵轨道半径</summary>
        internal const float MazeRadius = 470f;

        private int Mode => (int)(Projectile.ai[0]) / 100;
        private int Slot => (int)(Projectile.ai[0]) % 100;
        private Vector2 Anchor => new(Projectile.ai[1], Projectile.ai[2]);

        /// <summary>本地寿命计数（客户端可能晚 1~2 帧，假体判定本地自洽可容忍）</summary>
        private ref float Age => ref Projectile.localAI[0];
        /// <summary>0轨道 1冲刺中 2冲刺完成待碎</summary>
        private ref float DashPhase => ref Projectile.localAI[1];

        internal static float PackMode(int mode, int slot) => mode * 100 + slot;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 900;
        }

        public override void SetDefaults() {
            Projectile.width = 130;
            Projectile.height = 100;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 1200;
            Projectile.netImportant = true;
        }

        public override void AI() {
            Age++;
            NPC brain = BrainMotion.FindBrain();
            if (brain == null) {
                Projectile.Kill();
                return;
            }

            switch (Mode) {
                case ModeIntroConverge:
                    UpdateIntroConverge();
                    break;
                case ModePointMirror:
                    UpdatePointMirror(brain);
                    break;
                case ModeMazeOrbit:
                    UpdateMazeOrbit(brain);
                    break;
                case ModeGuidedDash:
                    UpdateGuidedDash();
                    break;
                case ModeSeizeRing:
                    UpdateSeizeRing();
                    break;
                case ModeSeizePierce:
                    UpdateSeizePierce();
                    break;
            }
        }

        #region 模式行为

        /// <summary>入场旋聚：自远处螺旋汇入锚点后消隐</summary>
        private void UpdateIntroConverge() {
            const int lifetime = 96;
            float t = MathHelper.Clamp(Age / lifetime, 0f, 1f);
            float angle = MathHelper.TwoPi * Slot / 3f + t * 4.4f;
            float radius = MathHelper.Lerp(560f, 8f, BrainMotion.SharpOut(t, 3));
            Projectile.Center = Anchor + angle.ToRotationVector2() * radius;
            Projectile.hostile = false;
            Projectile.rotation = (float)Math.Sin(Age * 0.05f + Slot * 2f) * 0.1f;

            if (Age >= lifetime) {
                //汇入本体，无碎裂动静
                Projectile.Kill();
            }
        }

        /// <summary>点对称镜像：位置=2*锚-真身位；伤害窗口镜像真身冲刺</summary>
        private void UpdatePointMirror(NPC brain) {
            Projectile.Center = Anchor * 2f - brain.Center;
            Projectile.velocity = Vector2.Zero;
            Projectile.rotation = brain.rotation;
            Projectile.spriteDirection = -brain.spriteDirection;

            //真身冲刺时假体同步“冲刺”（位置自然镜像），伤害窗随真身速度开合
            bool dashing = brain.velocity.Length() > 21f;
            Projectile.hostile = dashing && Age > 22f;
        }

        /// <summary>镜阵轨道：环绕锚点旋转，按槽位排程冲向环心，冲完即碎</summary>
        private void UpdateMazeOrbit(NPC brain) {
            if (DashPhase == 0f) {
                float t = Age * 0.011f;
                float angle = MathHelper.TwoPi * Slot / 6f + t;
                float breathing = 1f + 0.05f * (float)Math.Sin(Age * 0.05f + Slot);
                int dashTick = MazeFirstDashDelay + Slot * MazeDashInterval;
                //出手前12帧反向外撑（预读窗口）
                float untilDash = dashTick - Age;
                float reel = untilDash is <= 12f and > 0f ? (12f - untilDash) / 12f * 55f : 0f;
                Projectile.Center = Anchor + angle.ToRotationVector2() * (MazeRadius * breathing + reel);
                Projectile.velocity = Vector2.Zero;
                Projectile.hostile = false;

                if (Age >= dashTick) {
                    DashPhase = 1f;
                    Projectile.velocity = (Anchor - Projectile.Center).SafeNormalize(Vector2.UnitY) * 30f;
                    if (!VaultUtils.isServer && BrainMotion.OnScreen(Projectile.Center)) {
                        BrainMotion.FleshSquish(Projectile.Center, 0.75f, -0.5f);
                        BrainMotion.BloodMistBurst(Projectile.Center, 0.8f, 3, 5f);
                    }
                }
                return;
            }

            if (DashPhase == 1f) {
                Projectile.hostile = true;
                //穿过环心一段距离后碎裂
                if (Vector2.Dot(Projectile.Center - Anchor, Projectile.velocity) > 0f
                    && Projectile.Distance(Anchor) > MazeRadius * 0.85f) {
                    DashPhase = 2f;
                    Projectile.Kill();
                }
            }
        }

        /// <summary>骤停闪现：生成即带速度直线掠过，短命自碎</summary>
        private void UpdateGuidedDash() {
            Projectile.hostile = Age > 6f;
            Projectile.rotation = (float)Math.Sin(Age * 0.09f + Slot) * 0.08f;
            if (Age >= 54f) {
                Projectile.Kill();
            }
        }

        /// <summary>
        /// 摄心环：绕锁死锚点收缩（半径公式与状态端共用，确定性收环），全程无判定
        /// 捕获后未被点名穿刺的槽位停在持环半径上呼吸
        /// </summary>
        private void UpdateSeizeRing() {
            float angle = MathHelper.TwoPi * Slot / BrainMindSeizeState.MirrorCount
                + Age * BrainMindSeizeState.RingSpinRate;
            float radius = BrainMindSeizeState.RingRadius(Age);
            //收环完成后的驻留呼吸
            if (Age > BrainMindSeizeState.SnapTick) {
                radius += 6f * (float)Math.Sin((Age - BrainMindSeizeState.SnapTick) * 0.06f + Slot);
            }
            Projectile.Center = Anchor + angle.ToRotationVector2() * radius;
            Projectile.velocity = Vector2.Zero;
            Projectile.hostile = false;
            Projectile.rotation = (float)Math.Sin(Age * 0.045f + Slot * 1.7f) * 0.08f;

            //超时自碎兜底（正常流程由服务端在掷飞/落空时统一清场）
            if (Age >= BrainMindSeizeState.MirrorLifeCap) {
                Projectile.Kill();
            }
        }

        /// <summary>
        /// 摄持穿刺：模式翻转边沿记起点，顿帧→外撑收势→贯穿环心→越过即碎
        /// 伤害不走接触判定，由受害者客户端按同一节拍脚本结算
        /// </summary>
        private void UpdateSeizePierce() {
            //翻转边沿：记录穿刺本地起点（localAI[2] 惰性初始化，钳到≥1防哨兵值滑动）
            if (Projectile.localAI[2] == 0f) {
                Projectile.localAI[2] = Math.Max(Age, 1f);
            }
            float pierceAge = Age - Projectile.localAI[2];
            int start = BrainMindSeizeState.PierceReelStart(Slot);
            Projectile.hostile = false;

            if (DashPhase == 0f) {
                //起手静止（捕获顿帧）+ 排程前驻环呼吸
                float angle = MathHelper.TwoPi * Slot / BrainMindSeizeState.MirrorCount
                    + Age * BrainMindSeizeState.RingSpinRate;
                float radius = BrainMindSeizeState.HoldRadius;
                //出手前收势：外撑蓄力（二次曲线，末段骤然）
                float reelT = MathHelper.Clamp((pierceAge - start) / (float)BrainMindSeizeState.PierceReelTime, 0f, 1f);
                radius += reelT * reelT * 60f;
                Projectile.Center = Anchor + angle.ToRotationVector2() * radius;
                Projectile.velocity = Vector2.Zero;
                Projectile.rotation = (float)Math.Sin(Age * 0.05f + Slot) * 0.06f;

                if (pierceAge >= start + BrainMindSeizeState.PierceReelTime) {
                    DashPhase = 1f;
                    Projectile.velocity = (Anchor - Projectile.Center).SafeNormalize(Vector2.UnitY)
                        * BrainMindSeizeState.PierceDashSpeed;
                    if (!VaultUtils.isServer && BrainMotion.OnScreen(Projectile.Center)) {
                        BrainMotion.FleshSquish(Projectile.Center, 0.8f, -0.4f);
                        BrainMotion.BloodMistBurst(Projectile.Center, 0.7f, 3, 5f);
                    }
                }
                return;
            }

            if (DashPhase == 1f) {
                //贯穿：越过环心一段距离即自碎（OnKill 碎裂演出各端自播）
                if (Vector2.Dot(Projectile.Center - Anchor, Projectile.velocity) > 0f
                    && Projectile.Distance(Anchor) > 280f) {
                    DashPhase = 2f;
                    Projectile.Kill();
                }
            }
        }

        #endregion

        public override bool ShouldUpdatePosition() => Mode is ModeGuidedDash or ModeMazeOrbit or ModeSeizeRing or ModeSeizePierce;

        public override void OnKill(int timeLeft) {
            //入场旋聚汇入无碎裂
            if (Mode != ModeIntroConverge) {
                BrainMotion.MirrorShatter(Projectile.Center, 1.1f);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = BrainRenderHelper.GetBrainTexture();
            NPC brain = BrainMotion.FindBrain();
            if (tex == null) {
                return false;
            }

            //帧与真身同拍蠕动（同一贴图同一帧，跨端一致）
            Rectangle frameRect;
            if (brain != null && brain.frame.Height > 0) {
                frameRect = brain.frame;
            }
            else {
                frameRect = BrainRenderHelper.GetFrameRect(tex, 0);
            }

            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            SpriteEffects effects = Projectile.spriteDirection > 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            //实体化包络：出生渐显，濒死渐散
            float materialize = MathHelper.Clamp(Age / 26f, 0f, 1f);
            if (Mode == ModeIntroConverge) {
                materialize *= 0.7f;
            }

            //冲刺残影
            if (Projectile.velocity.Length() > 16f) {
                for (int i = 2; i < Projectile.oldPos.Length; i += 2) {
                    if (Projectile.oldPos[i] == Vector2.Zero) {
                        continue;
                    }
                    float k = 1f - i / (float)Projectile.oldPos.Length;
                    Vector2 ghostPos = Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition;
                    Main.spriteBatch.Draw(tex, ghostPos, frameRect, new Color(120, 40, 90, 0) * (0.4f * k * materialize),
                        Projectile.rotation, frameRect.Size() * 0.5f, Projectile.scale, effects, 0f);
                }
            }

            //冷色镜像身体：uCold=1 是可学习破绽（微冷偏色+无光）
            BrainRenderHelper.DrawBrainBody(Main.spriteBatch, tex, drawPos, frameRect,
                lightColor, Projectile.rotation, Projectile.scale, effects,
                1f - materialize, 1f, MathHelper.Lerp(0.3f, 0.92f, materialize));

            return false;
        }
    }
}
