using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Shortswords
{
    /// <summary>
    /// 铂短剑重铸「铂光残像」。<br/>
    /// 材质：冷调铂金重刃，刺得太快，光还留在原处。签名行为：①每次刺出在刺线位置驻留一道铂光残像
    /// ②残像存续约半秒有余，撞上的敌人吃 40% 伤害（单次判定）③全族最沉的金属重量阶梯，出手低音厚重
    /// </summary>
    internal class GsPlatinumShortsword : GsShortswordScheme
    {
        public override int TargetItemID => ItemID.PlatinumShortsword;

        protected override string GsDescFallback =>
            "Reforged: the thrust outruns its own light, leaving a platinum afterimage hanging along the line;\nfoes that brush the lingering light take 40% damage";
        protected override int HeldProjType => ModContent.ProjectileType<GsPlatinumShortswordHeld>();

        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.08f;//残像驻场是实打实的追加判定，底伤几乎不补
    }

    /// <summary>
    /// 铂短剑手持突刺：沉重时间线（出 4 刺 3 驻 3 收 7），刺出极快衬「光留原处」。
    /// 尖端定格瞬间在刺线驻留 GsPlatinumShortswordAfterProj
    /// </summary>
    internal class GsPlatinumShortswordHeld : GsThrustHeldBase
    {
        protected override int TargetItemType => ItemID.PlatinumShortsword;

        protected override float WindupFrames => 4f;
        protected override float ThrustFrames => 4f;//刺出仍是族内最锐
        protected override float DwellFrames => 3f;
        protected override float RecoverFrames => 7f;
        protected override float PullbackDist => 13f;
        protected override float StabReach => 38f;
        protected override float BladeLength => 46f;
        protected override float ThrustEasePower => 3f;//短剑值域上限，「光留原处」的锐度身份
        protected override int HitstopFrames => 2;
        protected override float LeanAmp => 0.042f;
        protected override float ThrustPitch => -0.28f;//铂金厚重低音

        /// <summary>尖端定格瞬间：在刺线驻留铂光残像（owner 端生成，几何随 ai/velocity 过线）</summary>
        protected override void OnDwellStart() {
            if (!Projectile.IsOwnedByLocalPlayer()) {
                return;
            }
            Vector2 start = Hand + stabUnit * 6f;
            Vector2 end = TipPos;
            Vector2 mid = (start + end) * 0.5f;
            float halfLen = (end - start).Length() * 0.5f;
            //velocity 只当方向载体（随生成包过线），ai0 = 半长
            Projectile.NewProjectile(Projectile.GetSource_FromAI(), mid, stabUnit,
                ModContent.ProjectileType<GsPlatinumShortswordAfterProj>(),
                Math.Max(1, (int)(BaseDamage * 0.40f)), Projectile.knockBack * 0.3f, Owner.whoAmI, halfLen);
        }
    }

    /// <summary>
    /// 铂光残像：刺线位置驻留的静止光刃，约 0.63 秒；撞上的敌人吃 40% 伤害（对每个目标单次判定）。<br/>
    /// ai[0]=半长；velocity 只作方向载体不推进。用原版附魔剑光束贴图沿刺线拉伸一笔，尾段渐隐停判
    /// </summary>
    internal class GsPlatinumShortswordAfterProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.EnchantedBeam;
        public override LocalizedText DisplayName => Language.GetText("ItemName.PlatinumShortsword");

        private const int LifeFrames = 38;
        /// <summary>尾段渐隐帧：不再造成伤害，只演淡出</summary>
        private const int FadeTail = 10;

        private float HalfLen => Projectile.ai[0];
        private Vector2 Unit => Projectile.velocity.SafeNormalize(Vector2.UnitX);
        private float LifeT => 1f - Projectile.timeLeft / (float)LifeFrames;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 26;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;//单次判定
            Projectile.timeLeft = LifeFrames;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            Projectile.rotation = Unit.ToRotation();

            //出生一声冷冽余音（各端自演一次）
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.25f, Pitch = 0.5f }, Projectile.Center);
                }
            }
        }

        /// <summary>尾段渐隐不再伤敌，只演淡出</summary>
        public override bool? CanDamage() => Projectile.timeLeft > FadeTail ? null : false;

        /// <summary>判定即残像线本身：细线碰撞，不吃贴身救济（它是驻场光，不是持械）</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Vector2 a = Projectile.Center - Unit * HalfLen;
            Vector2 b = Projectile.Center + Unit * HalfLen;
            float collisionPoint = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), a, b, 22f, ref collisionPoint);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (VaultUtils.isServer) {
                return;
            }
            //残像割痕：轻脆冷响
            SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.35f, Pitch = 0.5f }, target.Center);
        }

        /// <summary>残像线本体：原版光束贴图沿刺线拉伸一笔，寿命进度渐隐</summary>
        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            float fade = 1f - LifeT;
            fade *= fade;//二次曲线：前段亮足、尾段快速熄灭
            if (fade <= 0.02f) {
                return false;
            }
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 texSize = tex.Size();
            Main.spriteBatch.Draw(tex, drawPos, null, lightColor * fade, Projectile.rotation, texSize / 2f,
                new Vector2(HalfLen * 2f / texSize.X, 1f), SpriteEffects.None, 0f);
            return false;
        }
    }
}
