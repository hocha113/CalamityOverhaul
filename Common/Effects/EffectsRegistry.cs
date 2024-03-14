using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;
using Terraria.ModLoader;

namespace CalamityOverhaul.Common.Effects
{
    public static class EffectsRegistry
    {
        public static Effect PowerSFShader;
        public static MiscShaderData FlowColorShader;

        public static void LoadEffects() {
            var assets = CWRMod.Instance.Assets;
            LoadRegularShaders(assets);
        }

        public static void LoadRegularShaders(AssetRepository assets) {
            Ref<Effect> flowColorShader = new(assets.Request<Effect>(CWRConstant.noEffects + "FlowColorShader", AssetRequestMode.ImmediateLoad).Value);
            GameShaders.Misc["CWRMod:FlowColorShader"] = new MiscShaderData(flowColorShader, "PiercePass");

            Ref<Effect> powerSFShader = new(assets.Request<Effect>(CWRConstant.noEffects + "PowerSFShader", AssetRequestMode.ImmediateLoad).Value);
            Filters.Scene["CWRMod:powerSFShader"] = new Filter(new(powerSFShader, "Offset"), EffectPriority.VeryHigh);

            PowerSFShader = CWRMod.Instance.Assets.Request<Effect>(CWRConstant.noEffects + "PowerSFShader", AssetRequestMode.ImmediateLoad).Value;
            FlowColorShader = GameShaders.Misc["CWRMod:FlowColorShader"];
        }
    }
}
