using CalamityMod;
using CalamityMod.NPCs;
using CalamityMod.World;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Boss.SkeletronPrime;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime
{
    internal class PrimeCannonAI : PrimeArm
    {
        public override int TargetID => NPCID.PrimeCannon;
        public override bool CanLoad() => true;
        public override bool? CheckDead() => true;

        #region 状态枚举
        private enum AttackState
        {
            Idle = 0,           //待机瞄准
            SingleShot = 1,     //单发射击
            SpreadShot = 2,     //扩散射击
            BarrageMode = 3     //弹幕模式
        }
        #endregion

        #region 常量与变量
        private const float TimeToNotAttack = 180f;
        private int stateTimer = 0;
        private float recoilIntensity = 0f;
        private Vector2 aimDirection = Vector2.Zero;
        private bool isFiring = false;
        private int burstCount = 0;
        #endregion

        #region AI主循环
        public override bool ArmBehavior() {
            float timeToNotAttack = TimeToNotAttack;
            dontAttack = calNPC.newAI[2] < timeToNotAttack;

            if (dontAttack) {
                calNPC.newAI[2] += 1f;
                if (calNPC.newAI[2] >= timeToNotAttack) {
                    HeadPrimeAI.SendExtraAI(npc);
                }
            }

            //更新后坐力效果
            UpdateRecoilEffects();

            //移动控制
            Movement();

            //确定攻击模式
            bool fireSlower = false;
            if (laserAlive) {
                if (Main.npc[CWRWorld.primeLaser].ai[2] == 1f)
                    fireSlower = true;
            }
            else {
                fireSlower = npc.ai[2] == 0f;
                if (fireSlower) {
                    npc.ai[3] += 1f + CalculateChargeBonus();

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
                    npc.ai[3] += 1f + CalculateChargeBonus() * 0.5f;

                    float timeLimit = 120f * GetTimeMult();

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

            //执行攻击
            if (fireSlower) {
                State_SingleShot();
            }
            else {
                State_SpreadShot();
            }

            //跟随炮弹旋转
            if (FindPrimeCannonOnSpan(out Projectile primeCannonOnSpan)) {
                npc.rotation = primeCannonOnSpan.rotation - MathHelper.PiOver2;
            }

            return false;
        }
        #endregion

        #region 状态行为
        private void State_SingleShot() {
            stateTimer++;

            //检查头部状态
            if (head.ai[1] == 3f && npc.timeLeft > 10) {
                npc.timeLeft = 10;
            }

            //平滑瞄准
            SmoothAimAtPlayer(0.2f);

            if (!VaultUtils.isClient && !dontAttack) {
                npc.localAI[0] += 1f + CalculateChargeBonus();

                float fireCannonCooldown = 120f;
                if (death) fireCannonCooldown -= 20;
                if (masterMode) fireCannonCooldown -= 20;
                if (bossRush) fireCannonCooldown = 60;

                if (npc.localAI[0] >= fireCannonCooldown) {
                    FireSingleRocket();
                    npc.localAI[0] = 0f;
                    npc.TargetClosest();
                }
            }
        }

        private void State_SpreadShot() {
            stateTimer++;

            //瞄准玩家
            SmoothAimAtPlayer(0.2f);

            if (!VaultUtils.isClient && !dontAttack) {
                npc.localAI[0] += 1f + CalculateChargeBonus() * 0.5f;

                if (npc.localAI[0] >= 180f) {
                    FireSpreadRockets();
                    npc.localAI[0] = 0f;
                    npc.TargetClosest();
                }
            }
        }
        #endregion

        #region 攻击函数
        private void FireSingleRocket() {
            npc.TargetClosest();
            int type = ProjectileID.RocketSkeleton;
            int damage = HeadPrimeAI.SetMultiplier(npc.GetProjectileDamage(type));
            float rocketSpeed = 10f;

            Vector2 rocketVelocity = aimDirection * rocketSpeed;
            Vector2 spawnPos = npc.Center + aimDirection * 40f;

            if (death && masterMode || bossRush || ModGanged.InfernumModeOpenState) {
                int proj = Projectile.NewProjectile(npc.GetSource_FromAI(),
                    spawnPos, rocketVelocity,
                    ModContent.ProjectileType<PrimeCannonOnSpan>(), damage, 0f,
                    Main.myPlayer, npc.whoAmI, npc.target, 0);

                int cooldown = (int)(120f * 0.8f);
                if (masterMode) cooldown -= 20;
                if (cooldown > 60) cooldown = 60;
                Main.projectile[proj].timeLeft = cooldown;
            }
            else {
                int proj = Projectile.NewProjectile(npc.GetSource_FromAI(),
                    spawnPos, rocketVelocity, type, damage, 0f, Main.myPlayer, npc.target, 2f);
                Main.projectile[proj].timeLeft = 600;
            }

            //后坐力效果
            ApplyRecoil(12f);

            //发射音效
            SoundEngine.PlaySound(SoundID.Item62 with { Volume = 0.9f, Pitch = -0.2f }, npc.Center);

            //发射粒子
            SpawnFireParticles(1);
        }

        private void FireSpreadRockets() {
            npc.TargetClosest();
            int type = ProjectileID.RocketSkeleton;
            int damage = HeadPrimeAI.SetMultiplier(npc.GetProjectileDamage(type));
            float rocketSpeed = 10f;

            Vector2 baseVelocity = aimDirection * rocketSpeed;
            int numProj = bossRush ? 5 : 3;
            float rotation = MathHelper.ToRadians(bossRush ? 15 : 9);

            for (int i = 0; i < numProj; i++) {
                float rotOffset = MathHelper.Lerp(-rotation, rotation, i / (float)(numProj - 1));
                Vector2 perturbedSpeed = baseVelocity.RotatedBy(rotOffset);
                Vector2 spawnPos = npc.Center + aimDirection * 40f;

                if (CalamityWorld.death || CWRWorld.MachineRebellion || bossRush || ModGanged.InfernumModeOpenState) {
                    Projectile.NewProjectile(npc.GetSource_FromAI(),
                        spawnPos, perturbedSpeed,
                        ModContent.ProjectileType<PrimeCannonOnSpan>(), damage, 0f,
                        Main.myPlayer, npc.whoAmI, npc.target, rotOffset);
                }
                else {
                    int proj = Projectile.NewProjectile(npc.GetSource_FromAI(),
                        spawnPos, perturbedSpeed, type, damage, 0f, Main.myPlayer, npc.target, 2f);
                    Main.projectile[proj].timeLeft = 600;
                }
            }

            //更大的后坐力
            ApplyRecoil(18f);

            //扩散发射音效
            SoundEngine.PlaySound(SoundID.Item62 with { Volume = 1.0f, Pitch = 0f }, npc.Center);
            SoundEngine.PlaySound(SoundID.DD2_ExplosiveTrapExplode with { Volume = 0.6f, Pitch = 0.4f }, npc.Center);

            //大量发射粒子
            SpawnFireParticles(numProj);

            //屏幕震动
            if (Main.LocalPlayer.Distance(npc.Center) < 700f) {
                Main.LocalPlayer.Calamity().GeneralScreenShakePower = 4f;
            }
        }
        #endregion

        #region 辅助函数
        private void Movement() {
            float acceleration = bossRush ? 0.6f : death ? (masterMode ? 0.375f : 0.3f) : (masterMode ? 0.3125f : 0.25f);
            float accelerationMult = 1f;

            if (!laserAlive) {
                acceleration += 0.025f;
                accelerationMult += 0.5f;
            }
            if (!viceAlive) acceleration += 0.025f;
            if (!sawAlive) acceleration += 0.025f;
            if (masterMode) acceleration *= accelerationMult;

            //后坐力影响移动
            if (recoilIntensity > 0.5f) {
                npc.velocity -= aimDirection * (recoilIntensity * 0.3f);
            }

            float topVelocity = acceleration * 100f;
            float deceleration = masterMode ? 0.6f : 0.8f;

            //Y轴控制
            if (npc.position.Y > head.position.Y - 130f) {
                if (npc.velocity.Y > 0f) npc.velocity.Y *= deceleration;
                npc.velocity.Y -= acceleration;
                if (npc.velocity.Y > topVelocity) npc.velocity.Y = topVelocity;
            }
            else if (npc.position.Y < head.position.Y - 170f) {
                if (npc.velocity.Y < 0f) npc.velocity.Y *= deceleration;
                npc.velocity.Y += acceleration;
                if (npc.velocity.Y < -topVelocity) npc.velocity.Y = -topVelocity;
            }

            //X轴控制
            if (npc.Center.X > head.Center.X + 160f) {
                if (npc.velocity.X > 0f) npc.velocity.X *= deceleration;
                npc.velocity.X -= acceleration;
                if (npc.velocity.X > topVelocity) npc.velocity.X = topVelocity;
            }
            if (npc.Center.X < head.Center.X + 200f) {
                if (npc.velocity.X < 0f) npc.velocity.X *= deceleration;
                npc.velocity.X += acceleration;
                if (npc.velocity.X < -topVelocity) npc.velocity.X = -topVelocity;
            }
        }

        private void SmoothAimAtPlayer(float smoothness) {
            Vector2 toPlayer = player.Center - npc.Center;
            aimDirection = Vector2.Lerp(aimDirection, Vector2.Normalize(toPlayer), smoothness);
            if (aimDirection == Vector2.Zero) aimDirection = Vector2.UnitX;

            float targetRotation = aimDirection.ToRotation() - MathHelper.PiOver2;

            //添加后坐力抖动
            if (recoilIntensity > 1f) {
                targetRotation += Main.rand.NextFloat(-0.1f, 0.1f) * (recoilIntensity / 10f);
            }

            npc.rotation = MathHelper.Lerp(npc.rotation, targetRotation, smoothness);
        }

        private void ApplyRecoil(float intensity) {
            recoilIntensity = intensity;
            npc.velocity -= aimDirection * intensity * 0.5f;
            isFiring = true;
        }

        private void UpdateRecoilEffects() {
            //后坐力衰减
            recoilIntensity *= 0.88f;

            if (recoilIntensity < 0.1f) {
                recoilIntensity = 0f;
                isFiring = false;
            }

            //烟雾效果
            if (recoilIntensity > 2f && Main.rand.NextBool(2)) {
                Vector2 smokePos = npc.Center + aimDirection * 45f;
                Vector2 smokeVel = aimDirection * Main.rand.NextFloat(1f, 3f) + Main.rand.NextVector2Circular(1, 1);
                Dust dust = Dust.NewDustDirect(smokePos, 1, 1, DustID.Smoke, smokeVel.X, smokeVel.Y, 100, default, Main.rand.NextFloat(1.2f, 2.0f));
                dust.noGravity = false;
                dust.velocity *= 0.8f;
            }
        }

        private void SpawnFireParticles(int count) {
            for (int i = 0; i < count * 8; i++) {
                Vector2 particlePos = npc.Center + aimDirection * 50f;
                Vector2 particleVel = aimDirection.RotatedByRandom(0.4f) * Main.rand.NextFloat(3f, 8f);

                int dustType = Main.rand.Next(new int[] { DustID.Torch, DustID.Smoke, DustID.Flare });
                Dust dust = Dust.NewDustDirect(particlePos, 1, 1, dustType, particleVel.X, particleVel.Y, 100, default, Main.rand.NextFloat(1.5f, 2.5f));
                dust.noGravity = dustType != DustID.Smoke;
                dust.velocity *= 0.9f;
            }

            //爆炸闪光
            for (int i = 0; i < count * 5; i++) {
                Vector2 sparkPos = npc.Center + aimDirection * 45f;
                Vector2 sparkVel = aimDirection.RotatedByRandom(0.5f) * Main.rand.NextFloat(4f, 10f);
                Dust spark = Dust.NewDustDirect(sparkPos, 1, 1, DustID.Electric, sparkVel.X, sparkVel.Y, 100, Color.OrangeRed, Main.rand.NextFloat(1.0f, 1.8f));
                spark.noGravity = true;
                spark.fadeIn = 1.2f;
            }
        }

        private float CalculateChargeBonus() {
            float bonus = 0f;
            if (!laserAlive) bonus += 1f;
            if (!viceAlive) bonus += 1f;
            if (!sawAlive) bonus += 1f;
            return bonus;
        }

        private float GetTimeMult() {
            float timeMult = 1f;
            if (!laserAlive) timeMult *= 1.882075f;
            if (!viceAlive) timeMult *= 1.882075f;
            if (!sawAlive) timeMult *= 1.882075f;
            return timeMult;
        }

        private bool FindPrimeCannonOnSpan(out Projectile projectile) {
            projectile = null;
            int type = ModContent.ProjectileType<PrimeCannonOnSpan>();

            foreach (var proj in Main.ActiveProjectiles) {
                if (proj.type != type) continue;

                if (proj.ai[0] == npc.whoAmI && proj.ai[2] == 0) {
                    projectile = proj;
                    return true;
                }
            }

            return false;
        }
        #endregion

        #region 绘制
        public override bool? Draw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (HeadPrimeAI.DontReform()) {
                return true;
            }

            bool dir = (npc.rotation + MathHelper.PiOver2).ToRotationVector2().X > 0;

            HeadPrimeAI.DrawArm(spriteBatch, npc, screenPos);
            Texture2D mainValue = HeadPrimeAI.BSPCannon.Value;
            Texture2D mainValue2 = HeadPrimeAI.BSPCannonGlow.Value;

            //添加后坐力偏移
            Vector2 recoilOffset = Vector2.Zero;
            if (recoilIntensity > 1f) {
                recoilOffset = -aimDirection * (recoilIntensity * 2f);
                recoilOffset += Main.rand.NextVector2Circular(recoilIntensity * 0.5f, recoilIntensity * 0.5f);
            }

            Vector2 drawPos = npc.Center - Main.screenPosition + recoilOffset;

            Main.EntitySpriteDraw(mainValue, drawPos, null, drawColor,
                npc.rotation, mainValue.Size() / 2, npc.scale,
                dir ? SpriteEffects.None : SpriteEffects.FlipHorizontally, 0);

            //发光效果（发射时增强）
            float glowIntensity = isFiring ? MathHelper.Clamp(1.0f + recoilIntensity * 0.1f, 1.0f, 1.5f) : 1.0f;
            Color glowColor = Color.White * glowIntensity;
            if (isFiring) {
                glowColor = Color.Lerp(Color.White, Color.OrangeRed, recoilIntensity / 15f) * glowIntensity;
            }

            Main.EntitySpriteDraw(mainValue2, drawPos, null, glowColor,
                npc.rotation, mainValue.Size() / 2, npc.scale,
                dir ? SpriteEffects.None : SpriteEffects.FlipHorizontally, 0);

            return false;
        }

        public override bool PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) => !HeadPrimeAI.DontReform();
        #endregion
    }
}
