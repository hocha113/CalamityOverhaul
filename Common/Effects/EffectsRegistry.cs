using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;

namespace CalamityOverhaul.Common.Effects
{
    public static class EffectsRegistry
    {
        public static Effect PowerSFShader;
        public static Effect WarpShader;
        public static Effect NeutronRingShader;
        public static MiscShaderData FlowColorShader;
        public static ArmorShaderData InShootGlowShader;

        public static void LoadEffects() {
            var assets = CWRMod.Instance.Assets;
            LoadRegularShaders(assets);
        }

        public static void LoadRegularShaders(AssetRepository assets) {
            Asset<Effect> getEffect(string key) => assets.Request<Effect>(CWRConstant.noEffects + key, AssetRequestMode.ImmediateLoad);
            void loadFiltersEffect(string filtersKey, string filename, string passname, out Effect effect) {
                Asset<Effect> asset = getEffect(filename);
                Filters.Scene[filtersKey] = new Filter(new(asset, passname), EffectPriority.VeryHigh);
                effect = asset.Value;
            }

            loadFiltersEffect("CWRMod:powerSFShader", "PowerSFShader", "Offset", out PowerSFShader);
            loadFiltersEffect("CWRMod:warpShader", "WarpShader", "PrimitivesPass", out WarpShader);
            loadFiltersEffect("CWRMod:neutronRingShader", "NeutronRingShader", "NeutronRingPass", out NeutronRingShader);

            FlowColorShader = new MiscShaderData(getEffect("FlowColorShader"), "PiercePass");
            InShootGlowShader = new ArmorShaderData(getEffect("InShootGlow"), "InShootGlowPass");
        }
    }
}
