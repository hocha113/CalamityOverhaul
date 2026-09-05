using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicMorph.Projectiles;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicMorph
{
    /// <summary>
    /// 流星法杖重铸：流星落点残留 1.2s 熔坑（踩踏受灼）
    /// </summary>
    internal class GsMeteorStaff : GsMorphScheme
    {
        public override int TargetItemID => ItemID.MeteorStaff;

        protected override string GsDescFallback =>
            "Reforged: meteors leave a scorching crater where they land.\nrelease to call a five-meteor barrage across the cursor";
        protected override float BaseDamageMult => 1.08f;

        private static bool IsMeteor(int type)
            => type == ProjectileID.Meteor1 || type == ProjectileID.Meteor2 || type == ProjectileID.Meteor3;

        public override void GsProjOnKill(Projectile proj, int timeLeft, GodSmithProjRouter router) {
            if (!IsMeteor(proj.type) || !proj.IsOwnedByLocalPlayer()) {
                return;
            }
            //落点熔坑（真弹幕残留物，可并存；伤害 ×0.2 折算）
            Projectile.NewProjectile(proj.GetSource_FromAI(), proj.Center, Vector2.Zero,
                ModContent.ProjectileType<GsScorchDomainProj>(),
                (int)MathHelper.Max(1f, proj.damage * 0.2f), 0f, proj.owner);
        }
    }
}
