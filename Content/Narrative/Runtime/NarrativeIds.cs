using InnoVault.Narrative.Core;

namespace CalamityOverhaul.Content.Narrative.Runtime
{
    /// <summary>
    /// 宿主注册用的完整 id（<c>ModName/Name</c>）。<br/>
    /// 场景 <see cref="InnoVault.Narrative.Composition.NarrativeComposer"/> 构建期可写短名（如 <c>"Helen"</c>），框架会按所属 Mod 自动补全
    /// </summary>
    internal static class NarrativeIds
    {
        internal const string ModName = "CalamityOverhaul";

        internal static readonly StyleId Default = StyleId.ForMod(ModName, "Default");
        internal static readonly StyleId Sea = StyleId.ForMod(ModName, "Sea");
        internal static readonly StyleId Brimstone = StyleId.ForMod(ModName, "Brimstone");
        internal static readonly StyleId Draedon = StyleId.ForMod(ModName, "Draedon");
        internal static readonly StyleId Sulfsea = StyleId.ForMod(ModName, "Sulfsea");
        internal static readonly StyleId StarStream = StyleId.ForMod(ModName, "StarStream");
        internal static readonly StyleId SHPC = StyleId.ForMod(ModName, "SHPC");

        internal static readonly CharacterId OldDuke = CharacterId.ForMod(ModName, "OldDuke");
        internal static readonly CharacterId Helen = CharacterId.ForMod(ModName, "Helen");
        internal static readonly CharacterId HelenUnknown = CharacterId.ForMod(ModName, "HelenUnknown");
        internal static readonly CharacterId DraedonSpeaker = CharacterId.ForMod(ModName, "Draedon");
        internal static readonly CharacterId System = CharacterId.ForMod(ModName, "System");
        internal static readonly CharacterId SupCalUnknown = CharacterId.ForMod(ModName, "SupCalUnknown");
        internal static readonly CharacterId SupCal = CharacterId.ForMod(ModName, "SupCal");
        internal static readonly CharacterId SupCalShadow = CharacterId.ForMod(ModName, "SupCalShadow");
        internal static readonly CharacterId Shepel = CharacterId.ForMod(ModName, "Shepel");

        internal static readonly ExpressionId Doubt = new("Doubt");
        internal static readonly ExpressionId Serious = new("Serious");
        internal static readonly ExpressionId Enjoy = new("Enjoy");
        internal static readonly ExpressionId Solemn = new("Solemn");
        internal static readonly ExpressionId Amazed = new("Amazed");
        internal static readonly ExpressionId Wrath = new("Wrath");
        internal static readonly ExpressionId Silence = new("Silence");
        internal static readonly ExpressionId SlightAnnoyed = new("SlightAnnoyed");
        internal static readonly ExpressionId CloseEye = new("CloseEye");
        internal static readonly ExpressionId BeTo = new("BeTo");
        internal static readonly ExpressionId Despise = new("Despise");
        internal static readonly ExpressionId Shock = new("Shock");
        internal static readonly ExpressionId Smile = new("Smile");
        internal static readonly ExpressionId Sigh = new("Sigh");
        internal static readonly ExpressionId Red = new("Red");
        internal static readonly ExpressionId Alt = new("Alt");
        internal static readonly ExpressionId Naughty = new("Naughty");
        internal static readonly ExpressionId Naughty2 = new("Naughty2");
        internal static readonly ExpressionId Enjoy2 = new("Enjoy2");
        internal static readonly ExpressionId Enjoy3 = new("Enjoy3");
        internal static readonly ExpressionId Stern = new("Stern");
        internal static readonly ExpressionId Serious2 = new("Serious2");
    }
}
