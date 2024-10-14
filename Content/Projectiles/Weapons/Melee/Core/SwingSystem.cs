using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System.Collections.Generic;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.Core
{
    internal class SwingSystem : ICWRLoader
    {
        internal static List<BaseSwing> Swings;
        internal static Dictionary<string, int> SwingFullNameToType;
        internal static Dictionary<int, Asset<Texture2D>> trailTextures;
        internal static Dictionary<int, Asset<Texture2D>> gradientTextures;
        internal static Dictionary<int, Asset<Texture2D>> glowTextures;
        void ICWRLoader.LoadData() {
            Swings = [];
            SwingFullNameToType = [];
            trailTextures = [];
            gradientTextures = [];
            glowTextures = [];
        }
        void ICWRLoader.SetupData() {
            Swings = CWRUtils.GetSubclass<BaseSwing>();
            foreach (var swing in Swings) {
                string pathValue = swing.GetType().Name;
                int type = CWRMod.Instance.Find<ModProjectile>(pathValue).Type;
                SwingFullNameToType.Add(pathValue, type);
            }
        }
        void ICWRLoader.LoadAsset() {
            foreach (var swing in Swings) {
                string path1 = swing.trailTexturePath;
                string path2 = swing.gradientTexturePath;
                string path3 = swing.GlowTexturePath;

                if (path1 == "") {
                    path1 = CWRConstant.Masking + "MotionTrail3";
                }
                if (path2 == "") {
                    path2 = CWRConstant.ColorBar + "NullEffectColorBar";
                }

                int type = SwingFullNameToType[swing.GetType().Name];

                trailTextures.TryAdd(type, CWRUtils.GetT2DAsset(path1));
                gradientTextures.TryAdd(type, CWRUtils.GetT2DAsset(path2));

                if (path3 != "") {
                    glowTextures.TryAdd(type, CWRUtils.GetT2DAsset(path3));
                }
            }
        }
        void ICWRLoader.UnLoadData() {
            Swings = null;
            SwingFullNameToType = null;
            trailTextures = null;
            gradientTextures = null;
            glowTextures = null;
        }
    }
}
