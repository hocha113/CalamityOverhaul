using CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen;
using CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Layers.L4;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Machines
{
    //====================================================================
    //L4 水位门:把"放水与排水互换整层的路"这条设计主张接成可玩的。
    //
    //在此之前 L4WaterWorks.ApplyState 只被调试看样入口调用过,世界里的阀杆
    //接不到任何东西——生成期锁死满水,这一层的核心机制等于不存在。
    //
    //三个决定:
    //1.不做持久化。子世界 ShouldSave=false,每次进入都重跑生成,舱段表整次访问有效;
    //  联机下生成只在子服务器跑,表因此只在服务端存在——而水位本来就该服务端裁决,正好。
    //2.运行时走 ApplyStateRuntime(纯重写,不 settle)。生成期那套 settle 含全图
    //  WaterCheck,秒级,放运行时就是硬卡帧;堰坎舱段构造性密封 + NormalUpdates=false
    //  让 UpdateLiquid 不转,重写完即静定。
    //3.哪根杆算阀门:没接线的那些。堰闸走廊的拉杆有红线、管着自己的闸门(原版语义,
    //  不能抢);阀室与泵房的拉杆生成时就没接任何线,本来就是留给水位的。
    //====================================================================
    internal static class DungeonworldWaterGate
    {
        //一次切换要重写上万格并回播几十个区块,给个冷却别让人按住右键刷
        private const int CooldownFrames = 150;

        //同一协议双向复用:上行是"我拉了这根杆",下行是"水位已经翻了,演一下"
        private const byte MsgRequest = 0;
        private const byte MsgResult = 1;

        private static int _cooldown;
        private static bool _pending;
        private static bool _pendingHigh;

        internal static void Reset() {
            _cooldown = 0;
            _pending = false;
        }

        internal static void Update() {
            if (_cooldown > 0) {
                _cooldown--;
            }
            if (!_pending) {
                return;
            }
            _pending = false;
            Commit(_pendingHigh);
        }

        /// <summary>行是否落在 L4 水牢带内</summary>
        internal static bool InWaterBand(int y) {
            LayerBand band = DungeonworldMetrics.Bands[3];
            return y >= band.Top && y < band.Bottom;
        }

        /// <summary>
        /// 阀杆被拉。单机就地排进队列,联机客户端只上行请求——
        /// 水位是世界态,客户端本地写液体立刻 desync。
        /// </summary>
        internal static void RequestToggle(int leverX, int leverY) {
            if (_cooldown > 0) {
                return;
            }
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                //客户端这一份只防自己手抖连点;真正的节流在裁决方的 Queue 里
                _cooldown = CooldownFrames;
                ModPacket packet = CWRMod.Instance.GetPacket();
                packet.Write((byte)CWRMessageType.DungeonworldWaterValve);
                packet.Write(MsgRequest);
                packet.Write((short)leverX);
                packet.Write((short)leverY);
                packet.Send();
                //本机不预演:预演一遍再被服务端回播盖一次,水面会闪两下
                return;
            }
            Queue(!L4WaterWorks.HighState);
        }

        /// <summary>
        /// 阀门协议两个方向都走这里。<br/>
        /// 无论走哪个分支都必须把该分支的字节读干净,否则后面的包全部错位。
        /// </summary>
        internal static void HandleValveRequest(BinaryReader reader, int whoAmI) {
            byte kind = reader.ReadByte();
            if (kind == MsgResult) {
                bool resultHigh = reader.ReadBoolean();
                if (Main.netMode == NetmodeID.MultiplayerClient) {
                    Announce(resultHigh);
                }
                return;
            }
            int x = reader.ReadInt16();
            int y = reader.ReadInt16();
            //上行协议只朝服务端走;客户端收到属于错用,字节已读完,丢弃即可
            if (Main.netMode != NetmodeID.Server) {
                return;
            }
            if (!WorldGen.InWorld(x, y, 5) || !InWaterBand(y)
                || !Main.tile[x, y].HasTile || Main.tile[x, y].TileType != TileID.Lever) {
                CWRMod.Instance.Logger.Warn(
                    $"[DungeonworldWaterGate] 玩家{whoAmI}的阀门请求({x},{y})不是本层阀杆,驳回");
                return;
            }
            Queue(!L4WaterWorks.HighState);
        }

        //推迟一帧执行:不在 tile 交互回调里当场重写上万格。
        //节流也压在这里——联机时服务端自己不走 RequestToggle,
        //若只在那边设冷却,两个客户端轮流点就能把服务端刷穿
        private static void Queue(bool high) {
            if (_pending || _cooldown > 0) {
                return;
            }
            _cooldown = CooldownFrames;
            _pending = true;
            _pendingHigh = high;
        }

        private static void Commit(bool high) {
            int wet = L4WaterWorks.ApplyStateRuntime(high);
            if (wet < 0) {
                //舱段表为空=本进程没跑过 L4 生成(联机客户端就是这种),不该走到这
                CWRMod.Instance.Logger.Warn("[DungeonworldWaterGate] 无登记舱段,切换忽略");
                return;
            }
            Broadcast();
            //单机就地演;联机由服务端点名让各客户端自己演(服务端没有本地玩家可演)
            if (Main.netMode == NetmodeID.Server) {
                ModPacket packet = CWRMod.Instance.GetPacket();
                packet.Write((byte)CWRMessageType.DungeonworldWaterValve);
                packet.Write(MsgResult);
                packet.Write(high);
                packet.Send();
            }
            else {
                Announce(high);
            }
            CWRMod.Instance.Logger.Info(
                $"[DungeonworldWaterGate] 阀门→{(high ? "放水" : "排水")} 水格={wet}"
                + $" 舱段={L4WaterWorks.Compartments.Count}");
        }

        //逐舱段分块回播:整带一次性发会撑爆单包,舱段本身就是天然的分块单位
        private static void Broadcast() {
            if (Main.netMode != NetmodeID.Server) {
                return;
            }
            const int Chunk = 32;
            foreach (L4WaterWorks.Compartment c in L4WaterWorks.Compartments) {
                for (int x = c.Area.Left; x < c.Area.Right; x += Chunk) {
                    for (int y = c.Area.Top; y < c.Area.Bottom; y += Chunk) {
                        NetMessage.SendTileSquare(-1, x, y,
                            System.Math.Min(Chunk, c.Area.Right - x),
                            System.Math.Min(Chunk, c.Area.Bottom - y));
                    }
                }
            }
        }

        //整层水面在动,声音得够重——玩家得知道刚才那一拉动的是整条路而不是一扇门。
        //只播给还在水牢层里的人:隔着五层听见泵房换气很出戏
        private static void Announce(bool high) {
            if (Main.dedServ || !InWaterBand((int)(Main.LocalPlayer.Center.Y / 16f))) {
                return;
            }
            Vector2 at = Main.LocalPlayer.Center;
            SoundEngine.PlaySound(SoundID.Mech with { Volume = 0.9f, Pitch = -0.8f }, at);
            SoundEngine.PlaySound(
                (high ? SoundID.SplashWeak : SoundID.Drown) with { Volume = 0.8f, Pitch = high ? -0.3f : 0.2f }, at);
            Main.LocalPlayer.CWR()?.GetScreenShake(3.2f);
        }
    }

    /// <summary>
    /// 阀杆钩子:原版拉杆右键后 <c>Player.TileInteractionsUse</c> 紧接着就调
    /// <c>TileLoader.RightClick</c>(Player.cs:28947-28953),本钩子挂在那一步。<br/>
    /// 只认没接线的拉杆——有线的是堰闸走廊那几根,管着自己的闸门,不能抢。
    /// </summary>
    internal class DungeonworldValveTile : GlobalTile
    {
        //原版的拉杆翻面动画与 HitSwitch 在本钩子之前已经跑完,这里只是搭个便车
        public override void RightClick(int i, int j, int type) {
            if (type != TileID.Lever || !Dungeonworld.Active
                || !DungeonworldWaterGate.InWaterBand(j) || HasWireNearby(i, j)) {
                return;
            }
            DungeonworldWaterGate.RequestToggle(i, j);
        }

        //拉杆是2x2,点哪一格都算,所以扫3x3把整个杆体连同贴边的线都覆盖到
        private static bool HasWireNearby(int cx, int cy) {
            for (int x = cx - 1; x <= cx + 1; x++) {
                for (int y = cy - 1; y <= cy + 1; y++) {
                    if (!WorldGen.InWorld(x, y, 5)) {
                        continue;
                    }
                    Tile tile = Main.tile[x, y];
                    if (tile.RedWire || tile.BlueWire || tile.GreenWire || tile.YellowWire) {
                        return true;
                    }
                }
            }
            return false;
        }
    }
}
