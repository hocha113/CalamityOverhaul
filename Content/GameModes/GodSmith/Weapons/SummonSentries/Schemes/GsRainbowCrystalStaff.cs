using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonSentries.Projectiles;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonSentries.Schemes
{
    /// <summary>
    /// 彩虹水晶法杖「棱镜阵列」（经典不毁：彩色爆点语言原样保留，只加不改）：<br/>
    /// 联动 = 与任意哨兵成链时，每次爆点折射一道彩光射线击最近另一敌（0.5×，每塔 20 帧一次）；
    /// 充能 12，超频 300 帧「极光帷幕」= 水晶上方展开 300px 宽极光幕（0.35× tick）
    /// </summary>
    internal class GsRainbowCrystalStaff : GsSentryScheme
    {
        public override int TargetItemID => ItemID.RainbowCrystalStaff;

        protected override int FamilyIdx => GsSentryFamilyIdx.RainbowCrystal;

        protected override string GsDescFallback =>
            "Deploy doctrine: linked to any sentry, each burst refracts a prism ray into another foe\n" +
            "Hits charge the crystal, right-click when full to unfurl an aurora veil above it";

        protected override SentryKit BuildKit() => new() {
            TowerTypes = [ProjectileID.RainbowCrystal],
            BoltTypes = [ProjectileID.RainbowCrystalExplosion],
            ChargeMax = [12],
            OverdriveDuration = 300,
        };

        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.08f;

        /// <summary>棱光折射：爆点出生时向「爆点之外最近敌」放一道彩光射线（owner 端）</summary>
        protected override void OnBoltFirstFrame(Projectile bolt, Projectile tower, GsSentryLocal st) {
            if (bolt.type != ProjectileID.RainbowCrystalExplosion || tower == null
                || !bolt.IsOwnedByLocalPlayer()) {
                return;
            }
            GsSentryLocal towerState = SentryGrid.StateOf(tower);
            uint now = Main.GameUpdateCount;
            if (towerState.LinkCount <= 0 || towerState.LastComboTick + 20 > now) {
                return;
            }
            NPC next = null;
            float bestDist = 260f;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy(bolt)) {
                    continue;
                }
                float dist = npc.Center.Distance(bolt.Center);
                //40px 内视作爆点主目标，折射只找「另一敌」
                if (dist < 40f || dist >= bestDist) {
                    continue;
                }
                bestDist = dist;
                next = npc;
            }
            if (next == null) {
                return;
            }
            towerState.LastComboTick = now;
            Vector2 vel = (next.Center - bolt.Center).SafeNormalize(Vector2.UnitX) * 26f;
            //色相种子随爆点错离，五彩不齐步
            Projectile.NewProjectile(SentrySource(bolt), bolt.Center, vel,
                ModContent.ProjectileType<GsSentryBoltProj>(),
                (int)(tower.damage * 0.5f), 1f, bolt.owner,
                GsSentryBoltProj.StylePrismRay, bolt.identity * 0.13f % 1f);
        }

        /// <summary>超频「极光帷幕」：开场在水晶上方铺 300 帧极光幕</summary>
        internal override void OverdrivePulse(Projectile tower, Projectile odProj, int age) {
            if (age != 1 || !SentryGrid.TryGetTowerKit(tower.type, out SentryKit kit)) {
                return;
            }
            Projectile.NewProjectile(SentrySource(tower), tower.Center - new Vector2(0f, 120f), Vector2.Zero,
                ModContent.ProjectileType<GsSentryZoneProj>(),
                (int)(tower.damage * 0.35f), 0f, tower.owner,
                GsSentryZoneProj.StyleAurora, 150f, kit.OverdriveDuration);
        }
    }
}
