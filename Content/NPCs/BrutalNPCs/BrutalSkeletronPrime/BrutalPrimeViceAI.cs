using CalamityMod.Events;
using CalamityMod.NPCs;
using CalamityMod.World;
using CalamityOverhaul.Content.NPCs.Core;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime
{
    internal class BrutalPrimeViceAI : NPCOverride
    {
        public override int TargetID => NPCID.PrimeVice;

        private bool bossRush;
        private bool masterMode;
        private bool death;
        private bool cannonAlive;
        private bool sawAlive;
        private bool laserAlive;
        private NPC head;
        private Player player;
        private int frame;

        public override bool CanLoad() => true;

        // 计算加速度的函数
        private float CalculateAcceleration(bool bossRush, bool death, bool masterMode, bool cannonAlive, bool laserAlive, bool sawAlive) {
            float baseAcceleration = bossRush ? 0.6f : death ? (masterMode ? 0.375f : 0.3f) : (masterMode ? 0.3125f : 0.25f);
            if (!cannonAlive) baseAcceleration += 0.025f;
            if (!laserAlive) baseAcceleration += 0.025f;
            if (!sawAlive) baseAcceleration += 0.025f;
            return baseAcceleration;
        }

        // 计算加速度倍率的函数
        private float CalculateAccelerationMult(bool cannonAlive, bool laserAlive) {
            float mult = 1f;
            if (!cannonAlive) mult += 0.5f;
            if (!laserAlive) mult += 0.5f;
            return mult;
        }

        // 调整Y轴速度的函数
        private void AdjustVelocityY(NPC npc, float topVelocity, float deceleration, float acceleration, float upperBound, float lowerBound) {
            if (npc.position.Y > upperBound) {
                if (npc.velocity.Y > 0f)
                    npc.velocity.Y *= deceleration;
                npc.velocity.Y -= acceleration;
                if (npc.velocity.Y > topVelocity)
                    npc.velocity.Y = topVelocity;
            }
            else if (npc.position.Y < lowerBound) {
                if (npc.velocity.Y < 0f)
                    npc.velocity.Y *= deceleration;
                npc.velocity.Y += acceleration;
                if (npc.velocity.Y < -topVelocity)
                    npc.velocity.Y = -topVelocity;
            }
        }

        // 调整X轴速度的函数
        private void AdjustVelocityX(NPC npc, float topVelocity, float deceleration, float acceleration, float upperBound, float lowerBound, float factor = 1f) {
            if (npc.Center.X > upperBound) {
                if (npc.velocity.X > 0f)
                    npc.velocity.X *= deceleration;
                npc.velocity.X -= acceleration * factor;
                if (npc.velocity.X > topVelocity)
                    npc.velocity.X = topVelocity;
            }
            if (npc.Center.X < lowerBound) {
                if (npc.velocity.X < 0f)
                    npc.velocity.X *= deceleration;
                npc.velocity.X += acceleration * factor;
                if (npc.velocity.X < -topVelocity)
                    npc.velocity.X = -topVelocity;
            }
        }

        // 处理充能阶段的函数
        private void HandleChargePhase(NPC npc, bool masterMode, bool cannonAlive, bool laserAlive, bool sawAlive) {
            float deceleration = masterMode ? 0.75f : 0.8f;
            if (death) {
                deceleration = 0.5f;
            }
            if (npc.velocity.Y > 0f) {
                npc.velocity.Y *= deceleration;
            }

            Vector2 viceArmChargePosition = npc.Center;
            float viceArmChargeTargetX = Main.npc[(int)npc.ai[1]].Center.X - 280f * npc.ai[0] - viceArmChargePosition.X;
            float viceArmChargeTargetY = Main.npc[(int)npc.ai[1]].position.Y + 230f - viceArmChargePosition.Y;
            npc.rotation = (float)Math.Atan2(viceArmChargeTargetY, viceArmChargeTargetX) + MathHelper.PiOver2;

            npc.velocity.X = (npc.velocity.X * 5f + Main.npc[(int)npc.ai[1]].velocity.X) / 6f;
            npc.velocity.X += 0.5f;
            npc.velocity.Y -= 0.5f;
            if (npc.velocity.Y < -12f) npc.velocity.Y = -12f;

            if (npc.position.Y < Main.npc[(int)npc.ai[1]].position.Y - 280f) {
                npc.damage = npc.defDamage;

                float chargeVelocity = CalculateChargeVelocity(bossRush, cannonAlive, laserAlive, sawAlive, 20f, 16f);
                npc.ai[2] = 2f;
                npc.TargetClosest();
                viceArmChargePosition = npc.Center;
                viceArmChargeTargetX = player.Center.X - viceArmChargePosition.X;
                viceArmChargeTargetY = player.Center.Y - viceArmChargePosition.Y;
                float viceArmChargeTargetDist = (float)Math.Sqrt(viceArmChargeTargetX * viceArmChargeTargetX + viceArmChargeTargetY * viceArmChargeTargetY);
                viceArmChargeTargetDist = chargeVelocity / viceArmChargeTargetDist;
                npc.velocity.X = viceArmChargeTargetX * viceArmChargeTargetDist;
                npc.velocity.Y = viceArmChargeTargetY * viceArmChargeTargetDist;
                npc.netUpdate = true;
            }
        }

        // 处理不同类型充能的函数
        private void HandleDifferentCharge(NPC npc, bool bossRush, bool cannonAlive, bool laserAlive, bool sawAlive) {
            Vector2 viceArmOtherChargePosition = npc.Center;
            float viceArmOtherChargeTargetX = Main.npc[(int)npc.ai[1]].Center.X - 200f * npc.ai[0] - viceArmOtherChargePosition.X;
            float viceArmOtherChargeTargetY = Main.npc[(int)npc.ai[1]].position.Y + 230f - viceArmOtherChargePosition.Y;
            npc.rotation = (float)Math.Atan2(viceArmOtherChargeTargetY, viceArmOtherChargeTargetX) + MathHelper.PiOver2;

            npc.velocity.Y = (npc.velocity.Y * 5f + Main.npc[(int)npc.ai[1]].velocity.Y) / 6f;
            npc.velocity.X += 0.5f;
            if (npc.velocity.X > 12f) npc.velocity.X = 12f;

            if (npc.Center.X < Main.npc[(int)npc.ai[1]].Center.X - 500f || npc.Center.X > Main.npc[(int)npc.ai[1]].Center.X + 500f) {
                npc.damage = npc.defDamage;

                float chargeVelocity = CalculateChargeVelocity(bossRush, cannonAlive, laserAlive, sawAlive, 17.5f, 14f);
                npc.ai[2] = 5f;
                npc.TargetClosest();
                viceArmOtherChargePosition = npc.Center;
                viceArmOtherChargeTargetX = player.Center.X - viceArmOtherChargePosition.X;
                viceArmOtherChargeTargetY = player.Center.Y - viceArmOtherChargePosition.Y;
                float viceArmOtherChargeTargetDist = (float)Math.Sqrt(viceArmOtherChargeTargetX * viceArmOtherChargeTargetX + viceArmOtherChargeTargetY * viceArmOtherChargeTargetY);
                viceArmOtherChargeTargetDist = chargeVelocity / viceArmOtherChargeTargetDist;
                npc.velocity.X = viceArmOtherChargeTargetX * viceArmOtherChargeTargetDist;
                npc.velocity.Y = viceArmOtherChargeTargetY * viceArmOtherChargeTargetDist;
                npc.netUpdate = true;
            }
        }

        // 计算充能速度的函数
        private float CalculateChargeVelocity(bool bossRush, bool cannonAlive, bool laserAlive, bool sawAlive, float bossRushVelocity, float defaultVelocity) {
            float chargeVelocity = bossRush ? bossRushVelocity : defaultVelocity;
            if (!cannonAlive)
                chargeVelocity += 1.5f;
            if (!laserAlive)
                chargeVelocity += 1.5f;
            if (!sawAlive)
                chargeVelocity += 1.5f;
            return chargeVelocity;
        }

        // 计算充能次数的函数
        private float CalculateChargeAmt(bool cannonAlive, bool laserAlive, bool sawAlive, float baseAmt) {
            if (!cannonAlive)
                baseAmt += 1f;
            if (!laserAlive)
                baseAmt += 1f;
            if (!sawAlive)
                baseAmt += 1f;
            return baseAmt;
        }

        // 计算充能速率的函数
        private float CalculateChargeRate(bool cannonAlive, bool laserAlive, bool sawAlive, float baseRate) {
            float chargeRate = baseRate;
            if (!cannonAlive)
                chargeRate += 1f;
            if (!laserAlive)
                chargeRate += 1f;
            if (!sawAlive)
                chargeRate += 1f;
            return chargeRate;
        }

        public override bool? CheckDead() => true;

        public override bool AI() {
            bossRush = BossRushEvent.BossRushActive;
            masterMode = Main.masterMode || bossRush;
            death = CalamityWorld.death || bossRush;
            head = Main.npc[(int)npc.ai[1]];
            player = Main.player[npc.target];
            npc.spriteDirection = -(int)npc.ai[0];
            npc.damage = 0;
            CalamityGlobalNPC.primeVice = npc.whoAmI;
            BrutalSkeletronPrimeAI.FindPlayer(npc);
            BrutalSkeletronPrimeAI.CheakDead(npc, head);
            BrutalSkeletronPrimeAI.CheakRam(out cannonAlive, out _, out sawAlive, out laserAlive);
            npc.aiStyle = -1;
            npc.dontTakeDamage = false;
            if (BrutalSkeletronPrimeAI.SetArmRot(npc, head, 1)) {
                return false;
            }

            Vector2 viceArmPosition = npc.Center;
            float viceArmIdleXPos = head.Center.X - 200f * npc.ai[0] - viceArmPosition.X;
            float viceArmIdleYPos = head.position.Y + 230f - viceArmPosition.Y;
            float viceArmIdleDistance = MathF.Sqrt(viceArmIdleXPos * viceArmIdleXPos + viceArmIdleYPos * viceArmIdleYPos);
            if (npc.ai[2] != 99f) {
                if (viceArmIdleDistance > 800f)
                    npc.ai[2] = 99f;
            }
            else if (viceArmIdleDistance < 400f) {
                npc.ai[2] = 0f;
            }

            if (npc.ai[2] == 99f) {
                // 计算加速度
                float acceleration = CalculateAcceleration(bossRush, death, masterMode, cannonAlive, laserAlive, sawAlive);
                float accelerationMult = CalculateAccelerationMult(cannonAlive, laserAlive);
                if (masterMode) acceleration *= accelerationMult;

                float topVelocity = acceleration * 100f;
                float deceleration = masterMode ? 0.6f : 0.8f;

                // 调整Y轴速度
                AdjustVelocityY(npc, topVelocity, deceleration, acceleration, head.position.Y + 20f, head.position.Y - 20f);

                // 调整X轴速度
                AdjustVelocityX(npc, topVelocity, deceleration, acceleration, head.Center.X + 20f, head.Center.X - 20f, 2f);
            }
            else {
                // 保持在头部附近
                if (npc.ai[2] == 0f || npc.ai[2] == 3f) {
                    if (head.ai[1] == 3f && npc.timeLeft > 10)
                        npc.timeLeft = 10;

                    // 每死亡一个部件，加速充能
                    npc.ai[3] += CalculateChargeRate(cannonAlive, laserAlive, sawAlive, masterMode ? 150f : 600f);
                    if (npc.ai[3] >= (masterMode ? 150f : 600f)) {
                        npc.ai[2] += 1f;
                        npc.ai[3] = 0f;
                        npc.TargetClosest();
                        npc.netUpdate = true;
                    }

                    // 计算加速度
                    float acceleration = CalculateAcceleration(bossRush, death, masterMode, cannonAlive, laserAlive, sawAlive);
                    float accelerationMult = CalculateAccelerationMult(cannonAlive, laserAlive);
                    if (masterMode) acceleration *= accelerationMult;

                    float topVelocity = acceleration * 100f;
                    float deceleration = masterMode ? 0.6f : 0.8f;

                    // 调整Y轴速度
                    AdjustVelocityY(npc, topVelocity, deceleration, acceleration, head.position.Y + 290f, head.position.Y + 240f);

                    // 调整X轴速度
                    AdjustVelocityX(npc, topVelocity, deceleration, acceleration, head.Center.X + 150f, head.Center.X + 100f);

                    // 计算旋转角度
                    Vector2 viceArmReelbackCurrentPos = npc.Center;
                    float viceArmReelbackXDest = head.Center.X - 200f * npc.ai[0] - viceArmReelbackCurrentPos.X;
                    float viceArmReelbackYDest = head.position.Y + 230f - viceArmReelbackCurrentPos.Y;
                    npc.rotation = (float)Math.Atan2(viceArmReelbackYDest, viceArmReelbackXDest) + MathHelper.PiOver2;
                    return false;
                }

                // 向玩家冲锋
                if (npc.ai[2] == 1f) {
                    HandleChargePhase(npc, masterMode, cannonAlive, laserAlive, sawAlive);
                }

                // 冲锋次数根据部件死亡情况调整
                else if (npc.ai[2] == 2f) {
                    npc.damage = npc.defDamage;
                    if (npc.position.Y > player.position.Y || npc.velocity.Y < 0f) {
                        float chargeAmt = CalculateChargeAmt(cannonAlive, laserAlive, sawAlive, 4f);
                        if (npc.ai[3] >= chargeAmt) {
                            npc.ai[2] = 3f;
                            npc.ai[3] = 0f;
                            npc.TargetClosest();
                            return false;
                        }
                        npc.ai[2] = 1f;
                        npc.ai[3] += 1f;
                    }
                }

                // 不同类型的冲锋
                else if (npc.ai[2] == 4f) {
                    HandleDifferentCharge(npc, bossRush, cannonAlive, laserAlive, sawAlive);
                }

                // 冲锋次数根据部件死亡情况调整
                else if (npc.ai[2] == 5f && npc.Center.X < player.Center.X - 100f) {
                    npc.damage = npc.defDamage;
                    float chargeAmt = CalculateChargeAmt(cannonAlive, laserAlive, sawAlive, 4f);
                    if (death) {
                        npc.ai[2] = 4f;
                        npc.ai[3] += 1f;
                        return false;
                    }
                    if (npc.ai[3] >= chargeAmt) {
                        npc.ai[2] = 0f;
                        npc.ai[3] = 0f;
                        npc.TargetClosest();
                        return false;
                    }
                    npc.ai[2] = 4f;
                    npc.ai[3] += 1f;
                }
            }

            if (Main.GameUpdateCount % 10 == 0) {
                if (++frame > 1) {
                    frame = 0;
                }
            }

            return false;
        }

        public override bool? Draw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (!BrutalSkeletronPrimeAI.canLoaderAssetZunkenUp) {
                return false;
            }

            BrutalSkeletronPrimeAI.DrawArm(spriteBatch, npc, screenPos);
            Texture2D mainValue = BrutalSkeletronPrimeAI.BSPPliers.Value;
            Texture2D mainValue2 = BrutalSkeletronPrimeAI.BSPPliersGlow.Value;
            Main.EntitySpriteDraw(mainValue, npc.Center - Main.screenPosition, CWRUtils.GetRec(mainValue, frame, 2)
                , drawColor, npc.rotation, CWRUtils.GetOrig(mainValue, 2), npc.scale, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(mainValue2, npc.Center - Main.screenPosition, CWRUtils.GetRec(mainValue, frame, 2)
                , Color.White, npc.rotation, CWRUtils.GetOrig(mainValue, 2), npc.scale, SpriteEffects.None, 0);
            return false;
        }

        public override bool PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) => false;
    }
}
