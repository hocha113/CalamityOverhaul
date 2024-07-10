using CalamityMod.Events;
using CalamityMod.NPCs.NormalNPCs;
using CalamityMod.NPCs;
using CalamityMod.World;
using CalamityMod;
using CalamityOverhaul.Content.NPCs.Core;
using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEyeOfCthulhu
{
    internal class BrutalEyeOfCthulhuAI : NPCCoverage
    {
        private const float ProjectileOffset = 50f;
        public override int TargetID => NPCID.EyeofCthulhu;
        CalamityGlobalNPC calNPC => npc.Calamity();
        float lifeRatio => npc.life / (float)npc.lifeMax;
        bool bossRush => BossRushEvent.BossRushActive;
        bool masterMode => Main.masterMode || bossRush;
        bool death => CalamityWorld.death || bossRush;
        float phase2LifeRatio => masterMode ? 0.75f : 0.6f;
        float phase3LifeRatio => masterMode ? 0.4f : 0.3f;
        float finalPhaseRevLifeRatio => masterMode ? 0.2f : 0.15f;
        float penultimatePhaseDeathLifeRatio => masterMode ? 0.3f : 0.2f;
        float finalPhaseDeathLifeRatio => masterMode ? 0.15f : 0.1f;
        bool phase2 => lifeRatio < phase2LifeRatio;
        bool phase3 => lifeRatio < phase3LifeRatio;
        bool finalPhaseRev => lifeRatio < finalPhaseRevLifeRatio;
        bool penultimatePhaseDeath => lifeRatio < penultimatePhaseDeathLifeRatio;
        bool finalPhaseDeath => lifeRatio < finalPhaseDeathLifeRatio;
        float lineUpDist => death ? 15f : 20f;
        float servantAndProjectileVelocity => (death ? 8f : 6f) + (masterMode ? 2f : 0f);
        float enrageScale;

        public override void SetProperty() {
            enrageScale = bossRush ? 1f : masterMode ? 0.5f : 0f;
        }

        //首先，重构这个东西便是一场彻彻底底的灾难
        public override bool? AI() {
            if (Main.IsItDay() || bossRush) {
                npc.Calamity().CurrentlyEnraged = !bossRush;
                enrageScale += 2f;
            }

            npc.reflectsProjectiles = false;

            // Get a target
            if (npc.target < 0 || npc.target == Main.maxPlayers || Main.player[npc.target].dead || !Main.player[npc.target].active)
                npc.TargetClosest();

            bool dead = Main.player[npc.target].dead;
            float targetXDistance = npc.Center.X - Main.player[npc.target].position.X - (Main.player[npc.target].width / 2);
            float targetYDistance = npc.position.Y + npc.height - 59f - Main.player[npc.target].position.Y - (Main.player[npc.target].height / 2);
            float eyeRotation = (float)Math.Atan2(targetYDistance, targetXDistance) + MathHelper.PiOver2;

            if (eyeRotation < 0f)
                eyeRotation += MathHelper.TwoPi;
            else if (eyeRotation > MathHelper.TwoPi)
                eyeRotation -= MathHelper.TwoPi;

            float eyeRotationAcceleration = 0f;
            if (npc.ai[0] == 0f && npc.ai[1] == 0f)
                eyeRotationAcceleration = 0.04f;
            if (npc.ai[0] == 0f && npc.ai[1] == 2f && npc.ai[2] > 40f)
                eyeRotationAcceleration = 0.1f;
            if (npc.ai[0] == 3f && npc.ai[1] == 0f)
                eyeRotationAcceleration = 0.1f;
            if (npc.ai[0] == 3f && npc.ai[1] == 2f && npc.ai[2] > 40f)
                eyeRotationAcceleration = 0.16f;
            if (npc.ai[0] == 3f && npc.ai[1] == 4f && npc.ai[2] > lineUpDist)
                eyeRotationAcceleration = 0.3f;
            if (npc.ai[0] == 3f && npc.ai[1] == 5f)
                eyeRotationAcceleration = 0.1f;

            if (npc.rotation < eyeRotation) {
                if ((eyeRotation - npc.rotation) > MathHelper.Pi)
                    npc.rotation -= eyeRotationAcceleration;
                else
                    npc.rotation += eyeRotationAcceleration;
            }
            else if (npc.rotation > eyeRotation) {
                if ((npc.rotation - eyeRotation) > MathHelper.Pi)
                    npc.rotation += eyeRotationAcceleration;
                else
                    npc.rotation -= eyeRotationAcceleration;
            }

            if (npc.rotation > eyeRotation - eyeRotationAcceleration && npc.rotation < eyeRotation + eyeRotationAcceleration)
                npc.rotation = eyeRotation;
            if (npc.rotation < 0f)
                npc.rotation += MathHelper.TwoPi;
            else if (npc.rotation > MathHelper.TwoPi)
                npc.rotation -= MathHelper.TwoPi;
            if (npc.rotation > eyeRotation - eyeRotationAcceleration && npc.rotation < eyeRotation + eyeRotationAcceleration)
                npc.rotation = eyeRotation;

            if (Main.rand.NextBool(5)) {
                int randomBlood = Dust.NewDust(new Vector2(npc.position.X, npc.position.Y + npc.height * 0.25f), npc.width, (int)(npc.height * 0.5f), DustID.Blood, npc.velocity.X, 2f, 0, default, 1f);
                Dust dust = Main.dust[randomBlood];
                dust.velocity.X *= 0.5f;
                dust.velocity.Y *= 0.1f;
            }

            bool shootProjectile = Collision.CanHitLine(npc.Center, 1, 1, Main.player[npc.target].Center, 1, 1) &&
                npc.SafeDirectionTo(Main.player[npc.target].Center).AngleBetween((npc.rotation + MathHelper.PiOver2).ToRotationVector2()) < MathHelper.ToRadians(18f) &&
                Vector2.Distance(npc.Center, Main.player[npc.target].Center) > 240f;

            bool charge = Vector2.Distance(Main.player[npc.target].Center, npc.Center) >= 320f; // 20 tile distance

            if (dead) {
                npc.velocity.Y -= 0.04f;

                if (npc.timeLeft > 10)
                    npc.timeLeft = 10;
            }

            else if (npc.ai[0] == 0f) {
                if (npc.ai[1] == 0f) {
                    // Avoid cheap bullshit
                    npc.damage = 0;

                    float hoverSpeed = 7f;
                    float hoverAcceleration = 0.15f;
                    hoverSpeed += 5f * enrageScale;
                    hoverAcceleration += 0.1f * enrageScale;

                    if (death) {
                        hoverSpeed += 7f * (1f - lifeRatio);
                        hoverAcceleration += 0.15f * (1f - lifeRatio);
                    }

                    if (Main.getGoodWorld) {
                        hoverSpeed += 3f;
                        hoverAcceleration += 0.08f;
                    }

                    float attackSwitchTimer = (masterMode ? 120f : 180f) - (death ? 180f * (1f - lifeRatio) : 0f);
                    bool timeToCharge = npc.ai[2] >= attackSwitchTimer;
                    Vector2 hoverDestination = Main.player[npc.target].Center - Vector2.UnitY * 400f;
                    Vector2 idealVelocity = npc.SafeDirectionTo(hoverDestination) * (hoverSpeed + (timeToCharge ? ((npc.ai[2] - attackSwitchTimer) * 0.01f) : 0f));
                    npc.SimpleFlyMovement(idealVelocity, hoverAcceleration + (timeToCharge ? ((npc.ai[2] - attackSwitchTimer) * 0.001f) : 0f));

                    npc.ai[2] += 1f;
                    if (timeToCharge && charge) {
                        npc.ai[1] = 1f;
                        npc.ai[2] = 0f;
                        npc.ai[3] = 0f;
                        npc.TargetClosest();
                        npc.netUpdate = true;
                    }
                    else if (npc.WithinRange(hoverDestination, 900f)) {
                        if (!Main.player[npc.target].dead)
                            npc.ai[3] += 1f;

                        float servantSpawnGateValue = death ? (masterMode ? 10f : 20f) : (masterMode ? 25f : 40f);
                        if (Main.getGoodWorld)
                            servantSpawnGateValue *= 0.8f;

                        if (npc.ai[3] >= servantSpawnGateValue && shootProjectile) {
                            npc.ai[3] = 0f;
                            npc.rotation = eyeRotation;

                            Vector2 servantSpawnVelocity = npc.SafeDirectionTo(Main.player[npc.target].Center) * servantAndProjectileVelocity;
                            Vector2 servantSpawnCenter = npc.Center + servantSpawnVelocity.SafeNormalize(Vector2.UnitY) * ProjectileOffset;
                            int maxServants = 4;
                            bool spawnServant = NPC.CountNPCS(NPCID.ServantofCthulhu) < maxServants;
                            if (spawnServant)
                                SoundEngine.PlaySound(SoundID.NPCHit1, servantSpawnCenter);

                            if (Main.netMode != NetmodeID.MultiplayerClient) {
                                if (spawnServant) {
                                    int eye = NPC.NewNPC(npc.GetSource_FromAI(), (int)servantSpawnCenter.X, (int)servantSpawnCenter.Y, NPCID.ServantofCthulhu, 0, 0f, 0f, enrageScale);
                                    Main.npc[eye].velocity = servantSpawnVelocity;

                                    if (Main.netMode == NetmodeID.Server && eye < Main.maxNPCs)
                                        NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, eye);
                                }
                                else {
                                    int projType = ProjectileID.BloodNautilusShot;
                                    int projDamage = npc.GetProjectileDamage(projType);
                                    int proj = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center + servantSpawnVelocity.SafeNormalize(Vector2.UnitY) * ProjectileOffset, servantSpawnVelocity * 2f, projType, projDamage, 0f, Main.myPlayer);
                                    Main.projectile[proj].timeLeft = 600;
                                }
                            }

                            if (spawnServant) {
                                for (int m = 0; m < 10; m++)
                                    Dust.NewDust(servantSpawnCenter, 20, 20, DustID.Blood, servantSpawnVelocity.X * 0.4f, servantSpawnVelocity.Y * 0.4f, 0, default, 1f);
                            }
                        }
                    }
                }
                else if (npc.ai[1] == 1f) {
                    // Set damage
                    npc.damage = npc.defDamage;

                    npc.rotation = eyeRotation;
                    float additionalVelocityPerCharge = 2f;
                    float chargeSpeed = 8f + npc.ai[3] * additionalVelocityPerCharge;
                    chargeSpeed += 5f * enrageScale;
                    if (death)
                        chargeSpeed += 10f * (1f - lifeRatio);
                    if (Main.getGoodWorld)
                        chargeSpeed += 4f;

                    npc.velocity = npc.SafeDirectionTo(Main.player[npc.target].Center) * chargeSpeed;

                    npc.ai[1] = 2f;
                    npc.netUpdate = true;

                    if (npc.netSpam > 10)
                        npc.netSpam = 10;
                }
                else if (npc.ai[1] == 2f) {
                    // Set damage
                    npc.damage = npc.defDamage;

                    int chargeDelay = masterMode ? 70 : 90;
                    if (death)
                        chargeDelay -= (int)Math.Round(40f * (1f - lifeRatio));
                    if (Main.getGoodWorld)
                        chargeDelay -= 30;

                    float slowDownGateValue = chargeDelay * (death ? (masterMode ? 0.9f : 0.75f) : (masterMode ? 0.8f : 0.65f));

                    npc.ai[2] += 1f;
                    if (npc.ai[2] >= slowDownGateValue) {
                        // Avoid cheap bullshit
                        npc.damage = 0;

                        float decelerationScalar = death ? ((lifeRatio - phase2LifeRatio) / (1f - phase2LifeRatio)) : 1f;
                        if (decelerationScalar < 0f)
                            decelerationScalar = 0f;

                        npc.velocity *= (MathHelper.Lerp(masterMode ? 0.76f : 0.92f, masterMode ? 0.88f : 0.96f, decelerationScalar));
                        if (Main.getGoodWorld)
                            npc.velocity *= 0.99f;

                        if (npc.velocity.X > -0.1 && npc.velocity.X < 0.1)
                            npc.velocity.X = 0f;
                        if (npc.velocity.Y > -0.1 && npc.velocity.Y < 0.1)
                            npc.velocity.Y = 0f;
                    }
                    else
                        npc.rotation = npc.velocity.ToRotation() - MathHelper.PiOver2;

                    if (npc.ai[2] >= chargeDelay) {
                        npc.ai[3] += 1f;
                        npc.ai[2] = 0f;
                        npc.rotation = eyeRotation;

                        float numCharges = death ? 4f : 3f;
                        if (npc.ai[3] >= numCharges) {
                            // Avoid cheap bullshit
                            npc.damage = 0;

                            npc.ai[1] = 0f;
                            npc.ai[3] = 0f;
                        }
                        else
                            npc.ai[1] = 1f;
                    }
                }

                if (phase2) {
                    // Avoid cheap bullshit
                    npc.damage = 0;

                    npc.ai[0] = 1f;
                    npc.ai[1] = 0f;
                    npc.ai[2] = 0f;
                    npc.ai[3] = 0f;
                    npc.TargetClosest();
                    npc.netUpdate = true;

                    if (npc.netSpam > 10)
                        npc.netSpam = 10;
                }
            }

            else if (npc.ai[0] == 1f || npc.ai[0] == 2f) {
                // Avoid cheap bullshit
                npc.damage = 0;

                if (Main.getGoodWorld)
                    npc.reflectsProjectiles = true;

                if (npc.ai[0] == 1f) {
                    npc.ai[2] += 0.005f;
                    if (npc.ai[2] > 0.5f)
                        npc.ai[2] = 0.5f;
                }
                else {
                    npc.ai[2] -= 0.005f;
                    if (npc.ai[2] < 0f)
                        npc.ai[2] = 0f;
                }

                npc.rotation += npc.ai[2];

                float phaseChangeRate = death ? 2f : 1f;
                float servantSpawnGateValue = Main.getGoodWorld ? 4f : 20f;
                npc.ai[1] += phaseChangeRate;
                if (npc.ai[1] % servantSpawnGateValue == 0f) {
                    float servantVelocity = masterMode ? 11.3f : 5.65f;
                    Vector2 servantSpawnVelocity = Main.rand.NextVector2CircularEdge(servantVelocity, servantVelocity);
                    if (Main.getGoodWorld)
                        servantSpawnVelocity *= 3f;

                    Vector2 servantSpawnCenter = npc.Center + servantSpawnVelocity.SafeNormalize(Vector2.UnitY) * ProjectileOffset;
                    if (Main.netMode != NetmodeID.MultiplayerClient) {
                        int spawnType = NPCID.ServantofCthulhu;
                        if (masterMode) {
                            int maxBloodServants = 2;
                            bool spawnBloodServant = NPC.CountNPCS(ModContent.NPCType<BloodlettingServant>()) < maxBloodServants;
                            if (spawnBloodServant)
                                spawnType = ModContent.NPCType<BloodlettingServant>();
                        }

                        int servantSpawn = NPC.NewNPC(npc.GetSource_FromAI(), (int)servantSpawnCenter.X, (int)servantSpawnCenter.Y, spawnType, 0, 0f, 0f, enrageScale);
                        Main.npc[servantSpawn].velocity.X = servantSpawnVelocity.X;
                        Main.npc[servantSpawn].velocity.Y = servantSpawnVelocity.Y;

                        if (Main.netMode == NetmodeID.Server && servantSpawn < Main.maxNPCs)
                            NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, servantSpawn);

                        if (CalamityWorld.LegendaryMode) {
                            int type = ProjectileID.BloodNautilusShot;
                            Vector2 projectileVelocity = Main.rand.NextVector2CircularEdge(15f, 15f);
                            int numProj = 3;
                            int spread = 20;
                            float rotation = MathHelper.ToRadians(spread);
                            for (int i = 0; i < numProj; i++) {
                                Vector2 perturbedSpeed = projectileVelocity.RotatedBy(MathHelper.Lerp(-rotation, rotation, i / (float)(numProj - 1)));
                                int proj = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center + perturbedSpeed.SafeNormalize(Vector2.UnitY) * ProjectileOffset, perturbedSpeed, type, 15, 0f, Main.myPlayer);
                                Main.projectile[proj].timeLeft = 600;
                            }
                        }
                    }

                    for (int n = 0; n < 10; n++)
                        Dust.NewDust(servantSpawnCenter, 20, 20, DustID.Blood, servantSpawnVelocity.X * 0.4f, servantSpawnVelocity.Y * 0.4f, 0, default, 1f);
                }

                if (npc.ai[1] == 100f) {
                    npc.ai[0] += 1f;
                    npc.ai[1] = 0f;

                    if (npc.ai[0] == 3f) {
                        npc.ai[2] = 0f;
                    }
                    else {
                        SoundEngine.PlaySound(SoundID.NPCHit1, npc.Center);

                        if (Main.netMode != NetmodeID.Server) {
                            for (int phase2Gore = 0; phase2Gore < 2; phase2Gore++) {
                                Gore.NewGore(npc.GetSource_FromAI(), npc.position, new Vector2(Main.rand.Next(-30, 31) * 0.2f, Main.rand.Next(-30, 31) * 0.2f), 8, 1f);
                                Gore.NewGore(npc.GetSource_FromAI(), npc.position, new Vector2(Main.rand.Next(-30, 31) * 0.2f, Main.rand.Next(-30, 31) * 0.2f), 7, 1f);
                                Gore.NewGore(npc.GetSource_FromAI(), npc.position, new Vector2(Main.rand.Next(-30, 31) * 0.2f, Main.rand.Next(-30, 31) * 0.2f), 6, 1f);
                            }
                        }

                        for (int i = 0; i < 20; i++)
                            Dust.NewDust(npc.position, npc.width, npc.height, DustID.Blood, Main.rand.Next(-30, 31) * 0.2f, Main.rand.Next(-30, 31) * 0.2f, 0, default, 1f);

                        SoundEngine.PlaySound(SoundID.Roar, npc.Center);
                    }
                }

                Dust.NewDust(npc.position, npc.width, npc.height, DustID.Blood, Main.rand.Next(-30, 31) * 0.2f, Main.rand.Next(-30, 31) * 0.2f, 0, default, 1f);
                npc.velocity *= 0.98f;

                if (npc.velocity.X > -0.1 && npc.velocity.X < 0.1)
                    npc.velocity.X = 0f;
                if (npc.velocity.Y > -0.1 && npc.velocity.Y < 0.1)
                    npc.velocity.Y = 0f;
            }

            else {
                npc.defense = 0;
                int setDamage = (int)Math.Round(npc.defDamage * (phase3 ? 1.4 : 1.2));
                int reducedSetDamage = (int)Math.Round(setDamage * 0.5);

                if (npc.ai[1] == 0f & phase3)
                    npc.ai[1] = 5f;

                if (npc.ai[1] == 0f) {
                    // Deal less damage overall while not charging
                    npc.damage = reducedSetDamage;

                    float hoverSpeed = 5.5f + 3f * (phase2LifeRatio - lifeRatio);
                    float hoverAcceleration = 0.06f + 0.02f * (phase2LifeRatio - lifeRatio);
                    hoverSpeed += 4f * enrageScale;
                    hoverAcceleration += 0.04f * enrageScale;

                    if (death) {
                        hoverSpeed += 5.5f * (phase2LifeRatio - lifeRatio);
                        hoverAcceleration += 0.06f * (phase2LifeRatio - lifeRatio);
                    }

                    Vector2 hoverDestination = Main.player[npc.target].Center - Vector2.UnitY * 400f;
                    float distanceFromHoverDestination = npc.Distance(hoverDestination);

                    if (distanceFromHoverDestination > 400f) {
                        hoverSpeed += 1.25f;
                        hoverAcceleration += 0.075f;
                        if (distanceFromHoverDestination > 600f) {
                            hoverSpeed += 1.25f;
                            hoverAcceleration += 0.075f;
                            if (distanceFromHoverDestination > 800f) {
                                hoverSpeed += 1.25f;
                                hoverAcceleration += 0.075f;
                            }
                        }
                    }

                    if (Main.getGoodWorld) {
                        hoverSpeed += 1f;
                        hoverAcceleration += 0.1f;
                    }

                    float phaseLimit = (masterMode ? 160f : 200f) - (death ? 150f * (phase2LifeRatio - lifeRatio) : 0f);
                    bool timeToCharge = npc.ai[2] >= phaseLimit;
                    Vector2 idealHoverVelocity = npc.SafeDirectionTo(hoverDestination) * (hoverSpeed + (timeToCharge ? ((npc.ai[2] - phaseLimit) * 0.01f) : 0f));
                    npc.SimpleFlyMovement(idealHoverVelocity, hoverAcceleration + (timeToCharge ? ((npc.ai[2] - phaseLimit) * 0.001f) : 0f));

                    npc.ai[2] += 1f;
                    float projectileGateValue = (lifeRatio < 0.5f && death) ? (masterMode ? 40f : 50f) : (masterMode ? 60f : 80f);
                    if (npc.ai[2] % projectileGateValue == 0f && shootProjectile) {
                        Vector2 projectileVelocity = (Main.player[npc.target].Center - npc.Center).SafeNormalize(Vector2.UnitY) * servantAndProjectileVelocity * 2f;
                        Vector2 projectileSpawnCenter = npc.Center + projectileVelocity;
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            int type = ProjectileID.BloodNautilusShot;
                            int damage = npc.GetProjectileDamage(type);
                            int numProj = masterMode ? Main.rand.Next(4, 6) : 3;
                            int spread = masterMode ? numProj * 3 : 10;
                            float rotation = MathHelper.ToRadians(spread);
                            for (int i = 0; i < numProj; i++) {
                                Vector2 perturbedSpeed = projectileVelocity.RotatedBy(MathHelper.Lerp(-rotation, rotation, i / (float)(numProj - 1)));
                                int proj = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center + perturbedSpeed.SafeNormalize(Vector2.UnitY) * ProjectileOffset, perturbedSpeed, type, damage, 0f, Main.myPlayer);
                                Main.projectile[proj].timeLeft = 600;
                            }
                        }
                    }

                    if (timeToCharge && charge) {
                        npc.ai[1] = 1f;
                        npc.ai[2] = 0f;
                        npc.ai[3] = 0f;
                        npc.netUpdate = true;
                    }
                }

                else if (npc.ai[1] == 1f) {
                    // Set damage
                    npc.damage = setDamage;

                    SoundEngine.PlaySound(SoundID.ForceRoar, npc.Center);
                    npc.rotation = eyeRotation;

                    float additionalVelocityPerCharge = 3f;
                    float chargeSpeed = 10f + (3.5f * (phase2LifeRatio - lifeRatio)) + npc.ai[3] * additionalVelocityPerCharge;
                    chargeSpeed += 4f * enrageScale;
                    if (death)
                        chargeSpeed += 6.5f * (phase2LifeRatio - lifeRatio);
                    if (npc.ai[3] == 1f)
                        chargeSpeed *= 1.15f;
                    if (npc.ai[3] == 2f)
                        chargeSpeed *= 1.3f;
                    if (Main.getGoodWorld)
                        chargeSpeed *= 1.2f;

                    npc.velocity = npc.SafeDirectionTo(Main.player[npc.target].Center) * chargeSpeed;
                    npc.ai[1] = 2f;
                    npc.netUpdate = true;

                    if (npc.netSpam > 10)
                        npc.netSpam = 10;
                }

                else if (npc.ai[1] == 2f) {
                    // Set damage
                    npc.damage = setDamage;

                    int phase2ChargeDelay = masterMode ? 60 : 80;
                    if (death)
                        phase2ChargeDelay -= (int)Math.Round(35f * (phase2LifeRatio - lifeRatio));

                    float slowDownGateValue = phase2ChargeDelay * (death ? (masterMode ? 0.95f : 0.85f) : (masterMode ? 0.9f : 0.75f));

                    npc.ai[2] += 1f;
                    if (npc.ai[2] >= slowDownGateValue) {
                        // Deal less damage overall while not charging
                        npc.damage = reducedSetDamage;

                        float decelerationScalar = death ? ((lifeRatio - phase3LifeRatio) / (phase2LifeRatio - phase3LifeRatio)) : 1f;
                        if (decelerationScalar < 0f)
                            decelerationScalar = 0f;

                        npc.velocity *= (MathHelper.Lerp(masterMode ? 0.6f : 0.9f, masterMode ? 0.7f : 0.95f, decelerationScalar));
                        if (npc.velocity.X > -0.1 && npc.velocity.X < 0.1)
                            npc.velocity.X = 0f;
                        if (npc.velocity.Y > -0.1 && npc.velocity.Y < 0.1)
                            npc.velocity.Y = 0f;
                    }
                    else
                        npc.rotation = npc.velocity.ToRotation() - MathHelper.PiOver2;

                    if (npc.ai[2] >= phase2ChargeDelay) {
                        npc.ai[3] += 1f;
                        npc.ai[2] = 0f;
                        npc.rotation = eyeRotation;

                        float numCharges = death ? 4f : 3f;
                        if (npc.ai[3] >= numCharges) {
                            // Deal less damage overall while not charging
                            npc.damage = reducedSetDamage;

                            npc.ai[1] = 0f;
                            npc.ai[3] = 0f;
                            npc.netUpdate = true;

                            if (npc.netSpam > 10)
                                npc.netSpam = 10;
                        }
                        else
                            npc.ai[1] = 1f;
                    }
                }

                else if (npc.ai[1] == 3f) {
                    if ((npc.ai[3] == 4f & phase3) && npc.Center.Y > Main.player[npc.target].Center.Y) {
                        // Deal less damage overall while not charging
                        npc.damage = reducedSetDamage;

                        npc.ai[1] = 0f;
                        npc.ai[2] = 0f;
                        npc.ai[3] = 0f;
                        npc.netUpdate = true;

                        if (npc.netSpam > 10)
                            npc.netSpam = 10;
                    }
                    else if (Main.netMode != NetmodeID.MultiplayerClient) {
                        // Set damage
                        npc.damage = setDamage;

                        float speedBoost = death ? 10f * (phase3LifeRatio - lifeRatio) : 7f * (phase3LifeRatio - lifeRatio);
                        float finalChargeSpeed = 18f + speedBoost;
                        finalChargeSpeed += 10f * enrageScale;

                        Vector2 eyeChargeDirection = npc.Center;
                        float targetX = Main.player[npc.target].Center.X - eyeChargeDirection.X;
                        float targetY = Main.player[npc.target].Center.Y - eyeChargeDirection.Y;
                        float targetVelocity = Math.Abs(Main.player[npc.target].velocity.X) + Math.Abs(Main.player[npc.target].velocity.Y) / 4f;
                        targetVelocity += 10f - targetVelocity;

                        if (targetVelocity < (masterMode ? 2f : 5f))
                            targetVelocity = (masterMode ? 2f : 5f);
                        if (targetVelocity > (masterMode ? 6f : 15f))
                            targetVelocity = (masterMode ? 6f : 15f);

                        if (npc.ai[2] == -1f) {
                            targetVelocity *= 4f;
                            finalChargeSpeed *= 1.3f;
                        }

                        targetX -= Main.player[npc.target].velocity.X * targetVelocity;
                        targetY -= Main.player[npc.target].velocity.Y * targetVelocity / 4f;

                        float targetDistance = (float)Math.Sqrt(targetX * targetX + targetY * targetY);
                        float targetDistCopy = targetDistance;

                        targetDistance = finalChargeSpeed / targetDistance;
                        npc.velocity.X = targetX * targetDistance;
                        npc.velocity.Y = targetY * targetDistance;

                        if (targetDistCopy < 100f) {
                            if (Math.Abs(npc.velocity.X) > Math.Abs(npc.velocity.Y)) {
                                float absoluteXVel = Math.Abs(npc.velocity.X);
                                float absoluteYVel = Math.Abs(npc.velocity.Y);

                                if (npc.Center.X > Main.player[npc.target].Center.X)
                                    absoluteYVel *= -1f;
                                if (npc.Center.Y > Main.player[npc.target].Center.Y)
                                    absoluteXVel *= -1f;

                                npc.velocity.X = absoluteYVel;
                                npc.velocity.Y = absoluteXVel;
                            }
                        }
                        else if (Math.Abs(npc.velocity.X) > Math.Abs(npc.velocity.Y)) {
                            float absoluteEyeVel = (Math.Abs(npc.velocity.X) + Math.Abs(npc.velocity.Y)) / 2f;
                            float absoluteEyeVelBackup = absoluteEyeVel;

                            if (npc.Center.X > Main.player[npc.target].Center.X)
                                absoluteEyeVelBackup *= -1f;
                            if (npc.Center.Y > Main.player[npc.target].Center.Y)
                                absoluteEyeVel *= -1f;

                            npc.velocity.X = absoluteEyeVelBackup;
                            npc.velocity.Y = absoluteEyeVel;
                        }

                        npc.ai[1] = 4f;
                        npc.netUpdate = true;

                        if (npc.netSpam > 10)
                            npc.netSpam = 10;
                    }
                }

                else if (npc.ai[1] == 4f) {
                    // Set damage
                    npc.damage = setDamage;

                    if (npc.ai[2] == 0f)
                        SoundEngine.PlaySound(SoundID.ForceRoarPitched, npc.Center);

                    float lineUpDistControl = lineUpDist;
                    npc.ai[2] += 1f;

                    if (npc.ai[2] == lineUpDistControl && Vector2.Distance(npc.position, Main.player[npc.target].position) < 200f)
                        npc.ai[2] -= 1f;

                    if (npc.ai[2] >= lineUpDistControl) {
                        // Deal less damage overall while not charging
                        npc.damage = reducedSetDamage;

                        npc.velocity *= 0.95f;
                        if (npc.velocity.X > -0.1 && npc.velocity.X < 0.1)
                            npc.velocity.X = 0f;
                        if (npc.velocity.Y > -0.1 && npc.velocity.Y < 0.1)
                            npc.velocity.Y = 0f;
                    }
                    else
                        npc.rotation = npc.velocity.ToRotation() - MathHelper.PiOver2;

                    float lineUpDistNetUpdate = lineUpDistControl + 13f;
                    if (npc.ai[2] >= lineUpDistNetUpdate) {
                        npc.netUpdate = true;

                        if (npc.netSpam > 10)
                            npc.netSpam = 10;

                        npc.ai[3] += 1f;
                        npc.ai[2] = 0f;

                        float maxCharges = death ? (finalPhaseDeath ? 0f : penultimatePhaseDeath ? 1f : 2f) : finalPhaseRev ? 2f : 3f;
                        if (npc.ai[3] >= maxCharges) {
                            // Deal less damage overall while not charging
                            npc.damage = reducedSetDamage;

                            npc.ai[1] = 0f;
                            npc.ai[3] = 0f;
                        }
                        else
                            npc.ai[1] = 3f;
                    }
                }

                else if (npc.ai[1] == 5f) {
                    // Deal less damage overall while not charging
                    npc.damage = reducedSetDamage;

                    float offset = death ? 540f : 600f;
                    float speedBoost = death ? 15f * (phase3LifeRatio - lifeRatio) : 5f * (phase3LifeRatio - lifeRatio);
                    float accelerationBoost = death ? 0.425f * (phase3LifeRatio - lifeRatio) : 0.125f * (phase3LifeRatio - lifeRatio);
                    float hoverSpeed = 8f + speedBoost;
                    float hoverAcceleration = 0.25f + accelerationBoost;

                    bool horizontalCharge = calNPC.newAI[0] == 1f || calNPC.newAI[0] == 3f;
                    float timeGateValue = horizontalCharge ? (100f - (death ? 80f * (phase3LifeRatio - lifeRatio) : 0f)) : (85f - (death ? 70f * (phase3LifeRatio - lifeRatio) : 0f));
                    if (npc.ai[2] > timeGateValue) {
                        float velocityScalar = npc.ai[2] - timeGateValue;
                        hoverSpeed += velocityScalar * 0.05f;
                        hoverAcceleration += velocityScalar * 0.0025f;
                    }

                    hoverSpeed += enrageScale * 4f;
                    hoverAcceleration += enrageScale * 0.125f;

                    Vector2 eyeLineUpChargeDirection = npc.Center;
                    float lineUpChargeTargetX = Main.player[npc.target].Center.X - eyeLineUpChargeDirection.X;
                    float lineUpChargeTargetY = Main.player[npc.target].Center.Y + offset - eyeLineUpChargeDirection.Y;
                    Vector2 hoverDestination = Main.player[npc.target].Center + Vector2.UnitY * offset;

                    if (horizontalCharge) {
                        float horizontalChargeOffset = death ? 450f : 500f;
                        offset = calNPC.newAI[0] == 1f ? -horizontalChargeOffset : horizontalChargeOffset;
                        hoverSpeed *= 1.5f;
                        hoverAcceleration *= 1.5f;
                        hoverDestination = Main.player[npc.target].Center + Vector2.UnitX * offset;
                    }

                    Vector2 idealHoverVelocity = npc.SafeDirectionTo(hoverDestination) * hoverSpeed;
                    npc.SimpleFlyMovement(idealHoverVelocity, hoverAcceleration);

                    float servantSpawnGateValue = horizontalCharge ? (death ? 23f : 35f) : (death ? 17f : 27f);
                    float maxServantSpawnsPerAttack = 2f;

                    npc.ai[2] += 1f;
                    if (npc.ai[2] % servantSpawnGateValue == 0f && shootProjectile && npc.ai[2] <= servantSpawnGateValue * maxServantSpawnsPerAttack) {
                        Vector2 servantSpawnVelocity = (Main.player[npc.target].Center - npc.Center).SafeNormalize(Vector2.UnitY) * servantAndProjectileVelocity;
                        Vector2 servantSpawnCenter = npc.Center + servantSpawnVelocity.SafeNormalize(Vector2.UnitY) * ProjectileOffset;

                        int spawnType = NPCID.ServantofCthulhu;
                        bool spawnServant = false;
                        float enrageScaleToPass = enrageScale;
                        if (masterMode) {
                            int maxBloodServants = 2;
                            bool spawnBloodServant = NPC.CountNPCS(ModContent.NPCType<BloodlettingServant>()) < maxBloodServants;
                            if (spawnBloodServant) {
                                spawnType = ModContent.NPCType<BloodlettingServant>();
                                spawnServant = true;
                                enrageScaleToPass += 0.5f;
                            }
                            else {
                                int maxServants = death ? 1 : 2;
                                spawnServant = (penultimatePhaseDeath || finalPhaseRev) ? false : NPC.CountNPCS(NPCID.ServantofCthulhu) < maxServants;
                            }
                        }
                        else {
                            int maxServants = death ? (finalPhaseDeath ? 1 : penultimatePhaseDeath ? 2 : 3) : (finalPhaseRev ? 2 : 4);
                            spawnServant = NPC.CountNPCS(NPCID.ServantofCthulhu) < maxServants;
                        }

                        if (spawnServant)
                            SoundEngine.PlaySound(SoundID.NPCDeath13, servantSpawnCenter);

                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            if (spawnServant) {
                                int eye = NPC.NewNPC(npc.GetSource_FromAI(), (int)servantSpawnCenter.X, (int)servantSpawnCenter.Y, spawnType, 0, 0f, 0f, enrageScaleToPass);
                                Main.npc[eye].velocity.X = servantSpawnVelocity.X;
                                Main.npc[eye].velocity.Y = servantSpawnVelocity.Y;

                                if (Main.netMode == NetmodeID.Server && eye < Main.maxNPCs)
                                    NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, eye);
                            }
                            else if (!CalamityWorld.LegendaryMode) {
                                int projType = ProjectileID.BloodNautilusShot;
                                int projDamage = npc.GetProjectileDamage(projType);
                                int proj = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center + servantSpawnVelocity.SafeNormalize(Vector2.UnitY) * ProjectileOffset, servantSpawnVelocity * 2f, projType, projDamage, 0f, Main.myPlayer);
                                Main.projectile[proj].timeLeft = 600;
                            }

                            if (CalamityWorld.LegendaryMode) {
                                int type = ProjectileID.BloodNautilusShot;
                                Vector2 projectileVelocity = servantSpawnVelocity * 3f;
                                int numProj = 3;
                                int spread = 20;
                                float rotation = MathHelper.ToRadians(spread);
                                for (int i = 0; i < numProj; i++) {
                                    Vector2 perturbedSpeed = projectileVelocity.RotatedBy(MathHelper.Lerp(-rotation, rotation, i / (float)(numProj - 1)));
                                    int proj = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center + perturbedSpeed.SafeNormalize(Vector2.UnitY) * ProjectileOffset, perturbedSpeed, type, 15, 0f, Main.myPlayer);
                                    Main.projectile[proj].timeLeft = 600;
                                }
                            }
                        }

                        if (spawnServant) {
                            for (int m = 0; m < 10; m++)
                                Dust.NewDust(servantSpawnCenter, 20, 20, DustID.Blood, servantSpawnVelocity.X * 0.4f, servantSpawnVelocity.Y * 0.4f, 0, default, 1f);
                        }
                    }

                    float requiredDistanceForHorizontalCharge = 160f;
                    if (npc.ai[2] >= timeGateValue && (npc.Distance(hoverDestination) < requiredDistanceForHorizontalCharge || !horizontalCharge)) {
                        switch ((int)calNPC.newAI[0]) {
                            case 0: // Normal Eye behavior
                                npc.ai[1] = 3f;
                                npc.ai[2] = -1f;
                                npc.ai[3] = -1f;
                                break;

                            case 1: // Charge from the left
                                npc.ai[1] = 6f;
                                npc.ai[2] = 0f;
                                break;

                            case 2: // Normal Eye behavior
                                npc.ai[1] = 3f;
                                npc.ai[2] = -1f;
                                break;

                            case 3: // Charge from the right
                                npc.ai[1] = 6f;
                                npc.ai[2] = 0f;
                                break;

                            default:
                                break;
                        }

                        calNPC.newAI[0] += ((masterMode && calNPC.newAI[0] % 2f != 0f) ? Main.rand.Next(2) + 1f : 1f);
                        if (calNPC.newAI[0] > 3f)
                            calNPC.newAI[0] = masterMode ? Main.rand.Next(2) : 0f;

                        npc.SyncExtraAI();
                    }

                    npc.netUpdate = true;

                    if (npc.netSpam > 10)
                        npc.netSpam = 10;
                }

                else if (npc.ai[1] == 6f) {
                    // Set damage
                    npc.damage = setDamage;

                    if (Main.netMode != NetmodeID.MultiplayerClient) {
                        float speedBoost = death ? 15f * (phase3LifeRatio - lifeRatio) : 5f * (phase3LifeRatio - lifeRatio);
                        float chargeSpeed = 18f + speedBoost;
                        chargeSpeed += 10f * enrageScale;
                        npc.velocity = npc.SafeDirectionTo(Main.player[npc.target].Center) * chargeSpeed;

                        npc.ai[1] = 7f;
                        npc.netUpdate = true;

                        if (npc.netSpam > 10)
                            npc.netSpam = 10;
                    }
                }

                else if (npc.ai[1] == 7f) {
                    // Set damage
                    npc.damage = setDamage;

                    if (npc.ai[2] == 0f)
                        SoundEngine.PlaySound(SoundID.ForceRoar, npc.Center);

                    float lineUpDistControl = (float)Math.Round(lineUpDist * 2.5f);
                    npc.ai[2] += 1f;

                    if (npc.ai[2] == lineUpDistControl && Vector2.Distance(npc.position, Main.player[npc.target].position) < 200f)
                        npc.ai[2] -= 1f;

                    if (npc.ai[2] >= lineUpDistControl) {
                        // Deal less damage overall while not charging
                        npc.damage = reducedSetDamage;

                        npc.velocity *= 0.95f;
                        if (npc.velocity.X > -0.1 && npc.velocity.X < 0.1)
                            npc.velocity.X = 0f;
                        if (npc.velocity.Y > -0.1 && npc.velocity.Y < 0.1)
                            npc.velocity.Y = 0f;
                    }
                    else
                        npc.rotation = npc.velocity.ToRotation() - MathHelper.PiOver2;

                    float lineUpDistNetUpdate = lineUpDistControl + 13f;
                    if (npc.ai[2] >= lineUpDistNetUpdate) {
                        // Deal less damage overall while not charging
                        npc.damage = reducedSetDamage;

                        npc.netUpdate = true;

                        if (npc.netSpam > 10)
                            npc.netSpam = 10;

                        npc.ai[2] = 0f;
                        npc.ai[1] = 0f;
                    }
                }
            }

            return false;
        }
    }
}
