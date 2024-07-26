using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;

namespace CalamityOverhaul.Common.Effects
{
    public static class EffectsRegistry
    {
        public static Effect WarpShader;
        public static Effect PowerSFShader;
        public static Effect NeutronRingShader;
        public static Effect PrimeHaloShader;
        public static Effect TwistColoringShader;
        public static Effect KnifeRendering;
        public static Effect KnifeDistortion;
        public static ArmorShaderData StreamerDustShader;
        public static ArmorShaderData InShootGlowShader;

        public static void LoadEffects() {
            var assets = CWRMod.Instance.Assets;
            LoadRegularShaders(assets);
        }

        public static void UnLoad() {
            WarpShader = null;
            PowerSFShader = null;
            NeutronRingShader = null;
            PrimeHaloShader = null;
            TwistColoringShader = null;
            KnifeRendering  = null;
            KnifeDistortion = null;
            StreamerDustShader = null;
            InShootGlowShader = null;
        }

        public static void LoadRegularShaders(AssetRepository assets) {
            Asset<Effect> getEffect(string key) => assets.Request<Effect>(CWRConstant.noEffects + key, AssetRequestMode.ImmediateLoad);
            void loadFiltersEffect(string filtersKey, string filename, string passname, out Effect effect) {
                Asset<Effect> asset = getEffect(filename);
                Filters.Scene[filtersKey] = new Filter(new(asset, passname), EffectPriority.VeryHigh);
                effect = asset.Value;
            }

            loadFiltersEffect("CWRMod:powerSFShader", "PowerSFShader", "PowerSFShaderPass", out PowerSFShader);
            loadFiltersEffect("CWRMod:warpShader", "WarpShader", "PrimitivesPass", out WarpShader);
            loadFiltersEffect("CWRMod:neutronRingShader", "NeutronRingShader", "NeutronRingPass", out NeutronRingShader);
            loadFiltersEffect("CWRMod:primeHaloShader", "PrimeHaloShader", "PrimeHaloPass", out PrimeHaloShader);
            loadFiltersEffect("CWRMod:twistColoringShader", "TwistColoring", "TwistColoringPass", out TwistColoringShader);
            loadFiltersEffect("CWRMod:knifeRendering", "KnifeRendering", "KnifeRenderingPass", out KnifeRendering);
            loadFiltersEffect("CWRMod:knifeDistortion", "KnifeDistortion", "KnifeDistortionPass", out KnifeDistortion);

            StreamerDustShader = new ArmorShaderData(getEffect("StreamerDust"), "StreamerDustPass");
            InShootGlowShader = new ArmorShaderData(getEffect("InShootGlow"), "InShootGlowPass");
        }
    }
}
