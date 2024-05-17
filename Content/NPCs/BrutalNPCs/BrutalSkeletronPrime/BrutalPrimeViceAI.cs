using CalamityMod.Events;
using CalamityMod.NPCs;
using CalamityMod.World;
using CalamityOverhaul.Content.NPCs.Core;
using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime
{
    internal class BrutalPrimeViceAI : NPCSet
    {
        public override int targetID => NPCID.PrimeVice;
        public override bool? AI(NPC npc, Mod mod) {
            bool bossRush = BossRushEvent.BossRushActive;
            bool masterMode = Main.masterMode || bossRush;
            bool death = CalamityWorld.death || bossRush;

            // Get a target
            if (npc.target < 0 || npc.target == Main.maxPlayers || Main.player[npc.target].dead || !Main.player[npc.target].active)
                npc.TargetClosest();

            // Despawn safety, make sure to target another player if the current player target is too far away
            if (Vector2.Distance(Main.player[npc.target].Center, npc.Center) > CalamityGlobalNPC.CatchUpDistance200Tiles)
                npc.TargetClosest();

            // Direction
            npc.spriteDirection = -(int)npc.ai[0];

            // Where the vice should be in relation to the head
            Vector2 viceArmPosition = npc.Center;
            float viceArmIdleXPos = Main.npc[(int)npc.ai[1]].Center.X - 200f * npc.ai[0] - viceArmPosition.X;
            float viceArmIdleYPos = Main.npc[(int)npc.ai[1]].position.Y + 230f - viceArmPosition.Y;
            float viceArmIdleDistance = (float)Math.Sqrt(viceArmIdleXPos * viceArmIdleXPos + viceArmIdleYPos * viceArmIdleYPos);

            // Return the vice to its proper location in relation to the head if it's too far away
            if (npc.ai[2] != 99f) {
                if (viceArmIdleDistance > 800f)
                    npc.ai[2] = 99f;
            }
            else if (viceArmIdleDistance < 400f)
                npc.ai[2] = 0f;

            // Despawn if head is gone
            if (!Main.npc[(int)npc.ai[1]].active || Main.npc[(int)npc.ai[1]].aiStyle != NPCAIStyleID.SkeletronPrimeHead) {
                npc.ai[2] += 10f;
                if (npc.ai[2] > 50f || Main.netMode != NetmodeID.Server) {
                    npc.life = -1;
                    npc.HitEffect(0, 10.0);
                    npc.active = false;
                }
            }

            CalamityGlobalNPC.primeVice = npc.whoAmI;

            // Check if arms are alive
            bool cannonAlive = false;
            bool laserAlive = false;
            bool sawAlive = false;
            if (CalamityGlobalNPC.primeCannon != -1) {
                if (Main.npc[CalamityGlobalNPC.primeCannon].active)
                    cannonAlive = true;
            }
            if (CalamityGlobalNPC.primeLaser != -1) {
                if (Main.npc[CalamityGlobalNPC.primeLaser].active)
                    laserAlive = true;
            }
            if (CalamityGlobalNPC.primeSaw != -1) {
                if (Main.npc[CalamityGlobalNPC.primeSaw].active)
                    sawAlive = true;
            }

            // Avoid cheap bullshit
            npc.damage = 0;

            // Return to the head
            if (npc.ai[2] == 99f) {
                float acceleration = (bossRush ? 0.6f : death ? (masterMode ? 0.375f : 0.3f) : (masterMode ? 0.3125f : 0.25f));
                float accelerationMult = 1f;
                if (!cannonAlive) {
                    acceleration += 0.025f;
                    accelerationMult += 0.5f;
                }
                if (!laserAlive) {
                    acceleration += 0.025f;
                    accelerationMult += 0.5f;
                }
                if (!sawAlive)
                    acceleration += 0.025f;
                if (masterMode)
                    acceleration *= accelerationMult;

                float topVelocity = acceleration * 100f;
                float deceleration = masterMode ? 0.6f : 0.8f;

                if (npc.position.Y > Main.npc[(int)npc.ai[1]].position.Y + 20f) {
                    if (npc.velocity.Y > 0f)
                        npc.velocity.Y *= deceleration;

                    npc.velocity.Y -= acceleration;

                    if (npc.velocity.Y > topVelocity)
                        npc.velocity.Y = topVelocity;
                }
                else if (npc.position.Y < Main.npc[(int)npc.ai[1]].position.Y - 20f) {
                    if (npc.velocity.Y < 0f)
                        npc.velocity.Y *= deceleration;

                    npc.velocity.Y += acceleration;

                    if (npc.velocity.Y < -topVelocity)
                        npc.velocity.Y = -topVelocity;
                }

                if (npc.Center.X > Main.npc[(int)npc.ai[1]].Center.X + 20f) {
                    if (npc.velocity.X > 0f)
                        npc.velocity.X *= deceleration;

                    npc.velocity.X -= acceleration * 2f;

                    if (npc.velocity.X > topVelocity)
                        npc.velocity.X = topVelocity;
                }
                if (npc.Center.X < Main.npc[(int)npc.ai[1]].Center.X - 20f) {
                    if (npc.velocity.X < 0f)
                        npc.velocity.X *= deceleration;

                    npc.velocity.X += acceleration * 2f;

                    if (npc.velocity.X < -topVelocity)
                        npc.velocity.X = -topVelocity;
                }
            }

            // Other phases
            else {
                // Stay near the head
                if (npc.ai[2] == 0f || npc.ai[2] == 3f) {
                    // Despawn if head is despawning
                    if (Main.npc[(int)npc.ai[1]].ai[1] == 3f && npc.timeLeft > 10)
                        npc.timeLeft = 10;

                    // Start charging after 10 seconds (change this as each arm dies)
                    npc.ai[3] += 1f;
                    if (!cannonAlive)
                        npc.ai[3] += 1f;
                    if (!laserAlive)
                        npc.ai[3] += 1f;
                    if (!sawAlive)
                        npc.ai[3] += 1f;

                    if (npc.ai[3] >= (masterMode ? 150f : 600f)) {
                        npc.ai[2] += 1f;
                        npc.ai[3] = 0f;
                        npc.TargetClosest();
                        npc.netUpdate = true;
                    }

                    float acceleration = (bossRush ? 0.6f : death ? (masterMode ? 0.375f : 0.3f) : (masterMode ? 0.3125f : 0.25f));
                    float accelerationMult = 1f;
                    if (!cannonAlive) {
                        acceleration += 0.025f;
                        accelerationMult += 0.5f;
                    }
                    if (!laserAlive) {
                        acceleration += 0.025f;
                        accelerationMult += 0.5f;
                    }
                    if (!sawAlive)
                        acceleration += 0.025f;
                    if (masterMode)
                        acceleration *= accelerationMult;

                    float topVelocity = acceleration * 100f;
                    float deceleration = masterMode ? 0.6f : 0.8f;

                    if (npc.position.Y > Main.npc[(int)npc.ai[1]].position.Y + 290f) {
                        if (npc.velocity.Y > 0f)
                            npc.velocity.Y *= deceleration;

                        npc.velocity.Y -= acceleration;

                        if (npc.velocity.Y > topVelocity)
                            npc.velocity.Y = topVelocity;
                    }
                    else if (npc.position.Y < Main.npc[(int)npc.ai[1]].position.Y + 240f) {
                        if (npc.velocity.Y < 0f)
                            npc.velocity.Y *= deceleration;

                        npc.velocity.Y += acceleration;

                        if (npc.velocity.Y < -topVelocity)
                            npc.velocity.Y = -topVelocity;
                    }

                    if (npc.Center.X > Main.npc[(int)npc.ai[1]].Center.X + 150f) {
                        if (npc.velocity.X > 0f)
                            npc.velocity.X *= deceleration;

                        npc.velocity.X -= acceleration;

                        if (npc.velocity.X > topVelocity)
                            npc.velocity.X = topVelocity;
                    }
                    if (npc.Center.X < Main.npc[(int)npc.ai[1]].Center.X + 100f) {
                        if (npc.velocity.X < 0f)
                            npc.velocity.X *= deceleration;

                        npc.velocity.X += acceleration;

                        if (npc.velocity.X < -topVelocity)
                            npc.velocity.X = -topVelocity;
                    }

                    Vector2 viceArmReelbackCurrentPos = npc.Center;
                    float viceArmReelbackXDest = Main.npc[(int)npc.ai[1]].Center.X - 200f * npc.ai[0] - viceArmReelbackCurrentPos.X;
                    float viceArmReelbackYDest = Main.npc[(int)npc.ai[1]].position.Y + 230f - viceArmReelbackCurrentPos.Y;
                    npc.rotation = (float)Math.Atan2(viceArmReelbackYDest, viceArmReelbackXDest) + MathHelper.PiOver2;
                    return false;
                }

                // Charge towards the player
                if (npc.ai[2] == 1f) {
                    float deceleration = masterMode ? 0.75f : 0.8f;
                    if (npc.velocity.Y > 0f)
                        npc.velocity.Y *= deceleration;

                    Vector2 viceArmChargePosition = npc.Center;
                    float viceArmChargeTargetX = Main.npc[(int)npc.ai[1]].Center.X - 280f * npc.ai[0] - viceArmChargePosition.X;
                    float viceArmChargeTargetY = Main.npc[(int)npc.ai[1]].position.Y + 230f - viceArmChargePosition.Y;
                    npc.rotation = (float)Math.Atan2(viceArmChargeTargetY, viceArmChargeTargetX) + MathHelper.PiOver2;

                    npc.velocity.X = (npc.velocity.X * 5f + Main.npc[(int)npc.ai[1]].velocity.X) / 6f;
                    npc.velocity.X += 0.5f;

                    npc.velocity.Y -= 0.5f;
                    if (npc.velocity.Y < -12f)
                        npc.velocity.Y = -12f;

                    if (npc.position.Y < Main.npc[(int)npc.ai[1]].position.Y - 280f) {
                        // Set damage
                        npc.damage = npc.defDamage;

                        float chargeVelocity = bossRush ? 20f : 16f;
                        if (!cannonAlive)
                            chargeVelocity += 1.5f;
                        if (!laserAlive)
                            chargeVelocity += 1.5f;
                        if (!sawAlive)
                            chargeVelocity += 1.5f;

                        npc.ai[2] = 2f;
                        npc.TargetClosest();
                        viceArmChargePosition = npc.Center;
                        viceArmChargeTargetX = Main.player[npc.target].Center.X - viceArmChargePosition.X;
                        viceArmChargeTargetY = Main.player[npc.target].Center.Y - viceArmChargePosition.Y;
                        float viceArmChargeTargetDist = (float)Math.Sqrt(viceArmChargeTargetX * viceArmChargeTargetX + viceArmChargeTargetY * viceArmChargeTargetY);
                        viceArmChargeTargetDist = chargeVelocity / viceArmChargeTargetDist;
                        npc.velocity.X = viceArmChargeTargetX * viceArmChargeTargetDist;
                        npc.velocity.Y = viceArmChargeTargetY * viceArmChargeTargetDist;
                        npc.netUpdate = true;
                    }
                }

                // Charge 4 times (more if arms are dead)
                else if (npc.ai[2] == 2f) {
                    // Set damage
                    npc.damage = npc.defDamage;

                    if (npc.position.Y > Main.player[npc.target].position.Y || npc.velocity.Y < 0f) {
                        float chargeAmt = 4f;
                        if (!cannonAlive)
                            chargeAmt += 1f;
                        if (!laserAlive)
                            chargeAmt += 1f;
                        if (!sawAlive)
                            chargeAmt += 1f;

                        if (npc.ai[3] >= chargeAmt) {
                            // Return to head
                            npc.ai[2] = 3f;
                            npc.ai[3] = 0f;
                            npc.TargetClosest();
                            return false;
                        }

                        npc.ai[2] = 1f;
                        npc.ai[3] += 1f;
                    }
                }

                // Different type of charge
                else if (npc.ai[2] == 4f) {
                    Vector2 viceArmOtherChargePosition = npc.Center;
                    float viceArmOtherChargeTargetX = Main.npc[(int)npc.ai[1]].Center.X - 200f * npc.ai[0] - viceArmOtherChargePosition.X;
                    float viceArmOtherChargeTargetY = Main.npc[(int)npc.ai[1]].position.Y + 230f - viceArmOtherChargePosition.Y;
                    npc.rotation = (float)Math.Atan2(viceArmOtherChargeTargetY, viceArmOtherChargeTargetX) + MathHelper.PiOver2;

                    npc.velocity.Y = (npc.velocity.Y * 5f + Main.npc[(int)npc.ai[1]].velocity.Y) / 6f;

                    npc.velocity.X += 0.5f;
                    if (npc.velocity.X > 12f)
                        npc.velocity.X = 12f;

                    if (npc.Center.X < Main.npc[(int)npc.ai[1]].Center.X - 500f || npc.Center.X > Main.npc[(int)npc.ai[1]].Center.X + 500f) {
                        // Set damage
                        npc.damage = npc.defDamage;

                        float chargeVelocity = bossRush ? 17.5f : 14f;
                        if (!cannonAlive)
                            chargeVelocity += 1.15f;
                        if (!laserAlive)
                            chargeVelocity += 1.15f;
                        if (!sawAlive)
                            chargeVelocity += 1.15f;

                        npc.ai[2] = 5f;
                        npc.TargetClosest();
                        viceArmOtherChargePosition = npc.Center;
                        viceArmOtherChargeTargetX = Main.player[npc.target].Center.X - viceArmOtherChargePosition.X;
                        viceArmOtherChargeTargetY = Main.player[npc.target].Center.Y - viceArmOtherChargePosition.Y;
                        float viceArmOtherChargeTargetDist = (float)Math.Sqrt(viceArmOtherChargeTargetX * viceArmOtherChargeTargetX + viceArmOtherChargeTargetY * viceArmOtherChargeTargetY);
                        viceArmOtherChargeTargetDist = chargeVelocity / viceArmOtherChargeTargetDist;
                        npc.velocity.X = viceArmOtherChargeTargetX * viceArmOtherChargeTargetDist;
                        npc.velocity.Y = viceArmOtherChargeTargetY * viceArmOtherChargeTargetDist;
                        npc.netUpdate = true;
                    }
                }

                // Charge 4 times (more if arms are dead)
                else if (npc.ai[2] == 5f && npc.Center.X < Main.player[npc.target].Center.X - 100f) {
                    // Set damage
                    npc.damage = npc.defDamage;

                    float chargeAmt = 4f;
                    if (!cannonAlive)
                        chargeAmt += 1f;
                    if (!laserAlive)
                        chargeAmt += 1f;
                    if (!sawAlive)
                        chargeAmt += 1f;

                    if (npc.ai[3] >= chargeAmt) {
                        // Return to head
                        npc.ai[2] = 0f;
                        npc.ai[3] = 0f;
                        npc.TargetClosest();
                        return false;
                    }

                    npc.ai[2] = 4f;
                    npc.ai[3] += 1f;
                }
            }
            return false;
        }
    }
}
