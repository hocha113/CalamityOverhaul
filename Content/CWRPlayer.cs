using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content
{
    public class CWRPlayer : ModPlayer
    {
        /// <summary>
        /// 圣物的装备等级，这个字段决定了玩家会拥有什么样的弹幕效果
        /// </summary>
        public int theRelicLuxor = 0;

        public float PressureIncrease;

        public int CompressorPanelID = -1;//未使用的，这个属性属于一个未完成的UI
        /// <summary>
        /// 玩家是否坐在大排档塑料椅子之上
        /// </summary>
        public bool inFoodStallChair;
        /// <summary>
        /// 玩家是否装备休谟稳定器
        /// </summary>
        public bool EndlessStabilizerBool;
        /// <summary>
        /// 玩家是否手持鬼妖
        /// </summary>
        public bool HeldMurasamaBool;
        /// <summary>
        /// 玩家是否正在进行终结技
        /// </summary>
        public bool EndSkillEffectStartBool;
        /// <summary>
        /// 升龙技冷却时间
        /// </summary>
        public int RisingDragonCoolDownTime;
        /// <summary>
        /// 是否受伤
        /// </summary>
        public bool onHit;

        public override void Initialize() {
            theRelicLuxor = 0;
            PressureIncrease = 1;
            onHit = false;
        }

        public override void ResetEffects() {
            theRelicLuxor = 0;
            PressureIncrease = 1;
            inFoodStallChair = false;
            EndlessStabilizerBool = false;
            HeldMurasamaBool = false;
            EndSkillEffectStartBool = false;
            onHit = false;
        }

        public override void OnEnterWorld() {
            if (ContentConfig.Instance.ForceReplaceResetContent) {
                CWRUtils.Text(CWRMod.RItemIndsDict.Count + CWRLocalizationText.GetTextValue("OnEnterWorld_TextContent"), Color.GreenYellow);
            }
        }

        public override void OnHurt(Player.HurtInfo info) {
            onHit = true;
        }

        public override void ModifyHitByNPC(NPC npc, ref Player.HurtModifiers modifiers) {
            base.ModifyHitByNPC(npc, ref modifiers);
        }

        public override void ModifyHitByProjectile(Projectile proj, ref Player.HurtModifiers modifiers) {
            base.ModifyHitByProjectile(proj, ref modifiers);
        }

        public override bool CanBeHitByNPC(NPC npc, ref int cooldownSlot) {
            return base.CanBeHitByNPC(npc, ref cooldownSlot);
        }

        public override bool CanBeHitByProjectile(Projectile proj) {
            return base.CanBeHitByProjectile(proj);
        }
    }
}
