using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.VolleyBows.Projectiles;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.VolleyBows
{
    /// <summary>
    /// 骨髓弓（重铸 115%）：齐射成五骨箭十字（穿透 +1，0.55 each）。
    /// 骨标满 3 层处决「骨牢」：敌四向各生成一支向心骨箭夹击（50% each）。
    /// 期望：齐射 +1.75/15.3 ≈ +11%，骨牢 ≈ +3%
    /// </summary>
    internal class GsMarrow : GsVolleyBowScheme
    {
        public override int TargetItemID => ItemID.Marrow;

        protected override string GsDescFallback =>
            "Reforged: volley charge looses a 5-bone-arrow cross with +1 pierce, one ammo per volley\nExecuting a fully branded foe cages it with 4 converging bone arrows";

        protected override int VolleyCount => 5;
        protected override GsVolleyFormation Formation => GsVolleyFormation.Cross;
        protected override float SpreadPx => 20f;
        protected override float ChargePerShot => 7f;
        protected override float SideArrowMul => 0.55f;
        protected override Color TrailColor => new(226, 222, 200);

        protected override int VolleyProjType(int ammoProjType) => ProjectileID.BoneArrow;

        protected override void OnSpawnMarkedHook(Projectile proj, GodSmithProjRouter router) {
            int role = (int)router.MarkData;
            //穿透 +1 只对齐射骨箭；>0 守卫防 -1 无限穿被写坏
            if ((role == GsVolleyRole.VolleyMain || role == GsVolleyRole.VolleySide) && proj.penetrate > 0) {
                proj.penetrate++;
            }
        }

        /// <summary>骨牢：四向 240px 处向心骨箭夹击</summary>
        protected override void OnExecute(Player player, NPC target, Projectile proj, int damageDone) {
            int dmg = (int)(proj.damage * 0.5f);
            for (int i = 0; i < 4; i++) {
                Vector2 dir = (MathHelper.PiOver2 * i + MathHelper.PiOver4).ToRotationVector2();
                Vector2 from = target.Center + dir * 240f;
                Projectile.NewProjectile(player.GetSource_Misc("GsVolleyExecute"), from, -dir * 14f,
                    ProjectileID.BoneArrow, dmg, 2f, player.whoAmI);
            }
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 6; i++) {
                    PRTLoader.NewParticle<PRT_MarbleChip>(target.Center,
                        Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.5f, 4f),
                        TrailColor, Main.rand.NextFloat(0.4f, 0.65f))?.Configure(Main.rand.Next(16, 26));
                }
            }
        }
    }
}
