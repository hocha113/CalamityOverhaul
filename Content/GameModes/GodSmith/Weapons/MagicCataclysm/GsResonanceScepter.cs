using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicCataclysm.Projectiles;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicConduit;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicCataclysm
{
    /// <summary>
    /// 共鸣权杖重铸（P13 左键 rider）。材质身份：鎏金谐振晶（驻波节点的余振）。<br/>
    /// ①左键 rider：「波节」，王家音波每穿透第 3 名敌人，波节就落在他身上奏响
    /// 驻波脉冲②施法有杖头轻扬响应。波节脉冲 0.45× 只在穿群时触发 ≈ +6%，计入包络
    /// </summary>
    internal class GsResonanceScepter : GsCataclysmScheme
    {
        public override int TargetItemID => ItemID.PrincessWeapon;

        protected override string GsDescFallback =>
            "Reforged: the royal wave rings a standing wave pulse on every 3rd foe it pierces";
        public override int ChargePerHit => 3;

        protected override float PassiveDamageBonus => 0.08f;

        /// <summary>原版王家音波弹类型（无限穿透的驻波）</summary>
        private static int WaveType => ContentSamples.ItemsByType[ItemID.PrincessWeapon].shoot;

        /// <summary>穿深状态：各端各持（命中结算只在攻击方端）</summary>
        private class NodeState
        {
            public int Pierced;
        }

        //==================== 动画法：杖头轻扬 ====================

        public override void GsUseStyle(Item item, Player player, Rectangle heldItemFrame) {
            if (player.itemAnimationMax <= 0) {
                return;
            }
            //杖头轻扬：抬 3px 带一记后旋，读作敲响音叉（绝对剖面 0.09·p，差分施加防累积漂移）
            float n = player.itemAnimationMax;
            float progress = player.itemAnimation / n;
            player.itemLocation += new Vector2(-player.direction * 1.5f, -3f) * progress;
            GsMagicKickMath.ApplyKickDiff(player, 0.09f * progress, 0.09f * ((player.itemAnimation + 1) / n));
        }

        //==================== 左键 rider：波节脉冲 ====================

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router) {
            //基类积共振
            base.GsProjOnHitNPC(proj, target, hit, damageDone, router);
            if (proj.type != WaveType || !proj.IsOwnedByLocalPlayer()) {
                return;
            }
            //波节：同一支音波每穿透第 3 名敌人，波节落在他身上（穿深里程碑，攻击方端结算）
            NodeState st = router.GetOrCreateState<NodeState>();
            st.Pierced++;
            if (st.Pierced % 3 != 0) {
                return;
            }
            //驻波脉冲（真弹幕跨端可见；Misc 源不承签）
            Player owner = Main.player[proj.owner];
            Projectile.NewProjectile(owner.GetSource_Misc("GsCataclysmRider"), target.Center, Vector2.Zero,
                ModContent.ProjectileType<GsCataclysmRiderBurstProj>(),
                Math.Max(1, (int)(proj.damage * 0.45f)), 2f, proj.owner,
                80f, GsCataclysmRiderBurstProj.ThemeNode);
        }
    }
}
