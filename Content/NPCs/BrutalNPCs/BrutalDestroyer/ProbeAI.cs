using CalamityMod;
using CalamityMod.Events;
using CalamityMod.NPCs;
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
        public static int ReelBackTime => BossRushEvent.BossRushActive ? 30 : 60;
        public override bool? CanCWROverride() {
            if (CWRWorld.MachineRebellion) {
                return true;
            }
            return null;
        }
        public override void SetProperty() {
            if (CWRWorld.MachineRebellion) {
                npc.life = npc.lifeMax *= 28;
                npc.defDefense = npc.defense = 20;
                npc.defDamage = npc.damage *= 3;
                npc.knockBackResist = 0.1f;//在机械暴乱中拥有很强的抗击退能力
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

            Vector2 spawnOffset = Vector2.UnitY.RotatedBy(MathHelper.Lerp(-0.97f, 0.97f, npc.whoAmI % 16f / 16f)) * 300f;
            if (npc.whoAmI * 113 % 2 == 1) {
                spawnOffset *= -1f;
            }

            Vector2 destination = target.Center + spawnOffset;

            ref float generalTimer = ref npc.ai[2];
            ref float attackTimer = ref npc.ai[1];
            ref float state = ref npc.ai[0];

            Lighting.AddLight(npc.Center, Color.Red.ToVector3() * npc.scale);

            float hoverSpeed = 22f;
            if (DestroyerHeadAI.BossRush) {
                hoverSpeed *= 1.5f;
            }

            //默认无伤害，只有冲刺阶段启用伤害
            npc.damage = state == 2f ? npc.defDamage : 0;

            switch (state) {
                case 0f://先靠近
                    npc.velocity = Vector2.Lerp(npc.velocity, npc.SafeDirectionTo(destination) * hoverSpeed, 0.1f);
                    if (npc.WithinRange(destination, npc.velocity.Length() * 1.35f)) {
                        npc.velocity = npc.SafeDirectionTo(target.Center) * -7f;
                        state = 1f;
                        attackTimer = 0f;
                        npc.netUpdate = true;
                    }
                    npc.rotation = npc.AngleTo(target.Center);
                    break;

                case 1f://退退退
                    npc.velocity *= 0.975f;
                    attackTimer++;
                    npc.rotation = npc.AngleTo(target.Center);
                    if (attackTimer == ReelBackTime / 2 && !VaultUtils.isClient) {
                        SpawnPinkLaser();
                    }
                    if (attackTimer >= ReelBackTime) {
                        npc.velocity = npc.SafeDirectionTo(target.Center) * hoverSpeed;
                        npc.oldPos = new Vector2[npc.oldPos.Length];
                        state = 2f;
                        attackTimer = 0f;
                        npc.netUpdate = true;
                    }
                    break;

                case 2f://开始蓄力冲刺
                    npc.knockBackResist = 0f;
                    npc.rotation = npc.velocity.ToRotation();
                    npc.damage = 95;

                    attackTimer++;

                    //条件：冲刺持续时间超过一定时长 或 碰撞地形
                    if (attackTimer > 60f || npc.collideX || npc.collideY) {
                        npc.velocity = Vector2.Zero;
                        state = 0f;
                        attackTimer = 0f;
                        npc.netUpdate = true;
                    }
                    break;
            }

            npc.rotation += MathHelper.Pi;//旋转朝向修正
            generalTimer++;
            return false;
        }
        private void SpawnPinkLaser() {
            //我真的非常厌恶这些莫名其妙的伤害计算，泰拉的伤害计算就是一堆非常庞大的垃圾堆
            int damage = npc.GetProjectileDamage(ProjectileID.PinkLaser);
            //仅在启用 EarlyHardmodeProgressionRework 且非 BossRush 模式时调整伤害
            if (CalamityConfig.Instance.EarlyHardmodeProgressionRework && !BossRushEvent.BossRushActive) {
                //计算击败的机械 Boss 数量
                int downedMechBosses = (NPC.downedMechBoss1 ? 1 : 0) + (NPC.downedMechBoss2 ? 1 : 0) + (NPC.downedMechBoss3 ? 1 : 0);
                //根据击败的机械 Boss 数量调整伤害
                if (downedMechBosses == 0) {
                    damage = (int)(damage * CalamityGlobalNPC.EarlyHardmodeProgressionReworkFirstMechStatMultiplier_Expert);
                }
                else if (downedMechBosses == 1) {
                    damage = (int)(damage * CalamityGlobalNPC.EarlyHardmodeProgressionReworkSecondMechStatMultiplier_Expert);
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
            float drawRot = npc.rotation;
            if (npc.spriteDirection > 0) {
                spriteEffects = SpriteEffects.FlipHorizontally;
            }

            Texture2D value = Probe.Value;
            spriteBatch.Draw(value, npc.Center - Main.screenPosition
                , null, drawColor, drawRot, value.Size() / 2, npc.scale, spriteEffects, 0);
            Texture2D value2 = Probe_Glow.Value;
            spriteBatch.Draw(value2, npc.Center - Main.screenPosition
                , null, Color.White, drawRot, value.Size() / 2, npc.scale, spriteEffects, 0);
            return false;
        }
        public override bool PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) => !HeadPrimeAI.DontReform();
    }
}
