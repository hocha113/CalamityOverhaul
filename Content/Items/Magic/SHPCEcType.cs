using CalamityMod;
using CalamityMod.Items.Weapons.Magic;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Magic.HeldProjs;
using Terraria.ModLoader;
using Terraria;
using System.Collections.Generic;

namespace CalamityOverhaul.Content.Items.Magic
{
    internal class SHPCEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Magic + "SHPC";
        public static bool IsLegend => Main.zenithWorld || CWRServerConfig.Instance.WeaponEnhancementSystem;
        public override void SetDefaults() {
            Item.SetCalamitySD<SHPC>();
            Item.SetHeldProj<SHPCHeldProj>();
        }

        public override void ModifyWeaponDamage(Player player, ref StatModifier damage) {
            if (IsLegend) {
                bool plantera = NPC.downedPlantBoss;
                bool golem = NPC.downedGolemBoss;
                bool cultist = NPC.downedAncientCultist;
                bool moonLord = NPC.downedMoonlord;
                bool providence = DownedBossSystem.downedProvidence;
                bool devourerOfGods = DownedBossSystem.downedDoG;
                bool yharon = DownedBossSystem.downedYharon;
                float damageMult = 1f +
                    (plantera ? 0.1f : 0f) + //1.1
                    (golem ? 0.15f : 0f) + //1.25
                    (cultist ? 3.5f : 0f) + //4.75
                    (moonLord ? 4.5f : 0f) + //9.25
                    (providence ? 7.5f : 0f) + //16.75
                    (devourerOfGods ? 2.5f : 0f) + //19.25
                    (yharon ? 30f : 0f); //49.25
                damage *= damageMult;
            }
        }

        public override void ModifyTooltips(List<TooltipLine> list) {
            list.FindAndReplace("[GFB]", IsLegend ? CWRLocText.GetTextValue("SHPC_No_legend_Content_1") : CWRLocText.GetTextValue("SHPC_No_legend_Content_2"));
        }
    }
}
