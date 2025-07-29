﻿using CalamityMod;
using CalamityMod.NPCs;
using CalamityMod.World;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Boss.SkeletronPrime;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime
{
    internal class PrimeCannonAI : PrimeArm
    {
        public override int TargetID => NPCID.PrimeCannon;
        public override bool CanLoad() => true;
        public override bool? CheckDead() => true;
        internal void Movement() {
            float acceleration = (bossRush ? 0.6f : death ? (masterMode ? 0.375f : 0.3f) : (masterMode ? 0.3125f : 0.25f));
            float accelerationMult = 1f;
            if (!laserAlive) {
                acceleration += 0.025f;
                accelerationMult += 0.5f;
            }
            if (!viceAlive)
                acceleration += 0.025f;
            if (!sawAlive)
                acceleration += 0.025f;
            if (masterMode)
                acceleration *= accelerationMult;

            float topVelocity = acceleration * 100f;
            float deceleration = masterMode ? 0.6f : 0.8f;

            if (npc.position.Y > head.position.Y - 130f) {
                if (npc.velocity.Y > 0f)
                    npc.velocity.Y *= deceleration;

                npc.velocity.Y -= acceleration;

                if (npc.velocity.Y > topVelocity)
                    npc.velocity.Y = topVelocity;
            }
            else if (npc.position.Y < head.position.Y - 170f) {
                if (npc.velocity.Y < 0f)
                    npc.velocity.Y *= deceleration;

                npc.velocity.Y += acceleration;

                if (npc.velocity.Y < -topVelocity)
                    npc.velocity.Y = -topVelocity;
            }

            if (npc.Center.X > head.Center.X + 160f) {
                if (npc.velocity.X > 0f)
                    npc.velocity.X *= deceleration;

                npc.velocity.X -= acceleration;

                if (npc.velocity.X > topVelocity)
                    npc.velocity.X = topVelocity;
            }
            if (npc.Center.X < head.Center.X + 200f) {
                if (npc.velocity.X < 0f)
                    npc.velocity.X *= deceleration;

                npc.velocity.X += acceleration;

                if (npc.velocity.X < -topVelocity)
                    npc.velocity.X = -topVelocity;
            }
        }

        internal void FireSlowerAttack() {
            if (head.ai[1] == 3f && npc.timeLeft > 10)
                npc.timeLeft = 10;

            Vector2 cannonArmPosition = npc.Center;
            float cannonArmTargetX = player.Center.X - cannonArmPosition.X;
            float cannonArmTargetY = player.Center.Y - cannonArmPosition.Y;
            float cannonArmTargetDist = (float)Math.Sqrt(cannonArmTargetX * cannonArmTargetX + cannonArmTargetY * cannonArmTargetY);
            float targetRot = (float)Math.Atan2(cannonArmTargetY, cannonArmTargetX) - MathHelper.PiOver2;
            npc.EntityToRot(targetRot, 0.2f);

            if (Main.netMode != NetmodeID.MultiplayerClient && !dontAttack) {
                npc.localAI[0] += 1f;
                if (!laserAlive)
                    npc.localAI[0] += 1f;
                if (!viceAlive)
                    npc.localAI[0] += 1f;
                if (!sawAlive)
                    npc.localAI[0] += 1f;

                float fireCannonCoolding = 120f;
                if (death) {
                    fireCannonCoolding -= 20;
                }
                if (masterMode) {
                    fireCannonCoolding -= 20;
                }
                if (bossRush) {
                    fireCannonCoolding = 60;
                }
                if (npc.localAI[0] >= fireCannonCoolding) {
                    npc.localAI[0] = 0f;
                    npc.TargetClosest();
                    int type = ProjectileID.RocketSkeleton;
                    int damage = HeadPrimeAI.SetMultiplier(npc.GetProjectileDamage(type));
                    float rocketSpeed = 10f;
                    cannonArmTargetDist = rocketSpeed / cannonArmTargetDist;
                    cannonArmTargetX *= cannonArmTargetDist;
                    cannonArmTargetY *= cannonArmTargetDist;

                    Vector2 rocketVelocity = new Vector2(cannonArmTargetX, cannonArmTargetY);
                    if (death && masterMode || bossRush || ModGanged.InfernumModeOpenState) {
                        int proj = Projectile.NewProjectile(npc.GetSource_FromAI()
                        , cannonArmPosition + rocketVelocity.SafeNormalize(Vector2.UnitY) * 40f, rocketVelocity
                        , ModContent.ProjectileType<PrimeCannonOnSpan>(), damage, 0f
                        , Main.myPlayer, npc.whoAmI, npc.target, 0);
                        Main.projectile[proj].timeLeft = (int)(fireCannonCoolding * 0.8f);
                        if (Main.projectile[proj].timeLeft > 60) {
                            Main.projectile[proj].timeLeft = 60;
                        }
                    }
                    else {
                        int proj = Projectile.NewProjectile(npc.GetSource_FromAI()
                            , cannonArmPosition + rocketVelocity.SafeNormalize(Vector2.UnitY) * 40f
                            , rocketVelocity, type, damage, 0f, Main.myPlayer, npc.target, 2f);
                        Main.projectile[proj].timeLeft = 600;
                    }
                }
            }
        }
        internal void OtherFireSlowerAttack() {
            Vector2 cannonSpreadArmPosition = npc.Center;
            float cannonSpreadArmTargetX = player.Center.X - cannonSpreadArmPosition.X;
            float cannonSpreadArmTargetY = player.Center.Y - cannonSpreadArmPosition.Y;
            float targetRot = (float)Math.Atan2(cannonSpreadArmTargetY, cannonSpreadArmTargetX) - MathHelper.PiOver2;
            npc.EntityToRot(targetRot, 0.2f);

            if (Main.netMode != NetmodeID.MultiplayerClient && !dontAttack) {
                npc.localAI[0] += 1f;
                if (!laserAlive)
                    npc.localAI[0] += 0.5f;
                if (!viceAlive)
                    npc.localAI[0] += 0.5f;
                if (!sawAlive)
                    npc.localAI[0] += 0.5f;

                if (npc.localAI[0] >= 180f) {
                    npc.localAI[0] = 0f;
                    npc.TargetClosest();
                    int type = ProjectileID.RocketSkeleton;
                    int damage = HeadPrimeAI.SetMultiplier(npc.GetProjectileDamage(type));
                    float rocketSpeed = 10f;
                    Vector2 cannonSpreadTargetDist = (player.Center - npc.Center).SafeNormalize(Vector2.UnitY) * rocketSpeed;
                    int numProj = bossRush ? 5 : 3;
                    float rotation = MathHelper.ToRadians(bossRush ? 15 : 9);
                    for (int i = 0; i < numProj; i++) {
                        float rotoffset = MathHelper.Lerp(-rotation, rotation, i / (float)(numProj - 1));
                        Vector2 perturbedSpeed = cannonSpreadTargetDist.RotatedBy(rotoffset);
                        if (CalamityWorld.death || CWRWorld.MachineRebellion || bossRush || ModGanged.InfernumModeOpenState) {
                            Projectile.NewProjectile(npc.GetSource_FromAI()
                            , npc.Center, perturbedSpeed
                            , ModContent.ProjectileType<PrimeCannonOnSpan>(), damage, 0f
                            , Main.myPlayer, npc.whoAmI, npc.target, rotoffset);
                        }
                        else {
                            int proj = Projectile.NewProjectile(npc.GetSource_FromAI()
                                , npc.Center + perturbedSpeed.SafeNormalize(Vector2.UnitY) * 40f
                                , perturbedSpeed, type, damage, 0f, Main.myPlayer, npc.target, 2f);
                            Main.projectile[proj].timeLeft = 600;
                        }
                    }
                }
            }
        }
        public override bool ArmBehavior() {
            float timeToNotAttack = 180f;
            dontAttack = calNPC.newAI[2] < timeToNotAttack;
            if (dontAttack) {
                calNPC.newAI[2] += 1f;
                if (calNPC.newAI[2] >= timeToNotAttack) {
                    HeadPrimeAI.SendExtraAI(npc);
                }
            }

            bool fireSlower = false;
            if (laserAlive) {
                if (Main.npc[CalamityGlobalNPC.primeLaser].ai[2] == 1f)
                    fireSlower = true;
            }
            else {
                fireSlower = npc.ai[2] == 0f;
                if (fireSlower) {
                    npc.ai[3] += 1f;
                    if (!laserAlive)
                        npc.ai[3] += 1f;
                    if (!viceAlive)
                        npc.ai[3] += 1f;
                    if (!sawAlive)
                        npc.ai[3] += 1f;

                    if (npc.ai[3] >= (masterMode ? 200f : 800f)) {
                        npc.localAI[0] = 0f;
                        npc.ai[2] = 1f;
                        fireSlower = false;
                        npc.ai[3] = 0f;
                        npc.TargetClosest();
                        npc.netUpdate = true;
                    }
                }
                else {
                    npc.ai[3] += 1f;

                    float timeLimit = 120f;
                    float timeMult = 1.882075f;
                    if (!laserAlive)
                        timeLimit *= timeMult;
                    if (!viceAlive)
                        timeLimit *= timeMult;
                    if (!sawAlive)
                        timeLimit *= timeMult;

                    if (npc.ai[3] >= timeLimit) {
                        npc.localAI[0] = 0f;
                        npc.ai[2] = 0f;
                        fireSlower = true;
                        npc.ai[3] = 0f;
                        npc.TargetClosest();
                        npc.netUpdate = true;
                    }
                }
            }

            Movement();

            if (fireSlower) {
                FireSlowerAttack();
            }
            else {
                OtherFireSlowerAttack();
            }

            if (FindPrimeCannonOnSpan(out Projectile primeCannonOnSpan)) {
                npc.rotation = primeCannonOnSpan.rotation - MathHelper.PiOver2;
            }

            return false;
        }

        private bool FindPrimeCannonOnSpan(out Projectile projectile) {
            bool reset = false;
            projectile = null;
            int type = ModContent.ProjectileType<PrimeCannonOnSpan>();
            foreach (var proj in Main.ActiveProjectiles) {
                if (proj.type != type) {
                    continue;
                }
                if (proj.ai[0] == npc.whoAmI && proj.ai[2] == 0) {
                    projectile = proj;
                    reset = true;
                    break;
                }
            }

            return reset;
        }

        public override bool? Draw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (HeadPrimeAI.DontReform()) {
                return true;
            }

            bool dir = (npc.rotation + MathHelper.PiOver2).ToRotationVector2().X > 0;

            HeadPrimeAI.DrawArm(spriteBatch, npc, screenPos);
            Texture2D mainValue = HeadPrimeAI.BSPCannon.Value;
            Texture2D mainValue2 = HeadPrimeAI.BSPCannonGlow.Value;
            Main.EntitySpriteDraw(mainValue, npc.Center - Main.screenPosition, null, drawColor
                , npc.rotation, mainValue.Size() / 2, npc.scale
                , dir ? SpriteEffects.None : SpriteEffects.FlipHorizontally);
            Main.EntitySpriteDraw(mainValue2, npc.Center - Main.screenPosition, null, Color.White
                , npc.rotation, mainValue.Size() / 2, npc.scale
                , dir ? SpriteEffects.None : SpriteEffects.FlipHorizontally);
            return false;
        }
        public override bool PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) => !HeadPrimeAI.DontReform();
    }
}
