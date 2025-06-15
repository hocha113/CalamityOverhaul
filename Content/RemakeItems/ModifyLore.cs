using CalamityMod.Items.LoreItems;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.RemakeItems.Core;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems
{
    internal abstract class ModifyLore<T> : ItemOverride where T : ModItem
    {
        public override string LocalizationCategory => "ModifyLores";
        public override int TargetID => ModContent.ItemType<T>();
        public override bool DrawingInfo => false;
        public LocalizedText Legend { get; set; }
        public override void PostSetStaticDefaults() => Legend = this.GetLocalization(nameof(Legend));
        //只修改中文区，因为其他语言的文本暂时没有制作完成
        //暂时全部禁用，在写手把那些故事重写完成之前不要进行修改
        public override bool? CanOverride(int id) => false;// id == TargetID && Language.ActiveCulture.LegacyId == (int)GameCulture.CultureName.Chinese;
        public override bool? On_ModifyTooltips(Item item, List<TooltipLine> tooltips) {
            CWRItems.OverModifyTooltip(item, tooltips);
            KeyboardState state = Keyboard.GetState();
            if ((state.IsKeyDown(Keys.LeftShift) || state.IsKeyDown(Keys.RightShift))) {
                string newContent = Language.GetTextValue($"Mods.CalamityOverhaul.ModifyLores.{Name}.Legend");
                tooltips.ReplaceTooltip("[legend]", VaultUtils.FormatColorTextMultiLine(newContent, Color.White), "");
            }
            else {
                string newContent = CWRLocText.Instance.Item_LegendOnMouseLang.Value;
                Color newColor = Color.Lerp(Color.BlueViolet, Color.White, 0.5f + (float)Math.Sin(Main.GlobalTimeWrappedHourly) * 0.5f);
                tooltips.ReplaceTooltip("[legend]", VaultUtils.FormatColorTextMultiLine(newContent, newColor), "");
            }
            return false;
        }
    }

    internal class ModifyLoreAbyss : ModifyLore<LoreAbyss> { }

    internal class ModifyLoreBloodMoon : ModifyLore<LoreBloodMoon> { }

    internal class ModifyLoreArchmage : ModifyLore<LoreArchmage> { }

    internal class ModifyLoreAquaticScourge : ModifyLore<LoreAquaticScourge> { }

    internal class ModifyLoreAstralInfection : ModifyLore<LoreAstralInfection> { }

    internal class ModifyLoreAstrumAureus : ModifyLore<LoreAstrumAureus> { }

    internal class ModifyLoreAstrumDeus : ModifyLore<LoreAstrumDeus> { }

    internal class ModifyLoreAwakening : ModifyLore<LoreAwakening> { }

    internal class ModifyLoreAzafure : ModifyLore<LoreAzafure> { }

    internal class ModifyLoreBrainofCthulhu : ModifyLore<LoreBrainofCthulhu> { }

    internal class ModifyLoreBrimstoneElemental : ModifyLore<LoreBrimstoneElemental> { }

    internal class ModifyLoreCalamitas : ModifyLore<LoreCalamitas> { }

    internal class ModifyLoreSignus : ModifyLore<LoreSignus> { }

    internal class ModifyLoreSkeletron : ModifyLore<LoreSkeletron> { }

    internal class ModifyLoreSkeletronPrime : ModifyLore<LoreSkeletronPrime> { }

    internal class ModifyLoreSlimeGod : ModifyLore<LoreSlimeGod> { }

    internal class ModifyLoreStormWeaver : ModifyLore<LoreStormWeaver> { }

    internal class ModifyLoreSulphurSea : ModifyLore<LoreSulphurSea> { }

    internal class ModifyLoreTwins : ModifyLore<LoreTwins> { }

    internal class ModifyLoreUnderworld : ModifyLore<LoreUnderworld> { }

    internal class ModifyLoreWallofFlesh : ModifyLore<LoreWallofFlesh> { }

    internal class ModifyLoreYharon : ModifyLore<LoreYharon> { }

    internal class ModifyLoreLeviathanAnahita : ModifyLore<LoreLeviathanAnahita> { }

    internal class ModifyLoreMechs : ModifyLore<LoreMechs> { }

    internal class ModifyLoreDevourerofGods : ModifyLore<LoreDevourerofGods> { }

    internal class ModifyLoreHiveMind : ModifyLore<LoreHiveMind> { }

    internal class ModifyLoreKingSlime : ModifyLore<LoreKingSlime> { }

    internal class ModifyLoreRavager : ModifyLore<LoreRavager> { }

    internal class ModifyLoreRequiem : ModifyLore<LoreRequiem> { }

    internal class ModifyLorePlaguebringerGoliath : ModifyLore<LorePlaguebringerGoliath> { }

    internal class ModifyLorePlantera : ModifyLore<LorePlantera> { }

    internal class ModifyLorePolterghast : ModifyLore<LorePolterghast> { }

    internal class ModifyLorePrelude : ModifyLore<LorePrelude> { }

    internal class ModifyLoreProfanedGuardians : ModifyLore<LoreProfanedGuardians> { }

    internal class ModifyLoreProvidence : ModifyLore<LoreProvidence> { }

    internal class ModifyLoreQueenBee : ModifyLore<LoreQueenBee> { }

    internal class ModifyLoreQueenSlime : ModifyLore<LoreQueenSlime> { }

    internal class ModifyLoreCalamitasClone : ModifyLore<LoreCalamitasClone> { }

    internal class ModifyLoreCeaselessVoid : ModifyLore<LoreCeaselessVoid> { }

    internal class ModifyLoreCorruption : ModifyLore<LoreCorruption> { }

    internal class ModifyLoreDragonfolly : ModifyLore<LoreDragonfolly> { }

    internal class ModifyLoreDukeFishron : ModifyLore<LoreDukeFishron> { }

    internal class ModifyLoreEaterofWorlds : ModifyLore<LoreEaterofWorlds> { }

    internal class ModifyLoreEmpressofLight : ModifyLore<LoreEmpressofLight> { }

    internal class ModifyLoreOldDuke : ModifyLore<LoreOldDuke> { }

    internal class ModifyLorePerforators : ModifyLore<LorePerforators> { }

    internal class ModifyLoreExoMechs : ModifyLore<LoreExoMechs> { }

    internal class ModifyLoreEyeofCthulhu : ModifyLore<LoreEyeofCthulhu> { }

    internal class ModifyLoreGolem : ModifyLore<LoreGolem> { }

    internal class ModifyLoreCrabulon : ModifyLore<LoreCrabulon> { }

    internal class ModifyLoreCrimson : ModifyLore<LoreCrimson> { }

    internal class ModifyLoreCynosure : ModifyLore<LoreCynosure> { }

    internal class ModifyLoreDesertScourge : ModifyLore<LoreDesertScourge> { }

    internal class ModifyLoreDestroyer : ModifyLore<LoreDestroyer> { }
}
