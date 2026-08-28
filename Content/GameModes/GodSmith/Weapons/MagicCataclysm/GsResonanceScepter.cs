using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicCataclysm.Projectiles;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicCataclysm
{
    /// <summary>
    /// 共鸣权杖重铸（P13 左键 rider）。材质身份：鎏金谐振晶（驻波节点的余振）。<br/>
    /// ①左键 rider：「波节」，王家音波每穿透第 3 名敌人，波节就落在他身上奏响
    /// 驻波脉冲，呼应大招的波节打击②主波曳金环涟漪与鎏金重影③满量右键「谐振崩解」照旧
    /// ④施法有杖头轻扬响应。波节脉冲 0.45× 只在穿群时触发 ≈ +6%，计入包络
    /// </summary>
    internal class GsResonanceScepter : GsCataclysmScheme
    {
        public override int TargetItemID => ItemID.PrincessWeapon;

        protected override string GsDescFallback =>
            "Reforged: hits build Resonance; at full charge, right click to collapse the harmonics at your cursor\n" +
            "Five standing waves cross the area and their nodes strike whatever lingers\n" +
            "The royal wave rings a standing wave pulse on every 3rd foe it pierces";

        public override int ChargePerHit => 3;

        public override int CataclysmManaCost => 45;

        protected override float PassiveDamageBonus => 0.08f;

        protected override int DirectorType => ModContent.ProjectileType<GsResonanceCollapseDirector>();

        protected override Color AccentColor => new(255, 214, 130);

        protected override SoundStyle TriggerSound => SoundID.Item25;

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
            //杖头轻扬：抬 3px 带一记后旋，读作敲响音叉（确定性输入，各端一致）
            float progress = player.itemAnimation / (float)player.itemAnimationMax;
            player.itemLocation += new Vector2(-player.direction * 1.5f, -3f) * progress;
            player.itemRotation -= player.direction * 0.09f * progress;
        }

        //==================== 左键 rider：谐振涟漪 + 波节脉冲 ====================

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            if (proj.type != WaveType || VaultUtils.isServer) {
                return;
            }
            Lighting.AddLight(proj.Center, GsResonanceCollapseDirector.ResonGold.ToVector3() * 0.28f);
            //金环涟漪：音波一路荡开的同心环
            if (proj.timeLeft % 12 == 0) {
                PRTLoader.NewParticle<PRT_StarPulseRing>(proj.Center, Vector2.Zero,
                    GsResonanceCollapseDirector.ResonGold, 0.04f)?.Configure(0.04f, 0.16f, 12);
            }
            if (proj.timeLeft % 5 == 0) {
                PRTLoader.NewParticle<PRT_Light>(proj.Center - proj.velocity * 0.4f,
                    -proj.velocity * 0.05f, GsResonanceCollapseDirector.ResonPink, 0.07f)?.Configure(10, 0.8f);
            }
        }

        public override bool? GsProjPreDraw(Projectile proj, ref Color lightColor, GodSmithProjRouter router) {
            if (proj.type != WaveType) {
                return null;
            }
            GsCataclysmRiderLib.DrawSpeedGhost(proj, GsResonanceCollapseDirector.ResonGold, 0.42f);
            return null;
        }

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router) {
            //基类积共振
            base.GsProjOnHitNPC(proj, target, hit, damageDone, router);
            if (proj.type != WaveType) {
                return;
            }
            //命中反馈：金振尘
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 2; i++) {
                    PRTLoader.NewParticle<PRT_Light>(target.Center + Main.rand.NextVector2Circular(8f, 8f),
                        Main.rand.NextVector2Circular(1.2f, 1.2f),
                        GsResonanceCollapseDirector.ResonGold, Main.rand.NextFloat(0.08f, 0.12f))?.Configure(12, 0.85f);
                }
            }
            if (!proj.IsOwnedByLocalPlayer()) {
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
