using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System.Collections.Generic;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.Core
{
    internal class SwingSystem : ILoader
    {
        internal static List<BaseSwing> Swings;
        internal static Dictionary<string, int> SwingFullNameToType;
        internal static Dictionary<int, Asset<Texture2D>> trailTextures;
        internal static Dictionary<int, Asset<Texture2D>> gradientTextures;
        void ILoader.LoadData() {
            Swings = new List<BaseSwing>();
            SwingFullNameToType = new Dictionary<string, int>();
            trailTextures = new Dictionary<int, Asset<Texture2D>>();
            gradientTextures = new Dictionary<int, Asset<Texture2D>>();
        }
        void ILoader.SetupData() {
            Swings = CWRUtils.HanderSubclass<BaseSwing>();
            foreach (var swing in Swings) {
                string pathValue = swing.GetType().Name;
                int type = CWRMod.Instance.Find<ModProjectile>(pathValue).Type;
                SwingFullNameToType.Add(pathValue, type);
            }
        }
        void ILoader.LoadAsset() {
            foreach (var swing in Swings) {
                string path1 = swing.trailTexturePath;
                string path2 = swing.gradientTexturePath;
                if (path1 == "") {
                    path1 = CWRConstant.Masking + "MotionTrail3";
                }
                if (path2 == "") {
                    path2 = CWRConstant.ColorBar + "NullEffectColorBar";
                }
                int type = SwingFullNameToType[swing.GetType().Name];
                trailTextures.TryAdd(type, CWRUtils.GetT2DAsset(path1));
                gradientTextures.TryAdd(type, CWRUtils.GetT2DAsset(path2));
            }
        }
        void ILoader.UnLoadData() {
            Swings = null;
            SwingFullNameToType = null;
            trailTextures = null;
            gradientTextures = null;
        }
    }
}
