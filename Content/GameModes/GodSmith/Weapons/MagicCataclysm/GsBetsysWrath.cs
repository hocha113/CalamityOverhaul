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
    /// 贝特西之怒重铸（P13 左键 rider）。材质身份：龙王孽焰（贝特西吐息的咒火）。<br/>
    /// ①左键 rider：火弹拖双螺旋焰幕；命中带贝特西之咒的敌人额外积 2 点龙焰计量，
    /// 每第 4 次咒火命中翻出一小团孽火爆，呼应大招的焰幕俯冲②满量右键「龙王孽焰」照旧
    /// ③施法有掷矛前压响应。孽火爆 0.25×/4 ≈ +6%，计入包络
    /// </summary>
    internal class GsBetsysWrath : GsCataclysmScheme
    {
        public override int TargetItemID => ItemID.ApprenticeStaffT3;

        protected override string GsDescFallback =>
            "Reforged: hits build Dragonfire; at full charge, right click to invoke the Dragon's Wrath\n" +
            "Betsy's shade dives three times trailing flame curtains, leaving a cursed pyre bed\n" +
            "Hits on cursed foes bank extra Dragonfire, and every 4th cursed hit bursts into hexfire";

        public override int ChargePerHit => 4;

        public override int CataclysmManaCost => 50;

        protected override float PassiveDamageBonus => 0.08f;

        protected override int DirectorType => ModContent.ProjectileType<GsDragonWrathDirector>();

        protected override Color AccentColor => new(255, 150, 60);

        protected override SoundStyle TriggerSound => SoundID.DD2_BetsyScream;

        /// <summary>原版龙焰弹类型</summary>
        private static int WrathType => ContentSamples.ItemsByType[ItemID.ApprenticeStaffT3].shoot;

        /// <summary>咒火命中计数（owner 端命中钩子消费，本机契约）</summary>
        private int cursedHits;

        //==================== 动画法：掷矛前压 ====================

        public override void GsUseStyle(Item item, Player player, Rectangle heldItemFrame) {
            if (player.itemAnimationMax <= 0) {
                return;
            }
            //掷矛前压：矛身前送 4px 带下压，读作把怒火掷出去（确定性输入，各端一致）
            float progress = player.itemAnimation / (float)player.itemAnimationMax;
            player.itemLocation += new Vector2(player.direction * 4f, 1f) * progress;
            player.itemRotation += player.direction * 0.08f * progress;
        }

        //==================== 左键 rider：双螺旋焰幕 + 咒火积怒 ====================

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            if (proj.type != WrathType || VaultUtils.isServer) {
                return;
            }
            Lighting.AddLight(proj.Center, GsDragonWrathDirector.BetsyOrange.ToVector3() * 0.32f);
            //双螺旋焰幕：两股孽火绕弹道对旋（identity 定相）
            if (proj.timeLeft % 3 == 0) {
                float phase = proj.timeLeft * 0.5f + proj.identity * 1.1f;
                Vector2 side = proj.velocity.SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2)
                    * MathF.Sin(phase) * 10f;
                PRTLoader.NewParticle<PRT_HellFlame>(proj.Center + side - proj.velocity * 0.3f,
                    -proj.velocity * 0.06f, GsDragonWrathDirector.BetsyOrange, Main.rand.NextFloat(0.28f, 0.42f));
                PRTLoader.NewParticle<PRT_HellFlame>(proj.Center - side - proj.velocity * 0.3f,
                    -proj.velocity * 0.06f, GsDragonWrathDirector.BetsyEmber, Main.rand.NextFloat(0.22f, 0.34f));
            }
        }

        public override bool? GsProjPreDraw(Projectile proj, ref Color lightColor, GodSmithProjRouter router) {
            if (proj.type != WrathType) {
                return null;
            }
            GsCataclysmRiderLib.DrawSpeedGhost(proj, GsDragonWrathDirector.BetsyOrange, 0.42f);
            return null;
        }

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router) {
            //基类积龙焰
            base.GsProjOnHitNPC(proj, target, hit, damageDone, router);
            if (proj.type != WrathType) {
                return;
            }
            //命中反馈：孽火迸溅
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 2; i++) {
                    PRTLoader.NewParticle<PRT_HellFlame>(target.Center + Main.rand.NextVector2Circular(8f, 8f),
                        Main.rand.NextVector2Circular(1.6f, 1.6f) - new Vector2(0f, 1f),
                        GsDragonWrathDirector.BetsyOrange, Main.rand.NextFloat(0.32f, 0.5f));
                }
            }
            //咒火积怒：怒火在被诅咒者身上烧得更旺（原版弹自带贝特西之咒）
            if (!proj.IsOwnedByLocalPlayer() || !target.HasBuff(BuffID.BetsysCurse)) {
                return;
            }
            Main.player[proj.owner].GetModPlayer<GsCataclysmPlayer>().AddCharge(2, ChargeMax, TargetItemID);
            cursedHits++;
            if (cursedHits % 4 != 0) {
                return;
            }
            //孽火爆：小团咒火翻起（真弹幕跨端可见；Misc 源不承签）
            Player owner = Main.player[proj.owner];
            Projectile.NewProjectile(owner.GetSource_Misc("GsCataclysmRider"), target.Center, Vector2.Zero,
                ModContent.ProjectileType<GsCataclysmRiderBurstProj>(),
                Math.Max(1, (int)(proj.damage * 0.25f)), 2f, proj.owner,
                60f, GsCataclysmRiderBurstProj.ThemeEmber);
        }
    }
}
