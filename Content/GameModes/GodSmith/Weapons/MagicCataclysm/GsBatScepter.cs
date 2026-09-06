using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicConduit;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicCataclysm
{
    /// <summary>
    /// 蝙蝠权杖重铸（P13 左键 rider）。材质身份：暮紫夜革（月下蝠群的翼膜暗光）。<br/>
    /// ①左键 rider：每第 8 次蝙蝠咬中分出一只幻影蝠扑向近旁另一敌②施法有权杖上挑响应。
    /// 幻影蝠 0.4×/8 ≈ +5%，计入包络
    /// </summary>
    internal class GsBatScepter : GsCataclysmScheme
    {
        public override int TargetItemID => ItemID.BatScepter;

        protected override string GsDescFallback =>
            "Reforged: every 8th bat bite splits off a phantom bat that dives at another nearby foe";
        public override int ChargePerHit => 3;

        protected override float PassiveDamageBonus => 0.08f;

        /// <summary>原版蝙蝠弹类型</summary>
        private static int BatType => ContentSamples.ItemsByType[ItemID.BatScepter].shoot;

        /// <summary>蝙蝠咬中计数（owner 端命中钩子消费，本机契约）</summary>
        private int biteCounter;

        /// <summary>幻影蝠出生窗旗标（打标继承窗口写角色）</summary>
        private bool pendingPhantom;

        //==================== 动画法：权杖上挑 ====================

        public override void GsUseStyle(Item item, Player player, Rectangle heldItemFrame) {
            if (player.itemAnimationMax <= 0) {
                return;
            }
            //挥杖唤蝠：杖头上挑 3px 带一记后旋（绝对剖面 0.14·p，差分施加防累积漂移）
            float n = player.itemAnimationMax;
            float progress = player.itemAnimation / n;
            player.itemLocation += new Vector2(-player.direction * 2f, -3f) * progress;
            GsMagicKickMath.ApplyKickDiff(player, 0.14f * progress, 0.14f * ((player.itemAnimation + 1) / n));
        }

        //==================== 左键 rider：幻影蝠 ====================

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router) {
            //基类积夜幕（计量是攻击方本地量）
            base.GsProjOnHitNPC(proj, target, hit, damageDone, router);
            if (proj.type != BatType) {
                return;
            }
            //幻影蝠不再分裂（防自喂）；每第 8 咬渡出一只扑向近旁另一敌
            if (router.MarkData >= 1f || !proj.IsOwnedByLocalPlayer()) {
                return;
            }
            biteCounter++;
            if (biteCounter % 8 != 0) {
                return;
            }
            NPC next = GsCataclysmRiderLib.FindAnotherEnemy(target, target.Center, 460f);
            Vector2 vel = next != null
                ? (next.Center - target.Center).SafeNormalize(-Vector2.UnitY) * 7f
                : -Vector2.UnitY * 5f;
            int damage = Math.Max(1, (int)(proj.damage * 0.4f));
            pendingPhantom = true;
            Projectile.NewProjectile(proj.GetSource_FromThis(), target.Center, vel,
                BatType, damage, proj.knockBack * 0.5f, proj.owner);
            pendingPhantom = false;
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.NPCDeath4 with { Volume = 0.3f, Pitch = 0.5f, MaxInstances = 3 }, target.Center);
            }
        }

        public override void GsProjOnSpawnInherited(Projectile proj, GodSmithProjRouter router,
            Projectile parent, GodSmithProjRouter parentRouter) {
            //幻影蝠：出生窗打角色标（先于生成包），缩体 0.8
            if (pendingPhantom && proj.owner == Main.myPlayer) {
                router.MarkData = 1f;
                proj.scale *= 0.8f;
                proj.netUpdate = true;
            }
        }
    }
}
