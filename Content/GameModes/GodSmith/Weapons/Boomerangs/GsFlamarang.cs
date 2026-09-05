using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Boomerangs
{
    /// <summary>
    /// 烈焰回旋镖重铸。材质：狱岩燃刃。签名行为：全程点燃命中目标，悬停顶点有爆燃声
    /// </summary>
    internal class GsFlamarang : GsBoomerScheme
    {
        public override int TargetItemID => ItemID.Flamarang;

        internal override int BoomerProjType => ModContent.ProjectileType<GsFlamarangProj>();

        internal override float DamageMul => 1.0f;

        protected override string GsDescFallback =>
            "Every hit sets the target on fire +";
    }

    /// <summary>燃刃镖体：引燃弧线</summary>
    internal class GsFlamarangProj : GsBoomerProjBase
    {
        internal override int SourceItemID => ItemID.Flamarang;

        protected override int HoverTime => 20;

        protected override SoundStyle HitSound => SoundID.Item20 with { Volume = 0.4f, Pitch = 0.2f };

        protected override void OnHitEffects(NPC target, NPC.HitInfo hit, int damageDone)
            => target.AddBuff(BuffID.OnFire, 240);

        protected override void OnEnterPhase(int phase, Player owner) {
            if (phase != PhaseHover || VaultUtils.isServer) {
                return;
            }
            //悬停顶点爆燃声
            SoundEngine.PlaySound(SoundID.Item20 with { Volume = 0.7f, Pitch = -0.1f }, Projectile.Center);
        }
    }
}
