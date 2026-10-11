using System.Text;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OnikiriRebuild.Geometry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OnikiriRebuild;

internal sealed class SakuraDashSystem : ModSystem
{
    internal const string BasePath = "CalamityOverhaul/Content/LegendWeapon/OnikiriLegend/OnikiriRebuild/";
    private const string AssetPath = "Content/LegendWeapon/OnikiriLegend/OnikiriRebuild/";
    private RibbonDefinition definition;
    private Texture2D ring, wind, body, edge, noise, line, lineNoise, lineDark;
    private Effect slashEffect, lineEffect;
    private SakuraDashRenderer renderer;

    public override void Load()
    {
        if (Main.dedServ) return;
        definition = RibbonDefinition.Parse(Encoding.UTF8.GetString(Mod.GetFileBytes(AssetPath + "DashSlash.json")));
        ring = Texture("SakuraDashProj");
        wind = Texture("air_037_4r");
        body = Texture("DashSlash");
        edge = Texture("DashSlash_Edge");
        noise = Texture("PerlinX");
        line = Texture("ShotLine");
        lineNoise = Texture("voronoiOrigin");
        lineDark = Texture("Eff_Trail_518_02");
        slashEffect = Mod.Assets.Request<Effect>(AssetPath + "SakuraSlash", AssetRequestMode.ImmediateLoad).Value;
        lineEffect = Mod.Assets.Request<Effect>(AssetPath + "FuzzyEdge", AssetRequestMode.ImmediateLoad).Value;
    }

    private Texture2D Texture(string name) => Mod.Assets.Request<Texture2D>(AssetPath + name, AssetRequestMode.ImmediateLoad).Value;

    internal void Draw(Vector2 origin, float rotation, float age, float distance, Matrix worldToScreen, bool gravityInverted)
    {
        if (Main.dedServ) return;
        // 首次在绘制线程创建一次，不在弹幕/玩家上各自分配，也不逐帧读取任何资源。
        renderer ??= new SakuraDashRenderer(Main.instance.GraphicsDevice, definition, ring, wind,
            body, edge, noise, slashEffect, line, lineNoise, lineEffect, lineDark);
        renderer.Draw(origin, rotation, age, distance, worldToScreen, gravityInverted);
    }

    public override void Unload()
    {
        renderer?.Dispose();
        renderer = null;
        definition = null;
        ring = wind = body = edge = noise = line = lineNoise = lineDark = null;
        slashEffect = lineEffect = null;
    }
}
