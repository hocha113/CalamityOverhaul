using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonSentries.Projectiles;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonSentries.Schemes
{
    /// <summary>
    /// 弩炮三档「贯通工事」共享基类：<br/>
    /// 充能 6，超频 240 帧「攻城连弩」= 每发原矢补一发 0.75× 伴矢（等效射速 ×1.75）、
    /// 原矢穿透 +2（带 &gt;0 守卫）、T3 矢升格巨型攻城矢（×1.4 体）、超频矢曳割裂气流；
    /// 组合技（弩炮×弩炮成链）「交叉火力」= 两矢 45 帧内命中同目标追加 1.2× 十字钉刺
    /// </summary>
    internal abstract class GsBallistaBase : GsSentryScheme
    {
        protected sealed override int FamilyIdx => GsSentryFamilyIdx.Ballista;

        protected abstract float DamageMult { get; }

        public sealed override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= DamageMult;

        /// <summary>超频「攻城连弩」出膛升格（owner 端，改动随 netUpdate 过线）</summary>
        protected sealed override void OnOverdriveBoltSpawn(Projectile bolt, Projectile tower, int tier) {
            //-1 是无限穿：只对有限穿透做加法（历史事故守卫）
            if (bolt.penetrate > 0) {
                bolt.penetrate += 2;
            }
            if (tier == 2) {
                bolt.scale *= 1.4f;
                bolt.Resize((int)(bolt.width * 1.4f), (int)(bolt.height * 1.4f));
            }
            SpawnBoltHandled(tower, bolt.Center, bolt.velocity.RotatedBy(0.05f),
                bolt.type, (int)(bolt.damage * 0.75f), bolt.knockBack);
        }

        /// <summary>交叉火力：45 帧内第二矢命中同目标 → 追加钉刺（目标 90 帧冷却）</summary>
        protected sealed override void OnSentryHit(Projectile proj, Projectile tower, NPC target,
            NPC.HitInfo hit, int damageDone, GsSentryLocal st) {
            if (tower == null || proj.type != ProjectileID.DD2BallistraProj) {
                return;
            }
            if ((SentryGrid.StateOf(tower).LinkMask & 1 << GsSentryFamilyIdx.Ballista) == 0
                || !SentryGrid.CrossFireReady(target)) {
                return;
            }
            Projectile.NewProjectile(SentrySource(proj), target.Center, Vector2.Zero,
                ModContent.ProjectileType<GsSentryBurstProj>(),
                (int)(tower.damage * 1.2f), 4f, proj.owner,
                GsSentryBurstProj.StyleCrossSpike, 70f);
        }

        /// <summary>超频矢的割裂气流带（跨端可见：远端也按出生判定画）</summary>
        protected sealed override void DrawBoltExtra(Projectile bolt, SentryKit kit, GsSentryLocal st, Color lightColor) {
            if (!st.OverdriveShot || bolt.velocity.LengthSquared() < 1f) {
                return;
            }
            Texture2D air = CWRAsset.Airflow?.Value;
            if (air == null) {
                return;
            }
            Color c = new Color(255, 226, 150) * 0.4f;
            c.A = 0;
            float rot = bolt.velocity.ToRotation();
            Main.EntitySpriteDraw(air, bolt.Center - bolt.velocity * 1.6f - Main.screenPosition, null, c,
                rot, new Vector2(air.Width * 0.15f, air.Height * 0.5f),
                new Vector2(0.65f, 0.10f * bolt.scale), SpriteEffects.None, 0);
        }
    }

    /// <summary>弩炮杆 T1（kit 宿主）</summary>
    internal class GsBallistaT1 : GsBallistaBase
    {
        public override int TargetItemID => ItemID.DD2BallistraTowerT1Popper;

        protected override float DamageMult => 1.12f;

        protected override string GsDescFallback =>
            "Deploy doctrine: hits charge the tower, right-click when full for siege repeater fire\n" +
            "Two linked ballistas nail a cross-fire spike into any target both hit within moments";

        protected override SentryKit BuildKit() => new() {
            TowerTypes = [ProjectileID.DD2BallistraTowerT1, ProjectileID.DD2BallistraTowerT2, ProjectileID.DD2BallistraTowerT3],
            BoltTypes = [ProjectileID.DD2BallistraProj],
            ChargeMax = [6, 6, 6],
            OverdriveDuration = 240,
        };
    }

    /// <summary>弩炮藤 T2</summary>
    internal class GsBallistaT2 : GsBallistaBase
    {
        public override int TargetItemID => ItemID.DD2BallistraTowerT2Popper;

        protected override float DamageMult => 1.10f;

        protected override string GsDescFallback =>
            "Deploy doctrine: hits charge the tower, right-click when full for siege repeater fire\n" +
            "Two linked ballistas nail a cross-fire spike into any target both hit within moments";

        protected override SentryKit BuildKit() => null;
    }

    /// <summary>弩炮杖 T3（超频矢升格巨型攻城矢）</summary>
    internal class GsBallistaT3 : GsBallistaBase
    {
        public override int TargetItemID => ItemID.DD2BallistraTowerT3Popper;

        protected override float DamageMult => 1.08f;

        protected override string GsDescFallback =>
            "Deploy doctrine: hits charge the tower, right-click when full for siege repeater fire\n" +
            "Overdriven bolts grow into massive siege shafts; cross-fire spikes reward paired towers";

        protected override SentryKit BuildKit() => null;
    }
}
