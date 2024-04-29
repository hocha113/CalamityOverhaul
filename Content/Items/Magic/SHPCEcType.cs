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

        public static void SHPCDamage(ref StatModifier damage) {
            if (IsLegend) {
                bool plantera = NPC.downedPlantBoss;
                bool golem = NPC.downedGolemBoss;
                bool cultist = NPC.downedAncientCultist;
                bool moonLord = NPC.downedMoonlord;
                bool providence = DownedBossSystem.downedProvidence;
                bool devourerOfGods = DownedBossSystem.downedDoG;
                bool yharon = DownedBossSystem.downedYharon;
                float damageMult = 1f +
                    (plantera ? 0.1f : 0f) +
                    (golem ? 0.11f : 0f) +
                    (cultist ? 2.6f : 0f) +
                    (moonLord ? 4f : 0f) +
                    (providence ? 3.6f : 0f) +
                    (devourerOfGods ? 3.5f : 0f) +
                    (yharon ? 6f : 0f);
                damage *= damageMult;
            }
        }

        public override void ModifyWeaponDamage(Player player, ref StatModifier damage) {
            SHPCDamage(ref damage);
        }

        public override void ModifyTooltips(List<TooltipLine> list) {
            list.FindAndReplace("[GFB]", IsLegend ? CWRLocText.GetTextValue("SHPC_No_legend_Content_1") : CWRLocText.GetTextValue("SHPC_No_legend_Content_2"));
        }
    }
}
