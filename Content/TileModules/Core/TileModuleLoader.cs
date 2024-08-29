using CalamityOverhaul.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.TileModules.Core
{
    /// <summary>
    /// Tile模块的管理类。此类负责加载、设置和卸载Tile模块，并处理与世界生成和网络通信相关的操作
    /// </summary>
    internal class TileModuleLoader : GlobalTile, ILoader, INetWork
    {
        /// <summary>
        /// 所有Tile模块的列表该列表在加载时初始化，并包含所有Tile模块的实例
        /// </summary>
        public static List<BaseTileModule> TileModulesList { get; private set; } = [];
        /// <summary>
        /// 当前世界中的Tile模块列表此列表在世界加载和操作时动态更新
        /// </summary>
        public static List<BaseTileModule> TileModuleInWorld { get; internal set; } = [];
        /// <summary>
        /// 将Tile模块的类型映射到其对应的ID的字典
        /// </summary>
        public static Dictionary<Type, int> TileModuleTypeToID { get; private set; } = [];
        /// <summary>
        /// 将Tile模块的类型映射到模块实例的字典
        /// </summary>
        public static Dictionary<Type, BaseTileModule> ModuleTypeToInstance { get; private set; } = [];
        /// <summary>
        /// 记录当前世界中每个模块ID对应的Tile模块数量
        /// </summary>
        public static Dictionary<int, int> ModuleIDHanderModuleHasNumInWorld { get; internal set; } = [];
        /// <summary>
        /// 将模块ID映射到模块实例的字典
        /// </summary>
        public static Dictionary<int, BaseTileModule> ModuleIDToModuleInstance { get; private set; } = [];
        /// <summary>
        /// 将目标Tile的ID映射到模块实例的字典
        /// </summary>
        public static Dictionary<int, BaseTileModule> TargetTileToModuleInstance { get; private set; } = [];
        /// <summary>
        /// 在世界中的Tile模块的最大存在数量
        /// </summary>
        public const int MaxTileModuleInWorldCount = 1000;
        internal delegate void On_Tile_KillMultiTile_Dalegate(int i, int j, int frameX, int frameY, int type);
        public static Type tileLoaderType;
        public static MethodBase onTile_KillMultiTile_Method;
        public static INetWork NetInstance;

        void INetWork.LoadNet() => NetInstance = this;
        void ILoader.LoadData() {
            TileModulesList = CWRUtils.HanderSubclass<BaseTileModule>();
            for (int i = 0; i < TileModulesList.Count; i++) {
                BaseTileModule module = TileModulesList[i];
                module.Load();
                TileModuleTypeToID.Add(module.GetType(), i);
                ModuleTypeToInstance.Add(module.GetType(), module);
                ModuleIDToModuleInstance.Add(module.ModuleID, module);
                ModuleIDHanderModuleHasNumInWorld.Add(module.ModuleID, 0);
                TargetTileToModuleInstance.Add(module.TargetTileID, module);
            }

            tileLoaderType = typeof(TileLoader);
            onTile_KillMultiTile_Method = tileLoaderType
                .GetMethod("KillMultiTile", BindingFlags.Public | BindingFlags.Static);
            CWRHook.Add(onTile_KillMultiTile_Method, OnKillMultiTileHook);

            WorldGen.Hooks.OnWorldLoad += LoadWorldTileModule;
        }

        void ILoader.SetupData() {
            foreach (var module in TileModulesList) {
                module.SetStaticProperty();
            }
        }

        void ILoader.UnLoadData() {
            foreach (var module in TileModulesList) {
                module.UnLoad();
            }
            TileModulesList.Clear();
            TileModuleTypeToID.Clear();
            ModuleTypeToInstance.Clear();
            ModuleIDToModuleInstance.Clear();
            ModuleIDHanderModuleHasNumInWorld.Clear();
            TargetTileToModuleInstance.Clear();
            tileLoaderType = null;
            onTile_KillMultiTile_Method = null;
            NetInstance = null;
            WorldGen.Hooks.OnWorldLoad -= LoadWorldTileModule;
        }

        private static void OnKillMultiTileHook(On_Tile_KillMultiTile_Dalegate orig
            , int i, int j, int frameX, int frameY, int type) {
            foreach (var module in TileModuleInWorld) {
                if (!module.Active) {
                    continue;
                }
                module.KillMultiTileSet(frameX, frameY);
            }
            orig.Invoke(i, j, frameX, frameY, type);
        }

        /// <summary>
        /// 加载世界中的所有 TileModule，初始化和激活它们
        /// </summary>
        /// <remarks>
        /// 此方法会首先移除不再活跃的模块，然后扫描整个世界的每一个 Tile，
        /// 识别出多结构物块的左上角 Tile，并将其添加到世界中的模块列表中
        /// 最后，加载所有处于激活状态的模块
        /// </remarks>
        internal static void LoadWorldTileModule() {
            TileModuleInWorld = [];

            for (int x = 0; x < Main.tile.Width; x++) {
                for (int y = 0; y < Main.tile.Height; y++) {
                    Tile tile = Main.tile[x, y];
                    if (tile != null && tile.HasTile && CWRUtils.IsTopLeft(x, y, out Point16 point)) {
                        AddInWorld(tile.TileType, point, null);
                    }
                }
            }

            foreach (BaseTileModule module in TileModuleInWorld) {
                if (!module.Active) {
                    continue;
                }
                module.LoadInWorld();
            }

            if (CWRUtils.isServer) {
                TMEInWorldNetWork.NetInstance.NetSend();
            }
        }

        /// <summary>
        /// 向世界中的模块列表添加一个新的 TileModule
        /// </summary>
        /// <param name="tileID">要添加的 Tile 的 ID</param>
        /// <param name="position">该模块的左上角位置</param>
        /// <param name="item">用于跟踪该模块的物品，可以为 null</param>
        /// <remarks>
        /// 该方法会首先尝试从 <see cref="TileModuleTypeToID"/> 获取对应的模块，然后克隆该模块并设置其位置、跟踪物品和激活状态
        /// 如果有空闲的模块槽位，会将新模块放入该槽位，否则会添加到列表的末尾
        /// </remarks>
        internal static void AddInWorld(int tileID, Point16 position, Item item) {
            if (TargetTileToModuleInstance.TryGetValue(tileID, out BaseTileModule module)) {
                BaseTileModule newModule = module.Clone();
                newModule.Position = position;
                newModule.TrackItem = item;
                newModule.Active = true;
                newModule.SetProperty();

                bool add = true;
                for (int i = 0; i < TileModuleInWorld.Count; i++) {
                    if (!TileModuleInWorld[i].Active) {
                        newModule.WhoAmI = TileModuleInWorld[i].WhoAmI;
                        TileModuleInWorld[i] = newModule;
                        add = false;
                        break;
                    }
                }

                if (add && TileModuleInWorld.Count < MaxTileModuleInWorldCount) {
                    newModule.WhoAmI = TileModuleInWorld.Count;
                    TileModuleInWorld.Add(newModule);
                }
            }
        }

        /// <summary>
        /// 根据指定类型获取对应的模块ID
        /// </summary>
        /// <param name="type">模块的类型</param>
        /// <returns>返回该类型对应的模块ID</returns>
        public static int GetModuleID(Type type) => TileModuleTypeToID[type];

        /// <summary>
        /// 使用精确搜索查找与指定ID及坐标对应的模块，并将其转换为指定类型的模块
        /// </summary>
        /// <typeparam name="T">要返回的模块的类型，必须继承自 <see cref="BaseTileModule"/></typeparam>
        /// <param name="ID">要查找的模块的ID</param>
        /// <param name="x">要查找的模块的x坐标</param>
        /// <param name="y">要查找的模块的y坐标</param>
        /// <returns>返回与指定ID及坐标对应的模块，如果未找到则返回<see langword="null"/></returns>
        public static T FindModulePreciseSearch<T>(int ID, int x, int y)
            where T : BaseTileModule => FindModulePreciseSearch(ID, x, y) as T;
        /// <summary>
        /// 使用精确搜索查找与指定ID及坐标对应的模块
        /// </summary>
        /// <param name="ID">要查找的模块的ID</param>
        /// <param name="x">要查找的模块的x坐标</param>
        /// <param name="y">要查找的模块的y坐标</param>
        /// <returns>返回与指定ID及坐标对应的 <see cref="BaseTileModule"/>，如果未找到则返回<see langword="null"/></returns>
        public static BaseTileModule FindModulePreciseSearch(int ID, int x, int y) {
            BaseTileModule module = null;
            // 判断坐标是否为多结构物块的左上角，并获取其左上角位置
            if (CWRUtils.IsTopLeft(x, y, out var point)) {
                // 遍历世界中的所有模块，查找与指定ID和坐标匹配的模块
                foreach (var inds in TileModuleInWorld) {
                    if (inds.Position.X == point.X && inds.Position.Y == point.Y && inds.ModuleID == ID) {
                        module = inds;
                        break;
                    }
                }
            }
            return module;
        }

        /// <summary>
        /// 在指定范围内查找与指定ID和坐标最接近的模块，并将其转换为指定类型的模块
        /// </summary>
        /// <typeparam name="T">要返回的模块的类型，必须继承自 <see cref="BaseTileModule"/></typeparam>
        /// <param name="ID">要查找的模块的ID</param>
        /// <param name="x">要查找的模块的x坐标</param>
        /// <param name="y">要查找的模块的y坐标</param>
        /// <param name="maxFindLeng">搜索范围的最大距离</param>
        /// <returns>返回与指定ID及坐标最接近的模块，如果未找到则返回<see cref="null"/></returns>
        public static T FindModuleRangeSearch<T>(int ID, int x, int y, int maxFindLeng)
            where T : BaseTileModule => FindModuleRangeSearch(ID, x, y, maxFindLeng) as T;
        /// <summary>
        /// 在指定范围内查找与指定ID和坐标最接近的模块
        /// </summary>
        /// <param name="ID">要查找的模块的ID</param>
        /// <param name="x">要查找的模块的x坐标</param>
        /// <param name="y">要查找的模块的y坐标</param>
        /// <param name="maxFindLeng">搜索范围的最大距离</param>
        /// <returns>返回与指定ID及坐标最接近的 <see cref="BaseTileModule"/>，如果未找到则返回<see langword="null"/></returns>
        public static BaseTileModule FindModuleRangeSearch(int ID, int x, int y, int maxFindLeng) {
            BaseTileModule module = null;
            float findValue = maxFindLeng;
            // 遍历世界中的所有模块，查找与指定ID匹配并且距离最近的模块
            foreach (var inds in TileModuleInWorld) {
                if (inds.ModuleID != ID) {
                    continue;
                }
                // 计算当前模块与指定坐标之间的距离
                float value = inds.PosInWorld.To(new Vector2(x, y) * 16).Length();
                if (value > findValue) {
                    continue;
                }
                // 更新最接近的模块及其距离
                module = inds;
                findValue = value;
            }
            return module;
        }

        public override void PlaceInWorld(int i, int j, int type, Item item) {
            if (CWRUtils.SafeGetTopLeft(i, j, out Point16 point)) {
                AddInWorld(type, point, item);
                $"即将开始同步 TileModuleInWorld最大值为{TileModuleInWorld.Count}".Domp();
                if (CWRUtils.isClient) {
                    NetInstance.NetSend(Main.myPlayer, type, point);
                    TMEInWorldNetWork.NetInstance.NetSend();
                }
            }
        }

        void INetWork.NetSendBehavior(ModPacket netMessage, params object[] args) {
            netMessage.Write((byte)CWRMessageType.NetWorks);
            netMessage.Write(((INetWork)this).messageID);
            netMessage.Write((int)args[0]);
            netMessage.Write((int)args[1]);
            netMessage.WritePoint16((Point16)args[2]);

            bool isSvr = false;
            if (args.Length >= 4) {
                isSvr = (bool)args[3];
            }
            netMessage.Write(isSvr);

            if (CWRUtils.isClient && !isSvr) {
                netMessage.Send();
            }
            else if (CWRUtils.isServer) {
                netMessage.Send(-1, (int)args[0]);
            }
        }

        void INetWork.NetReceive(Mod mod, BinaryReader reader, int whoAmI) {
            int playerIndex = reader.ReadInt32();
            int type = reader.ReadInt32();
            Point16 point16 = reader.ReadPoint16();
            AddInWorld(type, point16, null);
            if (CWRUtils.isServer) {
                NetInstance.NetSend(playerIndex, type, point16, true);
            }
            $"同步完成 TileModuleInWorld最大值为{TileModuleInWorld.Count}".Domp();
        }
    }
}
