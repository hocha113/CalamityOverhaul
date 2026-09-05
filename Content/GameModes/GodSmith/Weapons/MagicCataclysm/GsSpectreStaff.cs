using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicConduit;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicCataclysm
{
    /// <summary>
    /// 幽灵法杖重铸（A 档）。材质身份：渡魂青焰（幽灵套的苍青冥火）。<br/>
    /// ①命中积攒「收魂」，每渡满四分之一计量分出一缕副魂扑向近旁另一敌；②施法有举杖响应
    /// </summary>
    internal class GsSpectreStaff : GsCataclysmScheme
    {
        public override int TargetItemID => ItemID.SpectreStaff;

        protected override string GsDescFallback =>
            "Reforged: every quarter of the gauge banked splits off an echo soul that hunts another nearby foe";
        protected override float PassiveDamageBonus => 0.08f;
        public override int ChargePerHit => 4;

        /// <summary>原版迷魂弹类型</summary>
        internal static int SoulType => ContentSamples.ItemsByType[ItemID.SpectreStaff].shoot;

        //==================== 动画法：举杖 ====================

        public override void GsUseStyle(Item item, Player player, Rectangle heldItemFrame) {
            if (player.itemAnimationMax <= 0) {
                return;
            }
            //施法举杖：杖头抬升 5px 再缓落（绝对剖面 0.12·p，差分施加防累积漂移）
            float n = player.itemAnimationMax;
            float progress = player.itemAnimation / n;
            player.itemLocation += new Vector2(-player.direction * 2f, -5f) * progress;
            GsMagicKickMath.ApplyKickDiff(player, 0.12f * progress, 0.12f * ((player.itemAnimation + 1) / n));
        }

        //==================== 左键 rider：渡魂副魂 ====================

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router) {
            if (proj.type != SoulType || !proj.IsOwnedByLocalPlayer()) {
                base.GsProjOnHitNPC(proj, target, hit, damageDone, router);
                return;
            }
            //计量里程碑检测：跨过四分之一刻度即渡出副魂（副魂命中不再触发，防自喂）
            GsCataclysmPlayer state = Main.player[proj.owner].GetModPlayer<GsCataclysmPlayer>();
            int before = state.BoundItemType == TargetItemID ? state.Charge : 0;
            base.GsProjOnHitNPC(proj, target, hit, damageDone, router);
            if (router.MarkData >= 1f || state.Charge <= before || state.Charge >= ChargeMax) {
                return;
            }
            if (state.Charge / 25 > before / 25) {
                SpawnEchoSoul(proj, target);
            }
        }

        /// <summary>渡出副魂：从命中处扑向近旁另一敌（MarkData=1 防自喂标）</summary>
        private void SpawnEchoSoul(Projectile proj, NPC target) {
            NPC next = FindAnotherEnemy(target);
            Vector2 vel = next != null
                ? (next.Center - target.Center).SafeNormalize(-Vector2.UnitY) * 5f
                : -Vector2.UnitY * 4f;
            int damage = Math.Max(1, (int)(proj.damage * 0.35f));
            //出生前挂私有形态：经打标继承窗口写 MarkData（先于生成包）
            pendingEcho = true;
            Projectile.NewProjectile(proj.GetSource_FromThis(), target.Center, vel,
                SoulType, damage, proj.knockBack * 0.4f, proj.owner);
            pendingEcho = false;
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.NPCDeath6 with { Volume = 0.4f, Pitch = 0.55f, MaxInstances = 3 }, target.Center);
            }
        }

        private bool pendingEcho;

        public override void GsProjOnSpawnInherited(Projectile proj, GodSmithProjRouter router,
            Projectile parent, GodSmithProjRouter parentRouter) {
            if (pendingEcho && proj.owner == Main.myPlayer) {
                router.MarkData = 1f;
                proj.scale *= 0.72f;
                proj.netUpdate = true;
            }
        }

        /// <summary>找 target 之外最近的可追击敌怪</summary>
        private static NPC FindAnotherEnemy(NPC target) {
            NPC best = null;
            float bestDist = 480f * 480f;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy() || npc.whoAmI == target.whoAmI) {
                    continue;
                }
                float d = Vector2.DistanceSquared(npc.Center, target.Center);
                if (d < bestDist) {
                    bestDist = d;
                    best = npc;
                }
            }
            return best;
        }
    }
}
