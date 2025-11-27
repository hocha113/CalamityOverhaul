using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ID;

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
                    npc.velocity = Vector2.Lerp(npc.velocity, npc.To(destination).UnitVector() * hoverSpeed, 0.1f);
                    npc.rotation = npc.AngleTo(target.Center);

                    if (npc.WithinRange(destination, npc.velocity.Length() * 1.65f)) {
                        npc.velocity = npc.To(target.Center).UnitVector() * -7f;
                        state = 1f;
                        attackTimer = 0f;
                        npc.netUpdate = true;
                    }
                    break;

                case 1f: //蓄力准备
                    npc.velocity *= 0.975f;
                    npc.rotation = npc.AngleTo(target.Center);
                    attackTimer++;

                    if (attackTimer == ReelBackTime / 2 && !VaultUtils.isClient) {
                        SpawnPinkLaser();
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
                        npc.velocity = dashDir * hoverSpeed;

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

                    //冲刺失败后进入短暂思考状态
                    if (attackTimer > 60f || npc.collideX || npc.collideY) {
                        npc.velocity = -Vector2.UnitY.RotatedByRandom(0.6f) * 3f;
                        state = 3f;
                        attackTimer = 0f;
                        npc.netUpdate = true;
                    }
                    break;

                case 3f: //停顿等待阶段（失败后思考）
                    npc.velocity *= 0.9f;
                    npc.rotation = npc.AngleTo(target.Center);
                    attackTimer++;

                    if (attackTimer > 20f) {
                        state = 0f;
                        attackTimer = 0f;
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
            int proj = Projectile.NewProjectile(npc.FromObjectGetParent(), npc.Center
                , npc.rotation.ToRotationVector2() * 8, ProjectileID.PinkLaser, damage, 2);
            Main.projectile[proj].timeLeft = 300;
            Main.projectile[proj].netUpdate = true;
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
