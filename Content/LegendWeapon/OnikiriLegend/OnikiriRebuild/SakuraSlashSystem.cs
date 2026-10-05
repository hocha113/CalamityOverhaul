using System.Collections.Generic;
using System.Text;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OnikiriRebuild.Geometry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OnikiriRebuild;

internal sealed class SakuraSlashSystem : ModSystem
{
    private const string BasePath = "Content/LegendWeapon/OnikiriLegend/OnikiriRebuild/";
    private readonly List<SakuraSlashInstance> active = new();
    private RibbonDefinition definition;
    private RibbonDefinition definition2;
    private SakuraSlashHitArc hitArc;
    private SakuraSlashHitArc hitArc2;
    private SakuraSlashPetalBurst petalBurst;
    private SakuraSlashPetalBurst petalBurst2;
    private SakuraWeaponAnimation animation;
    private SakuraWeaponAnimation animation2;
    private Texture2D trail, noise, blade;
    private Texture2D weapon;
    private Effect shader;
    private SakuraSlashRenderer renderer;
    private SakuraSlashRenderer renderer2;

    internal SakuraWeaponAnimation GetAnimation(bool second) => second ? animation2 : animation;
    internal SakuraSlashHitArc GetHitArc(bool second) => second ? hitArc2 : hitArc;
    internal Texture2D WeaponTexture => weapon;

    public override void Load()
    {
        definition = RibbonDefinition.Parse(Encoding.UTF8.GetString(Mod.GetFileBytes(BasePath + "SakuraSlash.json")));
        definition2 = RibbonDefinition.Parse(Encoding.UTF8.GetString(Mod.GetFileBytes(BasePath + "SakuraSlash2.json")));
        hitArc = new SakuraSlashHitArc(definition);
        hitArc2 = new SakuraSlashHitArc(definition2);
        petalBurst = new SakuraSlashPetalBurst(definition);
        petalBurst2 = new SakuraSlashPetalBurst(definition2);
        animation = SakuraWeaponAnimation.Parse(Encoding.UTF8.GetString(Mod.GetFileBytes(BasePath + "SakuraSlash.weapon.anim.json")));
        animation2 = SakuraWeaponAnimation.Parse(Encoding.UTF8.GetString(Mod.GetFileBytes(BasePath + "SakuraSlash2.weapon.anim.json")));
        if (Main.dedServ) return;
        trail = Mod.Assets.Request<Texture2D>(BasePath + "Eff_Trail_63_03", AssetRequestMode.ImmediateLoad).Value;
        blade = Mod.Assets.Request<Texture2D>(BasePath + "Eff_Trail_63_03_Blade", AssetRequestMode.ImmediateLoad).Value;
        noise = Mod.Assets.Request<Texture2D>(BasePath + "PerlinX", AssetRequestMode.ImmediateLoad).Value;
        weapon = Mod.Assets.Request<Texture2D>(BasePath + "OnikiriItem", AssetRequestMode.ImmediateLoad).Value;
        shader = Mod.Assets.Request<Effect>(BasePath + "SakuraSlash", AssetRequestMode.ImmediateLoad).Value;
    }

    internal static SakuraSlashInstance Spawn(Vector2 position, Matrix transform, float lifetime, bool second)
    {
        if (Main.dedServ) return null;
        var system = ModContent.GetInstance<SakuraSlashSystem>();
        var slash = new SakuraSlashInstance(position, transform, lifetime,
            second ? system.petalBurst2 : system.petalBurst);
        system.active.Add(slash);
        return slash;
    }

    public override void PostUpdateDusts()
    {
        if (Main.dedServ || Main.gamePaused) return;
        for (int i = active.Count - 1; i >= 0; i--)
        {
            active[i].Advance(1f / 60f);
            if (active[i].TakePetalEmission(out int firstIndex, out int count))
                active[i].PetalBurst?.Emit(active[i], firstIndex, count, SakuraParticleSystem.EmitBurst);
            if (!active[i].IsActive) active.RemoveAt(i);
        }
    }

    internal void Draw(SakuraSlashInstance slash, bool second)
    {
        SakuraSlashRenderer selected = second ? renderer2 : renderer;
        if (selected == null)
        {
            selected = new SakuraSlashRenderer(Main.instance.GraphicsDevice,
                second ? definition2 : definition, trail, shader, noise, blade);
            SakuraSlashStyle.Apply(selected);
            foreach (string warning in selected.Warnings) Mod.Logger.Warn(warning);
            if (second) renderer2 = selected;
            else renderer = selected;
        }
        selected.Draw(slash, Main.screenPosition, Main.GameViewMatrix.TransformationMatrix);
    }

    public override void OnWorldUnload()
    {
        foreach (var slash in active) slash.Kill();
        active.Clear();
    }

    public override void Unload()
    {
        OnWorldUnload();
        var oldRenderer = renderer;
        var oldRenderer2 = renderer2;
        renderer = null;
        renderer2 = null;
        if (oldRenderer != null) Main.QueueMainThreadAction(oldRenderer.Dispose);
        if (oldRenderer2 != null) Main.QueueMainThreadAction(oldRenderer2.Dispose);
        definition = null;
        definition2 = null;
        hitArc = null;
        hitArc2 = null;
        petalBurst = null;
        petalBurst2 = null;
        animation = null;
        animation2 = null;
        trail = noise = blade = null;
        weapon = null;
        shader = null;
    }
}
