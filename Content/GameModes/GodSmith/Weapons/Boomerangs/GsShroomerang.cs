using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Boomerangs
{
    /// <summary>
    /// 蘑菇镖重铸。材质：发光菌盖弯镖。签名行为：①悬停期喷出一团滞留孢子云，持续灼蚀云内敌人
    /// ②飞行路径撒落发光孢子，蓝辉余痕长过镖体 ③命中软质噗声与孢子四溅
    /// </summary>
    internal class GsShroomerang : GsBoomerScheme
    {
        public override int TargetItemID => ItemID.Shroomerang;

        internal override int BoomerProjType => ModContent.ProjectileType<GsShroomerangProj>();

        internal override float DamageMul => 1.0f;

        protected override string GsDescFallback =>
            "At the hover peak it puffs out a lingering spore cloud that keeps burning foes inside\n" +
            "The flight path rains glowing spores\n" +
            "Right click while it flies: command it to dash toward your cursor";
    }

    /// <summary>菌盖镖体：悬停喷孢子云</summary>
    internal class GsShroomerangProj : GsBoomerProjBase
    {
        internal override int SourceItemID => ItemID.Shroomerang;

        protected override Color GlowColor => new(96, 160, 255);

        protected override Color TrailColor => new(130, 200, 255);

        protected override int HoverTime => 20;

        protected override SoundStyle HitSound => SoundID.NPCHit1 with { Volume = 0.4f, Pitch = 0.5f };

        protected override void OnEnterPhase(int phase, Player owner) {
            if (phase != PhaseHover) {
                return;
            }
            //悬停顶点：owner 端放置滞留孢子云（18% 伤害持续判定）
            if (Projectile.IsOwnedByLocalPlayer()) {
                int dmg = Math.Max(1, (int)(Projectile.damage * 0.18f));
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, Vector2.Zero,
                    ModContent.ProjectileType<GsShroomerangSporeCloudProj>(), dmg, 0f, owner.whoAmI);
            }
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.45f, Pitch = 0.4f }, Projectile.Center);
            }
        }

        protected override void FlightFX(Player owner) {
            base.FlightFX(owner);
            //路径撒孢子：寿命长过镖体的发光孢子
            if (PhaseTimer % 5 == 0) {
                PRTLoader.NewParticle<PRT_FarmSpore>(
                    Projectile.Center + Main.rand.NextVector2Circular(6f, 6f),
                    new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), Main.rand.NextFloat(0.1f, 0.5f)),
                    TrailColor, Main.rand.NextFloat(0.5f, 0.8f))
                    ?.Configure(Main.rand.Next(40, 70), true);
            }
        }

        protected override void HitBurstFX(NPC target, NPC.HitInfo hit) {
            PRTLoader.NewParticle<PRT_Light>(target.Center, Vector2.Zero, GlowColor, 0.22f)?.Configure(10, 0.8f);
            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_FarmSpore>(target.Center,
                    Main.rand.NextVector2Circular(2.5f, 2.5f), TrailColor,
                    Main.rand.NextFloat(0.5f, 0.8f))?.Configure(Main.rand.Next(30, 50), true);
            }
        }
    }

    /// <summary>滞留孢子云：低频持续判定 + 软辉孢雾自绘</summary>
    internal class GsShroomerangSporeCloudProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int LifeTime = 100;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 84;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = LifeTime;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
        }

        public override void AI() {
            Projectile.velocity = Vector2.Zero;
            Lighting.AddLight(Projectile.Center, 0.15f, 0.25f, 0.5f);
            if (!VaultUtils.isServer && Projectile.timeLeft % 6 == 0) {
                PRTLoader.NewParticle<PRT_FarmSpore>(
                    Projectile.Center + Main.rand.NextVector2Circular(34f, 30f),
                    new Vector2(0f, Main.rand.NextFloat(-0.3f, 0.2f)),
                    new Color(130, 200, 255), Main.rand.NextFloat(0.5f, 0.9f))
                    ?.Configure(Main.rand.Next(30, 55), true);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) {
                return false;
            }
            //三团错拍呼吸的孢雾（whoAmI 种子，不掷 Main.rand）
            float life = Projectile.timeLeft / (float)LifeTime;
            float fade = MathF.Min(1f, MathF.Min((1f - life) * 6f, life * 3f));
            for (int i = 0; i < 3; i++) {
                float seed = (Projectile.whoAmI * 2.3f) + (i * 2.1f);
                Vector2 off = new(MathF.Sin(seed + (Main.GlobalTimeWrappedHourly * 1.6f)) * 18f,
                    MathF.Cos((seed * 1.7f) + (Main.GlobalTimeWrappedHourly * 1.2f)) * 14f);
                float pulse = 0.85f + (0.15f * MathF.Sin((Main.GlobalTimeWrappedHourly * 3f) + seed));
                Color c = new Color(96, 160, 255) * (0.28f * fade * pulse);
                c.A = 0;
                Main.spriteBatch.Draw(glow, Projectile.Center + off - Main.screenPosition, null, c,
                    0f, glow.Size() / 2f, 1.5f * pulse, SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
