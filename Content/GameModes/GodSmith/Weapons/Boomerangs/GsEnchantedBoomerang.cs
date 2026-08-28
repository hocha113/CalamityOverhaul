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
    /// 附魔回旋镖重铸。材质：秘法附魔的蓝钢镖身。签名行为：①悬停蓄势顶点迸出三枚追敌星辉
    /// ②回程拖出星尘余痕，粒子寿命长过镖体 ③命中星屑迸溅与清脆魔音
    /// </summary>
    internal class GsEnchantedBoomerang : GsBoomerScheme
    {
        public override int TargetItemID => ItemID.EnchantedBoomerang;

        internal override int BoomerProjType => ModContent.ProjectileType<GsEnchantedBoomerangProj>();

        internal override float DamageMul => 1.05f;

        protected override string GsDescFallback =>
            "Decelerates outbound, gathers starlight while hovering, accelerates home\n" +
            "At the hover peak it flings three homing star sparks, each dealing 30% damage\n" +
            "Right click while it flies: command it to dash toward your cursor";
    }

    /// <summary>附魔镖体：悬停顶点放星，回程星尘余痕</summary>
    internal class GsEnchantedBoomerangProj : GsBoomerProjBase
    {
        internal override int SourceItemID => ItemID.EnchantedBoomerang;

        protected override Color GlowColor => new(110, 170, 255);

        protected override Color TrailColor => new(150, 190, 255);

        /// <summary>星辉金，星辉弹与顶点闪光用</summary>
        internal static readonly Color StarGold = new(255, 228, 140);

        protected override int HoverTime => 22;

        protected override void OnEnterPhase(int phase, Player owner) {
            if (phase != PhaseHover) {
                return;
            }
            //悬停顶点：owner 端迸出三枚追星（30% 伤害随弹幕自带过线）
            if (Projectile.IsOwnedByLocalPlayer()) {
                int dmg = Math.Max(1, (int)(Projectile.damage * 0.30f));
                for (int i = 0; i < 3; i++) {
                    Vector2 vel = (Projectile.rotation + (MathHelper.TwoPi / 3f * i)).ToRotationVector2() * 5f;
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, vel,
                        ModContent.ProjectileType<GsEnchantedBoomerangStarProj>(), dmg, 0.5f, owner.whoAmI);
                }
            }
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item9 with { Volume = 0.6f, Pitch = 0.3f }, Projectile.Center);
                PRTLoader.NewParticle<PRT_Light>(Projectile.Center, Vector2.Zero, StarGold, 0.5f)?.Configure(12, 0.9f);
            }
        }

        protected override void FlightFX(Player owner) {
            base.FlightFX(owner);
            //回程星尘余痕：闪尘寿命 30 帧，镖回手后仍在空中残留
            if (Phase == PhaseReturn && PhaseTimer % 3 == 0) {
                PRTLoader.NewParticle<PRT_Sparkle>(
                    Projectile.Center + Main.rand.NextVector2Circular(8f, 8f),
                    -Projectile.velocity * 0.03f, StarGold, 0.4f)
                    ?.Configure(StarGold * 0.5f, 30, 0.1f, 0.8f);
            }
        }

        protected override void HitBurstFX(NPC target, NPC.HitInfo hit) {
            base.HitBurstFX(target, hit);
            //星屑迸溅：金蓝双色
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_Sparkle>(target.Center,
                    Main.rand.NextVector2Circular(3f, 3f), StarGold, 0.45f)
                    ?.Configure(GlowColor * 0.6f, 22, 0.15f);
            }
        }
    }

    /// <summary>追敌星辉：短寿命追踪星屑，四芒星自绘 + 闪尘尾迹</summary>
    internal class GsEnchantedBoomerangStarProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private NPC HomingTarget {
            get => Projectile.ai[0] > 0f ? Main.npc[(int)Projectile.ai[0] - 1] : null;
            set => Projectile.ai[0] = value == null ? 0f : value.whoAmI + 1;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 12;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 80;
        }

        public override void AI() {
            Projectile.rotation += 0.3f;

            NPC target = HomingTarget;
            if (target == null || !target.active || target.dontTakeDamage) {
                //各端同参搜索最近可打目标，结果确定性一致
                target = Projectile.FindTargetWithinRange(520f);
                HomingTarget = target;
            }
            if (target != null) {
                Vector2 desired = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitY) * 11f;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.09f);
            }
            else {
                Projectile.velocity *= 0.97f;
            }

            Lighting.AddLight(Projectile.Center, GsEnchantedBoomerangProj.StarGold.ToVector3() * 0.3f);
            if (!VaultUtils.isServer && Projectile.timeLeft % 4 == 0) {
                PRTLoader.NewParticle<PRT_Light>(Projectile.Center, Vector2.Zero,
                    GsEnchantedBoomerangProj.StarGold, 0.12f)?.Configure(10, 0.6f);
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.35f, Pitch = 0.5f }, target.Center);
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_Spark>(target.Center,
                    Main.rand.NextVector2Circular(3f, 3f), GsEnchantedBoomerangProj.StarGold,
                    Main.rand.NextFloat(0.3f, 0.45f))?.Configure(true, Main.rand.Next(10, 15));
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D star = CWRAsset.StarTexture_White?.Value;
            if (star == null) {
                return false;
            }
            Vector2 pos = Projectile.Center - Main.screenPosition;
            //外层金芒 + 内层白核，闪烁用 whoAmI 种子
            float tw = 0.85f + (0.15f * MathF.Sin((Main.GlobalTimeWrappedHourly * 9f) + Projectile.whoAmI));
            Color outer = GsEnchantedBoomerangProj.StarGold * (0.7f * tw);
            outer.A = 0;
            Main.spriteBatch.Draw(star, pos, null, outer, Projectile.rotation,
                star.Size() / 2f, 0.09f * tw, SpriteEffects.None, 0);
            Color core = Color.White * 0.85f;
            core.A = 0;
            Main.spriteBatch.Draw(star, pos, null, core, -Projectile.rotation * 0.7f,
                star.Size() / 2f, 0.045f, SpriteEffects.None, 0);
            return false;
        }
    }
}
