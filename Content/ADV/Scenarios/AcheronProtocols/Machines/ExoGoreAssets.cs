using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;

namespace CalamityOverhaul.Content.ADV.Scenarios.AcheronProtocols.Machines
{
    /// <summary>
    /// 统一的机械残骸纹理对象池，供天空背景与残骸Actor等外部模块共享使用 在模组加载时一次性请求所有ExoGore纹理，避免各处硬编码路径与重复加载
    /// </summary>
    internal static class ExoGoreAssets
    {
        ///// <summary>
        ///// ExoGore纹理资源所在目录
        ///// </summary>
        //public const string Path = "CalamityOverhaul/Content/ADV/Scenarios/AcheronProtocols/Machines/ExoGores/";

        ///// <summary>
        ///// 所有残骸纹理的文件名，不含扩展名
        ///// </summary>
        //public static readonly string[] Names = [
        //  "Apollo1", "Apollo2", "Apollo3", "Apollo4", "Apollo5",
        //  "AresArm_Gore1", "AresArm_Gore2", "AresArm_Gore3",
        //  "AresBody1", "AresBody2", "AresBody3", "AresBody4", "AresBody5", "AresBody6", "AresBody7",
        //  "AresGaussNuke1", "AresGaussNuke2", "AresGaussNuke3",
        //  "AresHandBase1", "AresHandBase2", "AresHandBase3",
        //  "AresLaserCannon1", "AresLaserCannon2",
        //  "AresPlasmaFlamethrower1", "AresPlasmaFlamethrower2",
        //  "AresTeslaCannon1", "AresTeslaCannon2",
        //  "Artemis1", "Artemis2", "Artemis3", "Artemis4", "Artemis5",
        //  "ThanatosBody1", "ThanatosBody1_2", "ThanatosBody1_3",
        //  "ThanatosBody2", "ThanatosBody2_2", "ThanatosBody2_3",
        //  "ThanatosHead", "ThanatosHead2", "ThanatosHead3",
        //  "ThanatosTail", "ThanatosTail2", "ThanatosTail3", "ThanatosTail4"
        //];

        private static Asset<Texture2D>[] assets;

        /// <summary>

        /// 所有已加载的残骸纹理资源，服务器端或未加载时为<see langword="null"/>

        /// </summary>
        public static Asset<Texture2D>[] Assets => assets;

        /// <summary>

        /// 纹理资源数量

        /// </summary>
        public static int Count => assets?.Length ?? 0;

        /// <summary>

        /// 根据索引安全获取纹理资源，越界或未加载时返回<see langword="null"/>

        /// </summary>
        public static Asset<Texture2D> Get(int index) {
            if (assets == null || (uint)index >= (uint)assets.Length) {
                return null;
            }
            return assets[index];
        }

        /// <summary>

        /// 根据索引获取已就绪的纹理，未加载或越界时返回<see langword="null"/>

        /// </summary>
        public static Texture2D GetTexture(int index) {
            var asset = Get(index);
            return asset != null && asset.IsLoaded ? asset.Value : null;
        }

        private sealed class Loader : ICWRLoader
        {
            void ICWRLoader.LoadData() {
                if (VaultUtils.isServer) {
                    return;
                }
                //var buffer = new Asset<Texture2D>[Names.Length];
                //for (int i = 0; i < Names.Length; i++) {
                //  buffer[i] = ModContent.Request<Texture2D>(Path + Names[i], AssetRequestMode.AsyncLoad);
                //}
                var buffer = new Asset<Texture2D>[3];
                for (int i = 0; i < 3; i++) {
                    buffer[i] = VaultAsset.placeholder3;
                }
                assets = buffer;
            }

            void ICWRLoader.UnLoadData() {
                assets = null;
            }
        }
    }
}
