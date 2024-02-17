using CalamityOverhaul.Content.NPCs.Core;
using Terraria;

namespace CalamityOverhaul.Content.CWRDamageTypes
{
    internal class EndlessDamageNPCCustomizer : NPCCustomizer
    {
        public override bool On_OnHitByProjectile_IfSpan(Projectile proj) => proj.DamageType == EndlessDamageClass.Instance;

        public override bool? On_OnHitByProjectile(NPC npc, Projectile projectile, in NPC.HitInfo hit, int damageDone){
            //if (npc.type != CWRIDs.HEBody && npc.type != CWRIDs.HEBodyAlt) {

            //}
            //return null;
            int upDamage = (int)(npc.lifeMax / 100f);
            if (upDamage > projectile.damage * 2)
                upDamage = projectile.damage * 2;
            projectile.damage += upDamage;
            return false;
        }

        public override bool? On_ModifyIncomingHit(NPC npc, ref NPC.HitModifiers modifiers){
            if (modifiers.DamageType == EndlessDamageClass.Instance){
                //我们希望无尽伤害类型不会受到其他代码的减伤影响，所以，如果是无尽伤害，那么就阻止后面所有代码的执行
                return false;
            }
            return base.On_ModifyIncomingHit(npc, ref modifiers);
        }
    }
}
