using System;
using Terraria.IO;
using Terraria.WorldBuilding;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.BossRooms
{
    /// <summary>
    /// 深牢禁室落位 pass（待注册）。A 路接线：在 Dungeonworld.Tasks 的 P50 装修段
    /// 前后插入 <c>new GaolBossRoomPass(() => 选址)</c> 即可，坐标提供者返回房间
    /// 左上角 tile 坐标（尺寸见 GaolBossRoom.Width/Height），返回 null 则本次跳过。
    /// 选址建议参数见 Doc\plans\Dungeonworld\BOSS-DeepGaolWraith.md §Boss 房。
    /// </summary>
    internal class GaolBossRoomPass : GenPass
    {
        private readonly Func<Point?> originProvider;

        internal GaolBossRoomPass(Func<Point?> originProvider) : base("Dungeonworld Gaol Boss Room", 1f) {
            this.originProvider = originProvider;
        }

        /// <summary>坐标已定时的便捷重载</summary>
        internal GaolBossRoomPass(Point origin) : this(() => origin) { }

        protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration) {
            progress.Message = "铸造深牢禁室...";
            Point? origin = originProvider?.Invoke();
            if (origin == null) {
                CWRMod.Instance.Logger.Warn("[GaolBossRoomPass] 坐标提供者返回 null，本次生成跳过 Boss 房");
                return;
            }
            GaolBossRoom.Place(origin.Value.X, origin.Value.Y);
        }
    }
}
