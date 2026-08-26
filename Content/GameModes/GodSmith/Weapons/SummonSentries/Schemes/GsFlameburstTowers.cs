using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonSentries.Projectiles;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonSentries.Schemes
{
    /// <summary>
    /// 火焰爆裂塔三档「压制火炮」共享基类（kit 由 T1 宿主注册，档位差异按 tier 分支）：<br/>
    /// 充能 8/8/10，超频 300 帧「饱和轰击」= 每发火球补两发 ±6°（0.65×）且命中留火圈
    /// （半径 60/75/90，0.3× tick）；T3 超频追加每 90 帧一轮 5 枚迫击火雨（0.5× 抛物线）；
    /// 与弩炮成链：火球命中额外环带溅射（0.5×，只打原爆炸圈外，等效爆炸半径 +30%）
    /// </summary>
    internal abstract class GsFlameburstBase : GsSentryScheme
    {
        protected sealed override int FamilyIdx => GsSentryFamilyIdx.Flameburst;

        /// <summary>数值行档位旋钮</summary>
        protected abstract float DamageMult { get; }

        public sealed override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= DamageMult;

        /// <summary>火圈半径按档：60/75/90</summary>
        private static float FireRingRadius(int tier) => 60f + 15f * tier;

        /// <summary>超频「饱和轰击」：补两发偏角火球，等效三连发</summary>
        protected sealed override void OnOverdriveBoltSpawn(Projectile bolt, Projectile tower, int tier) {
            for (int i = -1; i <= 1; i += 2) {
                SpawnBoltHandled(tower, bolt.Center, bolt.velocity.RotatedBy(i * 0.105f),
                    bolt.type, (int)(bolt.damage * 0.65f), bolt.knockBack);
            }
        }

        protected sealed override void OnSentryHit(Projectile proj, Projectile tower, NPC target,
            NPC.HitInfo hit, int damageDone, GsSentryLocal st) {
            if (tower == null || !SentryGrid.TryGetTowerKit(tower.type, out SentryKit kit)) {
                return;
            }
            int tier = kit.TierOf(tower.type);
            GsSentryLocal towerState = SentryGrid.StateOf(tower);
            //超频弹命中留火圈（owner 真弹幕，队友可见）
            if (st.OverdriveShot) {
                Projectile.NewProjectile(SentrySource(proj), target.Center, Vector2.Zero,
                    ModContent.ProjectileType<GsSentryZoneProj>(),
                    (int)(tower.damage * 0.3f), 0f, proj.owner,
                    GsSentryZoneProj.StyleFireRing, FireRingRadius(tier), 60f);
            }
            //组合技（与弩炮成链）：环带溅射，等效爆炸半径 +30%；每塔 30 帧一次
            uint now = Main.GameUpdateCount;
            if ((towerState.LinkMask & 1 << GsSentryFamilyIdx.Ballista) != 0
                && towerState.LastComboTick + 30 <= now) {
                towerState.LastComboTick = now;
                Projectile.NewProjectile(SentrySource(proj), target.Center, Vector2.Zero,
                    ModContent.ProjectileType<GsSentryBurstProj>(),
                    (int)(tower.damage * 0.5f), 2f, proj.owner,
                    GsSentryBurstProj.StyleFlameSplash, 92f);
            }
        }

        /// <summary>T3 超频周期技：每 90 帧向最近敌方向抛 5 枚迫击火雨</summary>
        internal sealed override void OverdrivePulse(Projectile tower, Projectile odProj, int age) {
            if (age % 90 != 0 || !SentryGrid.TryGetTowerKit(tower.type, out SentryKit kit)
                || kit.TierOf(tower.type) != 2) {
                return;
            }
            NPC target = null;
            float bestDist = 900f;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy(odProj)) {
                    continue;
                }
                float dist = npc.Center.Distance(tower.Center);
                if (dist < bestDist) {
                    bestDist = dist;
                    target = npc;
                }
            }
            float aimX = target != null
                ? MathHelper.Clamp((target.Center.X - tower.Center.X) / 45f, -8f, 8f)
                : tower.spriteDirection * 3f;
            for (int i = 0; i < 5; i++) {
                Vector2 vel = new(aimX + Main.rand.NextFloat(-1.6f, 1.6f), -Main.rand.NextFloat(9f, 12.5f));
                Projectile.NewProjectile(SentrySource(tower), tower.Center - new Vector2(0f, 12f), vel,
                    ModContent.ProjectileType<GsSentryBoltProj>(),
                    (int)(tower.damage * 0.5f), 2f, tower.owner, GsSentryBoltProj.StyleMortar);
            }
        }
    }

    /// <summary>火焰爆裂杆 T1（kit 宿主：塔与弹六类型通道皆注册于此）</summary>
    internal class GsFlameburstT1 : GsFlameburstBase
    {
        public override int TargetItemID => ItemID.DD2FlameburstTowerT1Popper;

        protected override float DamageMult => 1.12f;

        protected override string GsDescFallback =>
            "Deploy doctrine: hits charge the tower, right-click when full for saturation barrage\n" +
            "Overdriven shots fly in triples and scorch a fire ring on hit; linking a ballista widens the blast";

        protected override SentryKit BuildKit() => new() {
            TowerTypes = [ProjectileID.DD2FlameBurstTowerT1, ProjectileID.DD2FlameBurstTowerT2, ProjectileID.DD2FlameBurstTowerT3],
            BoltTypes = [ProjectileID.DD2FlameBurstTowerT1Shot, ProjectileID.DD2FlameBurstTowerT2Shot, ProjectileID.DD2FlameBurstTowerT3Shot],
            ChargeMax = [8, 8, 10],
            OverdriveDuration = 300,
        };
    }

    /// <summary>火焰爆裂藤 T2（物品面档，弹幕通道由 T1 宿主统一认领）</summary>
    internal class GsFlameburstT2 : GsFlameburstBase
    {
        public override int TargetItemID => ItemID.DD2FlameburstTowerT2Popper;

        protected override float DamageMult => 1.10f;

        protected override string GsDescFallback =>
            "Deploy doctrine: hits charge the tower, right-click when full for saturation barrage\n" +
            "Overdriven shots fly in triples and scorch a wider fire ring; linking a ballista widens the blast";

        protected override SentryKit BuildKit() => null;
    }

    /// <summary>火焰爆裂杖 T3（超频追加迫击火雨）</summary>
    internal class GsFlameburstT3 : GsFlameburstBase
    {
        public override int TargetItemID => ItemID.DD2FlameburstTowerT3Popper;

        protected override float DamageMult => 1.08f;

        protected override string GsDescFallback =>
            "Deploy doctrine: hits charge the tower, right-click when full for saturation barrage\n" +
            "Overdrive adds mortar rain every 1.5s on top of triple shots and fire rings";

        protected override SentryKit BuildKit() => null;
    }
}
