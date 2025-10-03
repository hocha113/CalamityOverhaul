using System;
using System.Diagnostics;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.OtherMods.ImproveGame;

public record Ammo(ItemTypeData ItemData, int Times) : TagSerializable
{
    public int Times = Times;
    public ItemTypeData ItemData = ItemData;

    // tML反射获取叫这个的Field，获取不到就报错，不能删啊
    public static Func<TagCompound, Ammo> DESERIALIZER = s => DeserializeAmmo(s);

    public static Ammo DeserializeAmmo(TagCompound tag) =>
        new(tag.Get<ItemTypeData>("item"), tag.GetInt("times"));

    public TagCompound SerializeData() => throw new UnreachableException("This method should never be called");
}