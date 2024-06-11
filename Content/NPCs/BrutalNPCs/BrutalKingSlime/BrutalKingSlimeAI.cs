using CalamityMod;
using CalamityMod.Events;
using CalamityMod.NPCs;
using CalamityMod.NPCs.NormalNPCs;
using CalamityMod.World;
using CalamityOverhaul.Content.NPCs.Core;
using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime
{
    /*
    internal class BrutalKingSlimeAI : NPCCoverage
    {
        public override int TargetID => NPCID.KingSlime;

        private bool bossRush => BossRushEvent.BossRushActive;

        private bool masterMode => Main.masterMode || bossRush;

        private bool death => CalamityWorld.death || bossRush;

        private bool crystalAlive = true;
        private bool blueCrystalAlive = false;
        private bool greenCrystalAlive = true;
        private float lifeRatio;
        private float lifeRatio2;
        private float teleportScale = 1f;
        private float teleportScaleSpeed;
        private float teleportGateValue;
        private bool teleporting = false;
        private bool teleported = false;
        private bool phase2;
        private bool phase3;
        private Color dustColor;
        private int setDamage;
        private NPC NPC;

        public override bool CanLoad() {
            return false;
        }

        public override bool? AI(NPC foremNPC, Mod mod) {
            NPC = foremNPC;
            // Percent life remaining
            lifeRatio = NPC.life / (float)NPC.lifeMax;
            lifeRatio2 = lifeRatio;

            // Variables
            teleportScale = 1f;
            teleporting = false;
            teleported = false;
            NPC.aiAction = 0;
            teleportScaleSpeed = 2f;
            if (Main.getGoodWorld) {
                teleportScaleSpeed -= 1f - lifeRatio;
                teleportScale *= teleportScaleSpeed;
            }

            // Get a target
            if (NPC.target < 0 || NPC.target == Main.maxPlayers || Main.player[NPC.target].dead || !Main.player[NPC.target].active)
                NPC.TargetClosest();

            // Despawn safety, make sure to target another player if the current player target is too far away
            if (Vector2.Distance(Main.player[NPC.target].Center, NPC.Center) > CalamityGlobalNPC.CatchUpDistance200Tiles)
                NPC.TargetClosest();

            // Phases based on life percentage

            // Higher velocity jumps phase
            phase2 = lifeRatio < 0.75f;

            // Spawn Crystal phase
            phase3 = lifeRatio < 0.5f;

            // Check if the crystals are alive
            crystalAlive = true;
            blueCrystalAlive = false;
            greenCrystalAlive = true;
            if (phase3) {
                crystalAlive = NPC.AnyNPCs(ModContent.NPCType<KingSlimeJewelEmerald>());
                if (masterMode) {
                    blueCrystalAlive = NPC.AnyNPCs(ModContent.NPCType<KingSlimeJewelRuby>());
                    greenCrystalAlive = NPC.AnyNPCs(ModContent.NPCType<KingSlimeJewelSapphire>());
                }
            }

            // Sapphire Crystal buffs
            setDamage = NPC.defDamage;
            NPC.defense = NPC.defDefense;
            if (blueCrystalAlive) {
                setDamage = (int)Math.Round(setDamage * 1.5);
                NPC.defense *= 2;
            }

            // Dust color when the blue crystal is alive
            dustColor = Color.Lerp(new Color(0, 0, 150, NPC.alpha), new Color(125, 125, 255, NPC.alpha), (float)Math.Sin(Main.GlobalTimeWrappedHourly) / 2f + 0.5f);

            // Spawn crystal in phase 2
            if (phase3 && NPC.Calamity().newAI[0] == 0f) {
                NPC.Calamity().newAI[0] = 1f;
                NPC.SyncExtraAI();
                Vector2 vector = NPC.Center + new Vector2(-40f, -(float)NPC.height / 2) * NPC.scale;
                int totalDustPerCrystalSpawn = 20;
                for (int i = 0; i < totalDustPerCrystalSpawn; i++) {
                    int rubyDust = Dust.NewDust(vector, NPC.width / 2, NPC.height / 2, DustID.GemRuby, 0f, 0f, 100, default, 2f);
                    Main.dust[rubyDust].velocity *= 2f;
                    Main.dust[rubyDust].noGravity = true;
                    if (Main.rand.NextBool()) {
                        Main.dust[rubyDust].scale = 0.5f;
                        Main.dust[rubyDust].fadeIn = 1f + Main.rand.Next(10) * 0.1f;
                    }
                }

                if (masterMode) {
                    for (int i = 0; i < totalDustPerCrystalSpawn; i++) {
                        int sapphireDust = Dust.NewDust(vector, NPC.width / 2, NPC.height / 2, DustID.GemSapphire, 0f, 0f, 100, default, 2f);
                        Main.dust[sapphireDust].velocity *= 2f;
                        Main.dust[sapphireDust].noGravity = true;
                        if (Main.rand.NextBool()) {
                            Main.dust[sapphireDust].scale = 0.5f;
                            Main.dust[sapphireDust].fadeIn = 1f + Main.rand.Next(10) * 0.1f;
                        }
                    }

                    for (int i = 0; i < totalDustPerCrystalSpawn; i++) {
                        int emeraldDust = Dust.NewDust(vector, NPC.width / 2, NPC.height / 2, DustID.GemEmerald, 0f, 0f, 100, default, 2f);
                        Main.dust[emeraldDust].velocity *= 2f;
                        Main.dust[emeraldDust].noGravity = true;
                        if (Main.rand.NextBool()) {
                            Main.dust[emeraldDust].scale = 0.5f;
                            Main.dust[emeraldDust].fadeIn = 1f + Main.rand.Next(10) * 0.1f;
                        }
                    }
                }

                SoundEngine.PlaySound(SoundID.Item38, vector);

                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    NPC.NewNPC(NPC.GetSource_FromAI(), (int)vector.X, (int)vector.Y, ModContent.NPCType<KingSlimeJewelEmerald>());
                    if (masterMode) {
                        NPC.NewNPC(NPC.GetSource_FromAI(), (int)vector.X, (int)vector.Y, ModContent.NPCType<KingSlimeJewelRuby>());
                        NPC.NewNPC(NPC.GetSource_FromAI(), (int)vector.X, (int)vector.Y, ModContent.NPCType<KingSlimeJewelSapphire>());
                    }
                }
            }

            // Set up health value for spawning slimes
            if (NPC.ai[3] == 0f && NPC.life > 0)
                NPC.ai[3] = NPC.lifeMax;

            // Spawn with attack delay
            if (NPC.localAI[3] == 0f && Main.netMode != NetmodeID.MultiplayerClient) {
                NPC.ai[0] = -100f;
                NPC.localAI[3] = 1f;
                NPC.netUpdate = true;
            }

            // Despawn
            int despawnDistance = 500;
            if (Main.player[NPC.target].dead || Math.Abs(NPC.Center.X - Main.player[NPC.target].Center.X) / 16f > despawnDistance) {
                NPC.TargetClosest();
                if (Main.player[NPC.target].dead || Math.Abs(NPC.Center.X - Main.player[NPC.target].Center.X) / 16f > despawnDistance) {
                    if (NPC.timeLeft > 10)
                        NPC.timeLeft = 10;

                    if (Main.player[NPC.target].Center.X < NPC.Center.X)
                        NPC.direction = 1;
                    else
                        NPC.direction = -1;
                }
            }

            // Faster fall
            if (NPC.velocity.Y > 0f) {
                float fallSpeedBonus = (bossRush ? 0.1f : death ? 0.05f : 0f) + (!crystalAlive ? 0.1f : 0f) + (masterMode ? 0.1f : 0f);
                NPC.velocity.Y += fallSpeedBonus;
            }

            // Activate teleport
            ActivateTeleport();

            if (!Collision.CanHitLine(NPC.Center, 0, 0, Main.player[NPC.target].Center, 0, 0) || Math.Abs(NPC.Top.Y - Main.player[NPC.target].Bottom.Y) > 160f) {
                if (Main.netMode != NetmodeID.MultiplayerClient)
                    NPC.localAI[0] += 1f;
            }
            else if (Main.netMode != NetmodeID.MultiplayerClient) {
                NPC.localAI[0] -= 1f;

                if (NPC.localAI[0] < 0f)
                    NPC.localAI[0] = 0f;
            }

            if (NPC.timeLeft < 10 && (NPC.ai[0] != 0f || NPC.ai[1] != 0f)) {
                NPC.ai[0] = 0f;
                NPC.ai[1] = 0f;
                NPC.netUpdate = true;
                teleporting = false;
            }

            // Get closer to activating teleport
            if (NPC.ai[2] < teleportGateValue) {
                if (!Collision.CanHitLine(NPC.Center, 0, 0, Main.player[NPC.target].Center, 0, 0) || Math.Abs(NPC.Top.Y - Main.player[NPC.target].Bottom.Y) > (masterMode ? 160f : 320f))
                    NPC.ai[2] += death ? 3f : 2f;
                else
                    NPC.ai[2] += 1f;
            }

            Teleport();

            NPC.noTileCollide = false;

            // Jump
            if (NPC.velocity.Y == 0f) {
                Jump();
            }

            // Change jump velocity
            else if (NPC.target < Main.maxPlayers) {
                ChangeJumpVelocity();
            }

            int idleSlimeDust = Dust.NewDust(NPC.position, NPC.width, NPC.height, blueCrystalAlive ? DustID.GemSapphire : DustID.TintableDust, NPC.velocity.X, NPC.velocity.Y, blueCrystalAlive ? 100 : 255, blueCrystalAlive ? dustColor : new Color(0, 80, 255, 80), NPC.scale * 1.2f);
            Main.dust[idleSlimeDust].noGravity = true;
            Main.dust[idleSlimeDust].velocity *= blueCrystalAlive ? 0f : 0.5f;

            if (NPC.life <= 0)
                return false;

            // Adjust size based on HP
            float maxScale = death ? (Main.getGoodWorld ? 6f : 3f) : (Main.getGoodWorld ? 3f : 1.25f);
            float minScale = death ? 0.5f : 0.75f;
            float maxScaledValue = maxScale - minScale;

            // Inversed scale in FTW
            if (Main.getGoodWorld)
                lifeRatio = (maxScaledValue - lifeRatio * maxScaledValue) + minScale;
            else
                lifeRatio = lifeRatio * maxScaledValue + minScale;

            lifeRatio *= teleportScale;
            if (lifeRatio != NPC.scale) {
                NPC.position.X += NPC.width / 2;
                NPC.position.Y += NPC.height;
                NPC.scale = lifeRatio;
                NPC.width = (int)(98f * NPC.scale);
                NPC.height = (int)(92f * NPC.scale);
                NPC.position.X -= NPC.width / 2;
                NPC.position.Y -= NPC.height;
            }

            SlimeSpawning();
            return false;
        }

        private void ActivateTeleport() {
            teleportGateValue = 480f;
            if (!Main.player[NPC.target].dead && NPC.ai[2] >= teleportGateValue && NPC.ai[1] < 5f && NPC.velocity.Y == 0f) {
                // Avoid cheap bullshit
                NPC.damage = 0;

                NPC.ai[2] = 0f;
                NPC.ai[0] = 0f;
                NPC.ai[1] = 5f;

                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    NPC.TargetClosest(false);
                    float distanceAhead = 800f;
                    Vector2 randomDefault = Main.rand.NextBool() ? Vector2.UnitX : -Vector2.UnitX;
                    Vector2 vectorAimedAheadOfTarget = Main.player[NPC.target].Center + new Vector2((float)Math.Round(Main.player[NPC.target].velocity.X), 0f).SafeNormalize(randomDefault) * distanceAhead;
                    Point predictiveTeleportPoint = vectorAimedAheadOfTarget.ToTileCoordinates();
                    int randomPredictiveTeleportOffset = 5;
                    int teleportTries = 0;
                    while (teleportTries < 100) {
                        teleportTries++;
                        int teleportTileX = Main.rand.Next(predictiveTeleportPoint.X - randomPredictiveTeleportOffset, predictiveTeleportPoint.X + randomPredictiveTeleportOffset + 1);
                        int teleportTileY = Main.rand.Next(predictiveTeleportPoint.Y - randomPredictiveTeleportOffset, predictiveTeleportPoint.Y);

                        if (!Main.tile[teleportTileX, teleportTileY].HasUnactuatedTile) {
                            bool canTeleportToTile = true;
                            if (canTeleportToTile && Main.tile[teleportTileX, teleportTileY].LiquidType == LiquidID.Lava)
                                canTeleportToTile = false;
                            if (canTeleportToTile && !Collision.CanHitLine(NPC.Center, 0, 0, predictiveTeleportPoint.ToVector2() * 16, 0, 0))
                                canTeleportToTile = false;

                            if (canTeleportToTile) {
                                NPC.localAI[1] = teleportTileX * 16 + 8;
                                NPC.localAI[2] = teleportTileY * 16 + 16;
                                break;
                            }
                            else
                                predictiveTeleportPoint.X += predictiveTeleportPoint.X < 0f ? 1 : -1;
                        }
                        else
                            predictiveTeleportPoint.X += predictiveTeleportPoint.X < 0f ? 1 : -1;
                    }

                    // Default teleport if the above conditions aren't met in 100 iterations
                    if (teleportTries >= 100) {
                        Vector2 bottom = Main.player[Player.FindClosest(NPC.position, NPC.width, NPC.height)].Bottom;
                        NPC.localAI[1] = bottom.X;
                        NPC.localAI[2] = bottom.Y;
                    }
                }
            }
        }

        private void Teleport() {
            // Teleport
            if (NPC.ai[1] == 5f) {
                // Avoid cheap bullshit
                NPC.damage = 0;

                teleporting = true;
                NPC.aiAction = 1;

                float teleportRate = crystalAlive ? 1f : 2f;
                if (masterMode)
                    teleportRate *= 2f;

                NPC.ai[0] += teleportRate;
                teleportScale = MathHelper.Clamp((60f - NPC.ai[0]) / 60f, 0f, 1f);
                teleportScale = 0.5f + teleportScale * 0.5f;
                if (Main.getGoodWorld)
                    teleportScale *= teleportScaleSpeed;

                if (NPC.ai[0] >= 60f)
                    teleported = true;

                if (NPC.ai[0] == 60f && Main.netMode != NetmodeID.Server)
                    Gore.NewGore(NPC.GetSource_FromAI(), NPC.Center + new Vector2(-40f, -(float)NPC.height / 2), NPC.velocity, 734, 1f);

                if (NPC.ai[0] >= 60f && Main.netMode != NetmodeID.MultiplayerClient) {
                    NPC.Bottom = new Vector2(NPC.localAI[1], NPC.localAI[2]);
                    NPC.ai[1] = 6f;
                    NPC.ai[0] = 0f;
                    NPC.netUpdate = true;
                }

                if (Main.netMode == NetmodeID.MultiplayerClient && NPC.ai[0] >= 120f) {
                    NPC.ai[1] = 6f;
                    NPC.ai[0] = 0f;
                }

                if (!teleported) {
                    for (int i = 0; i < 10; i++) {
                        int slimeDust = Dust.NewDust(NPC.position + Vector2.UnitX * -20f, NPC.width + 40, NPC.height, blueCrystalAlive ? DustID.GemSapphire : DustID.TintableDust, NPC.velocity.X, NPC.velocity.Y, blueCrystalAlive ? 100 : 150, blueCrystalAlive ? dustColor : new Color(78, 136, 255, 80), 2f);
                        Main.dust[slimeDust].noGravity = true;
                        Main.dust[slimeDust].velocity *= blueCrystalAlive ? 0f : 0.5f;
                    }
                }
            }
            // Post-teleport
            else if (NPC.ai[1] == 6f) {
                // Avoid cheap bullshit
                NPC.damage = 0;

                teleporting = true;
                NPC.aiAction = 0;

                float teleportRate = crystalAlive ? 1f : 2f;
                if (masterMode)
                    teleportRate *= 2f;

                NPC.ai[0] += teleportRate;
                teleportScale = MathHelper.Clamp(NPC.ai[0] / 30f, 0f, 1f);
                teleportScale = 0.5f + teleportScale * 0.5f;
                if (Main.getGoodWorld)
                    teleportScale *= teleportScaleSpeed;

                if (NPC.ai[0] >= 30f && Main.netMode != NetmodeID.MultiplayerClient) {
                    NPC.ai[1] = 0f;
                    NPC.ai[0] = -15f;
                    NPC.netUpdate = true;
                    NPC.TargetClosest();
                }

                if (Main.netMode == NetmodeID.MultiplayerClient && NPC.ai[0] >= 60f) {
                    NPC.ai[1] = 0f;
                    NPC.ai[0] = -15f;
                    NPC.TargetClosest();
                }

                for (int j = 0; j < 10; j++) {
                    int slimyDust = Dust.NewDust(NPC.position + Vector2.UnitX * -20f, NPC.width + 40, NPC.height, blueCrystalAlive ? DustID.GemSapphire : DustID.TintableDust, NPC.velocity.X, NPC.velocity.Y, blueCrystalAlive ? 100 : 150, blueCrystalAlive ? dustColor : new Color(78, 136, 255, 80), 2f);
                    Main.dust[slimyDust].noGravity = true;
                    Main.dust[slimyDust].velocity *= blueCrystalAlive ? 0f : 2f;
                }
            }
        }

        private void Jump() {
            // Avoid cheap bullshit
            NPC.damage = 0;

            NPC.velocity.X *= 0.8f;
            if (NPC.velocity.X > -0.1f && NPC.velocity.X < 0.1f)
                NPC.velocity.X = 0f;

            if (!teleporting) {
                NPC.ai[0] += (bossRush ? 15f : MathHelper.Lerp(1f, 8f, 1f - lifeRatio));
                if (NPC.ai[0] >= 0f) {
                    // Set damage
                    NPC.damage = setDamage;

                    NPC.netUpdate = true;
                    NPC.TargetClosest();

                    float distanceBelowTarget = NPC.position.Y - (Main.player[NPC.target].position.Y + 80f);
                    float speedMult = 1f;
                    if (distanceBelowTarget > 0f)
                        speedMult += distanceBelowTarget * 0.002f;

                    if (speedMult > 2f)
                        speedMult = 2f;

                    bool deathModeRapidHops = death && lifeRatio < 0.3f;
                    if (deathModeRapidHops)
                        NPC.ai[1] = 2f;

                    float bossRushJumpSpeedMult = 1.5f;

                    // Jump type
                    if (NPC.ai[1] == 3f) {
                        NPC.velocity.Y = -10f * speedMult;
                        NPC.velocity.X += (phase2 ? (death ? 5.5f : 4.5f) : 3.5f) * NPC.direction;
                        NPC.ai[0] = -100f;
                        NPC.ai[1] = 0f;
                    }
                    else if (NPC.ai[1] == 2f) {
                        NPC.velocity.Y = -6f * speedMult;
                        NPC.velocity.X += (phase2 ? (deathModeRapidHops ? 8f : death ? 6.5f : 5.5f) : 4.5f) * NPC.direction;
                        NPC.ai[0] = -60f;

                        // Use the quick forward jump over and over while at low HP in death mode
                        if (!deathModeRapidHops)
                            NPC.ai[1] += 1f;
                    }
                    else {
                        NPC.velocity.Y = -8f * speedMult;
                        NPC.velocity.X += (phase2 ? (death ? 6f : 5f) : 4f) * NPC.direction;
                        NPC.ai[0] = -60f;
                        NPC.ai[1] += 1f;
                    }

                    if (!greenCrystalAlive) {
                        NPC.velocity.X *= 1.2f;
                        NPC.velocity.Y *= 0.6f;
                    }

                    if (masterMode)
                        NPC.velocity.X *= 1.4f;

                    if (bossRush)
                        NPC.velocity.X *= bossRushJumpSpeedMult;

                    NPC.noTileCollide = true;
                }
                else if (NPC.ai[0] >= -30f)
                    NPC.aiAction = 1;
            }
        }

        private void ChangeJumpVelocity() {
            float jumpVelocityLimit = crystalAlive ? 3f : 4.5f;
            if (masterMode)
                jumpVelocityLimit += 3f;
            if (Main.getGoodWorld)
                jumpVelocityLimit = 8f;

            if ((NPC.direction == 1 && NPC.velocity.X < jumpVelocityLimit) || (NPC.direction == -1 && NPC.velocity.X > -jumpVelocityLimit)) {
                if ((NPC.direction == -1 && NPC.velocity.X < 0.1) || (NPC.direction == 1 && NPC.velocity.X > -0.1)) {
                    NPC.velocity.X += (bossRush ? 0.4f : death ? 0.25f : 0.2f) * NPC.direction;
                    if (masterMode)
                        NPC.velocity.X += 0.3f * NPC.direction;
                }
                else {
                    NPC.velocity.X *= bossRush ? 0.9f : death ? 0.92f : 0.93f;
                    if (masterMode)
                        NPC.velocity.X *= 0.9f;
                }
            }

            if (!Main.player[NPC.target].dead) {
                if (NPC.velocity.Y > 0f && NPC.Bottom.Y > Main.player[NPC.target].Top.Y)
                    NPC.noTileCollide = false;
                else if (Collision.CanHit(NPC.position, NPC.width, NPC.height, Main.player[NPC.target].Center, 1, 1) && !Collision.SolidCollision(NPC.position, NPC.width, NPC.height))
                    NPC.noTileCollide = false;
                else
                    NPC.noTileCollide = true;
            }
        }

        private void SlimeSpawning() {
            // Slime spawning
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                int slimeSpawnThreshold = (int)(NPC.lifeMax * 0.03);
                if (NPC.life + slimeSpawnThreshold < NPC.ai[3]) {
                    NPC.ai[3] = NPC.life;
                    int slimeAmt = Main.rand.Next(1, 3);
                    for (int i = 0; i < slimeAmt; i++) {
                        float minLowerLimit = death ? 5f : 0f;
                        float maxLowerLimit = death ? 7f : 2f;
                        int minTypeChoice = (int)MathHelper.Lerp(minLowerLimit, 7f, 1f - lifeRatio2);
                        int maxTypeChoice = (int)MathHelper.Lerp(maxLowerLimit, 9f, 1f - lifeRatio2);

                        int npcType;
                        switch (Main.rand.Next(minTypeChoice, maxTypeChoice + 1)) {
                            default:
                                npcType = NPCID.SlimeSpiked;
                                break;
                            case 0:
                                npcType = NPCID.GreenSlime;
                                break;
                            case 1:
                                npcType = Main.raining ? NPCID.UmbrellaSlime : NPCID.BlueSlime;
                                break;
                            case 2:
                                npcType = NPCID.IceSlime;
                                break;
                            case 3:
                                npcType = NPCID.RedSlime;
                                break;
                            case 4:
                                npcType = NPCID.PurpleSlime;
                                break;
                            case 5:
                                npcType = NPCID.YellowSlime;
                                break;
                            case 6:
                                npcType = NPCID.SlimeSpiked;
                                break;
                            case 7:
                                npcType = NPCID.SpikedIceSlime;
                                break;
                            case 8:
                                npcType = NPCID.SpikedJungleSlime;
                                break;
                        }

                        if (((Main.raining && Main.hardMode) || bossRush) && Main.rand.NextBool(50))
                            npcType = NPCID.RainbowSlime;

                        if (masterMode)
                            npcType = Main.rand.NextBool() ? NPCID.SpikedIceSlime : NPCID.SpikedJungleSlime;

                        if (Main.rand.NextBool(100))
                            npcType = NPCID.Pinky;

                        if (CalamityWorld.LegendaryMode)
                            npcType = NPCID.RainbowSlime;

                        int spawnZoneWidth = NPC.width - 32;
                        int spawnZoneHeight = NPC.height - 32;
                        int x = (int)(NPC.position.X + Main.rand.Next(spawnZoneWidth));
                        int y = (int)(NPC.position.Y + Main.rand.Next(spawnZoneHeight));
                        int slimeSpawns = NPC.NewNPC(NPC.GetSource_FromAI(), x, y, npcType);
                        Main.npc[slimeSpawns].SetDefaults(npcType);
                        Main.npc[slimeSpawns].velocity.X = Main.rand.Next(-15, 16) * 0.1f;
                        Main.npc[slimeSpawns].velocity.Y = Main.rand.Next(-30, 31) * 0.1f;
                        Main.npc[slimeSpawns].ai[0] = -1000 * Main.rand.Next(3);
                        Main.npc[slimeSpawns].ai[1] = 0f;

                        if (Main.netMode == NetmodeID.Server && slimeSpawns < Main.maxNPCs)
                            NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, slimeSpawns);
                    }
                }
            }
        }
    }
    */
}
