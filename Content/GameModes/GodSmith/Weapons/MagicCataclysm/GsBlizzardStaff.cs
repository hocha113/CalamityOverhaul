using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicCataclysm.Projectiles;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicConduit;
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
    /// 暴雪法杖重铸（P13 左键 rider）。材质身份：极寒冰晶（白灾前奏的霜种）。<br/>
    /// ①左键 rider：每第 5 支冰矢淬成「霜种矢」，通体亮白、消亡时炸开小型霜爆
    /// 并留下升腾冰晶，呼应大招散场的冰晶棘②普通冰矢曳雪雾③满量右键「白灾」照旧
    /// ④施法有举杖响应。霜种爆 0.5×/5 折算命中率 ≈ +5%，计入包络
    /// </summary>
    internal class GsBlizzardStaff : GsCataclysmScheme
    {
        public override int TargetItemID => ItemID.BlizzardStaff;

        protected override string GsDescFallback =>
            "Reforged: hits build Snowsquall; at full charge, right click to call the Whiteout over your cursor\n" +
            "A giant blizzard hammers the area and leaves rime spikes on the ground\n" +
            "Every 5th icicle is a rime seed that bursts into a small frost blast where it dies";

        public override int ChargePerHit => 3;

        public override int CataclysmManaCost => 50;

        protected override float PassiveDamageBonus => 0.08f;

        protected override int DirectorType => ModContent.ProjectileType<GsWhiteoutDirector>();

        protected override Color AccentColor => new(150, 210, 255);

        protected override SoundStyle TriggerSound => SoundID.Item30;

        /// <summary>原版冰矢弹类型</summary>
        private static int IcicleType => ContentSamples.ItemsByType[ItemID.BlizzardStaff].shoot;

        /// <summary>冰矢出生计数（打标窗口只在生成端执行，本机契约；原版每次施放降 2 支，逐支计）</summary>
        private int icicleCounter;

        //==================== 动画法：举杖唤雪 ====================

        public override void GsUseStyle(Item item, Player player, Rectangle heldItemFrame) {
            if (player.itemAnimationMax <= 0) {
                return;
            }
            //举杖唤雪：杖头抬升 4px 再缓落（绝对剖面 0.1·p，差分施加防累积漂移；本杖动画双发，中途 snap 由差分清账）
            float n = player.itemAnimationMax;
            float progress = player.itemAnimation / n;
            player.itemLocation += new Vector2(-player.direction * 1.5f, -4f) * progress;
            GsMagicKickMath.ApplyKickDiff(player, 0.1f * progress, 0.1f * ((player.itemAnimation + 1) / n));
        }

        //==================== 左键 rider：霜种矢 ====================

        public override void GsProjOnSpawnMarked(Projectile proj, GodSmithProjRouter router) {
            if (proj.type != IcicleType) {
                return;
            }
            //每第 5 支冰矢淬霜种：MarkData=1 随生成包过线，远端同样看到霜种形态
            icicleCounter++;
            if (icicleCounter % 5 == 0) {
                router.MarkData = 1f;
            }
        }

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            if (proj.type != IcicleType || VaultUtils.isServer) {
                return;
            }
            bool seed = router.MarkData >= 1f;
            Lighting.AddLight(proj.Center, GsWhiteoutDirector.FrostBlue.ToVector3() * (seed ? 0.32f : 0.16f));
            //雪雾曳尾：霜种更密
            if (proj.timeLeft % (seed ? 3 : 5) == 0) {
                PRTLoader.NewParticle<PRT_Light>(proj.Center - proj.velocity * 0.5f,
                    -proj.velocity * 0.04f + Main.rand.NextVector2Circular(0.3f, 0.3f),
                    seed ? GsWhiteoutDirector.FrostPale : GsWhiteoutDirector.FrostBlue,
                    seed ? 0.1f : 0.07f)?.Configure(11, 0.8f);
            }
        }

        public override bool? GsProjPreDraw(Projectile proj, ref Color lightColor, GodSmithProjRouter router) {
            if (proj.type != IcicleType) {
                return null;
            }
            //霜种矢亮白重影加倍，普通冰矢淡霜影
            bool seed = router.MarkData >= 1f;
            GsCataclysmRiderLib.DrawSpeedGhost(proj,
                seed ? GsWhiteoutDirector.FrostPale : GsWhiteoutDirector.FrostBlue,
                seed ? 0.52f : 0.24f);
            return null;
        }

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router) {
            base.GsProjOnHitNPC(proj, target, hit, damageDone, router);
            if (proj.type != IcicleType || VaultUtils.isServer) {
                return;
            }
            //冰晶迸散命中反馈
            for (int i = 0; i < 2; i++) {
                PRTLoader.NewParticle<PRT_Sparkle>(target.Center + Main.rand.NextVector2Circular(8f, 8f),
                    Main.rand.NextVector2Circular(1.2f, 1.2f) - new Vector2(0f, 0.6f),
                    GsWhiteoutDirector.FrostPale, Main.rand.NextFloat(0.26f, 0.4f))
                    ?.Configure(GsWhiteoutDirector.FrostBlue, 18, 0.05f, 0.85f);
            }
        }

        public override void GsProjOnKill(Projectile proj, int timeLeft, GodSmithProjRouter router) {
            if (proj.type != IcicleType || router.MarkData < 1f) {
                return;
            }
            //霜种落点：小型霜爆（真弹幕跨端可见；Misc 源不承签，防爆体再袭）+ 升腾冰晶余痕
            if (proj.IsOwnedByLocalPlayer()) {
                Player owner = Main.player[proj.owner];
                Projectile.NewProjectile(owner.GetSource_Misc("GsCataclysmRider"), proj.Center, Vector2.Zero,
                    ModContent.ProjectileType<GsCataclysmRiderBurstProj>(),
                    Math.Max(1, (int)(proj.damage * 0.5f)), 2f, proj.owner,
                    70f, GsCataclysmRiderBurstProj.ThemeFrost);
            }
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_Sparkle>(proj.Center + Main.rand.NextVector2Circular(12f, 6f),
                    new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), -Main.rand.NextFloat(0.5f, 1.3f)),
                    GsWhiteoutDirector.FrostPale, Main.rand.NextFloat(0.3f, 0.5f))
                    ?.Configure(GsWhiteoutDirector.FrostBlue, 30, 0.04f, 1f);
            }
        }
    }
}
