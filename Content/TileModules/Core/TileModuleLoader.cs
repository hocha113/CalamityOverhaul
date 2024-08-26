using CalamityOverhaul.Common;
using System;
using System.Collections.Generic;
using System.Reflection;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.TileModules.Core
{
    internal class TileModuleLoader : GlobalTile, ILoader
    {
        public static Dictionary<Type, int> TileModuleID { get; private set; } = [];
        public static Dictionary<Type, BaseTileModule> TileModuleDict { get; private set; } = [];
        public static Dictionary<int, BaseTileModule> TileModuleFormeTileID { get; private set; } = [];
        public static List<BaseTileModule> TileModulesList { get; private set; } = [];
        public static List<BaseTileModule> TileModuleInWorld { get; private set; } = [];

        internal delegate void On_Tile_KillMultiTile_Dalegate(int i, int j, int frameX, int frameY, int type);
        public static Type tileLoaderType;
        public static MethodBase onTile_KillMultiTile_Method;

        void ILoader.LoadData() {
            TileModulesList = CWRUtils.HanderSubclass<BaseTileModule>();
            for (int i = 0; i < TileModulesList.Count; i++) {
                BaseTileModule module = TileModulesList[i];
                module.Load();
                TileModuleID.Add(module.GetType(), i);
                TileModuleDict.Add(module.GetType(), module);
                TileModuleFormeTileID.Add(module.TargetTileID, module);
            }

            tileLoaderType = typeof(TileLoader);
            onTile_KillMultiTile_Method = tileLoaderType
                .GetMethod("KillMultiTile", BindingFlags.Public | BindingFlags.Static);
            CWRHook.Add(onTile_KillMultiTile_Method, OnKillMultiTileHook);
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
            TileModuleDict.Clear();
            TileModuleID.Clear();
            tileLoaderType = null;
            onTile_KillMultiTile_Method = null;
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

        internal static void LoadWorldTileModule() {
            foreach (BaseTileModule module in TileModuleInWorld) {
                if (!module.Active) {
                    continue;
                }
                module.Kill();
            }

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
        }

        internal static void AddInWorld(int tileID, Point16 position, Item item) {
            if (TileModuleFormeTileID.TryGetValue(tileID, out BaseTileModule module)) {
                BaseTileModule newModule = module.Clone();
                newModule.Position = position;
                newModule.TrackItem = item;
                newModule.Active = true;

                foreach (var inds in TileModulesList) {
                    if (inds.IsDaed()) {
                        inds.Active = false;
                    }
                }

                bool onAdd = true;

                for (int i = 0; i < TileModuleInWorld.Count; i++) {
                    if (!TileModuleInWorld[i].Active) {
                        TileModuleInWorld[i] = newModule;
                        newModule.WhoAmI = i;
                        onAdd = false;
                        break;
                    }
                }

                if (onAdd) {
                    newModule.WhoAmI = TileModuleInWorld.Count;
                    TileModuleInWorld.Add(newModule);
                }
            }
        }

        public static int GetModuleID(Type type) => TileModuleID[type];

        public static T FindModulePreciseSearch<T>(int ID, int x, int y)
            where T : BaseTileModule => FindModulePreciseSearch(ID, x, y) as T;
        public static BaseTileModule FindModulePreciseSearch(int ID, int x, int y) {
            BaseTileModule module = null;
            foreach (var inds in TileModuleInWorld) {
                if (inds.Position.X == x && inds.Position.Y == y && inds.ModuleID == ID) {
                    module = inds;
                    break;
                }
            }
            return module;
        }

        public static T FindModuleRangeSearch<T>(int ID, int x, int y, int maxFindLeng) 
            where T : BaseTileModule => FindModuleRangeSearch(ID, x, y, maxFindLeng) as T;
        public static BaseTileModule FindModuleRangeSearch(int ID, int x, int y, int maxFindLeng) {
            BaseTileModule module = null;
            float findValue = maxFindLeng;
            foreach (var inds in TileModuleInWorld) {
                if (inds.ModuleID != ID) {
                    continue;
                }

                float value = inds.PosInWorld.To(new Vector2(x, y) * 16).Length();
                if (value > findValue) {
                    continue;
                }

                module = inds;
                findValue = value;
            }
            return module;
        }

        public override void PlaceInWorld(int i, int j, int type, Item item) {
            AddInWorld(type, new Point16(i, j), item);
        }
    }
}
