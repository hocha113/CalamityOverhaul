using CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Layers.L6;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Machines
{
    //====================================================================
    //L6 机器驱动:把 L6MachineSlots 登记的活塞/碾压槽真正开动起来。
    //在此之前这两类槽只有 Cog 剪影,零伤害，"每条走廊都上了膛"是句空话。
    //
    //数据来源:子世界 ShouldSave=false,每次进入都重跑生成,槽位表因此在
    //整次访问里都有效,不需要额外的持久化层。联机下生成只在子服务器跑,
    //所以表只在服务端存在，伤害本来就该由服务端裁决,正好对上。
    //
    //时钟:自己数帧,不吃 Main.GameUpdateCount。子世界 NormalUpdates=false,
    //原版世界更新整段停摆,唯一保证每帧到的钩子是 Subworld.Update()(SLib
    //在 PreUpdateWorld 之后、PostUpdateWorld 之前调),本类由 Dungeonworld.Update 驱动。
    //====================================================================
    internal static class DungeonworldMachines
    {
        //一台机器多久走一趟(帧)。整层同周期但按槽序错相,避免全层同帧一起砸
        private const int Cycle = 170;
        //机器只在有人靠近时开动:一层一千两百行、几十台机器,没人看的地方不烧弹幕位
        private const int WakeRangeTiles = 70;

        private const int PistonDamage = 40;
        private const int RollerDamage = 30;

        private static int _tick;

        internal static void Reset() => _tick = 0;

        internal static void Update() {
            //伤害由服务端/单机裁决:弹幕生成包自带同步,客户端自行生成会各打各的
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                return;
            }
            var slots = L6MachineSlots.Slots;
            if (slots.Count == 0) {
                return;
            }
            _tick++;
            for (int i = 0; i < slots.Count; i++) {
                L6MachineSlot slot = slots[i];
                bool roller = slot.Kind == L6SlotKind.GearCrush;
                if (!roller && slot.Kind != L6SlotKind.Piston) {
                    continue;
                }
                //错相:相邻槽差 37 帧,一条走廊上的机关串因此是波浪不是齐射
                if ((_tick + i * 37) % Cycle != 0 || !AnyPlayerNear(slot.Frame)) {
                    continue;
                }
                Fire(slot, roller);
            }
        }

        private static void Fire(L6MachineSlot slot, bool roller) {
            Rectangle frame = slot.Frame;
            //登记的帧只是机器本体的包络,不含行程，行程按现场量:
            //从槽内往下找第一层实心,那就是它要捶到/碾过的行走面。
            //这样活塞多深、碾轮贴哪一行都由几何自己说了算,不靠帧尺寸猜
            Vector2 origin;
            float travel;
            if (roller) {
                //碾轮左右交替来向,不至于永远从同一头滚出来
                bool rightward = (_tick / Cycle & 1) == 0;
                int startX = rightward ? frame.Left + 1 : frame.Right - 2;
                if (!TryFindFloor(startX, frame.Top, frame.Height + 6, out int floorRow)) {
                    return;
                }
                origin = new Vector2(startX * 16f + 8f, floorRow * 16f - 22f);
                travel = (frame.Width - 2) * (rightward ? 1 : -1);
            }
            else {
                int headX = frame.Left + frame.Width / 2;
                if (!TryFindFloor(headX, frame.Top + 1, 16, out int floorRow)) {
                    return;
                }
                //锤头3格高,行程留2格让砸面刚好停在地面上
                travel = floorRow - frame.Top - 2;
                origin = new Vector2(headX * 16f + 8f, frame.Top * 16f + 8f);
            }
            if (MathF.Abs(travel) < 2f) {
                return;
            }
            Projectile.NewProjectile(new EntitySource_WorldEvent(), origin, Vector2.Zero,
                ModContent.ProjectileType<L6MachineStrike>(),
                roller ? RollerDamage : PistonDamage, 6f, Main.myPlayer,
                roller ? 1f : 0f, travel);
        }

        //自 startY 向下找第一层可站实心(平台不算行走面,机器要砸的是砖)
        private static bool TryFindFloor(int x, int startY, int maxDrop, out int floorRow) {
            for (int i = 0; i <= maxDrop; i++) {
                int y = startY + i;
                if (!WorldGen.InWorld(x, y, 5)) {
                    break;
                }
                Tile tile = Main.tile[x, y];
                if (tile.HasTile && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType]) {
                    floorRow = y;
                    return true;
                }
            }
            floorRow = 0;
            return false;
        }

        private static bool AnyPlayerNear(Rectangle frame) {
            var center = new Vector2((frame.Left + frame.Width * 0.5f) * 16f,
                (frame.Top + frame.Height * 0.5f) * 16f);
            float rangeSq = WakeRangeTiles * 16f * (WakeRangeTiles * 16f);
            foreach (Player player in Main.player) {
                if (player.active && !player.dead
                    && Vector2.DistanceSquared(player.Center, center) <= rangeSq) {
                    return true;
                }
            }
            return false;
        }
    }
}
