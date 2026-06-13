using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Power
{
    /// <summary>暗物质核心：球飞行阶段吸引附近敌人向球</summary>
    internal sealed class DarkMatterCoreModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Power;
        //暗物质深紫黑
        public override Color TintColor => new(80, 20, 180);

        public override void Apply(ref ShootContext ctx) {
            ctx.OrbFlyingAttract = true;
            ctx.OrbExplosionRadiusMul += 0.32f;
            ctx.ManaCostMul += 0.72f;
        }

        public override void OnOrbFlyingAI(CyberChargeOrbProj orb) {
            if (orb.Projectile.owner != Main.myPlayer) return;
            const float range = 320f;
            const float strength = 0.9f;
            float rangeSq = range * range;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.friendly || npc.dontTakeDamage || npc.noGravity) continue;
                if (Vector2.DistanceSquared(npc.Center, orb.Projectile.Center) > rangeSq) continue;
                Vector2 pull = (orb.Projectile.Center - npc.Center).SafeNormalize(Vector2.Zero) * strength;
                npc.velocity += pull;
            }
        }
    }
}
