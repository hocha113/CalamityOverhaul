using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonSentries.Projectiles;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonSentries.Schemes
{
    /// <summary>
    /// 闪电光环三档「力场发生器」共享基类（联动核心件）：<br/>
    /// 谐振区 = 两自家光环重叠处 tick ×1.5（owner 端判定，重叠带电弧桥视觉）；
    /// 充能 20 tick，超频 300 帧「过载力场」= 外扩 40% 的过载电环（0.5× tick），
    /// tick 附 90 帧感电（链内其他哨兵对感电目标 +10%）；T3 超频每 60 帧放链状闪电（跳 4 目标各 0.8×）
    /// </summary>
    internal abstract class GsLightningAuraBase : GsSentryScheme
    {
        protected sealed override int FamilyIdx => GsSentryFamilyIdx.LightningAura;

        protected abstract float DamageMult { get; }

        public sealed override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= DamageMult;

        /// <summary>光环覆盖半径：原版光环弹幕的判定体即覆盖区</summary>
        private static float AuraRadius(Projectile aura) => aura.width * 0.5f;

        private static bool IsAuraType(int type)
            => type == ProjectileID.DD2LightningAuraT1 || type == ProjectileID.DD2LightningAuraT2
                || type == ProjectileID.DD2LightningAuraT3;

        /// <summary>谐振区：命中点同时处于另一自家光环覆盖内 → tick ×1.5</summary>
        protected sealed override void ModifySentryHit(Projectile proj, Projectile tower, NPC target,
            ref NPC.HitModifiers modifiers, GsSentryLocal st) {
            if (!IsAuraType(proj.type) || st.LinkedTowers == null) {
                return;
            }
            foreach (int who in st.LinkedTowers) {
                if (who < 0 || who >= Main.maxProjectiles) {
                    continue;
                }
                Projectile other = Main.projectile[who];
                if (!other.active || other.owner != proj.owner || !IsAuraType(other.type)) {
                    continue;
                }
                if (other.Center.Distance(target.Center) <= AuraRadius(other)) {
                    modifiers.FinalDamage *= 1.5f;
                    return;
                }
            }
        }

        /// <summary>超频开场铺过载电环；T3 追加周期链状闪电</summary>
        internal sealed override void OverdrivePulse(Projectile tower, Projectile odProj, int age) {
            if (age == 1 && SentryGrid.TryGetTowerKit(tower.type, out SentryKit kit)) {
                Projectile.NewProjectile(SentrySource(tower), tower.Center, Vector2.Zero,
                    ModContent.ProjectileType<GsSentryZoneProj>(),
                    (int)(tower.damage * 0.5f), 0f, tower.owner,
                    GsSentryZoneProj.StyleOverloadRing, AuraRadius(tower) * 1.4f, kit.OverdriveDuration);
            }
            if (age % 60 != 0 || !SentryGrid.TryGetTowerKit(tower.type, out SentryKit kit2)
                || kit2.TierOf(tower.type) != 2) {
                return;
            }
            NPC target = null;
            float bestDist = 340f;
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
            if (target == null) {
                return;
            }
            Vector2 vel = (target.Center - tower.Center).SafeNormalize(Vector2.UnitX) * 14f;
            //跳 4 目标：首发 + 3 次续跳
            Projectile.NewProjectile(SentrySource(tower), tower.Center, vel,
                ModContent.ProjectileType<GsSentryChainLightningProj>(),
                (int)(tower.damage * 0.8f), 1f, tower.owner, target.whoAmI, 3f);
        }

        /// <summary>谐振电弧桥：重叠光环对之间撒微电弧（identity 小端执行防双份，各端本地）</summary>
        protected sealed override void TowerPostAI(Projectile tower, SentryKit kit, GsSentryLocal st) {
            if (VaultUtils.isServer || !IsAuraType(tower.type) || st.LinkedTowers == null
                || Main.GameUpdateCount % 6 != 0) {
                return;
            }
            foreach (int who in st.LinkedTowers) {
                if (who < 0 || who >= Main.maxProjectiles) {
                    continue;
                }
                Projectile other = Main.projectile[who];
                if (!other.active || other.owner != tower.owner || !IsAuraType(other.type)
                    || other.identity <= tower.identity) {
                    continue;
                }
                float span = other.Center.Distance(tower.Center);
                if (span > AuraRadius(tower) + AuraRadius(other)) {
                    continue;
                }
                //沿桥线随机点起弧，方向顺桥
                Vector2 dir = (other.Center - tower.Center) / span;
                Vector2 at = tower.Center + dir * span * Main.rand.NextFloat(0.25f, 0.75f);
                PRTLoader.NewParticle<PRT_GraniteVolt>(at + Main.rand.NextVector2Circular(10f, 10f),
                    dir * 2f, new Color(150, 205, 255), Main.rand.NextFloat(0.45f, 0.8f))?.Configure(5);
            }
        }
    }

    /// <summary>闪电光环杆 T1（kit 宿主）</summary>
    internal class GsLightningAuraT1 : GsLightningAuraBase
    {
        public override int TargetItemID => ItemID.DD2LightningAuraT1Popper;

        protected override float DamageMult => 1.14f;

        protected override string GsDescFallback =>
            "Deploy doctrine: overlap two auras to form a resonance zone dealing half again as much\n" +
            "Ticks charge the aura, right-click when full to overload it wider with a static field";

        protected override SentryKit BuildKit() => new() {
            TowerTypes = [ProjectileID.DD2LightningAuraT1, ProjectileID.DD2LightningAuraT2, ProjectileID.DD2LightningAuraT3],
            BoltTypes = [],
            ChargeMax = [20, 20, 20],
            OverdriveDuration = 300,
        };
    }

    /// <summary>闪电光环藤 T2</summary>
    internal class GsLightningAuraT2 : GsLightningAuraBase
    {
        public override int TargetItemID => ItemID.DD2LightningAuraT2Popper;

        protected override float DamageMult => 1.12f;

        protected override string GsDescFallback =>
            "Deploy doctrine: overlap two auras to form a resonance zone dealing half again as much\n" +
            "Ticks charge the aura, right-click when full to overload it wider with a static field";

        protected override SentryKit BuildKit() => null;
    }

    /// <summary>闪电光环杖 T3（超频追加链状闪电）</summary>
    internal class GsLightningAuraT3 : GsLightningAuraBase
    {
        public override int TargetItemID => ItemID.DD2LightningAuraT3Popper;

        protected override float DamageMult => 1.10f;

        protected override string GsDescFallback =>
            "Deploy doctrine: overlap auras for resonance, overload adds a static field and shock marks\n" +
            "While overdriven the aura hurls chain lightning arcing through four foes";

        protected override SentryKit BuildKit() => null;
    }
}
