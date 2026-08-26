using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonSentries.Projectiles;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonSentries.Schemes
{
    /// <summary>
    /// 爆炸陷阱三档「雷区工程」共享基类：<br/>
    /// 充能 = 爆炸 5 次后自动超频 240 帧「高爆装药」= 爆炸判定 ×1.5 并追加 1.3× 高爆芯，
    /// T3 超频再向上抛 8 枚破片；「雷区殉爆网」= 陷阱间距 ≤200px 成网，
    /// 任一引爆 15 帧后邻雷位置生成殉爆弹链式清场（链深封顶 4，每座 90 帧至多一次）。
    /// 右键 = 手动提前引爆一轮殉爆检查。完全不动原版陷阱 AI 与冷却
    /// </summary>
    internal abstract class GsExplosiveTrapBase : GsSentryScheme
    {
        protected sealed override int FamilyIdx => GsSentryFamilyIdx.ExplosiveTrap;

        protected abstract float DamageMult { get; }

        public sealed override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= DamageMult;

        /// <summary>右键改为手动引爆殉爆检查（超频是 AutoOverdrive 自动触发）</summary>
        protected sealed override void HandleRightClick(Player player)
            => SentryGrid.TryManualChainCheck(player);

        /// <summary>超频「高爆装药」：爆炸判定 ×1.5 + 高爆芯；T3 追加向上破片扇</summary>
        protected sealed override void OnOverdriveBoltSpawn(Projectile bolt, Projectile tower, int tier) {
            bolt.Resize((int)(bolt.width * 1.5f), (int)(bolt.height * 1.5f));
            bolt.scale *= 1.5f;
            Projectile.NewProjectile(SentrySource(bolt), bolt.Center, Vector2.Zero,
                ModContent.ProjectileType<GsSentryBurstProj>(),
                (int)(tower.damage * 1.3f), 5f, bolt.owner,
                GsSentryBurstProj.StyleHighExplosive, 60f);
            if (tier != 2) {
                return;
            }
            for (int i = 0; i < 8; i++) {
                float ang = -MathHelper.PiOver2 + MathHelper.Lerp(-0.85f, 0.85f, i / 7f);
                Vector2 vel = ang.ToRotationVector2() * Main.rand.NextFloat(7f, 10f);
                Projectile.NewProjectile(SentrySource(bolt), bolt.Center, vel,
                    ModContent.ProjectileType<GsSentryBoltProj>(),
                    (int)(tower.damage * 0.4f), 1.5f, bolt.owner, GsSentryBoltProj.StyleShard);
            }
        }

        /// <summary>爆炸弹消亡（owner 端）：本座雷进入 90 帧殉爆节流，随后向邻雷传播殉爆链</summary>
        protected sealed override void OnBoltKilled(Projectile bolt, Projectile tower, GsSentryLocal st) {
            int baseDamage;
            if (tower != null) {
                SentryGrid.StateOf(tower).LastChainTick = Main.GameUpdateCount;
                baseDamage = (int)(tower.damage * 0.8f);
            }
            else {
                baseDamage = (int)(bolt.damage * 0.8f);
            }
            SentryGrid.PropagateChain(bolt.Center, bolt.owner, 1, Math.Max(baseDamage, 1));
        }
    }

    /// <summary>爆炸陷阱杆 T1（kit 宿主）</summary>
    internal class GsExplosiveTrapT1 : GsExplosiveTrapBase
    {
        public override int TargetItemID => ItemID.DD2ExplosiveTrapT1Popper;

        protected override float DamageMult => 1.14f;

        protected override string GsDescFallback =>
            "Deploy doctrine: traps within 200px form a minefield web, any blast chain-detonates neighbors\n" +
            "Five blasts auto-overdrive the trap into high explosive; right-click to trigger a chain check";

        protected override SentryKit BuildKit() => new() {
            TowerTypes = [ProjectileID.DD2ExplosiveTrapT1, ProjectileID.DD2ExplosiveTrapT2, ProjectileID.DD2ExplosiveTrapT3],
            BoltTypes = [ProjectileID.DD2ExplosiveTrapT1Explosion, ProjectileID.DD2ExplosiveTrapT2Explosion, ProjectileID.DD2ExplosiveTrapT3Explosion],
            ChargeMax = [5, 5, 5],
            OverdriveDuration = 240,
            AutoOverdrive = true,
            ChargeOnBoltKill = true,
        };
    }

    /// <summary>爆炸陷阱藤 T2</summary>
    internal class GsExplosiveTrapT2 : GsExplosiveTrapBase
    {
        public override int TargetItemID => ItemID.DD2ExplosiveTrapT2Popper;

        protected override float DamageMult => 1.12f;

        protected override string GsDescFallback =>
            "Deploy doctrine: traps within 200px form a minefield web, any blast chain-detonates neighbors\n" +
            "Five blasts auto-overdrive the trap into high explosive; right-click to trigger a chain check";

        protected override SentryKit BuildKit() => null;
    }

    /// <summary>爆炸陷阱杖 T3（超频追加向上破片）</summary>
    internal class GsExplosiveTrapT3 : GsExplosiveTrapBase
    {
        public override int TargetItemID => ItemID.DD2ExplosiveTrapT3Popper;

        protected override float DamageMult => 1.10f;

        protected override string GsDescFallback =>
            "Deploy doctrine: traps within 200px form a minefield web, any blast chain-detonates neighbors\n" +
            "Overdriven blasts hurl shrapnel skyward on top of the high-explosive core";

        protected override SentryKit BuildKit() => null;
    }
}
