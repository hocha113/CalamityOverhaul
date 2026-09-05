using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Flails
{
    /// <summary>
    /// 【连枷·肉球】肉球重铸：血肉缝合重锤。签名行为：①满转速一击命中爆出血浆冲击并挂流血
    /// ②慢充能大锤头，一击定音的重锤节奏
    /// </summary>
    internal class GsTheMeatball : GsFlailScheme
    {
        public override int TargetItemID => ItemID.TheMeatball;

        protected override int FlailProjType => ModContent.ProjectileType<GsTheMeatballHead>();

        protected override string GsDescFallback =>
            "Reforged: a fully charged strike bursts into a gout of gore\nThe burst wounds everything nearby and inflicts Bleeding";
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.12f;
    }

    /// <summary>
    /// 肉球锤头。慢充能大锤；满转命中在目标处引爆血浆冲击（owner 端生成）
    /// </summary>
    internal class GsTheMeatballHead : GsFlailHeadProj
    {
        public override int SourceItemID => ItemID.TheMeatball;
        public override int VanillaProjID => ProjectileID.TheMeatball;
        public override Asset<Texture2D> ChainTexture => TextureAssets.Chain13;

        public override int HeadSize => 34;
        public override float MaxChainLength => 330f;
        public override float LaunchSpeed => 15f;
        public override int ChargeFrames => 52;

        /// <summary>爆浆伤害系数</summary>
        private const float BurstDamageMul = 0.55f;

        protected override void OnHeadHit(NPC target, NPC.HitInfo hit, int damageDone, bool headHit) {
            if (!headHit || !Projectile.IsOwnedByLocalPlayer()) {
                return;
            }
            //满转爆浆：以目标为心的一跳血浆冲击
            if (LaunchCharge >= 0.99f && State == StateLaunch) {
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), target.Center, Vector2.Zero,
                    ModContent.ProjectileType<GsTheMeatballBurstProj>(),
                    Math.Max(1, (int)(Projectile.damage * BurstDamageMul)), 1f, Projectile.owner);
            }
        }
    }

    /// <summary>
    /// 血浆冲击：满转命中处的一跳小范围血爆（径约 90px），触者挂流血。
    /// 用原版血弹贴图按当前半径画一笔作范围提示
    /// </summary>
    internal class GsTheMeatballBurstProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.BloodShot;

        private const int LifeTicks = 22;
        private const int DamageWindow = 10;
        /// <summary>爆浆半径（径约 90px 按直径计）</summary>
        private const float MaxRadius = 46f;

        private float Age => LifeTicks - Projectile.timeLeft;

        /// <summary>血浪半径：前 7 帧猛涨后驻定，尾段随消散回缩</summary>
        private float RadiusNow {
            get {
                float grow = MathHelper.Clamp(Age / 7f, 0f, 1f);
                float fade = MathHelper.Clamp(Projectile.timeLeft / 8f, 0f, 1f);
                return MaxRadius * (1f - (1f - grow) * (1f - grow)) * (0.4f + 0.6f * fade);
            }
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;//一跳 AoE，同目标只结算一次
            Projectile.timeLeft = LifeTicks;
        }

        public override bool ShouldUpdatePosition() => false;

        public override bool? CanDamage() => Age <= DamageWindow ? null : false;

        public override void AI() {
            if (Age == 1 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.NPCDeath21 with { Volume = 0.5f, Pitch = -0.1f }, Projectile.Center);
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
            => target.AddBuff(BuffID.Bleeding, 180);

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float r = RadiusNow;
            if (r < 8f) {
                return false;
            }
            float nx = MathHelper.Clamp(Projectile.Center.X, targetHitbox.Left, targetHitbox.Right);
            float ny = MathHelper.Clamp(Projectile.Center.Y, targetHitbox.Top, targetHitbox.Bottom);
            return new Vector2(nx - Projectile.Center.X, ny - Projectile.Center.Y).LengthSquared() <= r * r;
        }

        /// <summary>范围提示：原版血弹贴图按当前半径缩放画一笔，随寿命淡出</summary>
        public override bool PreDraw(ref Color lightColor) {
            float r = RadiusNow;
            if (r < 6f) {
                return false;
            }
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            float fade = MathHelper.Clamp(Projectile.timeLeft / (float)LifeTicks, 0f, 1f);
            float scale = r * 2f / MathF.Max(tex.Width, 1);
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, lightColor * fade,
                0f, tex.Size() / 2f, scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
