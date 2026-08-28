using CalamityOverhaul.Content.NPCs.SeaShrimp.Rendering;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.SeaShrimp.Projectiles
{
    /// <summary>
    /// 空泡：空泡拳的第二拍。生长期无害（气泡本身即预告），
    /// 爆缩瞬间产生一圈半径 = 可见气泡半径的伤害（伤害窗=视觉窗），
    /// 爆缩前 8 帧先收缩到 40%（收缩后爆的吸气拍）。
    /// ai[0]=爆缩延迟帧，ai[1]=爆缩半径；计时由 timeLeft 反推（迟入端不重播预告）
    /// </summary>
    internal class SeaShrimpCavitationBubble : SeaShrimpModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        [VaultLoaden(CWRConstant.Masking + "DiffusionCircle")]
        private static Asset<Texture2D> RingTex = null;

        private int Delay => (int)Projectile.ai[0];
        private float BlastRadius => Projectile.ai[1];
        /// <summary>爆缩后的余帧（闪光衰减）</summary>
        private const int AfterFrames = 10;
        /// <summary>伤害窗帧数</summary>
        private const int DamageFrames = 6;

        /// <summary>
        /// 本地帧龄：localAI 逐端计数（OnSpawn/timeLeft 不跨端，反推会在远端错位）。
        /// 各端从收到生成包起步，偏差 ≤2 帧；受害者端的伤害窗与其本地可见相位严格对齐
        /// </summary>
        private int Age => (int)Projectile.localAI[0];

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.aiStyle = -1;
            Projectile.timeLeft = 120;
        }

        /// <summary>当前可见半径：生长→收缩→爆缩定格</summary>
        private float VisualRadius() {
            int age = Age;
            if (age >= Delay) {
                return BlastRadius;
            }
            float growEnd = Delay - 8;
            if (age < growEnd) {
                //三次方生长：起小终猛
                float t = age / growEnd;
                return BlastRadius * (0.12f + 0.88f * t * t * (3f - 2f * t));
            }
            //爆缩前收缩拍
            float s = (age - growEnd) / 8f;
            return BlastRadius * MathHelper.Lerp(1f, 0.4f, s);
        }

        public override void AI() {
            Projectile.localAI[0]++;
            int age = Age;
            if (age >= Delay + AfterFrames) {
                Projectile.Kill();
                return;
            }

            //光照：气泡蓝辉
            float lum = age < Delay ? 0.35f : 0.9f;
            Lighting.AddLight(Projectile.Center, 0.12f * lum, 0.25f * lum, 0.5f * lum);

            if (age == Delay) {
                //爆缩帧：声致发光——针点白闪 + 冲击环 + 就近震屏
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Item94 with { Volume = 0.9f, Pitch = 0.25f, MaxInstances = 3 }, Projectile.Center);
                    SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.5f, Pitch = 0.5f, MaxInstances = 3 }, Projectile.Center);
                    PRTLoader.NewParticle<PRT_StarPulseRing>(Projectile.Center, Vector2.Zero,
                        SeaShrimpRenderer.CrystalBlue, 1f)?.Configure(BlastRadius / 120f * 0.4f, BlastRadius / 120f * 1.7f, 16);
                    for (int i = 0; i < 14; i++) {
                        PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                            Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(4f, 11f),
                            Color.Lerp(SeaShrimpRenderer.CrystalBlue, Color.White, Main.rand.NextFloat(0.6f)),
                            Main.rand.NextFloat(0.5f, 0.9f))?.Configure(false, Main.rand.Next(10, 18));
                    }
                    if (Main.LocalPlayer != null
                        && Vector2.Distance(Main.LocalPlayer.Center, Projectile.Center) < 1200f) {
                        Main.LocalPlayer.CWR()?.GetScreenShake(6f);
                    }
                }
            }
            else if (age < Delay && !Main.dedServ) {
                //生长期：泡壁微光上浮的小气泡（密度在收缩拍截停——吸气静默）
                bool quiet = age > Delay - 8;
                if (!quiet && Main.GameUpdateCount % 3 == 0) {
                    Vector2 rim = Projectile.Center + Main.rand.NextVector2CircularEdge(1f, 1f) * VisualRadius();
                    PRTLoader.NewParticle<PRT_SHPCCoralBubble>(rim,
                        new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), -Main.rand.NextFloat(0.3f, 0.9f)),
                        Color.White * 0.7f, Main.rand.NextFloat(0.4f, 0.7f))?.Configure(Main.rand.Next(20, 34));
                }
            }
        }

        public override void OnKill(int timeLeft) {
            //余波：雾环在弹体死后继续存在（余波规则）
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_GhostRainMist>(
                    Projectile.Center + Main.rand.NextVector2Circular(BlastRadius * 0.5f, BlastRadius * 0.5f),
                    new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), -Main.rand.NextFloat(0.2f, 0.7f)),
                    new Color(120, 170, 235) * 0.5f, Main.rand.NextFloat(0.7f, 1.1f))?.Configure(Main.rand.Next(36, 60));
            }
        }

        /// <summary>伤害窗：仅爆缩后 6 帧；生长期是纯预告</summary>
        public override bool? CanDamage() {
            int age = Age;
            return age >= Delay && age < Delay + DamageFrames ? null : false;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            //圆形判定：与可见爆缩半径一致
            Vector2 nearest = new(
                MathHelper.Clamp(Projectile.Center.X, targetHitbox.Left, targetHitbox.Right),
                MathHelper.Clamp(Projectile.Center.Y, targetHitbox.Top, targetHitbox.Bottom));
            return Vector2.Distance(nearest, Projectile.Center) <= BlastRadius;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D ring = RingTex?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (ring == null) {
                return false;
            }
            int age = Age;
            float radius = VisualRadius();
            Vector2 pos = Projectile.Center - Main.screenPosition;

            if (age < Delay) {
                //泡体：真alpha扩散环压底（可遮挡），内侧淡蓝水膜，右上高光点
                float wobble = 1f + 0.04f * MathF.Sin(Main.GlobalTimeWrappedHourly * 21f + Projectile.identity);
                float scale = radius * 2f / ring.Width * wobble;
                Main.spriteBatch.Draw(ring, pos, null, new Color(150, 200, 255) * 0.85f, 0f,
                    ring.Size() * 0.5f, scale, SpriteEffects.None, 0f);
                Main.spriteBatch.Draw(ring, pos, null, new Color(40, 70, 130) * 0.35f, 0f,
                    ring.Size() * 0.5f, scale * 0.86f, SpriteEffects.None, 0f);
                if (glow != null) {
                    Main.spriteBatch.Draw(glow, pos + new Vector2(-radius * 0.34f, -radius * 0.4f), null,
                        new Color(255, 255, 255, 0) * 0.55f, 0f, glow.Size() * 0.5f,
                        radius * 0.5f / glow.Width * 2f, SpriteEffects.None, 0f);
                }
            }
            else {
                //爆缩闪：白热针点急速衰减
                float fade = 1f - (age - Delay) / (float)AfterFrames;
                if (glow != null && fade > 0f) {
                    Main.spriteBatch.Draw(glow, pos, null, new Color(255, 255, 255, 0) * (0.9f * fade), 0f,
                        glow.Size() * 0.5f, radius * 1.5f / glow.Width * 2f * fade, SpriteEffects.None, 0f);
                    Main.spriteBatch.Draw(glow, pos, null,
                        SeaShrimpRenderer.CrystalBlue with { A = 0 } * (0.8f * fade), 0f,
                        glow.Size() * 0.5f, radius * 2.6f / glow.Width * 2f, SpriteEffects.None, 0f);
                }
            }
            return false;
        }
    }
}
