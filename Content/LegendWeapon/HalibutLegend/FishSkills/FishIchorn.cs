using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    internal class FishIchorn : FishSkill
    {
        public override int UnlockFishID => ItemID.Ichorfish;
        public override int DefaultCooldown => 120;
    }

    internal class FishIchornGlobalProj : GlobalProjectile
    {
        public override void OnHitNPC(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone) {
            if (projectile.owner.TryGetPlayer(out var player) 
                && FishSkill.GetT<FishIchorn>().Active(player)) {
                target.AddBuff(BuffID.Ichor, 300);//在这个技能下攻击会附加灵液效果
            }
        }
    }
}
