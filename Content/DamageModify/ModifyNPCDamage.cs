using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.Items.Melee.SpearOfLonginuses;
using InnoVault.GameSystem;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.DamageModify
{
    internal class ModifyNPCDamage : NPCOverride, ICWRLoader
    {
        private static List<int> nihilityProjs = [];

        public override int TargetID => -1;

        void ICWRLoader.SetupData() {
            nihilityProjs = [
                ModContent.ProjectileType<Godslight>(),
                ModContent.ProjectileType<EXNeutronExplode>(),
            ];
        }
        void ICWRLoader.UnLoadData() {
            nihilityProjs?.Clear();
        }

        public override bool On_OnHitByProjectile_IfSpan(Projectile proj) {
            return proj.DamageType == EndlessDamageClass.Instance && !nihilityProjs.Contains(proj.type);
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
                //无尽伤害跳过后续减伤
                return false;
            }
            return base.On_ModifyIncomingHit(npc, ref modifiers);
        }
    }
}
