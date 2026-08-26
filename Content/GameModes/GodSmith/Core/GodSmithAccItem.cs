using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Core
{
    /// <summary>
    /// 饰品重铸的 GlobalItem 分发面（无实例数据）：
    /// UpdateAccessory 查注册表派发效果并向 <see cref="GodSmithPlayer"/> 登记本帧生效清单，
    /// ModifyTooltips 注入金色标题行与效果描述行；全部以 GodSmithActive 为闸
    /// </summary>
    internal class GodSmithAccItem : GlobalItem
    {
        //只挂饰品；具体是否被重铸由注册表按 type 再筛
        public override bool AppliesToEntity(Item entity, bool lateInstantiation)
            => lateInstantiation && entity.accessory;

        public override void UpdateAccessory(Item item, Player player, bool hideVisual) {
            if (!GameModeSystem.GodSmithActive || !GodSmithAccEffect.TryGet(item.type, out GodSmithAccEffect effect)) {
                return;
            }
            GodSmithPlayer state = player.GetModPlayer<GodSmithPlayer>();
            state.RegisterActiveAcc(item, effect);
            effect.UpdateAccessory(item, player, hideVisual, state);
        }

        public override void ModifyTooltips(Item item, List<TooltipLine> tooltips) {
            if (!GameModeSystem.GodSmithActive || !GodSmithAccEffect.TryGet(item.type, out GodSmithAccEffect effect)) {
                return;
            }
            GodSmithTooltip.EnsureTitle(tooltips);
            GodSmithTooltip.AddBodyLines(tooltips, "CWR_GodSmithAccDesc", effect.EffectDesc?.Value);
        }
    }
}
