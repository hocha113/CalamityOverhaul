using CalamityOverhaul.Content.NPCs.Core;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Neutrons;
using CalamityOverhaul.Content.Projectiles.Weapons.Rogue.GangarusProjectiles;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.CWRDamageTypes
{
    internal class EndlessDamageNPCCustomizer : NPCCustomizer, ILoader
    {
        private static List<int> nihilityProjs = [];

        public void Setup() {
            nihilityProjs = [
                ModContent.ProjectileType<Godslight>(),
                ModContent.ProjectileType<EXNeutronExplode>(),
            ];
        }

        public override bool On_OnHitByProjectile_IfSpan(Projectile proj) {
            return proj.DamageType != EndlessDamageClass.Instance ? false : !nihilityProjs.Contains(proj.type);
        }

        public override bool? On_OnHitByProjectile(NPC npc, Projectile projectile, in NPC.HitInfo hit, int damageDone) {
            int upDamage = (int)(npc.lifeMax / 100f);
            if (upDamage > projectile.damage * 2)
                upDamage = projectile.damage * 2;
            projectile.damage += upDamage;
            return false;
        }

        public override bool? On_ModifyIncomingHit(NPC npc, ref NPC.HitModifiers modifiers) {
            if (modifiers.DamageType == EndlessDamageClass.Instance) {
                //我们希望无尽伤害类型不会受到其他代码的减伤影响，所以，如果是无尽伤害，那么就阻止后面所有代码的执行
                return false;
            }
            return base.On_ModifyIncomingHit(npc, ref modifiers);
        }
    }
}
