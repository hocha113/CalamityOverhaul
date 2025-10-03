using System.Collections.Generic;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace CalamityOverhaul.OtherMods.Coralite
{
    public struct MagikeContainerData
    {
        /// <summary> 当前内部的魔能量 </summary>
        public int Magike;
        /// <summary> 当前的魔能上限 </summary>
        public int MagikeMax;
        /// <summary> 自身魔能基础容量，可以通过升级来变化 </summary>
        public int MagikeMaxBase;
        /// <summary> 额外魔能量，通过扩展膜附加的魔能容量 </summary>
        public float MagikeMaxBonus;

        public readonly override string ToString() => $"Name:{GetType().Name} " +
            $"\nMagike:{Magike} " +
            $"\nMagikeMax:{MagikeMax}" +
            $"\nMagikeMaxBase:{MagikeMaxBase}" +
            $"\nMagikeMaxBonus:{MagikeMaxBonus}";
    }

    internal class MagikeCrossed
    {
        public static bool HasMod => ModLoader.HasMod("Coralite");

        public static MagikeContainerData GetData(Point16 point) {
            List<object> list = (List<object>)CWRMod.Instance.coralite.Call("MagikeTP:GetMagikeContainerData", point);
            MagikeContainerData magikeContainerData = new MagikeContainerData();
            magikeContainerData.Magike = (int)list[0];
            magikeContainerData.MagikeMax = (int)list[1];
            magikeContainerData.MagikeMaxBase = (int)list[2];
            magikeContainerData.MagikeMaxBonus = (float)list[3];
            return magikeContainerData;
        }

        public static bool AddMagike(Point16 point, int value) => (bool)CWRMod.Instance.coralite.Call("MagikeTP:AddMagike", point, value);

        public static bool ReduceMagike(Point16 point, int value) => (bool)CWRMod.Instance.coralite.Call("MagikeTP:ReduceMagike", point, value);

        public static bool IsMagikeContainer(Point16 point) => (bool)CWRMod.Instance.coralite.Call("MagikeTP:IsMagikeContainer", point);
    }
}
