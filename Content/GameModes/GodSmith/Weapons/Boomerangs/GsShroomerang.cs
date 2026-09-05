using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Boomerangs
{
    /// <summary>
    /// 蘑菇镖重铸。材质：发光菌盖弯镖。签名行为：①悬停期喷出一团滞留孢子云，持续灼蚀云内敌人
    /// ②命中软质噗声
    /// </summary>
    internal class GsShroomerang : GsBoomerScheme
    {
        public override int TargetItemID => ItemID.Shroomerang;

        internal override int BoomerProjType => ModContent.ProjectileType<GsShroomerangProj>();

        internal override float DamageMul => 1.0f;

        protected override string GsDescFallback =>
            "At the hover peak it puffs out a lingering spore cloud that keeps burning foes inside\nThe flight path rains glowing spores";
    }

    /// <summary>菌盖镖体：悬停喷孢子云</summary>
    internal class GsShroomerangProj : GsBoomerProjBase
    {
        internal override int SourceItemID => ItemID.Shroomerang;

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
    }

    /// <summary>滞留孢子云：低频持续判定；用原版泡泡贴图按判定箱画一笔作范围提示</summary>
    internal class GsShroomerangSporeCloudProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.Bubble;

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
        }

        /// <summary>范围提示：原版泡泡贴图按判定箱缩放画一笔，随寿命淡出</summary>
        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            float life = Projectile.timeLeft / (float)LifeTime;
            float fade = MathF.Min(1f, MathF.Min((1f - life) * 6f, life * 3f));
            float scale = Projectile.width / MathF.Max(tex.Width, 1);
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, lightColor * fade,
                0f, tex.Size() / 2f, scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
