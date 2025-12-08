using System;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime
{
    /// <summary>
    /// 包含AI的移动和行为逻辑
    /// </summary>
    internal partial class HeadPrimeAI
    {
        private void MoveToPoint(Vector2 point) {
            npc.ChasingBehavior(point, 20);
        }

        private void AdjustVerticalMovement(float acceleration, float maxSpeed, float deceleration, int offset, int threshold) {
            if (npc.position.Y > Main.player[npc.target].position.Y - offset) {
                if (npc.velocity.Y > 0f) {
                    npc.velocity.Y *= deceleration;
                }
                npc.velocity.Y -= acceleration;
                if (npc.velocity.Y > maxSpeed) {
                    npc.velocity.Y = maxSpeed;
                }
            }
            else if (npc.position.Y < Main.player[npc.target].position.Y - threshold) {
                if (npc.velocity.Y < 0f) {
                    npc.velocity.Y *= deceleration;
                }
                npc.velocity.Y += acceleration;
                if (npc.velocity.Y < -maxSpeed) {
                    npc.velocity.Y = -maxSpeed;
                }
            }
        }

        private void AdjustHorizontalMovement(float acceleration, float maxSpeed, float deceleration, float offset) {
            if (npc.Center.X > Main.player[npc.target].Center.X + 100f + offset) {
                if (npc.velocity.X > 0f) {
                    npc.velocity.X *= deceleration;
                }
                npc.velocity.X -= acceleration;
                if (npc.velocity.X > maxSpeed) {
                    npc.velocity.X = maxSpeed;
                }
            }

            if (npc.Center.X < Main.player[npc.target].Center.X - 100f + offset) {
                if (npc.velocity.X < 0f) {
                    npc.velocity.X *= deceleration;
                }
                npc.velocity.X += acceleration;
                if (npc.velocity.X < -maxSpeed) {
                    npc.velocity.X = -maxSpeed;
                }
            }
        }

        private void UpdateRotation() {
            if (NPC.IsMechQueenUp || ai3 == 3) {
                npc.rotation = npc.rotation.AngleLerp(npc.velocity.X / 15f * 0.5f, 0.75f);
            }
            else {
                npc.rotation += npc.direction * 0.3f;
            }
        }

        private float CalculateSpeedMultiplier(float distance, float initialSpeed) {
            if (Main.expertMode) {
                float speed = Main.masterMode ? 5.5f : 4f;
                float speedFactor = Main.masterMode ? 1.125f : 1.1f;
                if (distance > 150f) speed *= Main.masterMode ? 1.05f : 1.025f;
                for (int threshold = 200; threshold <= 600; threshold += 50) {
                    if (distance > threshold) speed *= speedFactor;
                }
                return speed;
            }
            return initialSpeed;
        }

        private void UpdateVelocity(Vector2 targetVector, float speedMultiplier, float distance) {
            float adjustedSpeed = speedMultiplier / distance;
            if (death) {
                if (--calNPC.newAI[2] <= 0) {
                    npc.velocity.X = targetVector.X * adjustedSpeed / 2;
                    npc.velocity.Y = targetVector.Y * adjustedSpeed / 2;
                }
                else {
                    npc.velocity *= 0.99f;
                }

                if (death || Main.masterMode) {
                    if (++calNPC.newAI[1] > 90) {
                        Vector2 toD = npc.Center.To(player.Center) + player.velocity;
                        toD = toD.UnitVector();
                        float baseDashSpeed = 20f;
                        if (bossRush) {
                            baseDashSpeed *= 2;
                        }
                        npc.velocity += toD * baseDashSpeed;
                        baseDashSpeed += 6;
                        if (Main.npc[primeCannon].active) {
                            Main.npc[primeCannon].velocity += toD * baseDashSpeed;
                            Main.npc[primeCannon].netUpdate = true;
                        }
                        if (Main.npc[primeSaw].active) {
                            Main.npc[primeSaw].velocity += toD * baseDashSpeed;
                            Main.npc[primeSaw].netUpdate = true;
                        }
                        if (Main.npc[primeLaser].active) {
                            Main.npc[primeLaser].velocity += toD * baseDashSpeed;
                            Main.npc[primeLaser].netUpdate = true;
                        }
                        if (Main.npc[primeVice].active) {
                            Main.npc[primeVice].velocity += toD * baseDashSpeed;
                            Main.npc[primeVice].netUpdate = true;
                        }

                        calNPC.newAI[2] = 60;
                        calNPC.newAI[1] = 0;
                        npc.netUpdate = true;
                    }
                }
            }
            else {
                npc.velocity.X = targetVector.X * adjustedSpeed;
                npc.velocity.Y = targetVector.Y * adjustedSpeed;
            }

            if (NPC.IsMechQueenUp) {
                float distanceToPlayer = Vector2.Distance(npc.Center, Main.player[npc.target].Center);
                if (distanceToPlayer < 0.1f) distanceToPlayer = 0f;
                if (distanceToPlayer < speedMultiplier)
                    npc.velocity = npc.velocity.SafeNormalize(Vector2.Zero) * distanceToPlayer;
            }
        }

        private void EnrageNPC() {
            //增加 NPC 的伤害和防御
            npc.damage = 1000;
            npc.defense = 9999;
            //标记当前正在愤怒状态和增加防御力或伤害减免
            calNPC.CurrentlyEnraged = true;
            calNPC.CurrentlyIncreasingDefenseOrDR = true;
        }

        private void MoveTowardsPlayer(float baseSpeed, float minSpeed, float maxSpeed, float speedDivisor) {
            //计算玩家与 NPC 之间的向量和距离
            Vector2 npcCenter = npc.Center;
            Vector2 playerCenter = Main.player[npc.target].Center;
            Vector2 directionToPlayer = playerCenter - npcCenter;
            float distanceToPlayer = directionToPlayer.Length();
            //计算速度
            float adjustedSpeed = baseSpeed + distanceToPlayer / speedDivisor;
            adjustedSpeed = Math.Clamp(adjustedSpeed, minSpeed, maxSpeed);
            //根据计算出的向量调整速度
            directionToPlayer.Normalize();
            npc.velocity = directionToPlayer * adjustedSpeed;
        }

        private void HandleDespawn() {
            if (NPC.IsMechQueenUp) {
                DespawnNPC(NPCID.Retinazer);
                DespawnNPC(NPCID.Spazmatism);
                //如果 Retinazer 和 Spazmatism 都不在，则变形并消失
                if (!NPC.AnyNPCs(NPCID.Retinazer) && !NPC.AnyNPCs(NPCID.Spazmatism)) {
                    TransformOrDespawnNPC(NPCID.TheDestroyer, NPCID.TheDestroyerTail);
                }
                AdjustVelocity(0.1f, 0.95f, 13f);
            }
            else {
                npc.velocity = Vector2.Zero;
                if (++ai1 >= 60) {
                    SpawnHouengEffect();
                    npc.active = false;
                    return;
                }
                else {
                    int value = npc.lifeMax - npc.life;
                    int addNum = value / 60;
                    npc.life += addNum;
                    if (npc.life > npc.lifeMax) {
                        npc.life = npc.lifeMax;
                    }
                }
            }
        }

        private void DespawnNPC(int npcID) {
            int npcIndex = NPC.FindFirstNPC(npcID);
            if (npcIndex >= 0) {
                Main.npc[npcIndex].EncourageDespawn(5);
            }
        }

        private void TransformOrDespawnNPC(int findNpcID, int transformNpcID) {
            int npcIndex = NPC.FindFirstNPC(findNpcID);
            if (npcIndex >= 0) {
                Main.npc[npcIndex].Transform(transformNpcID);
            }
            npc.EncourageDespawn(5);
        }

        private void AdjustVelocity(float verticalAcceleration, float horizontalDeceleration, float maxVerticalSpeed) {
            npc.velocity.Y += verticalAcceleration;
            if (npc.velocity.Y < 0f) {
                npc.velocity.Y *= horizontalDeceleration;
            }

            npc.velocity.X *= horizontalDeceleration;
            if (npc.velocity.Y > maxVerticalSpeed) {
                npc.velocity.Y = maxVerticalSpeed;
            }
        }
    }
}
