using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicMorph.Projectiles;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicMorph
{
    /// <summary>
    /// 猩红魔杖重铸：小领域「血雨祭域」。<br/>
    /// 左键全接管：不再召原版血云，改为在光标处开 R150 领域 10s（同场唯一，重放即迁移）；
    /// 雨滴命中叠「血蚀」，3 层引爆小范围血爆；域内玩家每 2s 回 1HP；
    /// 右键免蓄力把领域迁到光标（8 蓝）。开域蓝耗为原版两倍
    /// </summary>
    internal class GsCrimsonRod : GsMorphScheme
    {
        public override int TargetItemID => ItemID.CrimsonRod;

        protected override string GsDescFallback =>
            "Reforged: the blood cloud becomes a sacrificial rain field.\nRaindrops stack Blood Erosion, three stacks burst; allies inside slowly mend.\nRight click migrates the field to your cursor for a sliver of mana";

        protected override float BaseDamageMult => 1.12f;

        /// <summary>开域蓝耗倍率（原版 mana 的 2 倍，约 20 蓝）</summary>
        public override void GsModifyManaCost(Item item, Player player, ref float reduce, ref float mult)
            => mult *= 2f;

        public override bool? GsShoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (player.whoAmI == Main.myPlayer) {
                Vector2 anchor = ClampAnchor(player, Main.MouseWorld);
                int rainDamage = (int)(damage * 0.4f);
                if (!GsDomainProj.TryMigrate<GsCrimsonDomainProj>(player, anchor)) {
                    Projectile.NewProjectile(source, anchor, Vector2.Zero,
                        ModContent.ProjectileType<GsCrimsonDomainProj>(), rainDamage, 1f, player.whoAmI);
                }
                SoundEngine.PlaySound(SoundID.Item46 with { Volume = 0.7f, Pitch = -0.3f }, anchor);
            }
            return false;
        }

        /// <summary>右键：免蓄力迁移（8 蓝）。无域在场时播失败音不扣蓝</summary>
        protected override void OnAltTrigger(Item item, Player player) {
            Vector2 anchor = ClampAnchor(player, Main.MouseWorld);
            bool hasDomain = false;
            int type = ModContent.ProjectileType<GsCrimsonDomainProj>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                if (Main.projectile[i].active && Main.projectile[i].type == type
                    && Main.projectile[i].owner == player.whoAmI) {
                    hasDomain = true;
                    break;
                }
            }
            if (!hasDomain || !player.CheckMana(item, 8, true, false)) {
                SoundEngine.PlaySound(SoundID.MaxMana with { Pitch = -0.7f, Volume = 0.5f }, player.Center);
                return;
            }
            GsDomainProj.TryMigrate<GsCrimsonDomainProj>(player, anchor);
            SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.6f, Pitch = -0.4f }, anchor);
        }

        /// <summary>本武器无蓄力形态，右键已改为瞬发迁移</summary>
        protected override void FireMorphB(Item item, Player player) { }

        private static Vector2 ClampAnchor(Player player, Vector2 target) {
            const float maxRange = 640f;
            return player.Center.Distance(target) > maxRange
                ? player.Center + (target - player.Center).SafeNormalize(Vector2.UnitX) * maxRange
                : target;
        }

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router) {
            if (proj.type != ProjectileID.BloodRain) {
                return;
            }
            //血蚀：攻击方本地层数，3 层引爆（爆体是真弹幕，全端可见）
            GsMorphNpc morphNpc = target.GetGlobalNPC<GsMorphNpc>();
            morphNpc.BloodErode++;
            morphNpc.BloodErodeTimer = 240;
            if (morphNpc.BloodErode >= 3) {
                morphNpc.BloodErode = 0;
                GsMorphBurstProj.Spawn(proj, target.Center, proj.damage * 2, 60f, 0);
            }
        }
    }
}
