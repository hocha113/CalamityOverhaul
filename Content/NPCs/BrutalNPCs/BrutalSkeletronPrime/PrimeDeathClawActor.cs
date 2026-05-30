using CalamityOverhaul.Content.PRTTypes;
using InnoVault.Actors;
using InnoVault.GameSystem;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime
{
    /// <summary>
    /// 机械骷髅王死亡演出专用的钳子机械臂——本地视觉 <see cref="Actor"/>，与原生钳子手 NPC 完全解耦。
    /// <para>钳子中心位置是"演出阶段 + 演出计时 + 头部位置 + 目标玩家位置 + 左右侧"的纯函数，
    /// 因此多人模式下各客户端独立推进即可保持表现一致；本实体不造成任何伤害，仅用于演出。</para>
    /// </summary>
    internal class PrimeDeathClawActor : Actor
    {
        private int headWhoAmI = -1;
        private int side = 1;
        private int clawFrame;        //0=张开 1=闭合
        private float alpha = 1f;
        private bool finaleBurst;

        /// <summary>由头部演出在本地生成钳子后立即调用，绑定头部与左右侧</summary>
        internal void Setup(int head, int sideSign) {
            headWhoAmI = head;
            side = sideSign;
        }

        public override void OnSpawn(params object[] args) {
            Width = 90;
            Height = 90;
            DrawExtendMode = 1400;
            DrawLayer = ActorDrawLayer.Default;
            Velocity = Vector2.Zero;
            clawFrame = 0;
            alpha = 1f;
            finaleBurst = false;
        }

        private NPC Head => (headWhoAmI >= 0 && headWhoAmI < Main.maxNPCs) ? Main.npc[headWhoAmI] : null;

        public override void AI() {
            NPC head = Head;
            if (head == null || !head.active || head.type != NPCID.SkeletronPrime) {
                ActorLoader.KillActor(WhoAmI);
                return;
            }

            HeadPrimeAI headAI = head.GetOverride<HeadPrimeAI>();
            if (headAI == null || !headAI.InDeathPerformance) {
                ActorLoader.KillActor(WhoAmI);
                return;
            }

            int t = headAI.DeathTimer;
            PrimeDeathPhase phase = headAI.CurrentDeathPhase;
            Player target = headAI.DeathTargetPlayer;
            Vector2 targetCenter = (target != null && target.active) ? target.Center : head.Center + new Vector2(0f, 300f);

            Vector2 standby = head.Center + new Vector2(side * 150f, 70f);
            Vector2 grabPoint = targetCenter + new Vector2(side * 48f, 0f);

            Vector2 clawPos;
            Vector2 aimTarget;
            switch (phase) {
                case PrimeDeathPhase.Summon: {
                    float p = EaseOut((t - HeadPrimeAI.PhaseFakeDeathEnd) / (float)(HeadPrimeAI.PhaseSummonEnd - HeadPrimeAI.PhaseFakeDeathEnd));
                    clawPos = Vector2.Lerp(head.Center, standby, p);
                    aimTarget = head.Center + (clawPos - head.Center) * 2f;
                    clawFrame = 0;
                    break;
                }
                case PrimeDeathPhase.Lunge: {
                    float p = EaseIn((t - HeadPrimeAI.PhaseSummonEnd) / (float)(HeadPrimeAI.PhaseLungeEnd - HeadPrimeAI.PhaseSummonEnd));
                    clawPos = Vector2.Lerp(standby, grabPoint, p);
                    aimTarget = targetCenter;
                    clawFrame = p > 0.65f ? 1 : 0;
                    break;
                }
                case PrimeDeathPhase.Drag:
                case PrimeDeathPhase.Roar: {
                    clawPos = grabPoint;
                    if (phase == PrimeDeathPhase.Roar) {
                        clawPos += Main.rand.NextVector2Circular(3f, 3f);
                    }
                    aimTarget = targetCenter;
                    clawFrame = 1;
                    break;
                }
                case PrimeDeathPhase.Finale: {
                    float p = (t - HeadPrimeAI.PhaseRoarEnd) / (float)(HeadPrimeAI.PhaseFinaleEnd - HeadPrimeAI.PhaseRoarEnd);
                    Vector2 outward = new Vector2(side, -0.35f).SafeNormalize(Vector2.UnitX * side);
                    clawPos = grabPoint + outward * p * 280f;
                    aimTarget = clawPos + outward;
                    clawFrame = 1;
                    alpha = MathHelper.Clamp(1f - p * 1.2f, 0f, 1f);
                    if (!finaleBurst && t == HeadPrimeAI.PhaseRoarEnd) {
                        finaleBurst = true;
                        SpawnClawShatter(clawPos);
                    }
                    break;
                }
                default: {
                    ActorLoader.KillActor(WhoAmI);
                    return;
                }
            }

            Position = clawPos - Size / 2f;
            Vector2 aimDir = (aimTarget - clawPos).SafeNormalize(Vector2.UnitY);
            Rotation = aimDir.ToRotation() - MathHelper.PiOver2;
        }

        private static void SpawnClawShatter(Vector2 pos) {
            if (VaultUtils.isServer) {
                return;
            }
            Color warm = new Color(255, 110, 50);
            PRTLoader.NewParticle<PRT_MechExplosion>(pos, Vector2.Zero, warm, 2f).Configure(30, warm);
            for (int i = 0; i < 22; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(1f, 1f) * Main.rand.NextFloat(3f, 10f);
                PRTLoader.NewParticle<PRT_Spark>(pos, vel,
                    Color.Lerp(warm, Color.LightGoldenrodYellow, Main.rand.NextFloat()),
                    Main.rand.NextFloat(1f, 1.7f)).Configure(true, Main.rand.Next(14, 26));
            }
        }

        private static float EaseOut(float p) {
            p = MathHelper.Clamp(p, 0f, 1f);
            return 1f - MathF.Pow(1f - p, 3f);
        }

        private static float EaseIn(float p) {
            p = MathHelper.Clamp(p, 0f, 1f);
            return p * p * p;
        }

        public override bool PreDraw(SpriteBatch spriteBatch, ref Color drawColor) {
            NPC head = Head;
            if (head == null || !head.active) {
                return false;
            }
            if (HeadPrimeAI.BSPPliers == null || !HeadPrimeAI.BSPPliers.IsLoaded) {
                return false;
            }

            Vector2 shoulder = head.Center + new Vector2(side * 55f, 28f);
            Vector2 claw = Center;
            //肘部下沉并向外侧撇，营造机械关节的折角
            Vector2 elbow = (shoulder + claw) * 0.5f + new Vector2(side * 18f, 26f);

            Color armColor = Lighting.GetColor((int)(claw.X / 16f), (int)(claw.Y / 16f)) * alpha;

            //上臂 shoulder→elbow，前臂 elbow→claw
            DrawArmSegment(spriteBatch, HeadPrimeAI.BSPRAM.Value, HeadPrimeAI.BSPRAMGlow.Value, shoulder, elbow, armColor, alpha);
            DrawArmSegment(spriteBatch, HeadPrimeAI.BSPRAM_Forearm.Value, HeadPrimeAI.BSPRAM_ForearmGlow.Value, elbow, claw, armColor, alpha);

            //钳子本体（2 帧：张开/闭合）
            Texture2D pliers = HeadPrimeAI.BSPPliers.Value;
            Texture2D pliersGlow = HeadPrimeAI.BSPPliersGlow.Value;
            Rectangle rect = pliers.GetRectangle(clawFrame, 2);
            Vector2 origin = VaultUtils.GetOrig(pliers, 2);
            Vector2 drawPos = claw - Main.screenPosition;
            spriteBatch.Draw(pliers, drawPos, rect, armColor, Rotation, origin, 1f, SpriteEffects.None, 0f);
            spriteBatch.Draw(pliersGlow, drawPos, rect, Color.White * alpha, Rotation, origin, 1f, SpriteEffects.None, 0f);

            return false;
        }

        /// <summary>沿 start→end 拉伸绘制一段机械臂纹理（纹理默认朝上 -Y，origin 取顶部中心）</summary>
        private static void DrawArmSegment(SpriteBatch sb, Texture2D tex, Texture2D glow, Vector2 start, Vector2 end, Color color, float alpha) {
            Vector2 diff = end - start;
            float len = diff.Length();
            if (len < 1f) {
                return;
            }
            float rot = diff.ToRotation() - MathHelper.PiOver2;
            Vector2 origin = new Vector2(tex.Width * 0.5f, 0f);
            float scaleY = len / tex.Height;
            Vector2 drawStart = start - Main.screenPosition;
            sb.Draw(tex, drawStart, null, color, rot, origin, new Vector2(1f, scaleY), SpriteEffects.None, 0f);
            sb.Draw(glow, drawStart, null, Color.White * alpha, rot, origin, new Vector2(1f, scaleY), SpriteEffects.None, 0f);
        }
    }
}
