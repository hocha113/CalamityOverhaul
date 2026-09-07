using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Rendering;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Projectiles
{
    /// <summary>
    /// 月屑：零伤害的暗色圆盘，缓慢背着她漂。世界照白时它背着她投下的阴影锥是唯一的安全处
    /// （几何在屏幕着色器与 <see cref="EmpressMoonShadow"/> 里同源）。ai[0]=宿主 whoAmI ai[1]=寿命帧
    /// </summary>
    internal class EmpressMoonShard : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "DiffusionCircle";

        [VaultLoaden(CWRConstant.Masking + "CrescentSoft01")]
        internal static Asset<Texture2D> Crescent = null;

        internal const float Radius = 64f;
        private const float DriftSpeed = 1.1f;

        private NPC Host => ((int)Projectile.ai[0]).TryGetNPC(out NPC n) ? n : null;
        private int Life => Math.Max((int)Projectile.ai[1], 60);
        private ref float Timer => ref Projectile.localAI[0];
        private static float DayBlend => VaultUtils.isServer ? 0f : EmpressDayDrive.Intensity;

        /// <summary>本帧活着的月屑（客户端，供屏幕着色器与安全判定）</summary>
        internal static readonly List<Projectile> Active = new();

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2400;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = (int)(Radius * 2f);
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 900;
        }

        public override void AI() {
            NPC host = Host;
            if (host == null || !host.active) {
                Projectile.Kill();
                return;
            }
            if (Timer == 0f) {
                Projectile.timeLeft = Life;
            }
            Timer++;
            //背着她慢慢漂：出生速度衰到匀速，再叠一点远离她的分量，阴影锥因此一直在扫
            Vector2 away = Projectile.DirectionFrom(host.Center);
            Vector2 desired = away * DriftSpeed;
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.02f);
            Projectile.rotation += 0.004f;

            //出场 20f 长出，末 20f 缩回
            float grow = MathHelper.Clamp(Timer / 20f, 0f, 1f) * MathHelper.Clamp(Projectile.timeLeft / 20f, 0f, 1f);
            Projectile.scale = grow;

            if (!VaultUtils.isServer) {
                Active.Add(Projectile);
                Lighting.AddLight(Projectile.Center, new Vector3(0.1f, 0.12f, 0.25f) * grow);
                //月尘：向下慢落的暗蓝细屑
                if (Main.rand.NextBool(5)) {
                    PRTLoader.NewParticle<PRT_EmpressPetalDust>(Projectile.Center + Main.rand.NextVector2Circular(Radius * 0.8f, Radius * 0.8f),
                        new Vector2(0f, Main.rand.NextFloat(0.3f, 0.9f)), new Color(90, 100, 170), Main.rand.NextFloat(0.3f, 0.5f))?.Configure(30, 0.66f, 0f);
                }
            }
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.6f, Pitch = -0.3f, MaxInstances = 3 }, Projectile.Center);
            for (int i = 0; i < 16; i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 6f);
                PRTLoader.NewParticle<PRT_EmpressSpark>(Projectile.Center + Main.rand.NextVector2Circular(Radius * 0.6f, Radius * 0.6f), vel,
                    new Color(150, 160, 230), Main.rand.NextFloat(0.6f, 1f))?.Configure(22, 0.66f, 0f);
            }
        }

        /// <summary>暗盘（真 alpha 圆）+ 朝她那侧的一线新月亮边</summary>
        public override bool PreDraw(ref Color lightColor) {
            Texture2D disc = Terraria.GameContent.TextureAssets.Projectile[Type].Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float s = Projectile.scale;
            float discScale = Radius * 2f / disc.Width * s;
            Color body = new(22, 20, 44);
            Color deep = new(10, 8, 24);
            Main.spriteBatch.Draw(disc, drawPos, null, deep, 0f, disc.Size() / 2f, discScale * 1.08f, SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(disc, drawPos, null, body, 0f, disc.Size() / 2f, discScale, SpriteEffects.None, 0f);
            //月盘上的暗纹：同图旋转再叠一次更暗色
            Main.spriteBatch.Draw(disc, drawPos + new Vector2(Radius * 0.15f, -Radius * 0.1f) * s, null, deep * 0.6f, Projectile.rotation, disc.Size() / 2f, discScale * 0.55f, SpriteEffects.None, 0f);

            if (Crescent?.Value is Texture2D crescent && Host is NPC host) {
                //被她照亮的那一侧：新月亮边指向她
                float toSun = Projectile.AngleTo(host.Center);
                float day = DayBlend;
                Color rimC = Color.Lerp(new Color(200, 210, 255), EmpressMotion.Sun(0.8f), day);
                Main.spriteBatch.Draw(crescent, drawPos, null, rimC * (0.85f * s), toSun + MathHelper.PiOver2, crescent.Size() / 2f,
                    Radius * 2.1f / crescent.Width * s, SpriteEffects.None, 0f);
            }
            return false;
        }
    }

    /// <summary>月影几何（与 EmpressScreenPrism.fx 的 ShadowCone 同源）：点是否在任一月屑背着她投下的本影里</summary>
    internal static class EmpressMoonShadow
    {
        /// <summary>阴影锥长度（世界 px）</summary>
        internal const float Length = 1600f;

        public static bool InShadow(Vector2 p, Vector2 sun, IReadOnlyList<Projectile> shards) {
            for (int i = 0; i < shards.Count; i++) {
                Projectile s = shards[i];
                if (!s.active) {
                    continue;
                }
                float radius = EmpressMoonShard.Radius * s.scale;
                if (radius < 2f) {
                    continue;
                }
                Vector2 axis = s.Center - sun;
                float dist = axis.Length() + 0.0001f;
                axis /= dist;
                Vector2 d = p - s.Center;
                float t = Vector2.Dot(d, axis);
                if (t < 0f || t > Length * 0.95f) {
                    continue;
                }
                float perp = Math.Abs(d.X * axis.Y - d.Y * axis.X);
                float halfW = radius + t * (radius / dist);
                if (perp <= halfW * 0.9f) {
                    return true;
                }
            }
            return false;
        }
    }
}
