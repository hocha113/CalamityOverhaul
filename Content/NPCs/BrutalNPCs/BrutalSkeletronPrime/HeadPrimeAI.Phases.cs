using CalamityMod;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Boss.SkeletronPrime;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime
{
    /// <summary>
    /// 包含AI的各个阶段逻辑
    /// </summary>
    internal partial class HeadPrimeAI
    {
        private void Debut() {
            if (ai0 == 0) {
                SpawnEye();
                npc.life = 1;
                npc.Center = player.Center + new Vector2(0, 1200);
            }

            npc.damage = 0;
            npc.dontTakeDamage = true;

            Vector2 toTarget = npc.Center.To(player.Center);
            npc.rotation = npc.rotation.AngleLerp(toTarget.X / 115f * 0.5f, 0.75f);
            npc.velocity = Vector2.Zero;
            npc.position += player.velocity;
            Vector2 toPoint = player.Center;

            if (ai0 < 60) {
                toPoint = player.Center + new Vector2(0, 500);
            }
            else {
                toPoint = player.Center + new Vector2(0, -500);
                if (ai0 == 90 && !VaultUtils.isServer) {
                    SoundEngine.PlaySound(CWRSound.MechanicalFullBloodFlow, Main.LocalPlayer.Center);
                }
                if (ai0 > 90) {
                    int addNum = (int)(npc.lifeMax / 80f);
                    if (npc.life >= npc.lifeMax) {
                        npc.life = npc.lifeMax;
                    }
                    else {
                        Lighting.AddLight(npc.Center, Color.White.ToVector3());
                        npc.life += addNum;
                        CombatText.NewText(npc.Hitbox, CombatText.HealLife, addNum);
                    }
                }
            }

            if (ai0 == 172 && !VaultUtils.isServer) {
                SpawnHouengEffect();
                SoundEngine.PlaySound(CWRSound.SpawnArmMgs, Main.LocalPlayer.Center);
            }
            if (ai0 == 180 && !VaultUtils.isClient) {
                SpawnArm();
            }

            if (ai0 > 220) {
                npc.dontTakeDamage = false;
                npc.damage = npc.defDamage;
                npc.ai[0] = 2;
                ai0 = 0;
                return;
            }

            npc.Center = Vector2.Lerp(npc.Center, toPoint, 0.065f);

            ai0++;
        }

        private void ProtogenesisAI() {
            if (npc.ai[1] == 0f) {
                npc.damage = 0;
                npc.ai[2] += 1f;
                float aiThreshold = Main.masterMode ? 600f : 800f;
                if (npc.ai[2] >= aiThreshold) {
                    npc.ai[2] = 0f;
                    npc.ai[1] = 1f;
                    calNPC.newAI[0]++;
                    if (!VaultUtils.isClient && calNPC.newAI[0] >= 2) {
                        if (CWRUtils.FindNPCFromeType(NPCID.TheDestroyer) == null) {
                            SoundEngine.PlaySound(SoundID.Roar, npc.Center);
                            int damage = SetMultiplier(npc.defDamage / 3);
                            Projectile.NewProjectile(npc.GetSource_FromAI(), player.Center, new Vector2(0, 0)
                                , ModContent.ProjectileType<SetPosingStarm>(), damage, 2, -1, 0, npc.whoAmI);
                        }
                        calNPC.newAI[0] = 0;
                        ai11++;
                        SendExtraAI(npc);
                        NetAISend();
                    }
                    npc.TargetClosest();
                    npc.netUpdate = true;
                }

                npc.rotation = NPC.IsMechQueenUp ? npc.rotation.AngleLerp(npc.velocity.X / 15f * 0.5f, 0.75f) : npc.velocity.X / 15f;

                float verticalAcceleration = 0.1f;
                float maxVerticalSpeed = 2f;
                float horizontalAcceleration = 0.1f;
                float maxHorizontalSpeed = 8f;
                float deceleration = Main.masterMode ? 0.94f : Main.expertMode ? 0.96f : 0.98f;
                int verticalOffset = 200;
                int verticalThreshold = 500;
                float horizontalOffset = 0f;
                int directionMultiplier = (Main.player[npc.target].Center.X < npc.Center.X) ? -1 : 1;

                if (NPC.IsMechQueenUp) {
                    horizontalOffset = -450f * directionMultiplier;
                    verticalOffset = 300;
                    verticalThreshold = 350;
                }

                if (Main.expertMode) {
                    verticalAcceleration = Main.masterMode ? 0.04f : 0.03f;
                    maxVerticalSpeed = Main.masterMode ? 5f : 4f;
                    horizontalAcceleration = Main.masterMode ? 0.1f : 0.08f;
                    maxHorizontalSpeed = Main.masterMode ? 10f : 9.5f;
                    if (death) {
                        verticalAcceleration += 0.01f;
                        maxVerticalSpeed += 0.3f;
                        horizontalAcceleration += 0.1f;
                        maxHorizontalSpeed += 1f;
                    }
                    if (bossRush) {
                        verticalAcceleration += 0.01f;
                        maxVerticalSpeed += 0.5f;
                        horizontalAcceleration += 0.1f;
                        maxHorizontalSpeed += 1f;
                    }
                    if (noArm) {
                        verticalAcceleration += 0.01f;
                        maxVerticalSpeed += 0.125f;
                        horizontalAcceleration += 0.025f;
                        maxHorizontalSpeed += 0.25f;
                    }
                }

                AdjustVerticalMovement(verticalAcceleration, maxVerticalSpeed, deceleration, verticalOffset, verticalThreshold);
                AdjustHorizontalMovement(horizontalAcceleration, maxHorizontalSpeed, deceleration, horizontalOffset);
            }
            else if (npc.ai[1] == 1f) {
                npc.defense *= (int)(npc.defDefense * 1.25f);
                npc.damage = npc.defDamage * 2;
                calNPC.CurrentlyIncreasingDefenseOrDR = true;

                npc.ai[2]++;
                if (npc.ai[2] == 2f) {
                    SoundEngine.PlaySound(SoundID.ForceRoar, npc.Center);
                }

                if (npc.ai[2] == 36f) {
                    SoundStyle sound = new SoundStyle("CalamityMod/Sounds/Custom/ExoMechs/AresEnraged");
                    SoundEngine.PlaySound(sound with { Pitch = 1.18f }, npc.Center);
                }

                float aiThreshold = Main.masterMode ? 200f : 300f;
                if (npc.ai[2] >= aiThreshold) {
                    npc.ai[2] = 0f;
                    npc.ai[1] = 0f;
                }

                UpdateRotation();

                Vector2 targetVector = Main.player[npc.target].Center - npc.Center;
                float distanceToTarget = targetVector.Length();
                float initialSpeed = 5f;
                float speedMultiplier = CalculateSpeedMultiplier(distanceToTarget, initialSpeed);
                if (NPC.IsMechQueenUp) {
                    float mechQueenSpeedFactor = NPC.npcsFoundForCheckActive[NPCID.TheDestroyerBody] ? 0.6f : 0.75f;
                    speedMultiplier *= mechQueenSpeedFactor;
                }

                UpdateVelocity(targetVector, speedMultiplier, distanceToTarget);
            }
            else if (npc.ai[1] == 2f) {
                EnrageNPC();
                UpdateRotation();
                MoveTowardsPlayer(10f, 8f, 32f, 100f);
            }
            else if (npc.ai[1] == 3f) {
                HandleDespawn();
            }
            else {
                FulyByCoinGun();
            }
        }

        private void FulyByCoinGun() {
            npc.damage = 999;
            npc.defense = 999;
            npc.ChasingBehavior(player.Center, 33);
            npc.rotation += npc.velocity.X > 0 ? 0.42f : -0.42f;
        }

        private bool TwoStageAI() {
            if (ai6 == 0 && ai9 > 2 && death && !bossRush) {
                ai3 = 3;
                ai6 = 1;
                if (npc.ai[1] != 2) {//白天狂暴时不用召唤双子
                    SpawnEye();
                }
                NetAISend();
            }

            int type = ProjectileID.RocketSkeleton;
            int damage = SetMultiplier(npc.GetProjectileDamage(type));
            float rocketSpeed = 10f;
            Vector2 cannonSpreadTargetDist = (player.Center - npc.Center).SafeNormalize(Vector2.UnitY) * rocketSpeed;
            int numProj = bossRush ? 5 : 3;
            float rotation = MathHelper.ToRadians(bossRush ? 15 : 9);

            if (npc.ai[1] != 1 || ai3 == 3) {
                SmokeDrawer.ParticleSpawnRate = 3;
                SmokeDrawer.BaseMoveRotation = MathHelper.ToRadians(90);
                SmokeDrawer.SpawnAreaCompactness = 80f;
                if (npc.life > npc.lifeMax / 10 && noEye && npc.ai[1] != 2) {
                    npc.life -= 12;
                }
            }
            SmokeDrawer.Update();

            if (setPosingStarmCount > 0 && !noEye && ai3 != 3) {
                npc.damage = 0;
                MoveToPoint(player.Center + new Vector2(0, -300));
                npc.rotation = npc.rotation.AngleLerp(npc.velocity.X / 15f * 0.5f, 0.75f);

                ai3 = 1;
                return true;
            }

            switch (ai3) {
                case 0:
                    if (++ai4 > 90) {
                        npc.TargetClosest();

                        if (!VaultUtils.isClient) {
                            if (ai8 % 2 == 0) {
                                int totalProjectiles = bossRush ? 9 : 6;
                                if (!noEye) {
                                    totalProjectiles = 3;
                                }
                                Vector2 laserFireDirection = npc.Center.To(player.Center).UnitVector();
                                for (int j = 0; j < totalProjectiles; j++) {
                                    Vector2 vector = laserFireDirection.RotatedBy((totalProjectiles / -2 + j) * 0.1f) * 6;
                                    if (bossRush) {
                                        vector *= 1.45f;
                                    }
                                    Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center + vector.UnitVector() * 100f
                                        , vector, ModContent.ProjectileType<DeadLaser>(), damage, 0f, Main.myPlayer, 1f, 0f);
                                }
                                SpanFireLerterDustEffect(npc, 73);
                            }
                            else {
                                for (int i = 0; i < numProj; i++) {
                                    float rotoffset = MathHelper.Lerp(-rotation, rotation, i / (float)(numProj - 1));
                                    Vector2 perturbedSpeed = cannonSpreadTargetDist.RotatedBy(rotoffset);
                                    if (death && Main.masterMode || bossRush) {
                                        Projectile.NewProjectile(npc.GetSource_FromAI()
                                        , npc.Center, perturbedSpeed
                                        , ModContent.ProjectileType<PrimeCannonOnSpan>(), damage, 0f
                                        , Main.myPlayer, npc.whoAmI, npc.target, rotoffset);
                                    }
                                    else {
                                        SoundEngine.PlaySound(SoundID.Item62, npc.Center);
                                        int proj = Projectile.NewProjectile(npc.GetSource_FromAI()
                                            , npc.Center + perturbedSpeed.SafeNormalize(Vector2.UnitY) * 40f
                                            , perturbedSpeed, type, damage, 0f, Main.myPlayer, npc.target, 2f);
                                        Main.projectile[proj].timeLeft = 600;
                                    }
                                }
                            }
                        }

                        ai4 = 0;
                        ai5++;
                        NetAISend();
                    }

                    if (ai5 > 3 || npc.ai[1] == 1) {
                        ai3 = 1;
                        ai4 = 0;
                        ai5 = 0;
                        ai8++;
                        NetAISend();
                    }
                    break;
                case 1:
                    if (ai4 > 90 && noEye && ai5 <= 2 && ai10 <= 0) {
                        ThisFromeFindPlayer();
                        Projectile setPointEntity = null;
                        foreach (var proj in Main.ActiveProjectiles) {
                            if (proj.type != ModContent.ProjectileType<SetPosingStarm>()) {
                                continue;
                            }
                            setPointEntity = proj;
                        }
                        if (!VaultUtils.isClient && (setPointEntity == null || setPointEntity.timeLeft < 220)) {
                            float maxLerNum = death ? 13 : 9f;
                            for (int i = 0; i < maxLerNum; i++) {
                                float rotoffset = MathHelper.TwoPi / maxLerNum * i;
                                Vector2 perturbedSpeed = cannonSpreadTargetDist.RotatedBy(rotoffset);
                                if (death && Main.masterMode || bossRush || ModGanged.InfernumModeOpenState) {
                                    Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, perturbedSpeed
                                    , ModContent.ProjectileType<PrimeCannonOnSpan>(), damage, 0f
                                    , Main.myPlayer, npc.whoAmI, npc.target, rotoffset);
                                }
                                else {
                                    SoundEngine.PlaySound(SoundID.Item62, npc.Center);
                                    int proj = Projectile.NewProjectile(npc.GetSource_FromAI()
                                        , npc.Center + perturbedSpeed.SafeNormalize(Vector2.UnitY) * 40f
                                        , perturbedSpeed, type, damage, 0f, Main.myPlayer, npc.target, 2f);
                                    Main.projectile[proj].timeLeft = 600;
                                }
                            }
                        }
                        ai4 = 0;
                        ai5++;
                    }

                    if (npc.ai[1] != 1 && ai5 > 2 && ++ai7 > 60) {
                        ai3 = 2;
                        ai4 = 0;
                        ai5 = 0;
                        ai7 = 0;
                        NetAISend();
                    }

                    ai4++;
                    break;
                case 2:
                    npc.damage = 0;
                    if (ai7 == 0) {
                        if (setPosingStarmCount > 0 || !death) {
                            ai3 = 0;
                            ai4 = 0;
                            ai5 = 0;
                            ai7 = 0;
                            NetAISend();
                            return false;
                        }

                        npc.TargetClosest();
                        Vector2 pos2 = player.Center;

                        if (!VaultUtils.isClient) {
                            int maxShootNum = 40;
                            for (int i = 0; i < maxShootNum; i++) {
                                int maxD = 200;
                                int maxF = maxD * maxShootNum;
                                int toL = maxF / -2;
                                Vector2 spanPos = pos2 + new Vector2(toL + maxD * i, 1800);
                                Vector2 vr1 = new Vector2(0, -6);
                                Projectile.NewProjectile(npc.GetSource_FromAI()
                                        , spanPos, vr1
                                        , ModContent.ProjectileType<PrimeCannonOnSpan>(), damage, 0f
                                        , Main.myPlayer, -1, -1, vr1.ToRotation());
                            }
                            for (int i = 0; i < maxShootNum; i++) {
                                int maxD = 200;
                                int maxF = maxD * maxShootNum;
                                int toL = maxF / -2;
                                Vector2 spanPos = pos2 + new Vector2(-1800, toL + maxD * i);
                                Vector2 vr1 = new Vector2(6, 0);
                                Projectile.NewProjectile(npc.GetSource_FromAI()
                                        , spanPos, vr1
                                        , ModContent.ProjectileType<PrimeCannonOnSpan>(), damage, 0f
                                        , Main.myPlayer, -1, -1, vr1.ToRotation());
                            }
                        }
                        ai7++;
                    }

                    if (!VaultUtils.isServer) {
                        foreach (Player p in Main.player) {
                            if (p.dead || !p.active) {
                                continue;
                            }
                            p.Calamity().infiniteFlight = true;
                        }
                    }

                    if (++ai5 > 120) {
                        npc.damage = npc.defDamage * 2;
                        ai3 = 0;
                        ai4 = 0;
                        ai5 = 0;
                        ai7 = 0;
                        NetAISend();
                    }

                    break;
                case 3:
                    npc.damage = 0;
                    npc.dontTakeDamage = true;
                    npc.ai[1] = 0;

                    Vector2 toTarget = npc.Center.To(player.Center);
                    npc.rotation = npc.rotation.AngleLerp(toTarget.X / 115f * 0.5f, 0.75f);
                    npc.velocity = Vector2.Zero;
                    npc.position += player.velocity;
                    Vector2 toPoint = player.Center;

                    toPoint = player.Center + new Vector2(0, death ? -400 : -500);
                    int value = npc.lifeMax - npc.life;
                    if (ai4 == 0) {
                        oneToTwoPrsAddNumBloodValue = (int)(value / 360f);
                    }
                    if (npc.life >= npc.lifeMax) {
                        npc.life = npc.lifeMax;
                    }
                    else {
                        npc.life += oneToTwoPrsAddNumBloodValue;
                        CombatText.NewText(npc.Hitbox, CombatText.HealLife, oneToTwoPrsAddNumBloodValue);
                    }

                    npc.Center = Vector2.Lerp(npc.Center, toPoint, 0.065f);

                    if (ai4 == 0 && VaultUtils.isServer) {
                        SoundEngine.PlaySound(CWRSound.MechanicalFullBloodFlow, Main.LocalPlayer.Center);
                    }

                    ai4++;
                    if (ai4 > 360f || npc.life >= npc.lifeMax) {
                        npc.dontTakeDamage = false;
                        npc.damage = npc.defDamage * 2;
                        ai3 = 0;
                        ai4 = 0;
                    }

                    return true;
            }

            return false;
        }

        private bool InIdleAI() {
            if (npc.ai[1] != 3 && npc.ai[1] != 4 && ai10 > 0) {
                npc.damage = 0;

                if (ai4 == 0) {
                    npc.velocity = new Vector2(0, -6);
                }
                if (++ai4 < 30) {
                    npc.velocity *= 0.98f;
                }
                else {
                    MoveToPoint(player.Center + new Vector2(0, -300));
                }

                npc.rotation = npc.rotation.AngleLerp(npc.velocity.X / 15f * 0.5f, 0.75f);

                if (noArm) {
                    SmokeDrawer.ParticleSpawnRate = 3;
                    SmokeDrawer.BaseMoveRotation = MathHelper.ToRadians(90);
                    SmokeDrawer.SpawnAreaCompactness = 80f;
                }
                SmokeDrawer.Update();

                ai10--;
                if (ai10 <= 0) {
                    npc.damage = npc.defDamage * (noArm ? 2 : 1);
                    ai4 = 0;
                }
                return true;
            }
            return false;
        }

        private void DealingFury() {
            if (npc.ai[1] == 3f) {
                return;
            }
            if (Main.IsItDay()) {
                if (npc.ai[1] != 2f) {
                    npc.ai[1] = 2f;
                    SoundEngine.PlaySound(SoundID.ForceRoar, npc.Center);
                }
                return;
            }
            Item heandItem = player.GetItem();
            if (heandItem.type == ItemID.CoinGun) {
                if (npc.ai[1] != 4f && player.GetShootState().AmmoTypes == ProjectileID.PlatinumCoin) {
                    npc.ai[1] = 4f;
                    SoundStyle sound = new SoundStyle("CalamityMod/Sounds/Custom/ExoMechs/AresEnraged");
                    SoundEngine.PlaySound(sound with { Pitch = -0.18f }, npc.Center);
                    SoundEngine.PlaySound(SoundID.ForceRoar, npc.Center);
                }
            }
        }
    }
}
