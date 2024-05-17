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
    internal class BrutalPrimeSawAI : NPCSet
    {
        public override int targetID => NPCID.PrimeSaw;
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

            Vector2 sawArmLocation = npc.Center;
            float sawArmIdleXPos = Main.npc[(int)npc.ai[1]].Center.X - 200f * npc.ai[0] - sawArmLocation.X;
            float sawArmIdleYPos = Main.npc[(int)npc.ai[1]].position.Y + 230f - sawArmLocation.Y;
            float sawArmIdleDistance = (float)Math.Sqrt(sawArmIdleXPos * sawArmIdleXPos + sawArmIdleYPos * sawArmIdleYPos);

            if (npc.ai[2] != 99f) {
                if (sawArmIdleDistance > 800f)
                    npc.ai[2] = 99f;
            }
            else if (sawArmIdleDistance < 400f)
                npc.ai[2] = 0f;

            npc.spriteDirection = -(int)npc.ai[0];

            if (!Main.npc[(int)npc.ai[1]].active || Main.npc[(int)npc.ai[1]].aiStyle != NPCAIStyleID.SkeletronPrimeHead) {
                npc.ai[2] += 10f;
                if (npc.ai[2] > 50f || Main.netMode != NetmodeID.Server) {
                    npc.life = -1;
                    npc.HitEffect(0, 10.0);
                    npc.active = false;
                }
            }

            CalamityGlobalNPC.primeSaw = npc.whoAmI;

            // Check if arms are alive
            bool cannonAlive = false;
            bool laserAlive = false;
            bool viceAlive = false;
            if (CalamityGlobalNPC.primeCannon != -1) {
                if (Main.npc[CalamityGlobalNPC.primeCannon].active)
                    cannonAlive = true;
            }
            if (CalamityGlobalNPC.primeLaser != -1) {
                if (Main.npc[CalamityGlobalNPC.primeLaser].active)
                    laserAlive = true;
            }
            if (CalamityGlobalNPC.primeVice != -1) {
                if (Main.npc[CalamityGlobalNPC.primeVice].active)
                    viceAlive = true;
            }

            // Min saw damage
            int reducedSetDamage = (int)Math.Round(npc.defDamage * 0.5);

            // Avoid cheap bullshit
            npc.damage = reducedSetDamage;

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
                if (!viceAlive)
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
            else {
                if (npc.ai[2] == 0f || npc.ai[2] == 3f) {
                    if (Main.npc[(int)npc.ai[1]].ai[1] == 3f && npc.timeLeft > 10)
                        npc.timeLeft = 10;

                    // Start charging after 3 seconds (change this as each arm dies)
                    npc.ai[3] += 1f;
                    if (!cannonAlive)
                        npc.ai[3] += 1f;
                    if (!laserAlive)
                        npc.ai[3] += 1f;
                    if (!viceAlive)
                        npc.ai[3] += 1f;

                    if (npc.ai[3] >= (masterMode ? 90f : 180f)) {
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
                    if (!viceAlive)
                        acceleration += 0.025f;
                    if (masterMode)
                        acceleration *= accelerationMult;

                    float topVelocity = acceleration * 100f;
                    float deceleration = masterMode ? 0.6f : 0.8f;

                    if (npc.position.Y > Main.npc[(int)npc.ai[1]].position.Y + 310f) {
                        if (npc.velocity.Y > 0f)
                            npc.velocity.Y *= deceleration;

                        npc.velocity.Y -= acceleration;

                        if (npc.velocity.Y > topVelocity)
                            npc.velocity.Y = topVelocity;
                    }
                    else if (npc.position.Y < Main.npc[(int)npc.ai[1]].position.Y + 270f) {
                        if (npc.velocity.Y < 0f)
                            npc.velocity.Y *= deceleration;

                        npc.velocity.Y += acceleration;

                        if (npc.velocity.Y < -topVelocity)
                            npc.velocity.Y = -topVelocity;
                    }

                    if (npc.Center.X > Main.npc[(int)npc.ai[1]].Center.X - 100f) {
                        if (npc.velocity.X > 0f)
                            npc.velocity.X *= deceleration;

                        npc.velocity.X -= acceleration * 1.5f;

                        if (npc.velocity.X > topVelocity)
                            npc.velocity.X = topVelocity;
                    }
                    if (npc.Center.X < Main.npc[(int)npc.ai[1]].Center.X - 150f) {
                        if (npc.velocity.X < 0f)
                            npc.velocity.X *= deceleration;

                        npc.velocity.X += acceleration * 1.5f;

                        if (npc.velocity.X < -topVelocity)
                            npc.velocity.X = -topVelocity;
                    }

                    Vector2 sawArmReelbackCurrentPos = npc.Center;
                    float sawArmReelbackXDest = Main.npc[(int)npc.ai[1]].Center.X - 200f * npc.ai[0] - sawArmReelbackCurrentPos.X;
                    float sawArmReelbackYDest = Main.npc[(int)npc.ai[1]].position.Y + 230f - sawArmReelbackCurrentPos.Y;
                    npc.rotation = (float)Math.Atan2(sawArmReelbackYDest, sawArmReelbackXDest) + MathHelper.PiOver2;
                    return false;
                }

                if (npc.ai[2] == 1f) {
                    Vector2 sawArmChargePos = npc.Center;
                    float sawArmChargeTargetX = Main.npc[(int)npc.ai[1]].Center.X - 200f * npc.ai[0] - sawArmChargePos.X;
                    float sawArmChargeTargetY = Main.npc[(int)npc.ai[1]].position.Y + 230f - sawArmChargePos.Y;
                    npc.rotation = (float)Math.Atan2(sawArmChargeTargetY, sawArmChargeTargetX) + MathHelper.PiOver2;

                    float deceleration = masterMode ? 0.875f : 0.9f;
                    npc.velocity.X *= deceleration;
                    npc.velocity.Y -= 0.5f;
                    if (npc.velocity.Y < -12f)
                        npc.velocity.Y = -12f;

                    if (npc.position.Y < Main.npc[(int)npc.ai[1]].position.Y - 200f) {
                        // Set damage
                        npc.damage = npc.defDamage;

                        float chargeVelocity = bossRush ? 27.5f : 22f;
                        if (!cannonAlive)
                            chargeVelocity += 1.5f;
                        if (!laserAlive)
                            chargeVelocity += 1.5f;
                        if (!viceAlive)
                            chargeVelocity += 1.5f;

                        npc.ai[2] = 2f;
                        npc.TargetClosest();
                        sawArmChargePos = npc.Center;
                        sawArmChargeTargetX = Main.player[npc.target].Center.X - sawArmChargePos.X;
                        sawArmChargeTargetY = Main.player[npc.target].Center.Y - sawArmChargePos.Y;
                        float sawArmChargeTargetDist = (float)Math.Sqrt(sawArmChargeTargetX * sawArmChargeTargetX + sawArmChargeTargetY * sawArmChargeTargetY);
                        sawArmChargeTargetDist = chargeVelocity / sawArmChargeTargetDist;
                        npc.velocity.X = sawArmChargeTargetX * sawArmChargeTargetDist;
                        npc.velocity.Y = sawArmChargeTargetY * sawArmChargeTargetDist;
                        npc.netUpdate = true;
                    }
                }

                else if (npc.ai[2] == 2f) {
                    // Set damage
                    npc.damage = npc.defDamage;

                    if (npc.position.Y > Main.player[npc.target].position.Y || npc.velocity.Y < 0f)
                        npc.ai[2] = 3f;
                }

                else {
                    if (npc.ai[2] == 4f) {
                        // Set damage
                        npc.damage = npc.defDamage;

                        float chargeVelocity = bossRush ? 13.5f : 11f;
                        if (!cannonAlive)
                            chargeVelocity += 1.5f;
                        if (!laserAlive)
                            chargeVelocity += 1.5f;
                        if (!viceAlive)
                            chargeVelocity += 1.5f;
                        if (masterMode)
                            chargeVelocity *= 1.25f;

                        Vector2 sawArmOtherChargePos = npc.Center;
                        float sawArmOtherChargeTargetX = Main.player[npc.target].Center.X - sawArmOtherChargePos.X;
                        float sawArmOtherChargeTargetY = Main.player[npc.target].Center.Y - sawArmOtherChargePos.Y;
                        float sawArmOtherChargeTargetDist = (float)Math.Sqrt(sawArmOtherChargeTargetX * sawArmOtherChargeTargetX + sawArmOtherChargeTargetY * sawArmOtherChargeTargetY);
                        sawArmOtherChargeTargetDist = chargeVelocity / sawArmOtherChargeTargetDist;
                        sawArmOtherChargeTargetX *= sawArmOtherChargeTargetDist;
                        sawArmOtherChargeTargetY *= sawArmOtherChargeTargetDist;

                        float acceleration = bossRush ? 0.3f : death ? 0.1f : 0.08f;
                        if (masterMode)
                            acceleration *= 1.25f;

                        float deceleration = masterMode ? 0.6f : 0.8f;

                        if (npc.velocity.X > sawArmOtherChargeTargetX) {
                            if (npc.velocity.X > 0f)
                                npc.velocity.X *= deceleration;

                            npc.velocity.X -= acceleration;
                        }
                        if (npc.velocity.X < sawArmOtherChargeTargetX) {
                            if (npc.velocity.X < 0f)
                                npc.velocity.X *= deceleration;

                            npc.velocity.X += acceleration;
                        }
                        if (npc.velocity.Y > sawArmOtherChargeTargetY) {
                            if (npc.velocity.Y > 0f)
                                npc.velocity.Y *= deceleration;

                            npc.velocity.Y -= acceleration;
                        }
                        if (npc.velocity.Y < sawArmOtherChargeTargetY) {
                            if (npc.velocity.Y < 0f)
                                npc.velocity.Y *= deceleration;

                            npc.velocity.Y += acceleration;
                        }

                        npc.ai[3] += 1f;
                        if (npc.justHit)
                            npc.ai[3] += 2f;

                        if (npc.ai[3] >= 600f) {
                            npc.ai[2] = 0f;
                            npc.ai[3] = 0f;
                            npc.TargetClosest();
                            npc.netUpdate = true;
                        }

                        sawArmOtherChargePos = npc.Center;
                        sawArmOtherChargeTargetX = Main.npc[(int)npc.ai[1]].Center.X - 200f * npc.ai[0] - sawArmOtherChargePos.X;
                        sawArmOtherChargeTargetY = Main.npc[(int)npc.ai[1]].position.Y + 230f - sawArmOtherChargePos.Y;
                        npc.rotation = (float)Math.Atan2(sawArmOtherChargeTargetY, sawArmOtherChargeTargetX) + MathHelper.PiOver2;
                        return false;
                    }

                    if (npc.ai[2] == 5f && ((npc.velocity.X > 0f && npc.Center.X > Main.player[npc.target].Center.X) || (npc.velocity.X < 0f && npc.Center.X < Main.player[npc.target].Center.X)))
                        npc.ai[2] = 0f;
                }
            }

            return false;
        }
    }
}
