using System.Collections.Generic;
using Terraria.ID;

namespace CalamityOverhaul.Content.Generator
{
    internal class FuelItems
    {
        public static readonly Dictionary<int, int> FuelItemToCombustion = new Dictionary<int, int>() {
            { ItemID.Wood, 50 },
            { ItemID.Coal, 250 },
            { ItemID.Hay, 50 },
        };
    }
}
