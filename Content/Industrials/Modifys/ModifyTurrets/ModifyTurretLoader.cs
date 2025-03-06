using CalamityMod.TileEntities;
using CalamityOverhaul.Common;
using System;
using System.Collections.Generic;
using System.Reflection;
using Terraria.DataStructures;

namespace CalamityOverhaul.Content.Industrials.Modifys.ModifyTurrets
{
    internal class ModifyTurretLoader : ICWRLoader
    {
        public delegate void UpdateDelegate(TEBaseTurret turret);
        void ICWRLoader.LoadData() {
            List<Type> turretTypes = VaultUtils.GetSubclassTypeList(typeof(TEBaseTurret));
            foreach (var type in turretTypes) {
                MethodInfo info = type.GetMethod("UpdateClient", BindingFlags.Public | BindingFlags.Instance);
                CWRHook.Add(info, OnUpdateHook);
                info = type.GetMethod("Update", BindingFlags.Public | BindingFlags.Instance);
                CWRHook.Add(info, OnUpdateHook);
            }
        }

        private static void OnUpdateHook(UpdateDelegate orig, TEBaseTurret turret) {
            //在更新时杀死所有炮塔而不运行任何逻辑，这样才能让自定义的TP实体发挥正常作用，否者这些炮塔会重叠
            turret.Kill(turret.Position.X, turret.Position.Y);
        }
    }
}
