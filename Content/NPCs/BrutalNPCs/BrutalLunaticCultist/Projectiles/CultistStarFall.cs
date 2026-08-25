using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Rendering;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Projectiles
{
    /// <summary>
    /// 坠星:原版坠落之星精灵做体,晶青染色,同材质残影拖尾<br/>
    /// ai[0]=下落角(弧度,全波次共用同一声明角=走位轴从二维压成一维的公平结构) ai[1]=起落延迟<br/>
    /// 公平阀:出生后 22 帧无判定(高空可读窗),落地即灭,不穿地
    /// </summary>
    internal class CultistStarFall : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.FallingStar;

        private ref float Timer => ref Projectile.localAI[0];
        private float FallAngle => Projectile.ai[0];
        private int Delay => (int)Projectile.ai[1];

        private const int HarmlessFrames = 22;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 7;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 20;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 300;
            Projectile.netImportant = true;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        public override void AI() {
            Timer++;
            if (Timer < Delay) {
                Projectile.velocity = Vector2.Zero;
                Projectile.tileCollide = false;
                return;
            }
            if (Timer == Delay && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item9 with { Volume = 0.5f, Pitch = 0.3f }, Projectile.Center);
            }

            //沿声明角下落,缓加速
            float speed = MathHelper.Clamp((Timer - Delay) * 0.55f, 3f, 15.5f);
            Projectile.velocity = FallAngle.ToRotationVector2() * speed;
            Projectile.tileCollide = Timer > Delay + 10;
            Projectile.rotation += 0.22f;

            //晶尘沿途
            if (!VaultUtils.isServer && Main.rand.NextBool(3) && CultistMotion.OnScreen(Projectile.Center, 200f)) {
                PRTLoader.NewParticle<PRT_CultistFrostMote>(Projectile.Center, -Projectile.velocity * 0.08f,
                    Color.Lerp(CultistMotion.StardustCore, CultistMotion.StardustEdge, Main.rand.NextFloat()),
                    Main.rand.NextFloat(0.6f, 1.1f))?.Configure(Main.rand.Next(16, 28));
            }
            Lighting.AddLight(Projectile.Center, CultistMotion.StardustCore.ToVector3() * 0.45f);
        }

        /// <summary>高空 22 帧无判定:先看见,再危险</summary>
        public override bool CanHitPlayer(Player target) => Timer > Delay + HarmlessFrames;

        public override void OnKill(int timeLeft) {
            CultistMotion.ImpactBurst(Projectile.Center, 1, 1.1f);
            CultistMotion.Shake(Projectile.Center, 2.5f, 6);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Timer < Delay) {
                return false;
            }
            Main.instance.LoadProjectile(ProjectileID.FallingStar);
            Texture2D tex = TextureAssets.Projectile[ProjectileID.FallingStar].Value;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 pos = Projectile.Center - Main.screenPosition;

            //同材质残影:本体星形重画,拖尾横截比≥0.5 体量(Contract 5)
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float t = 1f - i / (float)Projectile.oldPos.Length;
                Vector2 oldPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.EntitySpriteDraw(tex, oldPos, null,
                    CultistMotion.StardustCore with { A = 0 } * (0.32f * t),
                    Projectile.rotation - i * 0.22f, origin, 0.72f + 0.2f * t, SpriteEffects.None, 0);
            }

            //底晕+本体(白体吃晶青染)
            Main.EntitySpriteDraw(glow, pos, null, CultistMotion.StardustCore with { A = 0 } * 0.5f,
                0f, glow.Size() * 0.5f, 0.6f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, pos, null, Color.Lerp(Color.White, CultistMotion.StardustCore, 0.35f),
                Projectile.rotation, origin, 1f, SpriteEffects.None, 0);
            return false;
        }
    }
}
