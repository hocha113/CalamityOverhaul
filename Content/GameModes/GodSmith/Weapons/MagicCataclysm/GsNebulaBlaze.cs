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
    /// 星云烈焰重铸（P13 左键 rider）。材质身份：星云引信火（新星引爆的火种）。<br/>
    /// ①左键 rider：「引信」，星云喷发强化弹（原版概率打出的大弹）命中必定点燃一枚
    /// 微新星双环爆，呼应大招的错拍环爆②普通弹曳粉紫引信火花，强化弹白热芯加倍
    /// ③满量右键「新星引爆」照旧④施法有急促后坐响应。
    /// 微新星与强化弹联动 ≈ +5%，底伤加成自 10% 回缩至 8%
    /// </summary>
    internal class GsNebulaBlaze : GsCataclysmScheme
    {
        public override int TargetItemID => ItemID.NebulaBlaze;

        protected override string GsDescFallback =>
            "Reforged: hits build Blaze; at full charge, right click to mark a nova on the target near your cursor\n" +
            "It detonates in three staggered blast rings around a gravity well\n" +
            "Nebula eruption bolts now always ignite a micro nova where they strike";

        public override int ChargePerHit => 4;

        public override int CataclysmManaCost => 55;

        /// <summary>微新星是机制收益，底伤加成回缩（公约 §5）</summary>
        protected override float PassiveDamageBonus => 0.08f;

        protected override int DirectorType => ModContent.ProjectileType<GsNovaDetonationDirector>();

        protected override Color AccentColor => new(255, 120, 210);

        protected override SoundStyle TriggerSound => SoundID.Item103;

        /// <summary>原版普通弹类型</summary>
        private static int BlazeType => ContentSamples.ItemsByType[ItemID.NebulaBlaze].shoot;

        protected override void ModifyTriggerParams(Item item, Player player, ref Vector2 anchor, ref float ai1, ref float ai2) {
            //光标 300px 内最近可追踪敌作为新星锚点，无则锚定触发点
            ai1 = -1f;
            float best = 300f;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.friendly || !npc.CanBeChasedBy() || npc.type == NPCID.TargetDummy) {
                    continue;
                }
                float dist = Vector2.Distance(npc.Center, anchor);
                if (dist < best) {
                    best = dist;
                    ai1 = i;
                }
            }
        }

        //==================== 动画法：急促后坐 ====================

        public override void GsUseStyle(Item item, Player player, Rectangle heldItemFrame) {
            if (player.itemAnimationMax <= 0) {
                return;
            }
            //快弹道法器的急促后坐：3px 快速回坐（确定性输入，各端一致）
            float elapsed = 1f - player.itemAnimation / (float)player.itemAnimationMax;
            float kick = MathF.Exp(-6f * elapsed);
            Vector2 aimDir = player.itemRotation.ToRotationVector2() * player.direction;
            player.itemLocation -= aimDir * (3f * kick);
        }

        //==================== 左键 rider：引信火花 + 强化弹微新星 ====================

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            bool eruption = proj.type == ProjectileID.NebulaBlaze2;
            if ((proj.type != BlazeType && !eruption) || VaultUtils.isServer) {
                return;
            }
            Lighting.AddLight(proj.Center,
                GsNovaDetonationDirector.NovaPink.ToVector3() * (eruption ? 0.4f : 0.24f));
            //引信火花：粉紫双色交替坠散，强化弹更密并带环脉冲
            if (proj.timeLeft % (eruption ? 2 : 4) == 0) {
                PRTLoader.NewParticle<PRT_Spark>(proj.Center - proj.velocity * 0.35f,
                    -proj.velocity * 0.06f + Main.rand.NextVector2Circular(0.4f, 0.4f),
                    Main.rand.NextBool() ? GsNovaDetonationDirector.NovaPink : GsNovaDetonationDirector.NovaViolet,
                    Main.rand.NextFloat(0.2f, 0.32f))?.Configure(false, Main.rand.Next(8, 14));
            }
            if (eruption && proj.timeLeft % 10 == 0) {
                PRTLoader.NewParticle<PRT_StarPulseRing>(proj.Center, Vector2.Zero,
                    GsNovaDetonationDirector.NovaViolet, 0.05f)?.Configure(0.05f, 0.2f, 10);
            }
        }

        public override bool? GsProjPreDraw(Projectile proj, ref Color lightColor, GodSmithProjRouter router) {
            bool eruption = proj.type == ProjectileID.NebulaBlaze2;
            if (proj.type != BlazeType && !eruption) {
                return null;
            }
            //强化弹：白热芯双层重影；普通弹：单层粉影
            GsCataclysmRiderLib.DrawSpeedGhost(proj,
                GsNovaDetonationDirector.NovaPink, eruption ? 0.55f : 0.34f);
            if (eruption) {
                GsCataclysmRiderLib.DrawSpeedGhost(proj, Color.White, 0.22f);
            }
            return null;
        }

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router) {
            //基类积烈焰
            base.GsProjOnHitNPC(proj, target, hit, damageDone, router);
            bool eruption = proj.type == ProjectileID.NebulaBlaze2;
            if (proj.type != BlazeType && !eruption) {
                return;
            }
            //命中反馈：星火迸散
            if (!VaultUtils.isServer) {
                for (int i = 0; i < (eruption ? 4 : 2); i++) {
                    PRTLoader.NewParticle<PRT_Spark>(target.Center + Main.rand.NextVector2Circular(8f, 8f),
                        Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.5f, 4f),
                        Main.rand.NextBool() ? GsNovaDetonationDirector.NovaPink : GsNovaDetonationDirector.NovaViolet,
                        Main.rand.NextFloat(0.26f, 0.42f))?.Configure(true, Main.rand.Next(12, 20));
                }
            }
            //引信兑现：强化弹命中必燃微新星（真弹幕跨端可见；Misc 源不承签）
            if (!eruption || !proj.IsOwnedByLocalPlayer()) {
                return;
            }
            Player owner = Main.player[proj.owner];
            Projectile.NewProjectile(owner.GetSource_Misc("GsCataclysmRider"), target.Center, Vector2.Zero,
                ModContent.ProjectileType<GsCataclysmRiderBurstProj>(),
                Math.Max(1, (int)(proj.damage * 0.4f)), 3f, proj.owner,
                80f, GsCataclysmRiderBurstProj.ThemeNova);
        }
    }
}
