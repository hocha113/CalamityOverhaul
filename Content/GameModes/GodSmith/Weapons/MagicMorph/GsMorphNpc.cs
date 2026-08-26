using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicMorph
{
    /// <summary>
    /// MagicMorph 族 NPC 附着状态（instanced GlobalNPC）。<br/>
    /// 血蚀层与霜蚀易伤都是攻击方本地量：挂层发生在命中钩子（只在攻击方端执行），
    /// 消费（引爆判定/易伤乘区）也在攻击方端的命中管线里，端别自洽、无需过线；
    /// 引爆产物是真弹幕（GsBurstProj），由 owner 生成后全端可见
    /// </summary>
    internal class GsMorphNpc : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        /// <summary>血蚀层数（猩红魔杖血雨叠加，3 层引爆）</summary>
        public int BloodErode;

        /// <summary>血蚀层保鲜计时，归零清层</summary>
        public int BloodErodeTimer;

        /// <summary>霜蚀易伤剩余帧（寒霜法杖 B 霜矛命中挂加，期间受本端玩家伤害 +10%）</summary>
        public int FrostExposure;

        public override void ResetEffects(NPC npc) {
            if (BloodErodeTimer > 0 && --BloodErodeTimer == 0) {
                BloodErode = 0;
            }
            if (FrostExposure > 0) {
                FrostExposure--;
            }
        }

        public override void ModifyHitByProjectile(NPC npc, Projectile projectile, ref NPC.HitModifiers modifiers) {
            //自建钩子须自查模式闸门
            if (!GameModeSystem.GodSmithActive) {
                return;
            }
            if (FrostExposure > 0) {
                modifiers.FinalDamage *= 1.10f;
            }
        }
    }
}
