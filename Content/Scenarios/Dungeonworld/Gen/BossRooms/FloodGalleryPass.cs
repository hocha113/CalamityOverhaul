using System;
using Terraria.IO;
using Terraria.WorldBuilding;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.BossRooms
{
    /// <summary>
    /// 泄洪堂盖章 pass（镜像 GaolBossRoomPass）。接线：Dungeonworld.Tasks 里紧随
    /// GaolBossRoomPass 之后、LayerContentPass 之前插
    /// <c>new FloodGalleryPass(() => FloodGallerySiting.LastOrigin)</c>；
    /// 坐标提供者返回房间左上角 tile 坐标（P30 已定点并预留足印），返回 null 则本次跳过。
    /// </summary>
    internal class FloodGalleryPass : GenPass
    {
        private readonly Func<Point?> originProvider;

        internal FloodGalleryPass(Func<Point?> originProvider) : base("Dungeonworld Flood Gallery", 1f) {
            this.originProvider = originProvider;
        }

        protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration) {
            progress.Message = "灌浇泄洪堂...";
            Point? origin = originProvider?.Invoke();
            if (origin == null) {
                CWRMod.Instance.Logger.Warn("[FloodGalleryPass] 坐标提供者返回 null，本次生成跳过泄洪堂");
                return;
            }
            FloodGalleryRoom.Place(origin.Value.X, origin.Value.Y);
        }
    }
}
