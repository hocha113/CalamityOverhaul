using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonWhips.Projectiles
{
    /// <summary>
    /// 荆棘鞭处决「荆棘爆裂」主爆：目标身上荆棘倒刺炸开（0.6x），
    /// 棘刺由方案在同帧另行生成，本弹幕只管爆点判定
    /// </summary>
    internal class GsWhipThornBurstProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.ThornChakram;

        private const int GatherFrames = 4;
        private const int BurstFrames = 4;
        private const int LifeFrames = 22;

        private int Elapsed => LifeFrames - Projectile.timeLeft;

        public override void SetDefaults() {
            Projectile.width = 140;
            Projectile.height = 140;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LifeFrames;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override bool? CanDamage() => Elapsed >= GatherFrames && Elapsed < GatherFrames + BurstFrames ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
            => targetHitbox.Intersects(Utils.CenteredRectangle(Projectile.Center, new Vector2(140f)));

        public override void AI() {
            if (Elapsed == GatherFrames && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.9f, Pitch = -0.3f }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Item17 with { Volume = 0.7f, Pitch = -0.1f }, Projectile.Center);
            }
        }

        /// <summary>范围提示：原版荆棘飞轮贴图按判定框缩放画一笔（lightColor 着色），爆后随余帧渐隐</summary>
        public override bool PreDraw(ref Color lightColor) {
            if (Elapsed < GatherFrames) {
                return false;
            }
            float fade = 1f - MathHelper.Clamp((Elapsed - GatherFrames) / (float)(LifeFrames - GatherFrames), 0f, 1f);
            if (fade <= 0.01f) {
                return false;
            }
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            float scale = Projectile.width / (float)tex.Width;
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, lightColor * fade, 0f,
                tex.Size() * 0.5f, scale, SpriteEffects.None, 0);
            return false;
        }
    }

    /// <summary>
    /// 荆棘鞭处决棘刺：四散后转向追踪（各 0.4x），命中挂原版中毒。
    /// 贴图复用原版毒刺弹幕
    /// </summary>
    internal class GsWhipThornDartProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.Stinger;

        private const int ScatterFrames = 10;   //四散段
        private const int LifeFrames = 100;

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.penetrate = 1;
            Projectile.timeLeft = LifeFrames;
            Projectile.DamageType = DamageClass.Summon;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            int elapsed = LifeFrames - Projectile.timeLeft;
            if (elapsed < ScatterFrames) {
                Projectile.velocity *= 0.96f;
            }
            else {
                //追踪最近可追目标：逐帧限速转向，保留弧线感
                NPC target = FindNearest(560f);
                if (target != null) {
                    Vector2 want = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX) * 13f;
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, want, 0.09f);
                }
                else if (Projectile.velocity.Length() < 9f) {
                    Projectile.velocity *= 1.04f;
                }
            }
        }

        private NPC FindNearest(float range) {
            NPC best = null;
            float bestDist = range;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.friendly || !npc.CanBeChasedBy()) {
                    continue;
                }
                float d = Vector2.Distance(npc.Center, Projectile.Center);
                if (d < bestDist) {
                    bestDist = d;
                    best = npc;
                }
            }
            return best;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
            => target.AddBuff(BuffID.Poisoned, 180);
    }
}
