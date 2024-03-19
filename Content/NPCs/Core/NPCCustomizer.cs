using Terraria;

namespace CalamityOverhaul.Content.NPCs.Core
{
    internal abstract class NPCCustomizer
    {
        /// <summary>
        /// 这个属性用于<see cref="On_OnHitByProjectile"/>的实现，编辑方法生效的条件，一般判断会生效在那些目标弹幕ID之上
        /// </summary>
        public virtual bool On_OnHitByProjectile_IfSpan(Projectile proj) => false;

        /// <summary>
        /// 用于覆盖NPC的受击伤害计算公式
        /// </summary>
        /// <param name="npc"></param>
        /// <param name="projectile"></param>
        /// <param name="hit"></param>
        /// <param name="damageDone"></param>
        /// <returns>返回<see langword="null"/>会继续执行原来的方法，包括原ModNPC方法与G方法。
        /// 返回<see langword="true"/>仅仅会继续执行原ModNPC方法而阻止全局NPC类的额外修改运行。
        /// 返回<see langword="false"/>阻止后续所有修改的运行</returns>
        public virtual bool? On_ModifyIncomingHit(NPC npc, ref NPC.HitModifiers modifiers) {
            return null;
        }

        /// <summary>
        /// 用于覆盖NPC的弹幕受击行为
        /// </summary>
        /// <param name="npc"></param>
        /// <param name="projectile"></param>
        /// <param name="hit"></param>
        /// <param name="damageDone"></param>
        /// <returns>返回<see langword="null"/>会继续执行原来的方法，包括原ModNPC方法与G方法。
        /// 返回<see langword="true"/>仅仅会继续执行原ModNPC方法而阻止全局NPC类的额外修改运行。
        /// 返回<see langword="false"/>阻止后续所有修改的运行</returns>
        public virtual bool? On_OnHitByProjectile(NPC npc, Projectile projectile, in NPC.HitInfo hit, int damageDone) {
            return null;
        }

        /// <summary>
        /// 用于覆盖NPC的物品受击行为
        /// </summary>
        /// <param name="npc"></param>
        /// <param name="projectile"></param>
        /// <param name="hit"></param>
        /// <param name="damageDone"></param>
        /// <returns>返回<see langword="null"/>会继续执行原来的方法，包括原ModNPC方法与G方法。
        /// 返回<see langword="true"/>仅仅会继续执行原ModNPC方法而阻止全局NPC类的额外修改运行。
        /// 返回<see langword="false"/>阻止后续所有修改的运行</returns>
        public virtual bool? On_OnHitByItem(NPC npc, Projectile projectile, in NPC.HitInfo hit, int damageDone) {
            return null;
        }
    }
}
