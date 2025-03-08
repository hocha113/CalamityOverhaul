using CalamityMod.TileEntities;
using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace CalamityOverhaul.Content.Industrials.Modifys.ModifyTurrets
{
    internal class ModifyTurretLoader : ICWRLoader
    {
        public delegate void UpdateDelegate(TEBaseTurret turret);
        public static Asset<Texture2D> TurretBase { get; set; }
        public static Dictionary<int, Asset<Texture2D>> BodyAssetDic { get; set; } = [];
        public static Dictionary<int, Asset<Texture2D>> BodyGlowAssetDic { get; set; } = [];
        public static Dictionary<int, Asset<Texture2D>> BarrelAssetDic { get; set; } = [];
        public static Dictionary<int, Asset<Texture2D>> BarrelGlowAssetDic { get; set; } = [];
        void ICWRLoader.LoadData() {
            List<Type> turretTypes = VaultUtils.GetSubclassTypeList(typeof(TEBaseTurret));
            foreach (var type in turretTypes) {
                MethodInfo info = type.GetMethod("UpdateClient", BindingFlags.Public | BindingFlags.Instance);
                CWRHook.Add(info, OnUpdateHook);
                info = type.GetMethod("Update", BindingFlags.Public | BindingFlags.Instance);
                CWRHook.Add(info, OnUpdateHook);
            }
        }

        void ICWRLoader.LoadAsset() {
            TurretBase = CWRUtils.GetT2DAsset(CWRConstant.Turrets + "TurretBase");

            List<BaseTurretTP> baseTurretTPs = VaultUtils.GetSubclassInstances<BaseTurretTP>();
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
            }
        }

        void ICWRLoader.UnLoadData() {
            TurretBase = null;
            BodyAssetDic?.Clear();
            BodyGlowAssetDic?.Clear();
            BarrelAssetDic?.Clear();
            BarrelGlowAssetDic?.Clear();
        }

        private static void OnUpdateHook(UpdateDelegate orig, TEBaseTurret turret) {
            //在更新时杀死所有炮塔而不运行任何逻辑，这样才能让自定义的TP实体发挥正常作用，否者这些炮塔会重叠
            turret.Kill(turret.Position.X, turret.Position.Y);
        }
    }
}
