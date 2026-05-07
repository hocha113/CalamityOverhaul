using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Power
{
    /// <summary>
    /// 低温核心：能量球爆炸时对范围内敌人施加冰冻
    /// 直接在 OnOrbDetonation 中遍历NPC施加buff，与爆炸半径改件叠加有效
    /// </summary>
    internal sealed class CryoCoreModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Power;
        //冰冻深蓝
        public override Color TintColor => new(80, 170, 255);

        public override void Apply(ref ShootContext ctx) {
            ctx.OrbExplosionRadiusMul += 0.3f;
            ctx.ChargeTimeMul += 0.15f;
        }

        public override void OnOrbDetonation(CyberChargeOrbProj orb) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;
            //以爆炸半径（受改件叠加）为准搜索目标
            float baseRadius = 200f * orb.ExplosionRadiusMul;
            float radiusSq = baseRadius * baseRadius;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.friendly || npc.dontTakeDamage) continue;
                if (Vector2.DistanceSquared(npc.Center, orb.Projectile.Center) > radiusSq) continue;
                npc.AddBuff(BuffID.Frozen, 150);
            }
        }
    }
}
