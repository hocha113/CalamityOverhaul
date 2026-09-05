using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicCataclysm.Projectiles;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicConduit;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicCataclysm
{
    /// <summary>
    /// 大地法杖重铸（P13 左键 rider）。材质身份：熔岩脉岩浆巨石（造山的滚石前锋）。<br/>
    /// ①左键 rider：「碾磨」，巨石每碾穿一名敌人伤害递增 10%（至多 +40%）
    /// ②巨石碎裂处顶起一根半高岩柱（0.35×）③施法有沉杖响应。碾磨与岩柱计入包络：底伤加成自 10% 回缩至 6%
    /// </summary>
    internal class GsStaffofEarth : GsCataclysmScheme
    {
        public override int TargetItemID => ItemID.StaffofEarth;

        protected override string GsDescFallback =>
            "Reforged: the boulder grinds harder with every foe it crushes, and a half-height rock pillar erupts where it shatters";
        public override int ChargePerHit => 5;

        /// <summary>碾磨与碎裂岩柱是机制收益，底伤加成回缩（公约 §5）</summary>
        protected override float PassiveDamageBonus => 0.06f;

        /// <summary>原版巨石弹类型</summary>
        private static int BoulderType => ContentSamples.ItemsByType[ItemID.StaffofEarth].shoot;

        /// <summary>碾磨状态：各端各持（伤害修正只在攻击方端结算）</summary>
        private class GrindState
        {
            public int Crushed;
        }

        //==================== 动画法：沉杖起石 ====================

        public override void GsUseStyle(Item item, Player player, Rectangle heldItemFrame) {
            if (player.itemAnimationMax <= 0) {
                return;
            }
            //沉杖：杖头下压 3px 再回抬，读作把重量压进地里（绝对剖面 −0.1·p 下压，差分施加防累积漂移）
            float n = player.itemAnimationMax;
            float progress = player.itemAnimation / n;
            player.itemLocation += new Vector2(-player.direction * 1.5f, 3f) * progress;
            GsMagicKickMath.ApplyKickDiff(player, -0.1f * progress, -0.1f * ((player.itemAnimation + 1) / n));
        }

        //==================== 左键 rider：碾磨与碎裂岩柱 ====================

        public override void GsProjModifyHitNPC(Projectile proj, NPC target, ref NPC.HitModifiers modifiers, GodSmithProjRouter router) {
            if (proj.type != BoulderType) {
                return;
            }
            //碾磨：每碾穿一敌 +10%，至多 +40%（攻击方端结算）
            GrindState st = router.GetOrCreateState<GrindState>();
            modifiers.FinalDamage *= 1f + 0.10f * Math.Min(4, st.Crushed);
        }

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router) {
            base.GsProjOnHitNPC(proj, target, hit, damageDone, router);
            if (proj.type != BoulderType) {
                return;
            }
            GrindState st = router.GetOrCreateState<GrindState>();
            st.Crushed++;
            //碾轧反馈音
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Tink with { Volume = 0.5f, Pitch = -0.3f, MaxInstances = 3 }, target.Center);
            }
        }

        public override void GsProjOnKill(Projectile proj, int timeLeft, GodSmithProjRouter router) {
            if (proj.type != BoulderType) {
                return;
            }
            //碎裂余震：原地顶起半高岩柱（ai1=柱高；Misc 源不承签）
            if (proj.IsOwnedByLocalPlayer()) {
                Player owner = Main.player[proj.owner];
                Projectile.NewProjectile(owner.GetSource_Misc("GsCataclysmRider"),
                    proj.Center + new Vector2(0f, 8f), Vector2.Zero,
                    ModContent.ProjectileType<GsEarthPillarProj>(),
                    Math.Max(1, (int)(proj.damage * 0.35f)), proj.knockBack * 0.5f, proj.owner, 0f, 90f);
            }
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.45f, Pitch = 0.25f }, proj.Center);
        }
    }
}
