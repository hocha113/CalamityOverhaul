using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalGolem.Projectiles
{
    /// <summary>拳撞墙迸出的灼热碎石：受重力，落地碎裂</summary>
    internal class GolemStoneShrapnel : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder3;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 18;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 150;
        }

        public override void AI() {
            //迸出声画：首帧各端本地（OnSpawn 不在远端客户端执行）
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.NPCHit4 with { Pitch = 0.3f, Volume = 0.5f }, Projectile.Center);
                    for (int i = 0; i < 4; i++) {
                        Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Torch,
                            Projectile.velocity * 0.3f + Main.rand.NextVector2Circular(2f, 2f), 0, default, 1.3f);
                        dust.noGravity = true;
                    }
                }
            }

            //重力弧线 + 自旋
            Projectile.velocity.Y += 0.34f;
            Projectile.rotation += Projectile.velocity.X * 0.06f;
            Lighting.AddLight(Projectile.Center, new Vector3(0.4f, 0.25f, 0.08f));

            if (!Main.dedServ && Main.rand.NextBool(3)) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Torch,
                    -Projectile.velocity * 0.1f, 0, default, 1.1f);
                dust.noGravity = true;
            }
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Tink with { Pitch = -0.2f, Volume = 0.6f }, Projectile.Center);
            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_MarbleChip>(Projectile.Center,
                    Main.rand.NextVector2Circular(2.5f, 2f) - Vector2.UnitY * 1.5f,
                    new Color(122, 104, 78), Main.rand.NextFloat(0.6f, 1f)).Configure(36);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            //灼热岩块：石屑核心 + 热光晕，速度拉伸
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Texture2D star = CWRAsset.StarGlow01.Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float speed = Projectile.velocity.Length();
            float stretch = 1f + speed * 0.03f;

            Main.EntitySpriteDraw(glow, drawPos, null, new Color(255, 140, 40, 0) * 0.75f,
                0f, glow.Size() / 2f, 0.42f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(star, drawPos, null, new Color(255, 200, 110, 0) * 0.9f,
                Projectile.rotation, star.Size() / 2f, new Vector2(0.1f * stretch, 0.08f), SpriteEffects.None, 0);
            return false;
        }
    }
}
