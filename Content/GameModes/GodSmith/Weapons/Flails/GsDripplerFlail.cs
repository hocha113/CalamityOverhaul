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
    /// 【连枷·滴血链锤】滴血者血瘤锤：猩红血肉黑血渗液。签名行为：①高转速与掷出期沿途滴落带重力血珠
    /// ②血珠触敌或落地炸成小血刺爆 ③六帧血肉链节逐节轮播
    /// </summary>
    internal class GsDripplerFlail : GsFlailScheme
    {
        public override int TargetItemID => ItemID.DripplerFlail;

        protected override int FlailProjType => ModContent.ProjectileType<GsDripplerFlailHead>();

        protected override string GsDescFallback =>
            "Reforged: at high spin and through every throw, the head weeps gravity-bound blood beads\nBeads burst into stinging blood spikes on contact or landing";
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.08f;
    }

    /// <summary>
    /// 滴血链锤锤头。链贴图用 Extra[99] 六帧竖排血肉链，覆写 ChainFrame 逐节轮播（镜像原版对 757 的处理）；
    /// 血珠 owner 端生成、单场上限 10
    /// </summary>
    internal class GsDripplerFlailHead : GsFlailHeadProj
    {
        public override int SourceItemID => ItemID.DripplerFlail;
        public override int VanillaProjID => ProjectileID.DripplerFlail;
        public override Asset<Texture2D> ChainTexture => TextureAssets.Extra[99];

        /// <summary>滴珠间隔帧</summary>
        private const int DripInterval = 6;
        /// <summary>血珠伤害系数</summary>
        private const float BeadDamageMul = 0.35f;
        /// <summary>全场血珠上限</summary>
        private const int BeadCapTotal = 10;

        /// <summary>上一帧锤头位置，用于算移动速度（甩转期 velocity 恒为零）</summary>
        private Vector2 lastCenter;
        /// <summary>滴珠计时</summary>
        private int dripTimer;

        /// <summary>六帧竖排链逐节轮播，镜像原版 DrawProj_FlailChains 对 757 的处理</summary>
        public override Rectangle? ChainFrame(int linkIndex)
            => TextureAssets.Extra[99].Frame(1, 6, 0, linkIndex % 6);

        protected override void PostStateAI() {
            Vector2 moveDelta = lastCenter == Vector2.Zero ? Vector2.Zero : Projectile.Center - lastCenter;
            lastCenter = Projectile.Center;

            //充能 >0.5 的甩转期与整个掷出期滴珠；owner 端生成随包广播
            bool dripping = (State == StateSpin && spinCharge > 0.5f) || State == StateLaunch;
            if (!dripping || !Projectile.IsOwnedByLocalPlayer() || ++dripTimer < DripInterval) {
                return;
            }
            if (Owner.ownedProjectileCounts[ModContent.ProjectileType<GsDripplerFlailBeadProj>()] >= BeadCapTotal) {
                return;
            }
            dripTimer = 0;
            //初速=锤头速度×0.2+微下坠
            Vector2 vel = moveDelta * 0.2f + new Vector2(0f, 0.6f);
            Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, vel,
                ModContent.ProjectileType<GsDripplerFlailBeadProj>(),
                Math.Max(1, (int)(Projectile.damage * BeadDamageMul)), 0.8f, Projectile.owner);
        }
    }

    /// <summary>
    /// 血珠：带重力坠落（0.3/帧），触敌或落地炸成小血刺爆；
    /// 原版血弹贴图一笔，爆裂窗按判定箱放大作范围提示
    /// </summary>
    internal class GsDripplerFlailBeadProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.BloodShot;

        private const int LifeFrames = 90;
        /// <summary>爆裂窗帧数</summary>
        private const int BurstFrames = 10;

        /// <summary>ai[0]=1 进入爆裂态（owner 触发 + netUpdate 过线）；ai[1]=爆裂计时</summary>
        private bool Bursting => Projectile.ai[0] >= 1f;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 30;
            Projectile.timeLeft = LifeFrames;
        }

        public override void AI() {
            if (Bursting) {
                Projectile.velocity = Vector2.Zero;
                if (++Projectile.ai[1] >= BurstFrames) {
                    Projectile.Kill();
                }
                return;
            }
            //坠落：重力 0.3，横向微阻尼
            Projectile.velocity.Y = MathF.Min(Projectile.velocity.Y + 0.3f, 15f);
            Projectile.velocity.X *= 0.995f;
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
        }

        /// <summary>进入爆裂：判定盒撑大成小血刺爆，短窗结伤后消亡</summary>
        private void Burst() {
            if (Bursting) {
                return;
            }
            Projectile.ai[0] = 1f;
            Projectile.ai[1] = 0f;
            Projectile.Resize(46, 46);
            Projectile.timeLeft = BurstFrames + 2;
            Projectile.netUpdate = true;
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.NPCHit13 with { Volume = 0.4f, Pitch = 0.3f }, Projectile.Center);
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            //落地炸刺，不反弹不穿地
            Projectile.velocity = Vector2.Zero;
            Burst();
            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) => Burst();

        /// <summary>本体一笔：坠落态原尺寸，爆裂窗按判定箱放大并随窗淡出作范围提示</summary>
        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            float scale = 1f;
            Color color = lightColor;
            if (Bursting) {
                float t = Projectile.ai[1] / BurstFrames;
                scale = Projectile.width / MathF.Max(tex.Width, 1);
                color *= 1f - t;
            }
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, color,
                Projectile.rotation, tex.Size() / 2f, scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
