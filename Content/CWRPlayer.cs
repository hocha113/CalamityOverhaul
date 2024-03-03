using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content
{
    public class CWRPlayer : ModPlayer
    {
        /// <summary>
        /// 圣物的装备等级，这个字段决定了玩家会拥有什么样的弹幕效果
        /// </summary>
        public int theRelicLuxor = 0;
        /// <summary>
        /// 是否装备制动器
        /// </summary>
        public bool LoadMuzzleBrake;
        /// <summary>
        /// 应力缩放
        /// </summary>
        public float PressureIncrease;
        //未使用的，这个属性属于一个未完成的UI
        public int CompressorPanelID = -1;
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
            onHit = false;
            theRelicLuxor = 0;
            PressureIncrease = 1;
            LoadMuzzleBrake = false;
        }

        public override void ResetEffects() {
            onHit = false;
            theRelicLuxor = 0;
            PressureIncrease = 1;
            inFoodStallChair = false;
            EndlessStabilizerBool = false;
            HeldMurasamaBool = false;
            EndSkillEffectStartBool = false;
            LoadMuzzleBrake = false;
        }

        public override void OnEnterWorld() {
            if (ContentConfig.Instance.ForceReplaceResetContent) {
                CWRUtils.Text(CWRMod.RItemIndsDict.Count + CWRLocText.GetTextValue("OnEnterWorld_TextContent"), Color.GreenYellow);
            }
        }

        public override void OnHurt(Player.HurtInfo info) {
            onHit = true;
        }

        public override void ModifyWeaponDamage(Item item, ref StatModifier damage) {
            if (LoadMuzzleBrake) {
                if (item.DamageType == DamageClass.Ranged) {
                    damage *= 0.75f;
                }
            }
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

        public override IEnumerable<Item> AddStartingItems(bool mediumCoreDeath) {
            yield return new Item(ModContent.ItemType<OverhaulTheBibleBook>());
        }
    }
}
