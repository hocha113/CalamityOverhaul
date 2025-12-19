using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime;
using CalamityOverhaul.Content.Projectiles.Boss.SkeletronPrime;
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
            if (CWRWorld.MachineRebellion) {
                return true;
            }
            return null;
        }
        public override void SetProperty() {
            NPCID.Sets.TrailingMode[npc.type] = 1;
            NPCID.Sets.TrailCacheLength[npc.type] = 16;
            if (CWRWorld.MachineRebellion) {
                npc.life = npc.lifeMax *= 28;
                npc.defDefense = npc.defense = 20;
                npc.defDamage = npc.damage *= 3;
                npc.knockBackResist = 0.2f;//在机械暴乱中拥有很强的抗击退能力
                npc.scale += 0.3f;
            }
        }
        public override bool AI() {
            if (CWRWorld.CanTimeFrozen()) {
                CWRNpc.DoTimeFrozen(npc);
                return false;
            }

            npc.TargetClosest();
            Player target = Main.player[npc.target];

            //更自然的出生偏移角度（非对称 + 扰动）
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
                case 0f: //靠近预热
                    //使用更平滑的插值移动，模拟重型机械的惯性
                    Vector2 toDest = npc.To(destination);
                    float dist = toDest.Length();
                    
                    //根据距离动态调整速度，远处快近处慢
                    float targetSpeed = MathHelper.Clamp(dist / 20f, 5f, hoverSpeed);
                    npc.velocity = Vector2.Lerp(npc.velocity, toDest.UnitVector() * targetSpeed, 0.08f);
                    
                    //平滑旋转向目标
                    float targetAngle = npc.AngleTo(target.Center);
                    npc.rotation = npc.rotation.AngleLerp(targetAngle, 0.1f);

                    if (npc.WithinRange(destination, 100f) || (generalTimer > 180 && dist < 400)) {
                        state = 1f;
                        attackTimer = 0f;
                        npc.netUpdate = true;
                    }
                    break;

                case 1f: //蓄力准备
                    npc.velocity *= 0.92f; //更强的刹车感
                    npc.rotation = npc.AngleTo(target.Center);
                    attackTimer++;

                    //蓄力时的震动效果
                    if (attackTimer > ReelBackTime * 0.5f) {
                        npc.Center += Main.rand.NextVector2Circular(2f, 2f);
                    }

                    if (attackTimer == (int)(ReelBackTime * 0.7f) && !VaultUtils.isClient) {
                        SpawnPinkLaser();
                        //发射激光时的后坐力
                        npc.velocity -= npc.rotation.ToRotationVector2() * 6f;
                    }

                    //被攻击则提前打断蓄力
                    if (npc.justHit && attackTimer < ReelBackTime * 0.6f) {
                        npc.velocity = -npc.To(target.Center).UnitVector() * 4f;
                        state = 3f; //进入短暂停顿
                        attackTimer = 0f;
                        npc.netUpdate = true;
                        break;
                    }

                    if (attackTimer >= ReelBackTime) {
                        //冲刺方向扰动
                        float dashAngleOffset = Main.rand.NextFloat(-0.12f, 0.12f);
                        Vector2 dashDir = npc.To(target.Center).UnitVector().RotatedBy(dashAngleOffset);
                        //爆发性的冲刺速度
                        npc.velocity = dashDir * (hoverSpeed * 1.8f);
                        
                        //冲刺音效
                        SoundEngine.PlaySound(SoundID.Item74, npc.Center);

                        npc.oldPos = new Vector2[npc.oldPos.Length];
                        state = 2f;
                        attackTimer = 0f;
                        npc.netUpdate = true;
                    }
                    break;

                case 2f: //冲刺阶段
                    npc.knockBackResist = 0f;
                    npc.rotation = npc.velocity.ToRotation();
                    npc.damage = 95;
                    attackTimer++;
                    
                    //冲刺期间保持速度，模拟动量
                    if (attackTimer < 15) {
                        npc.velocity *= 1.02f;
                    } else {
                        npc.velocity *= 0.98f;
                    }

                    //冲刺失败后进入短暂思考状态
                    if (attackTimer > 45f || npc.collideX || npc.collideY) {
                        //撞击后的反弹或减速
                        npc.velocity *= 0.5f;
                        state = 3f;
                        attackTimer = 0f;
                        npc.netUpdate = true;
                    }
                    break;

                case 3f: //停顿等待阶段（失败后思考）
                    npc.velocity *= 0.94f;
                    //缓慢转向目标
                    float recoverAngle = npc.AngleTo(target.Center);
                    npc.rotation = npc.rotation.AngleLerp(recoverAngle, 0.05f);
                    attackTimer++;

                    if (attackTimer > 30f) {
                        state = 0f;
                        attackTimer = 0f;
                        generalTimer = 0; //重置总计时器
                        npc.netUpdate = true;
                    }
                    break;
            }

            generalTimer++;
            return false;
        }
        private void SpawnPinkLaser() {
            //我真的非常厌恶这些莫名其妙的伤害计算，泰拉的伤害计算就是一堆非常庞大的垃圾堆
            int damage = CWRRef.GetProjectileDamage(npc, ProjectileID.PinkLaser);
            //仅在启用 EarlyHardmodeProgressionRework 且非 BossRush 模式时调整伤害
            if (CWRRef.GetEarlyHardmodeProgressionReworkBool() && !CWRRef.GetBossRushActive()) {
                //计算击败的机械 Boss 数量
                int downedMechBosses = (NPC.downedMechBoss1 ? 1 : 0) + (NPC.downedMechBoss2 ? 1 : 0) + (NPC.downedMechBoss3 ? 1 : 0);
                //根据击败的机械 Boss 数量调整伤害
                if (downedMechBosses == 0) {
                    damage = (int)(damage * 0.9f);
                }
                else if (downedMechBosses == 1) {
                    damage = (int)(damage * 0.95f);
                }
                //如果击败了 2 个或更多机械 Boss，不调整伤害
            }
            //发射音效
            SoundEngine.PlaySound(SoundID.Item12, npc.Center);
            Projectile.NewProjectile(npc.GetSource_FromAI()
                                        , npc.Center, npc.rotation.ToRotationVector2()
                                        , ModContent.ProjectileType<PrimeCannonOnSpan>(), damage, 0f
                                        , Main.myPlayer, -1, -1, 0);

            //发射时的粒子效果
            for (int i = 0; i < 10; i++) {
                Vector2 dustVel = npc.rotation.ToRotationVector2().RotatedByRandom(0.5f) * Main.rand.NextFloat(2f, 5f);
                Dust.NewDust(npc.Center, 0, 0, DustID.PinkTorch, dustVel.X, dustVel.Y);
            }
        }
        public override bool? Draw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (HeadPrimeAI.DontReform()) {
                return true;
            }

            SpriteEffects spriteEffects = SpriteEffects.None;
            float drawRot = npc.rotation + MathHelper.Pi;
            if (npc.spriteDirection > 0) {
                spriteEffects = SpriteEffects.FlipHorizontally;
            }

            Texture2D value = Probe.Value;
            Texture2D value2 = Probe_Glow.Value;
            
            //冲刺时的残影效果增强
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
        public override bool PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) => !HeadPrimeAI.DontReform();
    }
}
