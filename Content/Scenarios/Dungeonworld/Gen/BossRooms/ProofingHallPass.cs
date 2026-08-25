using System;
using Terraria.IO;
using Terraria.WorldBuilding;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.BossRooms
{
    /// <summary>
    /// 验收堂盖章 pass（镜像 FloodGalleryPass）。接线：Dungeonworld.Tasks 里紧随
    /// FloodGalleryPass 之后、LayerContentPass 之前插
    /// <c>new ProofingHallPass(() => ProofingHallSiting.LastOrigin)</c>；
    /// 坐标提供者返回房间左上角 tile 坐标（P30 已定点并预留足印），返回 null 则本次跳过。
    /// </summary>
    internal class ProofingHallPass : GenPass
    {
        private readonly Func<Point?> originProvider;

        internal ProofingHallPass(Func<Point?> originProvider) : base("Dungeonworld Proofing Hall", 1f) {
            this.originProvider = originProvider;
        }

        protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration) {
            progress.Message = "架设验收堂...";
            Point? origin = originProvider?.Invoke();
            if (origin == null) {
                CWRMod.Instance.Logger.Warn("[ProofingHallPass] 坐标提供者返回 null，本次生成跳过验收堂");
                return;
            }
            ProofingHallRoom.Place(origin.Value.X, origin.Value.Y);
        }
    }
}
