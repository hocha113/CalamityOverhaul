using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicMorph.Projectiles
{
    /// <summary>
    /// 流星法杖「熔坑」：流星落点残留 1.2s 的贴地熔浆判定，踩踏受灼
    /// </summary>
    internal class GsScorchDomainProj : GsDomainProj
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.InfernoFriendlyBlast;

        protected override int DomainRadius => 50;
        protected override int DomainLife => 72;
        protected override int DomainTickRate => 18;

        public override void SetStaticDefaults() => Main.projFrames[Type] = Main.projFrames[ProjectileID.InfernoFriendlyBlast];

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
            => target.AddBuff(BuffID.OnFire, 60);
    }
}
