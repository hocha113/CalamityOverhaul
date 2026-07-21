using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.States;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.Actors;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime
{
    /// <summary>死亡钳Actor，解耦PrimeVice</summary>
    internal class PrimeDeathClawActor : Actor
    {
        private int headWhoAmI = -1;
        private int side = 1;
        private int clawFrame;        //0=张开 1=闭合
        private float alpha = 1f;
        private bool finaleBurst;

        /// <summary>本地生成后绑定头与侧</summary>
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
                    float p = EaseOut((t - PrimeDeathState.PhaseFakeDeathEnd) / (float)(PrimeDeathState.PhaseSummonEnd - PrimeDeathState.PhaseFakeDeathEnd));
                    clawPos = Vector2.Lerp(head.Center, standby, p);
                    aimTarget = head.Center + (clawPos - head.Center) * 2f;
                    clawFrame = 0;
                    break;
                }
                case PrimeDeathPhase.Lunge: {
                    //迅猛扑出
                    float p = EaseOut((t - PrimeDeathState.PhaseSummonEnd) / (float)(PrimeDeathState.PhaseLungeEnd - PrimeDeathState.PhaseSummonEnd));
                    clawPos = Vector2.Lerp(standby, grabPoint, p);
                    aimTarget = targetCenter;
                    clawFrame = p > 0.55f ? 1 : 0;
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
                    float p = (t - PrimeDeathState.PhaseRoarEnd) / (float)(PrimeDeathState.PhaseFinaleEnd - PrimeDeathState.PhaseRoarEnd);
                    Vector2 outward = new Vector2(side, -0.35f).SafeNormalize(Vector2.UnitX * side);
                    clawPos = grabPoint + outward * p * 280f;
                    aimTarget = clawPos + outward;
                    clawFrame = 1;
                    alpha = MathHelper.Clamp(1f - p * 1.2f, 0f, 1f);
                    if (!finaleBurst && t == PrimeDeathState.PhaseRoarEnd) {
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

            Vector2 claw = Center;
            Color armColor = Lighting.GetColor((int)(claw.X / 16f), (int)(claw.Y / 16f)) * alpha;

            DrawNativeStyleArm(spriteBatch, head, armColor);

            //钳2帧开合
            Texture2D pliers = HeadPrimeAI.BSPPliers.Value;
            Texture2D pliersGlow = HeadPrimeAI.BSPPliersGlow.Value;
            Rectangle rect = pliers.GetRectangle(clawFrame, 2);
            Vector2 origin = VaultUtils.GetOrig(pliers, 2);
            Vector2 drawPos = claw - Main.screenPosition;
            spriteBatch.Draw(pliers, drawPos, rect, armColor, Rotation, origin, 1f, SpriteEffects.None, 0f);
            spriteBatch.Draw(pliersGlow, drawPos, rect, Color.White * alpha, Rotation, origin, 1f, SpriteEffects.None, 0f);

            return false;
        }

        /// <summary>两段关节绘制，禁纵向拉伸</summary>
        private void DrawNativeStyleArm(SpriteBatch spriteBatch, NPC head, Color armColor) {
            Vector2 joint = new Vector2(Position.X + Width * 0.5f - 5f * side, Position.Y + 20f);
            Vector2 drawOrigin = new Vector2(TextureAssets.BoneArm.Width() * 0.5f, TextureAssets.BoneArm.Height() * 0.5f);
            Rectangle drawRect = new Rectangle(0, 0, TextureAssets.BoneArm.Width(), TextureAssets.BoneArm.Height());

            for (int k = 0; k < 2; k++) {
                float toHeadX = head.Center.X - joint.X;
                float toHeadY = head.Center.Y - joint.Y;
                float segmentLength;

                if (k == 0) {
                    toHeadX -= 200f * side;
                    toHeadY += 130f;
                    segmentLength = 92f;
                }
                else {
                    toHeadX -= 50f * side;
                    toHeadY += 80f;
                    segmentLength = 60f;
                }

                float distance = MathF.Sqrt(toHeadX * toHeadX + toHeadY * toHeadY);
                if (distance < 1f) {
                    continue;
                }

                float step = segmentLength / distance;
                joint.X += toHeadX * step;
                joint.Y += toHeadY * step;

                float rotation = MathF.Atan2(toHeadY, toHeadX) - MathHelper.PiOver2;
                Texture2D tex = k == 0 ? HeadPrimeAI.BSPRAM_Forearm.Value : HeadPrimeAI.BSPRAM.Value;
                Texture2D glow = k == 0 ? HeadPrimeAI.BSPRAM_ForearmGlow.Value : HeadPrimeAI.BSPRAMGlow.Value;
                SpriteEffects effects = k == 1 ? SpriteEffects.None : SpriteEffects.FlipVertically;
                Vector2 drawPos = joint - Main.screenPosition;

                spriteBatch.Draw(tex, drawPos, drawRect, armColor, rotation, drawOrigin, 1f, effects, 0f);
                spriteBatch.Draw(glow, drawPos, drawRect, Color.White * alpha, rotation, drawOrigin, 1f, effects, 0f);

                if (k == 0) {
                    joint.X += toHeadX * step / 2f;
                    joint.Y += toHeadY * step / 2f;
                }
            }
        }
    }
}
