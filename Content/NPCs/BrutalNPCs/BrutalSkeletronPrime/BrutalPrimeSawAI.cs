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
    internal class BrutalPrimeSawAI : NPCOverride
    {
        public override int TargetID => NPCID.PrimeSaw;

        private bool bossRush;
        private bool masterMode;
        private bool death;
        private bool cannonAlive;
        private bool viceAlive;
        private bool laserAlive;
        private NPC head;
        private Player player;
        private int frame;

        public override bool CanLoad() => true;
        public override bool? CheckDead() => true;

        // 计算加速度的函数
        private float CalculateAcceleration(bool bossRush, bool death, bool masterMode, bool cannonAlive, bool laserAlive, bool viceAlive) {
            float acceleration = bossRush ? 0.6f : (death ? (masterMode ? 0.375f : 0.3f) : (masterMode ? 0.3125f : 0.25f));
            if (!cannonAlive) acceleration += 0.025f;
            if (!laserAlive) acceleration += 0.025f;
            if (!viceAlive) acceleration += 0.025f;
            return acceleration;
        }

        // 调整Y轴速度的函数
        private void AdjustVelocityY(NPC npc, NPC head, float acceleration, float topVelocity, float deceleration) {
            if (npc.position.Y > head.position.Y + 20f) {
                if (npc.velocity.Y > 0f) npc.velocity.Y *= deceleration;
                npc.velocity.Y -= acceleration;
                if (npc.velocity.Y > topVelocity) npc.velocity.Y = topVelocity;
            }
            else if (npc.position.Y < head.position.Y - 20f) {
                if (npc.velocity.Y < 0f) npc.velocity.Y *= deceleration;
                npc.velocity.Y += acceleration;
                if (npc.velocity.Y < -topVelocity) npc.velocity.Y = -topVelocity;
            }
        }

        // 调整X轴速度的函数
        private void AdjustVelocityX(NPC npc, NPC head, float acceleration, float topVelocity, float deceleration) {
            if (npc.Center.X > head.Center.X + 20f) {
                if (npc.velocity.X > 0f) npc.velocity.X *= deceleration;
                npc.velocity.X -= acceleration * 2f;
                if (npc.velocity.X > topVelocity) npc.velocity.X = topVelocity;
            }
            else if (npc.Center.X < head.Center.X - 20f) {
                if (npc.velocity.X < 0f) npc.velocity.X *= deceleration;
                npc.velocity.X += acceleration * 2f;
                if (npc.velocity.X < -topVelocity) npc.velocity.X = -topVelocity;
            }
        }

        // 处理初始阶段的函数
        private void HandleInitialPhase(NPC npc, NPC head, bool cannonAlive, bool laserAlive, bool viceAlive, bool masterMode, bool bossRush) {
            if (head.ai[1] == 3f && npc.timeLeft > 10) {
                npc.timeLeft = 10;
            }

            // 开始充能
            npc.ai[3] += 1f;
            if (!cannonAlive) npc.ai[3] += 1f;
            if (!laserAlive) npc.ai[3] += 1f;
            if (!viceAlive) npc.ai[3] += 1f;

            float cooldingnum = masterMode ? 90f : 180f;
            if (death) {
                cooldingnum = 30;
            }
            if (npc.ai[3] >= cooldingnum) {
                npc.ai[2] += 1f;
                npc.ai[3] = 0f;
                npc.TargetClosest();
                npc.netUpdate = true;
            }

            float acceleration = CalculateAcceleration(bossRush, death, masterMode, cannonAlive, laserAlive, viceAlive);
            float topVelocity = acceleration * 100f;
            float deceleration = masterMode ? 0.6f : 0.8f;

            if (npc.position.Y > head.position.Y + 310f) {
                if (npc.velocity.Y > 0f) npc.velocity.Y *= deceleration;
                npc.velocity.Y -= acceleration;
                if (npc.velocity.Y > topVelocity) npc.velocity.Y = topVelocity;
            }
            else if (npc.position.Y < head.position.Y + 270f) {
                if (npc.velocity.Y < 0f) npc.velocity.Y *= deceleration;
                npc.velocity.Y += acceleration;
                if (npc.velocity.Y < -topVelocity) npc.velocity.Y = -topVelocity;
            }

            if (npc.Center.X > head.Center.X - 100f) {
                if (npc.velocity.X > 0f) npc.velocity.X *= deceleration;
                npc.velocity.X -= acceleration * 1.5f;
                if (npc.velocity.X > topVelocity) npc.velocity.X = topVelocity;
            }
            if (npc.Center.X < head.Center.X - 150f) {
                if (npc.velocity.X < 0f) npc.velocity.X *= deceleration;
                npc.velocity.X += acceleration * 1.5f;
                if (npc.velocity.X < -topVelocity) npc.velocity.X = -topVelocity;
            }

            Vector2 sawArmReelbackCurrentPos = npc.Center;
            float sawArmReelbackXDest = head.Center.X - 200f * npc.ai[0] - sawArmReelbackCurrentPos.X;
            float sawArmReelbackYDest = head.position.Y + 230f - sawArmReelbackCurrentPos.Y;
            npc.rotation = (float)Math.Atan2(sawArmReelbackYDest, sawArmReelbackXDest) + MathHelper.PiOver2;
        }

        // 处理冲锋阶段的函数
        private void HandleChargePhase(NPC npc, NPC head, Player player, bool bossRush, bool cannonAlive, bool laserAlive, bool viceAlive) {
            Vector2 sawArmChargePos = npc.Center;
            float sawArmChargeTargetX = head.Center.X - 200f * npc.ai[0] - sawArmChargePos.X;
            float sawArmChargeTargetY = head.position.Y + 230f - sawArmChargePos.Y;
            npc.rotation = (float)Math.Atan2(sawArmChargeTargetY, sawArmChargeTargetX) + MathHelper.PiOver2;

            float deceleration = masterMode ? 0.875f : 0.9f;
            npc.velocity.X *= deceleration;
            npc.velocity.Y -= 0.5f;
            if (npc.velocity.Y < -12f) npc.velocity.Y = -12f;

            if (npc.position.Y < head.position.Y - 200f) {
                npc.damage = npc.defDamage;

                float chargeVelocity = bossRush ? 27.5f : 22f;
                if (!cannonAlive) chargeVelocity += 1.5f;
                if (!laserAlive) chargeVelocity += 1.5f;
                if (!viceAlive) chargeVelocity += 1.5f;

                npc.ai[2] = 2f;
                npc.TargetClosest();
                sawArmChargePos = npc.Center;
                sawArmChargeTargetX = player.Center.X - sawArmChargePos.X;
                sawArmChargeTargetY = player.Center.Y - sawArmChargePos.Y;
                float sawArmChargeTargetDist = (float)Math.Sqrt(sawArmChargeTargetX * sawArmChargeTargetX + sawArmChargeTargetY * sawArmChargeTargetY);
                sawArmChargeTargetDist = chargeVelocity / sawArmChargeTargetDist;
                npc.velocity.X = sawArmChargeTargetX * sawArmChargeTargetDist;
                npc.velocity.Y = sawArmChargeTargetY * sawArmChargeTargetDist;
                npc.netUpdate = true;
            }
        }

        // 处理其他冲锋阶段的函数
        private void HandleOtherChargePhase(NPC npc, NPC head, Player player, bool bossRush, bool cannonAlive, bool laserAlive, bool viceAlive, bool masterMode) {
            npc.damage = npc.defDamage;

            float chargeVelocity = bossRush ? 13.5f : 11f;
            if (!cannonAlive) chargeVelocity += 1.5f;
            if (!laserAlive) chargeVelocity += 1.5f;
            if (!viceAlive) chargeVelocity += 1.5f;
            if (masterMode) chargeVelocity *= 1.25f;

            Vector2 sawArmOtherChargePos = npc.Center;
            float sawArmOtherChargeTargetX = player.Center.X - sawArmOtherChargePos.X;
            float sawArmOtherChargeTargetY = player.Center.Y - sawArmOtherChargePos.Y;
            float sawArmOtherChargeTargetDist = (float)Math.Sqrt(sawArmOtherChargeTargetX * sawArmOtherChargeTargetX + sawArmOtherChargeTargetY * sawArmOtherChargeTargetY);
            sawArmOtherChargeTargetDist = chargeVelocity / sawArmOtherChargeTargetDist;
            sawArmOtherChargeTargetX *= sawArmOtherChargeTargetDist;
            sawArmOtherChargeTargetY *= sawArmOtherChargeTargetDist;

            float acceleration = bossRush ? 0.3f : (death ? 0.1f : 0.08f);
            if (masterMode) acceleration *= 1.25f;

            float deceleration = masterMode ? 0.6f : 0.8f;

            AdjustChargeVelocity(ref npc.velocity.X, sawArmOtherChargeTargetX, acceleration, deceleration);
            AdjustChargeVelocity(ref npc.velocity.Y, sawArmOtherChargeTargetY, acceleration, deceleration);

            npc.ai[3] += 1f;
            if (npc.justHit) npc.ai[3] += 2f;

            if (npc.ai[3] >= 600f) {
                npc.ai[2] = 0f;
                npc.ai[3] = 0f;
                npc.TargetClosest();
                npc.netUpdate = true;
            }

            sawArmOtherChargePos = npc.Center;
            sawArmOtherChargeTargetX = head.Center.X - 200f * npc.ai[0] - sawArmOtherChargePos.X;
            sawArmOtherChargeTargetY = head.position.Y + 230f - sawArmOtherChargePos.Y;
            npc.rotation = (float)Math.Atan2(sawArmOtherChargeTargetY, sawArmOtherChargeTargetX) + MathHelper.PiOver2;
        }

        // 调整冲锋速度的辅助函数
        private void AdjustChargeVelocity(ref float currentVelocity, float targetVelocity, float acceleration, float deceleration) {
            if (currentVelocity > targetVelocity) {
                if (currentVelocity > 0f) currentVelocity *= deceleration;
                currentVelocity -= acceleration;
            }
            else if (currentVelocity < targetVelocity) {
                if (currentVelocity < 0f) currentVelocity *= deceleration;
                currentVelocity += acceleration;
            }
        }

        public override bool AI() {
            bossRush = BossRushEvent.BossRushActive;
            masterMode = Main.masterMode || bossRush;
            death = CalamityWorld.death || bossRush;
            head = Main.npc[(int)npc.ai[1]];
            player = Main.player[npc.target];
            npc.spriteDirection = -(int)npc.ai[0];
            npc.damage = (int)Math.Round(npc.defDamage * 0.5);
            CalamityGlobalNPC.primeSaw = npc.whoAmI;
            BrutalSkeletronPrimeAI.FindPlayer(npc);
            BrutalSkeletronPrimeAI.CheakDead(npc, head);
            BrutalSkeletronPrimeAI.CheakRam(out cannonAlive, out viceAlive, out _, out laserAlive);
            npc.aiStyle = -1;
            npc.dontTakeDamage = false;
            if (BrutalSkeletronPrimeAI.SetArmRot(npc, head, 3)) {
                return false;
            }

            Vector2 sawArmLocation = npc.Center;
            float sawArmIdleXPos = head.Center.X - 200f * npc.ai[0] - sawArmLocation.X;
            float sawArmIdleYPos = head.position.Y + 230f - sawArmLocation.Y;
            float sawArmIdleDistance = (float)Math.Sqrt(sawArmIdleXPos * sawArmIdleXPos + sawArmIdleYPos * sawArmIdleYPos);

            if (npc.ai[2] != 99f) {
                if (sawArmIdleDistance > 800f)
                    npc.ai[2] = 99f;
            }
            else if (sawArmIdleDistance < 400f) {
                npc.ai[2] = 0f;
            }

            if (npc.ai[2] == 99f) {
                // 根据游戏模式和NPC状态计算加速度
                float acceleration = CalculateAcceleration(bossRush, death, masterMode, cannonAlive, laserAlive, viceAlive);
                float accelerationMult = 1f;

                // 如果炮台、激光或副手不存活，增加加速度和加速度倍率
                if (!cannonAlive) {
                    acceleration += 0.025f;
                    accelerationMult += 0.5f;
                }
                if (!laserAlive) {
                    acceleration += 0.025f;
                    accelerationMult += 0.5f;
                }
                if (!viceAlive) {
                    acceleration += 0.025f;
                }

                // 在大师模式中，乘以加速度倍率
                if (masterMode) {
                    acceleration *= accelerationMult;
                }

                // 计算最高速度和减速率
                float topVelocity = acceleration * 100f;
                float deceleration = masterMode ? 0.6f : 0.8f;
                // 根据NPC和头部的位置调整Y轴速度
                AdjustVelocityY(npc, head, acceleration, topVelocity, deceleration);
                // 根据NPC和头部的位置调整X轴速度
                AdjustVelocityX(npc, head, acceleration, topVelocity, deceleration);
            }
            else {
                // 如果NPC处于特定AI状态
                if (npc.ai[2] == 0f || npc.ai[2] == 3f) {
                    HandleInitialPhase(npc, head, cannonAlive, laserAlive, viceAlive, masterMode, bossRush);
                }
                else if (npc.ai[2] == 1f) {
                    HandleChargePhase(npc, head, player, bossRush, cannonAlive, laserAlive, viceAlive);
                }
                else if (npc.ai[2] == 2f) {
                    // 确定NPC的伤害
                    npc.damage = npc.defDamage;
                    // 调整NPC状态
                    if (npc.position.Y > player.position.Y || npc.velocity.Y < 0f) {
                        npc.ai[2] = 3f;
                    }
                }
                else if (npc.ai[2] == 4f) {
                    HandleOtherChargePhase(npc, head, player, bossRush, cannonAlive, laserAlive, viceAlive, masterMode);
                }
                else if (npc.ai[2] == 5f) {
                    // 调整NPC状态
                    if ((npc.velocity.X > 0f && npc.Center.X > player.Center.X) || (npc.velocity.X < 0f && npc.Center.X < player.Center.X)) {
                        npc.ai[2] = 0f;
                    }
                }
            }

            if (Main.GameUpdateCount % 5 == 0) {
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
            Texture2D mainValue = BrutalSkeletronPrimeAI.BSPSAW.Value;
            Texture2D mainValue2 = BrutalSkeletronPrimeAI.BSPSAWGlow.Value;
            Main.EntitySpriteDraw(mainValue, npc.Center - Main.screenPosition, CWRUtils.GetRec(mainValue, frame, 2)
                , drawColor, npc.rotation, CWRUtils.GetOrig(mainValue, 2), npc.scale, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(mainValue2, npc.Center - Main.screenPosition, CWRUtils.GetRec(mainValue, frame, 2)
                , Color.White, npc.rotation, CWRUtils.GetOrig(mainValue, 2), npc.scale, SpriteEffects.None, 0);
            return false;
        }
        public override bool PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) => false;
    }
}
