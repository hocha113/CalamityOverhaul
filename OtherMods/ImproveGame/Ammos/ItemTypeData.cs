using System;
using System.Diagnostics;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.OtherMods.ImproveGame.Ammos;

public class ItemTypeData(Item item) : TagSerializable
{
    //tML反射获取叫这个的Field，获取不到就报错，不能删啊
    public static readonly Func<TagCompound, ItemTypeData> DESERIALIZER = Load;

    internal readonly Item Item = item;

    public TagCompound SerializeData() => throw new UnreachableException("This method should never be called");

    public static ItemTypeData Load(TagCompound tag) {
        var item = new Item();
        string modName = tag.GetString("mod");
        if (string.IsNullOrEmpty(modName)) {
            item.netDefaults(0);
            return new ItemTypeData(item);
        }

        if (modName == "Terraria") {
            item.netDefaults(tag.GetInt("id"));
        }
        else {
            var itemName = tag.GetString("name");
            if (string.IsNullOrEmpty(itemName)) {
                item.netDefaults(0);
                return new ItemTypeData(item);
            }

            if (ModContent.TryFind(modName, itemName, out ModItem modItem)) {
                item.SetDefaults(modItem.Type);
            }
        }

        return new ItemTypeData(item);
    }

    public override int GetHashCode() => Item.GetHashCode();
}