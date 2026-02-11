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
            if (!ModLoader.TryGetMod("CalamityMod", out var calamity)) {
                return;
            }
            Type teBaseTurretType = calamity.Code.GetType("CalamityMod.TileEntities.TEBaseTurret");
            if (teBaseTurretType is null) {
                return;
            }
            IList<Type> turretTypes = GetDerivedTypes(teBaseTurretType);
            if (turretTypes is null) {
                return;
            }
            foreach (var type in turretTypes) {
                MethodInfo info = type.GetMethod("UpdateClient", BindingFlags.Public | BindingFlags.Instance);
                if (info != null)
                    VaultHook.Add(info, OnUpdateHook);
                info = type.GetMethod("Update", BindingFlags.Public | BindingFlags.Instance);
                if (info != null)
                    VaultHook.Add(info, OnUpdateHook);
            }
        }

        private static IList<Type> GetDerivedTypes(Type baseType) {
            IList<Type> types = [];

            Type[] allTypes = VaultUtils.GetAnyModCodeType();

            foreach (Type type in allTypes) {
                //核心筛选逻辑:
                //类型必须是类 (IsClass)
                //类型不能是抽象类 (!IsAbstract)
                //类型必须可以赋值给 TBase (IsAssignableFrom)，这同时适用于类继承和接口实现
                //类型不能是基类型本身 (type != baseType)，避免自己实例化自己
                if (!type.IsClass || type.IsAbstract || !baseType.IsAssignableFrom(type) || type == baseType) {
                    continue;
                }

                types.Add(type);
            }

            return types;
        }

        void ICWRLoader.LoadAsset() {
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
