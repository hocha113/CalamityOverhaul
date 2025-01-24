using CalamityMod;
using CalamityMod.Events;
using CalamityMod.NPCs;
using CalamityMod.World;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.Core;
using CalamityOverhaul.Content.Projectiles.Boss.SkeletronPrime;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime
{
    internal class PrimeLaserAI : PrimeArm
    {
        public override int TargetID => NPCID.PrimeLaser;
        private const float timeToNotAttack = 180f;
        private bool normalLaserRotation;
        private int lerterFireIndex;
        public override bool CanLoad() => true;
        private void Movement() {
            float acceleration = (bossRush ? 0.6f : death ? (masterMode ? 0.375f : 0.3f) : (masterMode ? 0.3125f : 0.25f));
            float accelerationMult = 1f;
            if (!cannonAlive) {
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

            if (npc.position.Y > Main.npc[(int)npc.ai[1]].position.Y - 80f) {
                if (npc.velocity.Y > 0f)
                    npc.velocity.Y *= deceleration;

                npc.velocity.Y -= acceleration;

                if (npc.velocity.Y > topVelocity)
                    npc.velocity.Y = topVelocity;
            }
            else if (npc.position.Y < Main.npc[(int)npc.ai[1]].position.Y - 120f) {
                if (npc.velocity.Y < 0f)
                    npc.velocity.Y *= deceleration;

                npc.velocity.Y += acceleration;

                if (npc.velocity.Y < -topVelocity)
                    npc.velocity.Y = -topVelocity;
            }

            if (npc.Center.X > Main.npc[(int)npc.ai[1]].Center.X - 160f * npc.ai[0]) {
                if (npc.velocity.X > 0f)
                    npc.velocity.X *= deceleration;

                npc.velocity.X -= acceleration;

                if (npc.velocity.X > topVelocity)
                    npc.velocity.X = topVelocity;
            }
            if (npc.Center.X < Main.npc[(int)npc.ai[1]].Center.X - 200f * npc.ai[0]) {
                if (npc.velocity.X < 0f)
                    npc.velocity.X *= deceleration;

                npc.velocity.X += acceleration;

                if (npc.velocity.X < -topVelocity)
                    npc.velocity.X = -topVelocity;
            }
        }

        private void Attack() {
            // 如果头部正在冲刺
            if (head.ai[1] == 3f && npc.timeLeft > 10) {
                npc.timeLeft = 10;
            }

            npc.ai[3] += 1f;
            if (!cannonAlive)
                npc.ai[3] += 1f;
            if (!viceAlive)
                npc.ai[3] += 1f;
            if (!sawAlive)
                npc.ai[3] += 1f;

            if (npc.ai[3] >= (masterMode ? 120f : 300f)) {
                npc.localAI[0] = 0f;
                npc.ai[2] = 1f;
                npc.ai[3] = 0f;
                npc.TargetClosest();
                npc.netUpdate = true;
            }

            Vector2 laserArmPosition = npc.Center;
            float laserArmTargetX = Main.player[npc.target].Center.X - laserArmPosition.X;
            float laserArmTargetY = Main.player[npc.target].Center.Y - laserArmPosition.Y;
            float laserArmTargetDist = (float)Math.Sqrt(laserArmTargetX * laserArmTargetX + laserArmTargetY * laserArmTargetY);
            npc.rotation = MathF.Atan2(laserArmTargetY, laserArmTargetX) - MathHelper.PiOver2;

            if (Main.netMode != NetmodeID.MultiplayerClient && !dontAttack) {
                npc.localAI[0] += 1f;
                if (!cannonAlive)
                    npc.localAI[0] += 1f;
                if (!viceAlive)
                    npc.localAI[0] += 1f;
                if (!sawAlive)
                    npc.localAI[0] += 1f;

                int fireCoolding = 48;
                if (masterMode) {
                    fireCoolding -= 10;
                }
                if (death) {
                    fireCoolding -= 10;
                }
                if (bossRush) {
                    fireCoolding = 10;
                }

                bool canFire = true;
                if (CalamityWorld.death) {
                    canFire = bossRush;
                }

                if (npc.localAI[0] >= fireCoolding && canFire) {
                    npc.TargetClosest();
                    float laserSpeed = bossRush ? 5f : 4f;
                    int type = ProjectileID.DeathLaser;
                    int damage = HeadPrimeAI.SetMultiplier(npc.GetProjectileDamage(type));
                    HeadPrimeAI.SpanFireLerterDustEffect(npc, 3);
                    laserArmTargetDist = laserSpeed / laserArmTargetDist;
                    laserArmTargetX *= laserArmTargetDist;
                    laserArmTargetY *= laserArmTargetDist;
                    Vector2 laserVelocity = new Vector2(laserArmTargetX, laserArmTargetY);
                    Vector2 lerSpanPos = laserArmPosition + laserVelocity.SafeNormalize(Vector2.UnitY) * 100f;
                    laserVelocity *= 1 + (float)(lerterFireIndex * 0.1f);
                    if (death) {
                        type = ModContent.ProjectileType<DeadLaser>();
                        laserVelocity *= 0.65f;
                    }
                    Projectile.NewProjectile(npc.GetSource_FromAI(), lerSpanPos, laserVelocity, type, damage, 0f, Main.myPlayer, 1f, 0f);
                    npc.localAI[0] = 0f;
                    lerterFireIndex++;
                }
            }
        }

        private void OtherAttack() {
            npc.ai[3] += 1f;

            float timeLimit = 135f;
            float timeMult = 1.882075f;
            if (!cannonAlive)
                timeLimit *= timeMult;
            if (!viceAlive)
                timeLimit *= timeMult;
            if (!sawAlive)
                timeLimit *= timeMult;

            if (npc.ai[3] >= timeLimit) {
                npc.localAI[0] = 0f;
                npc.ai[2] = 0f;
                npc.ai[3] = 0f;
                npc.TargetClosest();
                npc.netUpdate = true;
            }

            Vector2 laserRingArmPosition = npc.Center;
            float laserRingTargetX = player.Center.X - laserRingArmPosition.X;
            float laserRingTargetY = player.Center.Y - laserRingArmPosition.Y;
            npc.rotation = (float)Math.Atan2(laserRingTargetY, laserRingTargetX) - MathHelper.PiOver2;

            int num = 0;
            int typeSetPosingStarm = ModContent.ProjectileType<SetPosingStarm>();
            foreach (var proj in Main.projectile) {
                if (proj.active && proj.type == typeSetPosingStarm) {
                    num++;
                }
            }

            if (Main.netMode != NetmodeID.MultiplayerClient && !dontAttack && num == 0) {
                npc.localAI[0] += 1f;
                if (!cannonAlive) {
                    npc.localAI[0] += 0.5f;
                }
                if (!viceAlive) {
                    npc.localAI[0] += 0.5f;
                }
                if (!sawAlive) {
                    npc.localAI[0] += 0.5f;
                }

                if (npc.localAI[0] >= 120f) {
                    npc.TargetClosest();

                    int totalProjectiles = bossRush ? 22 : (masterMode ? 13 : 10);
                    float radians = MathHelper.TwoPi / totalProjectiles;
                    int type = ProjectileID.DeathLaser;
                    int damage = HeadPrimeAI.SetMultiplier(npc.GetProjectileDamage(type));

                    float velocity = 3f;
                    double angleA = radians * 0.5;
                    double angleB = MathHelper.ToRadians(90f) - angleA;
                    float laserVelocityX = (float)(velocity * Math.Sin(angleA) / Math.Sin(angleB));
                    Vector2 spinningPoint = normalLaserRotation ? new Vector2(0f, -velocity) : new Vector2(-laserVelocityX, -velocity);
                    bool spanLerter = HeadPrimeAI.setPosingStarmCount <= 0;
                    if (spanLerter) {
                        if (death) {
                            totalProjectiles = bossRush ? 12 : 6;
                            radians = MathHelper.TwoPi / totalProjectiles;
                            if (ModGanged.InfernumModeOpenState) {
                                for (int j = 0; j < 5; j++) {
                                    for (int k = 0; k < totalProjectiles; k++) {
                                        float speedMode = 1.55f + j * 0.3f;
                                        if (bossRush) {
                                            speedMode = 1.7f + j * 0.35f;
                                        }
                                        Vector2 laserFireDirection = spinningPoint.RotatedBy(radians * k);
                                        Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center + laserFireDirection.SafeNormalize(Vector2.UnitY) * 100f
                                            , laserFireDirection * speedMode, ModContent.ProjectileType<DeadLaser>(), damage, 0f, Main.myPlayer, 1f, 0f);
                                    }
                                }
                            }
                            else {
                                Vector2 toTarget = npc.Center.To(player.Center).UnitVector();
                                for (int i = 0; i < 3; i++) {
                                    int index = i - 1;
                                    Vector2 laserFireDirection = spinningPoint.RotatedBy(index * 0.12f);
                                    Vector2 ver = toTarget.RotatedBy(index * 0.12f) * 3;
                                    Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center + laserFireDirection.SafeNormalize(Vector2.UnitY) * 100f
                                            , ver, ModContent.ProjectileType<DeadLaser>(), damage, 0f, Main.myPlayer, 1f, 0f);
                                }
                            }
                            HeadPrimeAI.SpanFireLerterDustEffect(npc, 33);
                        }
                        else {
                            for (int k = 0; k < totalProjectiles; k++) {
                                Vector2 laserFireDirection = spinningPoint.RotatedBy(radians * k);
                                int proj = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center + laserFireDirection.SafeNormalize(Vector2.UnitY) * 100f
                                    , laserFireDirection, type, damage, 0f, Main.myPlayer, 1f, 0f);
                                Main.projectile[proj].timeLeft = 900;
                            }
                        }
                    }

                    npc.localAI[1] += 1f;
                    npc.localAI[0] = 0f;
                }
            }
        }

        public override bool? CheckDead() => true;

        public override bool ArmBehavior() {
            dontAttack = calNPC.newAI[2] < timeToNotAttack;
            normalLaserRotation = npc.localAI[1] % 2f == 0f;
            if (dontAttack) {
                calNPC.newAI[2]++;
                if (calNPC.newAI[2] >= timeToNotAttack) {
                    HeadPrimeAI.SendExtraAI(npc);
                }
            }
            Movement();
            if (npc.ai[2] == 0f) {
                Attack();
            }
            else if (npc.ai[2] == 1f) {
                OtherAttack();
                lerterFireIndex = 0;
            }
            return false;
        }

        public override bool? Draw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (!HeadPrimeAI.canLoaderAssetZunkenUp) {
                return false;
            }
            if (HeadPrimeAI.DontReform()) {
                return true;
            }
            bool dir = (npc.rotation + MathHelper.PiOver2).ToRotationVector2().X > 0;
            HeadPrimeAI.DrawArm(spriteBatch, npc, screenPos);
            Texture2D mainValue = HeadPrimeAI.BSPlaser.Value;
            Texture2D mainValue2 = HeadPrimeAI.BSPlaserGlow.Value;
            Main.EntitySpriteDraw(mainValue, npc.Center - Main.screenPosition, null, drawColor
                , npc.rotation, mainValue.Size() / 2, npc.scale
                , dir ? SpriteEffects.None : SpriteEffects.FlipHorizontally);
            Main.EntitySpriteDraw(mainValue2, npc.Center - Main.screenPosition, null, Color.White
                , npc.rotation, mainValue2.Size() / 2, npc.scale
                , dir ? SpriteEffects.None : SpriteEffects.FlipHorizontally);
            return false;
        }
        public override bool PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) => !HeadPrimeAI.DontReform();
    }
}
