using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonSentries.Projectiles;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonSentries.Schemes
{
    /// <summary>
    /// 女王蜘蛛法杖「母巢工事」：<br/>
    /// 充能 10 命中，超频 360 帧「倾巢」= 每 60 帧额外孵 3 只小蜘蛛（场上自家 ≤12 封顶）
    /// 且吐蛋速率 ×1.5（每第 2 颗原蛋补 1 颗伴蛋）；
    /// 蛛蛋落点留 120 帧蛛网带（0.2× 蹭伤）；与闪电光环成链的小蜘蛛获电磁加速
    /// </summary>
    internal class GsQueenSpiderStaff : GsSentryScheme
    {
        public override int TargetItemID => ItemID.QueenSpiderStaff;

        protected override int FamilyIdx => GsSentryFamilyIdx.QueenSpider;

        protected override string GsDescFallback =>
            "Deploy doctrine: hits charge the hive, right-click when full to overdrive into a full swarm\n" +
            "Eggs leave webbed ground where they land; linking a lightning aura magnetizes the spiders";

        protected override SentryKit BuildKit() => new() {
            TowerTypes = [ProjectileID.SpiderHiver],
            BoltTypes = [ProjectileID.SpiderEgg, ProjectileID.BabySpider],
            ChargeMax = [10],
            OverdriveDuration = 360,
        };

        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.10f;

        /// <summary>超频吐蛋提速：每第 2 颗原蛋补 1 颗偏角伴蛋，等效速率 ×1.5</summary>
        protected override void OnOverdriveBoltSpawn(Projectile bolt, Projectile tower, int tier) {
            if (bolt.type != ProjectileID.SpiderEgg) {
                return;
            }
            GsSentryLocal towerState = SentryGrid.StateOf(tower);
            if (++towerState.EggCounter % 2 == 0) {
                SpawnBoltHandled(tower, bolt.Center,
                    bolt.velocity.RotatedBy(0.2f) * 0.92f, bolt.type, bolt.damage, bolt.knockBack);
            }
        }

        /// <summary>倾巢周期技：每 60 帧孵一窝 3 只（owner 端，封顶防挤爆）</summary>
        internal override void OverdrivePulse(Projectile tower, Projectile odProj, int age) {
            if (age % 60 != 0
                || Main.player[tower.owner].ownedProjectileCounts[ProjectileID.BabySpider] >= 12) {
                return;
            }
            for (int i = 0; i < 3; i++) {
                Vector2 vel = new(Main.rand.NextFloat(-2.5f, 2.5f), -Main.rand.NextFloat(2f, 4f));
                SpawnBoltHandled(tower, tower.Center + new Vector2(Main.rand.NextFloat(-12f, 12f), 6f),
                    vel, ProjectileID.BabySpider, tower.damage, 2f);
            }
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 4; i++) {
                    PRTLoader.NewParticle<PRT_ToxicBubble>(
                        tower.Center + Main.rand.NextVector2Circular(14f, 10f),
                        new Vector2(0f, -Main.rand.NextFloat(0.5f, 1f)),
                        new Color(150, 220, 90), Main.rand.NextFloat(0.4f, 0.7f))?.Configure(Main.rand.Next(16, 26));
                }
            }
        }

        /// <summary>蛛蛋落地：留蛛网带（owner 真弹幕，队友可见）</summary>
        protected override void OnBoltKilled(Projectile bolt, Projectile tower, GsSentryLocal st) {
            if (bolt.type != ProjectileID.SpiderEgg || tower == null) {
                return;
            }
            Projectile.NewProjectile(SentrySource(bolt), bolt.Center, Vector2.Zero,
                ModContent.ProjectileType<GsSentryZoneProj>(),
                (int)(tower.damage * 0.2f), 0f, bolt.owner,
                GsSentryZoneProj.StyleWebPatch, 60f, 120f);
        }

        /// <summary>光环链内小蜘蛛电磁加速：原版 AI 先跑我后写，模式关闭当帧即回原速</summary>
        protected override void BoltPostAI(Projectile bolt, SentryKit kit, GsSentryLocal st) {
            if (bolt.type != ProjectileID.BabySpider) {
                return;
            }
            Projectile tower = SentryGrid.ResolveHomeTower(bolt, st);
            if (tower == null
                || (SentryGrid.StateOf(tower).LinkMask & 1 << GsSentryFamilyIdx.LightningAura) == 0) {
                return;
            }
            bolt.velocity.X = MathHelper.Clamp(bolt.velocity.X * 1.3f, -8f, 8f);
            if (!VaultUtils.isServer && Main.GameUpdateCount % 6 == 0) {
                PRTLoader.NewParticle<PRT_GraniteVolt>(bolt.Center, -bolt.velocity * 0.1f,
                    new Color(140, 200, 255), Main.rand.NextFloat(0.3f, 0.5f))?.Configure(4);
            }
        }
    }
}
