using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonSentries.Schemes
{
    /// <summary>
    /// 眼犬发射器「警戒尖兵」（哨兵族垂直切片）：<br/>
    /// 充能 12 命中，超频 300 帧双瞳连射（每发补一发镜像眼弹、眼弹增体 20%）；
    /// 命中挂 90 帧曝光（owner 本地标），链内哨兵对曝光目标 +10%；
    /// 曝光目标被链内哨兵击杀时最近成链眼犬免费充能 +3（组合技在 SentryGrid.NotifySentryKill）
    /// </summary>
    internal class GsHoundiusShootius : GsSentryScheme
    {
        public override int TargetItemID => ItemID.HoundiusShootius;

        protected override int FamilyIdx => GsSentryFamilyIdx.Houndius;

        protected override string GsDescFallback =>
            "Deploy doctrine: hits charge the eye, right-click when full to overdrive it into twin-pupil rapid fire\nIts victims are exposed for a moment, linked sentries hit exposed foes harder\nLinked kills feed the eye free charge; redeployed sentries keep a quarter charge";
        protected override SentryKit BuildKit() => new() {
            TowerTypes = [ProjectileID.HoundiusShootius],
            BoltTypes = [ProjectileID.HoundiusShootiusFireball],
            ChargeMax = [12],
            OverdriveDuration = 300,
        };

        /// <summary>数值行：冷门武器上限档的微调旋钮，其余强度走机制层</summary>
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.12f;

        /// <summary>超频「双瞳连射」：本发增体两成，补发一发镜像眼弹，等效射速翻倍</summary>
        protected override void OnOverdriveBoltSpawn(Projectile bolt, Projectile tower, int tier) {
            bolt.scale *= 1.2f;
            bolt.Resize((int)(bolt.width * 1.2f), (int)(bolt.height * 1.2f));
            Vector2 mirrored = bolt.velocity.RotatedBy(0.09f);
            bolt.velocity = bolt.velocity.RotatedBy(-0.045f);
            SpawnBoltHandled(tower, bolt.Center, mirrored, bolt.type, bolt.damage, bolt.knockBack);
        }

        /// <summary>命中挂曝光</summary>
        protected override void OnSentryHit(Projectile proj, Projectile tower, NPC target,
            NPC.HitInfo hit, int damageDone, GsSentryLocal st) {
            if (proj.type != ProjectileID.HoundiusShootiusFireball) {
                return;
            }
            SentryGrid.MarkExposed(target);
        }
    }
}
