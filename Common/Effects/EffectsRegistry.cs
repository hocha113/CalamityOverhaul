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
            Asset<Effect> _flowColorShaderAsset = assets.Request<Effect>(CWRConstant.noEffects + "FlowColorShader", AssetRequestMode.ImmediateLoad);
            FlowColorShader = GameShaders.Misc["CWRMod:FlowColorShader"] = new MiscShaderData(_flowColorShaderAsset, "PiercePass");

            Asset<Effect> _powerSFShaderAsset = assets.Request<Effect>(CWRConstant.noEffects + "PowerSFShader", AssetRequestMode.ImmediateLoad);
            Filters.Scene["CWRMod:powerSFShader"] = new Filter(new(_powerSFShaderAsset, "Offset"), EffectPriority.VeryHigh);
            PowerSFShader = _powerSFShaderAsset.Value;
        }
    }
}
