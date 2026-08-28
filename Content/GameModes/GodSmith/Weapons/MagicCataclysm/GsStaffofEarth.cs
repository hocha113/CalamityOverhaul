using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicCataclysm.Projectiles;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicCataclysm
{
    /// <summary>
    /// 大地法杖重铸（P13 左键 rider）。材质身份：熔岩脉岩浆巨石（造山的滚石前锋）。<br/>
    /// ①左键 rider：「碾磨」，巨石每碾穿一名敌人伤害递增 10%（至多 +40%），滚行坠岩屑
    /// ②巨石碎裂处顶起一根半高岩柱（0.35×），呼应大招的造山岩柱③满量右键「造山」照旧
    /// ④施法有沉杖响应。碾磨与岩柱计入包络：底伤加成自 10% 回缩至 6%
    /// </summary>
    internal class GsStaffofEarth : GsCataclysmScheme
    {
        public override int TargetItemID => ItemID.StaffofEarth;

        protected override string GsDescFallback =>
            "Reforged: hits build Earthwrath; at full charge, right click to raise the Orogeny at your cursor\n" +
            "Rock pillars erupt in waves amid flying rubble, leaving a magma vein bed behind\n" +
            "The boulder grinds harder with every foe it crushes, and a half-height rock pillar erupts where it shatters";

        public override int ChargePerHit => 5;

        public override int CataclysmManaCost => 55;

        /// <summary>碾磨与碎裂岩柱是机制收益，底伤加成回缩（公约 §5）</summary>
        protected override float PassiveDamageBonus => 0.06f;

        protected override int DirectorType => ModContent.ProjectileType<GsOrogenyDirector>();

        protected override Color AccentColor => new(255, 140, 52);

        protected override SoundStyle TriggerSound => SoundID.Item14;

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
            //沉杖：杖头下压 3px 再回抬，读作把重量压进地里（确定性输入，各端一致）
            float progress = player.itemAnimation / (float)player.itemAnimationMax;
            player.itemLocation += new Vector2(-player.direction * 1.5f, 3f) * progress;
            player.itemRotation += player.direction * 0.1f * progress;
        }

        //==================== 左键 rider：碾磨与碎裂岩柱 ====================

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            if (proj.type != BoulderType || VaultUtils.isServer) {
                return;
            }
            Lighting.AddLight(proj.Center, GsOrogenyDirector.MagmaOrange.ToVector3() * 0.3f);
            //滚行岩屑：巨石沿途坠碎石与熔火星
            if (proj.timeLeft % 4 == 0) {
                PRTLoader.NewParticle<PRT_MarbleChip>(proj.Center + Main.rand.NextVector2Circular(10f, 10f),
                    -proj.velocity * 0.15f + new Vector2(0f, -Main.rand.NextFloat(0.5f, 1.2f)),
                    new Color(175, 130, 90), Main.rand.NextFloat(0.5f, 0.8f))?.Configure(Main.rand.Next(16, 26));
            }
            if (proj.timeLeft % 7 == 0) {
                PRTLoader.NewParticle<PRT_Spark>(proj.Center - proj.velocity * 0.3f,
                    -proj.velocity * 0.05f + new Vector2(0f, 0.5f),
                    GsOrogenyDirector.MagmaOrange, Main.rand.NextFloat(0.24f, 0.36f))?.Configure(true, Main.rand.Next(12, 18));
            }
        }

        public override bool? GsProjPreDraw(Projectile proj, ref Color lightColor, GodSmithProjRouter router) {
            if (proj.type != BoulderType) {
                return null;
            }
            //熔芯重影：巨石带岩浆色曳影，碾磨层数越高越亮
            GrindState st = router.GetOrCreateState<GrindState>();
            GsCataclysmRiderLib.DrawSpeedGhost(proj, GsOrogenyDirector.MagmaOrange, 0.3f + 0.06f * st.Crushed);
            return null;
        }

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
            //碾轧反馈：碎石横飞
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Tink with { Volume = 0.5f, Pitch = -0.3f, MaxInstances = 3 }, target.Center);
                for (int i = 0; i < 3; i++) {
                    PRTLoader.NewParticle<PRT_MarbleChip>(target.Center + Main.rand.NextVector2Circular(10f, 10f),
                        Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.5f, 3.5f) - new Vector2(0f, 1f),
                        new Color(175, 130, 90), Main.rand.NextFloat(0.5f, 0.8f))?.Configure(Main.rand.Next(18, 28));
                }
            }
        }

        public override void GsProjOnKill(Projectile proj, int timeLeft, GodSmithProjRouter router) {
            if (proj.type != BoulderType) {
                return;
            }
            //碎裂余震：原地顶起半高岩柱（复用造山岩柱，ai1=柱高；Misc 源不承签）
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
            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_MarbleChip>(proj.Center + Main.rand.NextVector2Circular(12f, 8f),
                    new Vector2(Main.rand.NextFloat(-2.4f, 2.4f), -Main.rand.NextFloat(1f, 3f)),
                    new Color(175, 130, 90), Main.rand.NextFloat(0.55f, 0.9f))?.Configure(Main.rand.Next(20, 32));
            }
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_Spark>(proj.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 4.5f),
                    GsOrogenyDirector.MagmaOrange, Main.rand.NextFloat(0.28f, 0.44f))?.Configure(true, Main.rand.Next(14, 22));
            }
        }
    }
}
