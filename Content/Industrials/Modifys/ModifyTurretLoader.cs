using CalamityOverhaul.Content.Industrials.Modifys.ModifyTurrets;
using InnoVault.GameSystem;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Industrials.Modifys
{
    internal class ModifyTurretLoader : ICWRLoader
    {
        public static Asset<Texture2D> TurretBase { get; set; }
        public static Dictionary<int, Asset<Texture2D>> BodyAssetDic { get; set; } = [];
        public static Dictionary<int, Asset<Texture2D>> BodyGlowAssetDic { get; set; } = [];
        public static Dictionary<int, Asset<Texture2D>> BarrelAssetDic { get; set; } = [];
        public static Dictionary<int, Asset<Texture2D>> BarrelGlowAssetDic { get; set; } = [];
        void ICWRLoader.LoadData() {
            if (!CWRRef.Has) {
                return;
            }

            IList<Type> turretTypes = CWRRef.GetTEBaseTurretTypes();
            if (turretTypes is null) {
                return;
            }
            foreach (var type in turretTypes) {
                MethodInfo info = type.GetMethod("UpdateClient", BindingFlags.Public | BindingFlags.Instance);
                VaultHook.Add(info, OnUpdateHook);
                info = type.GetMethod("Update", BindingFlags.Public | BindingFlags.Instance);
                VaultHook.Add(info, OnUpdateHook);
            }
        }

        void ICWRLoader.LoadAsset() {
            if (!CWRRef.Has) {
                return;
            }

            TurretBase = CWRUtils.GetT2DAsset(CWRConstant.Turrets + "TurretBase");

            List<BaseTurretTP> baseTurretTPs = VaultUtils.GetDerivedInstances<BaseTurretTP>();
            foreach (var tp in baseTurretTPs) {
                if (tp.BodyPath != "") {
                    BodyAssetDic.Add(tp.ID, CWRUtils.GetT2DAsset(tp.BodyPath));
                }
                else {
                    BodyAssetDic.Add(tp.ID, null);
                }

                if (tp.BodyGlowPath != "") {
                    BodyGlowAssetDic.Add(tp.ID, CWRUtils.GetT2DAsset(tp.BodyGlowPath));
                }
                else {
                    BodyGlowAssetDic.Add(tp.ID, null);
                }

                if (tp.BarrelPath != "") {
                    BarrelAssetDic.Add(tp.ID, CWRUtils.GetT2DAsset(tp.BarrelPath));
                }
                else {
                    BarrelAssetDic.Add(tp.ID, null);
                }

                if (tp.BarrelGlowPath != "") {
                    BarrelGlowAssetDic.Add(tp.ID, CWRUtils.GetT2DAsset(tp.BarrelGlowPath));
                }
                else {
                    BarrelGlowAssetDic.Add(tp.ID, null);
                }

                TextureAssets.Tile[tp.TargetTileID] = TurretBase;
            }

            List<BaseTurretItem> baseTurretItem = VaultUtils.GetDerivedInstances<BaseTurretItem>();
            foreach (var item in baseTurretItem) {
                TextureAssets.Item[item.TargetID] = CWRUtils.GetT2DAsset(string.Concat(CWRConstant.Turrets, item.Name.AsSpan(6), "Item"));
            }
        }

        void ICWRLoader.UnLoadData() {
            TurretBase = null;
            BodyAssetDic?.Clear();
            BodyGlowAssetDic?.Clear();
            BarrelAssetDic?.Clear();
            BarrelGlowAssetDic?.Clear();
        }

        internal static void OnUpdateHook(Action<ModTileEntity> orig, ModTileEntity te) {
            //在更新时杀死所有炮塔而不运行任何逻辑，这样才能让自定义的TP实体发挥正常作用，否者这些炮塔会重叠
            KillTE(te);
            SendKillTE(te);
        }

        public static void KillTE(ModTileEntity te) => te.Kill(te.Position.X, te.Position.Y);

        public static void SendKillTE(ModTileEntity te) {
            if (VaultUtils.isSinglePlayer) {
                return;
            }

            ModPacket modPacket = CWRMod.Instance.GetPacket();
            modPacket.Write((byte)CWRMessageType.KillTileEntity);
            modPacket.WritePoint16(te.Position);
            modPacket.Send();
        }

        public static void HandlerNetKillTE(BinaryReader reader, int whoAmI) {
            Point16 point = reader.ReadPoint16();
            if (!TileEntity.ByPosition.TryGetValue(point, out TileEntity te)) {
                return;
            }
            KillTE((ModTileEntity)te);
            if (VaultUtils.isServer) {
                ModPacket modPacket = CWRMod.Instance.GetPacket();
                modPacket.Write((byte)CWRMessageType.KillTileEntity);
                modPacket.WritePoint16(te.Position);
                modPacket.Send(-1, whoAmI);
            }
        }
    }
}
