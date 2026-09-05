using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonMinions.Projectiles
{
    /// <summary>
    /// 狱炎柱：小恶魔聚火令的结算体。目标脚下竖起 120 高火柱，
    /// 三相 = 喷发 8 帧（伤害窗）/ 舔舐 24 帧（伤害窗，每目标至多两段）/ 熄灭 8 帧（无伤害）
    /// </summary>
    internal class GsImpPyreProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.BallofFire;

        public override string LocalizationCategory => "GodSmithSummonMinionsA";

        private const int EruptFrames = 8;
        private const int BlazeFrames = 24;
        private const int FadeFrames = 8;
        private const int TotalFrames = EruptFrames + BlazeFrames + FadeFrames;
        private const float PillarHeight = 120f;
        private const float PillarWidth = 44f;

        private int Elapsed => TotalFrames - Projectile.timeLeft;

        private bool Fading => Elapsed >= EruptFrames + BlazeFrames;

        /// <summary>喷发进度（0~1，柱身从地面窜起）</summary>
        private float RiseT => MathHelper.Clamp(Elapsed / (float)EruptFrames, 0f, 1f);

        public override void SetDefaults() {
            Projectile.width = 44;
            Projectile.height = 130;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalFrames;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            //舔舐期每目标至多两段
            Projectile.localNPCHitCooldown = 20;
        }

        public override void AI() {
            Projectile.velocity = Vector2.Zero;
            //喷发帧音效（AI 各端都跑，远端也可闻）
            if (Elapsed == 1 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item74 with { Volume = 0.55f, Pitch = 0.1f },
                    Projectile.Center);
            }
        }

        /// <summary>熄灭相不再伤害</summary>
        public override bool? CanDamage() => Fading ? false : null;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float height = PillarHeight * RiseT;
            Rectangle pillar = new((int)(Projectile.Bottom.X - PillarWidth / 2f),
                (int)(Projectile.Bottom.Y - height), (int)PillarWidth, (int)height);
            return pillar.Intersects(targetHitbox);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
            => target.AddBuff(BuffID.OnFire, 240);

        /// <summary>区域尺寸提示：原版火球贴图按柱身矩形拉伸一笔（底部锚地，熄灭相渐隐）</summary>
        public override bool PreDraw(ref Color lightColor) {
            float fade = Fading
                ? MathHelper.Clamp(Projectile.timeLeft / (float)FadeFrames, 0f, 1f) : 1f;
            float height = PillarHeight * RiseT;
            if (height < 1f || fade <= 0.01f) {
                return false;
            }
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Main.EntitySpriteDraw(tex, Projectile.Bottom - Main.screenPosition, null, lightColor * fade,
                0f, new Vector2(tex.Width / 2f, tex.Height),
                new Vector2(PillarWidth / tex.Width, height / tex.Height), SpriteEffects.None, 0);
            return false;
        }
    }
}
