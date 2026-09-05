using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicCataclysm
{
    /// <summary>
    /// 星云奥秘重铸（P13 抬档 B→A，终局件）。材质身份：星云凝浆（漩臂里舀出来的一捧活星雾）。<br/>
    /// ①左键 rider：奥秘巨球 300px 内非 Boss 小敌被缓缓拽向球心②施法有推杖蓄势。
    /// 拖曳是控场收益不计伤害，底伤加成保持 5%
    /// </summary>
    internal class GsNebulaArcanum : GsCataclysmScheme
    {
        public override int TargetItemID => ItemID.NebulaArcanum;

        protected override string GsDescFallback =>
            "Reforged: the arcanum orb itself now swells in flight and slowly drags lesser foes toward its heart";
        public override int ChargePerHit => 3;

        protected override float PassiveDamageBonus => 0.05f;

        /// <summary>原版奥秘巨球弹类型</summary>
        private static int OrbType => ContentSamples.ItemsByType[ItemID.NebulaArcanum].shoot;

        //==================== 动画法：推杖蓄势 ====================

        public override void GsUseStyle(Item item, Player player, Rectangle heldItemFrame) {
            if (player.itemAnimationMax <= 0) {
                return;
            }
            //推杖蓄势：杖身先前送 4px 微抬再指数回坐，读作把凝浆推出去（确定性输入，各端一致）
            float elapsed = 1f - player.itemAnimation / (float)player.itemAnimationMax;
            float kick = MathF.Exp(-4.5f * elapsed);
            Vector2 aimDir = player.itemRotation.ToRotationVector2() * player.direction;
            player.itemLocation += aimDir * (4f * kick);
            player.itemLocation.Y -= 1.2f * kick * player.gravDir;
        }

        //==================== 左键 rider：微引力 ====================

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            if (proj.type != OrbType) {
                return;
            }
            //微引力：300px 内非 Boss、吃击退的小敌被缓缓拽向球心。
            //NPC 位移权威在服务器（单人即本机），联机客机不写速度防漂移
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                foreach (NPC npc in Main.ActiveNPCs) {
                    if (!npc.CanBeChasedBy() || npc.boss || npc.knockBackResist <= 0f) {
                        continue;
                    }
                    if (npc.realLife >= 0 && Main.npc[npc.realLife].boss) {
                        continue;
                    }
                    float dist = Vector2.Distance(npc.Center, proj.Center);
                    if (dist > 300f || dist < 24f) {
                        continue;
                    }
                    Vector2 pull = (proj.Center - npc.Center).SafeNormalize(Vector2.Zero)
                        * 0.22f * npc.knockBackResist * (1f - dist / 300f);
                    npc.velocity += pull;
                }
            }
        }

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router) {
            //基类积奥秘
            base.GsProjOnHitNPC(proj, target, hit, damageDone, router);
            if (VaultUtils.isServer) {
                return;
            }
            //命中反馈：巨球命中音
            if (proj.type == OrbType) {
                SoundEngine.PlaySound(SoundID.Item103 with { Volume = 0.35f, Pitch = -0.2f, MaxInstances = 3 }, target.Center);
            }
        }
    }
}
