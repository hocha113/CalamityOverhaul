using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.States;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.Projectiles;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer
{
    internal class ProbeAI : CWRNPCOverride
    {
        public override int TargetID => NPCID.Probe;
        [VaultLoaden(CWRConstant.NPC + "BTD/")]
        private static Asset<Texture2D> Probe { get; set; }
        [VaultLoaden(CWRConstant.NPC + "BTD/")]
        private static Asset<Texture2D> Probe_Glow { get; set; }
        public static int ReelBackTime => CWRRef.GetBossRushActive() ? 30 : 60;
        public override bool? CanCWROverride() {
            return null;
        }
        public override void SetProperty() {
            NPCID.Sets.TrailingMode[npc.type] = 1;
            NPCID.Sets.TrailCacheLength[npc.type] = 16;
        }
        public override bool AI() {
            //阵列态，ProbeMatrix接管
            if (npc.ai[3] == -1f) {
                npc.timeLeft = 600;
                Lighting.AddLight(npc.Center, Color.Red.ToVector3() * npc.scale);

                //落位瞬间闪光(客户端)
                if (!VaultUtils.isServer) {
                    if (npc.velocity.Length() > 6f) {
                        npc.localAI[2] = 1f;
                    }
                    else if (npc.localAI[2] == 1f) {
                        npc.localAI[2] = 2f;
                        PRTLoader.NewParticle<PRT_StarPulseRing>(npc.Center, Vector2.Zero,
                            new Color(255, 90, 110), 0.05f).Configure(0.05f, 0.5f, 18);
                        for (int i = 0; i < 4; i++) {
                            PRTLoader.NewParticle<PRT_Spark>(npc.Center,
                                Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 6f),
                                new Color(255, 120, 130), Main.rand.NextFloat(0.6f, 1f))
                                .Configure(true, Main.rand.Next(10, 16));
                        }
                        SoundEngine.PlaySound(SoundID.Item25 with { Volume = 0.5f, Pitch = 0.4f, MaxInstances = 5 }, npc.Center);
                    }
                }
                return false;
            }

            //死亡演出僵直，殉爆见DeathState
            if (DestroyerDeathState.IsProbeInDeathPerformance(npc)) {
                HandleDeathPerformanceProbe();
                return false;
            }

            npc.TargetClosest();
            Player target = Main.player[npc.target];

            //出生角偏移
            float indexFrac = (npc.whoAmI % 16f) / 16f;
            float angle = MathHelper.Lerp(-0.97f, 0.97f, indexFrac) + Main.rand.NextFloat(-0.1f, 0.1f);
            Vector2 spawnOffset = Vector2.UnitY.RotatedBy(angle) * 300f;

            if (npc.whoAmI % 2 == 1) {
                spawnOffset *= -1f;
            }

            Vector2 destination = target.Center + spawnOffset;

            ref float generalTimer = ref npc.ai[2];
            ref float attackTimer = ref npc.ai[1];
            ref float state = ref npc.ai[0];

            Lighting.AddLight(npc.Center, Color.Red.ToVector3() * npc.scale);

            float hoverSpeed = 22f;
            if (CWRWorld.BossRush) {
                hoverSpeed *= 1.5f;
            }

            npc.damage = state == 2f ? npc.defDamage : 0;

            switch (state) {
                case 0f: //靠近
                    Vector2 toDest = npc.To(destination);
                    float dist = toDest.Length();

                    float targetSpeed = MathHelper.Clamp(dist / 20f, 5f, hoverSpeed);
                    npc.velocity = Vector2.Lerp(npc.velocity, toDest.UnitVector() * targetSpeed, 0.08f);

                    float targetAngle = npc.AngleTo(target.Center);
                    npc.rotation = npc.rotation.AngleLerp(targetAngle, 0.1f);

                    if (npc.WithinRange(destination, 100f) || (generalTimer > 180 && dist < 400)) {
                        state = 1f;
                        attackTimer = 0f;
                        npc.netUpdate = true;
                    }
                    break;

                case 1f: //蓄力
                    npc.velocity *= 0.92f;
                    //发射前跟瞄，发射后锁向
                    if (attackTimer < (int)(ReelBackTime * 0.7f)) {
                        npc.rotation = npc.AngleTo(target.Center);
                    }
                    attackTimer++;

                    if (attackTimer > ReelBackTime * 0.5f) {
                        npc.Center += Main.rand.NextVector2Circular(2f, 2f);
                    }

                    if (attackTimer == (int)(ReelBackTime * 0.7f) && !VaultUtils.isClient) {
                        SpawnPinkLaser();
                        npc.velocity -= npc.rotation.ToRotationVector2() * 6f;
                    }

                    //被打打断蓄力
                    if (npc.justHit && attackTimer < ReelBackTime * 0.6f) {
                        npc.velocity = -npc.To(target.Center).UnitVector() * 4f;
                        state = 3f;
                        attackTimer = 0f;
                        npc.netUpdate = true;
                        break;
                    }

                    if (attackTimer >= ReelBackTime) {
                        Vector2 dashDir = npc.rotation.ToRotationVector2();
                        npc.velocity = dashDir * (hoverSpeed * 1.8f);

                        SoundEngine.PlaySound(SoundID.Item74, npc.Center);

                        npc.oldPos = new Vector2[npc.oldPos.Length];
                        state = 2f;
                        attackTimer = 0f;
                        npc.netUpdate = true;
                    }
                    break;

                case 2f: //冲刺
                    npc.knockBackResist = 0f;
                    npc.rotation = npc.velocity.ToRotation();
                    npc.damage = 95;
                    attackTimer++;

                    if (attackTimer < 15) {
                        npc.velocity *= 1.02f;
                    }
                    else {
                        npc.velocity *= 0.98f;
                    }

                    if (attackTimer > 45f || npc.collideX || npc.collideY) {
                        npc.velocity *= 0.5f;
                        state = 3f;
                        attackTimer = 0f;
                        npc.netUpdate = true;
                    }
                    break;

                case 3f: //停顿
                    npc.velocity *= 0.94f;
                    float recoverAngle = npc.AngleTo(target.Center);
                    npc.rotation = npc.rotation.AngleLerp(recoverAngle, 0.05f);
                    attackTimer++;

                    if (attackTimer > 30f) {
                        state = 0f;
                        attackTimer = 0f;
                        generalTimer = 0;
                        npc.netUpdate = true;
                    }
                    break;
            }

            generalTimer++;
            return false;
        }

        /// <summary>死亡演出僵直，殉爆见DeathState</summary>
        private void HandleDeathPerformanceProbe() {
            npc.velocity *= 0.85f;
            if (npc.velocity.Length() < 0.1f) {
                npc.velocity = Vector2.Zero;
            }
            npc.damage = 0;
            npc.dontTakeDamage = true;
            if (npc.life < 1) {
                npc.life = 1;
            }
            npc.timeLeft = 120;

            //故障抖动
            if (Main.rand.NextBool(5)) {
                npc.Center += Main.rand.NextVector2Circular(1.2f, 1.2f);

                Color warm = Color.Lerp(new Color(255, 150, 50), new Color(255, 85, 35), Main.rand.NextFloat());
                PRTLoader.NewParticle<PRT_MechExplosion>(npc.Center, Main.rand.NextVector2Circular(1.5f, 1.5f), warm, 0.4f)
                    .Configure(Main.rand.Next(18, 32), warm);

                Vector2 vel = Main.rand.NextVector2Circular(3.5f, 3.5f);
                PRTLoader.NewParticle<PRT_LavaFire>(npc.Center + Main.rand.NextVector2Circular(12f, 12f), vel,
                    Color.White, Main.rand.NextFloat(0.2f, 0.4f)).SetLifetime(10, 20);
            }

            Lighting.AddLight(npc.Center, Color.Red.ToVector3() * (0.45f + 0.15f * npc.scale));
        }

        public override bool? CheckDead() {
            if (DestroyerDeathState.IsProbeInDeathPerformance(npc)) {
                npc.life = 1;
                npc.dontTakeDamage = true;
                return false;
            }
            return null;
        }
        private void SpawnPinkLaser() {
            int damage = HeadPrimeAI.SetMultiplier(CWRRef.GetProjectileDamage(npc, ModContent.ProjectileType<PrimeCannonOnSpan>()));
            SoundEngine.PlaySound(SoundID.Item12, npc.Center);
            Projectile.NewProjectile(npc.GetSource_FromAI()
                                        , npc.Center, npc.rotation.ToRotationVector2()
                                        , ModContent.ProjectileType<PrimeCannonOnSpan>(), damage, 0f
                                        , Main.myPlayer, -1, -1, 0);

            for (int i = 0; i < 10; i++) {
                Vector2 dustVel = npc.rotation.ToRotationVector2().RotatedByRandom(0.5f) * Main.rand.NextFloat(2f, 5f);
                Dust.NewDust(npc.Center, 0, 0, DustID.PinkTorch, dustVel.X, dustVel.Y);
            }
        }
        public override bool? Draw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            SpriteEffects spriteEffects = SpriteEffects.None;
            float drawRot = npc.rotation + MathHelper.Pi;
            if (npc.spriteDirection > 0) {
                spriteEffects = SpriteEffects.FlipHorizontally;
            }

            Texture2D value = Probe.Value;
            Texture2D value2 = Probe_Glow.Value;

            //冲刺残影
            if (npc.ai[0] == 2f) {
                for (int i = 0; i < npc.oldPos.Length; i += 2) {
                    Vector2 drawOldPos = npc.oldPos[i] + npc.Size / 2 - Main.screenPosition;
                    float opacity = 0.6f * (1f - i / (float)npc.oldPos.Length);
                    spriteBatch.Draw(value2, drawOldPos, null, Color.Red * opacity
                        , drawRot, value2.Size() / 2, npc.scale, spriteEffects, 0);
                }
            }

            spriteBatch.Draw(value, npc.Center - Main.screenPosition
                , null, drawColor, drawRot, value.Size() / 2, npc.scale, spriteEffects, 0);

            float sengs = 0.2f;
            for (int i = 0; i < npc.oldPos.Length; i++) {
                Vector2 drawOldPos = npc.oldPos[i] + npc.Size / 2 - Main.screenPosition;
                spriteBatch.Draw(value, drawOldPos, null, drawColor * sengs
                    , drawRot, value.Size() / 2, npc.scale, spriteEffects, 0);
                sengs *= 0.8f;
            }
            sengs = 0.4f;
            for (int i = 0; i < npc.oldPos.Length; i++) {
                Vector2 drawOldPos = npc.oldPos[i] + npc.Size / 2 - Main.screenPosition;
                spriteBatch.Draw(value2, drawOldPos, null, Color.White * sengs
                    , drawRot, value2.Size() / 2, npc.scale, spriteEffects, 0);
                sengs *= 0.8f;
            }

            spriteBatch.Draw(value2, npc.Center - Main.screenPosition
                , null, Color.White, drawRot, value.Size() / 2, npc.scale, spriteEffects, 0);
            return false;
        }
        public override bool PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) => true;
    }
}
