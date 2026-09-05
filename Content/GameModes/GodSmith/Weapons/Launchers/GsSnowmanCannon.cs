using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Launchers
{
    /// <summary>
    /// 雪人加农炮重铸：经典不毁强化。原版追踪群保留，命中给目标盖雪花印，
    /// 印在时本枪对其 +8%。特种火箭效果 100% 保真
    /// </summary>
    internal class GsSnowmanCannon : GsLauncherScheme
    {
        public override int TargetItemID => ItemID.SnowmanCannon;

        protected override string GsDescFallback =>
            "Reforged: hits brand victims with a snowflake (+8% from this weapon)";
        /// <summary>雪人火箭主弹全家（子雷 ClusterSnowmanFragments 不在内，不接管）</summary>
        internal static readonly HashSet<int> SnowRocketTypes = [
            ProjectileID.RocketSnowmanI, ProjectileID.RocketSnowmanII,
            ProjectileID.RocketSnowmanIII, ProjectileID.RocketSnowmanIV,
            ProjectileID.ClusterSnowmanRocketI, ProjectileID.ClusterSnowmanRocketII,
            ProjectileID.WetSnowmanRocket, ProjectileID.LavaSnowmanRocket,
            ProjectileID.HoneySnowmanRocket,
            ProjectileID.MiniNukeSnowmanRocketI, ProjectileID.MiniNukeSnowmanRocketII,
            ProjectileID.DrySnowmanRocket,
        ];

        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.06f;

        public override bool? GsShoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            LaunchRecoil(player, velocity, 1.3f);
            return null;
        }

        public override void GsProjModifyHitNPC(Projectile proj, NPC target,
            ref NPC.HitModifiers modifiers, GodSmithProjRouter router) {
            //雪花印在身：本枪一切打标弹（含承签子雷）对其 +8%
            int markType = ModContent.ProjectileType<GsSnowMarkProj>();
            foreach (Projectile p in Main.ActiveProjectiles) {
                if (p.type == markType && p.owner == proj.owner && (int)p.ai[0] == target.whoAmI) {
                    modifiers.FinalDamage *= 1.08f;
                    return;
                }
            }
        }

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit,
            int damageDone, GodSmithProjRouter router) {
            //命中盖雪花印；已有印则续时
            if (!SnowRocketTypes.Contains(proj.type) || !target.active || target.life <= 0) {
                return;
            }
            int markType = ModContent.ProjectileType<GsSnowMarkProj>();
            foreach (Projectile p in Main.ActiveProjectiles) {
                if (p.type == markType && p.owner == proj.owner && (int)p.ai[0] == target.whoAmI) {
                    p.timeLeft = 240;
                    p.netUpdate = true;
                    return;
                }
            }
            Projectile.NewProjectile(proj.GetSource_FromThis(), target.Center, Vector2.Zero,
                markType, 0, 0f, proj.owner, target.whoAmI);
        }
    }

    /// <summary>
    /// 雪花印：盖在目标头顶的冰晶烙印（无伤状态载体，天然同步）。
    /// 宿主死亡或 4 秒后融化；借原版北极雪花贴图默认绘制
    /// </summary>
    internal class GsSnowMarkProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.NorthPoleSnowflake;

        private NPC Host => Main.npc[(int)Projectile.ai[0]];

        /// <summary>原版雪花贴图可能是多帧条，帧数对齐后按 identity 定一帧</summary>
        public override void SetStaticDefaults() => Main.projFrames[Type] = Main.projFrames[ProjectileID.NorthPoleSnowflake];

        public override void SetDefaults() {
            Projectile.width = 8;
            Projectile.height = 8;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 240;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            NPC host = Host;
            if (!host.active || host.life <= 0) {
                Projectile.Kill();
                return;
            }
            Projectile.Center = host.Top - new Vector2(0f, 18f);
            Projectile.frame = Projectile.identity % Main.projFrames[Type];
        }
    }
}
