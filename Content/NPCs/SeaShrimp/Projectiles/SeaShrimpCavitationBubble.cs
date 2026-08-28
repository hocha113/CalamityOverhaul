using CalamityOverhaul.Content.Items.Magic.Everdeeps;
using CalamityOverhaul.Content.Items.Melee.Abyssrends;
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
    /// 空泡:空泡拳的第二拍。生长期无害(气泡本身即预告,膜面随涨压绷紧发亮,
    /// 外围水滴被向心吸入,密度随涨压升、爆缩前 8 帧静默——吸气拍),
    /// 爆缩瞬间声致发光白闪,冲击波前撕裂外扩,判定半径逐帧对齐可见波前(伤害窗=视觉窗)。
    /// ai[0]=爆缩延迟帧,ai[1]=爆缩半径;计时由 localAI 逐端计数(迟入端不重播预告)
    /// </summary>
    internal class SeaShrimpCavitationBubble : SeaShrimpModProjectile, ISeaShrimpBubbleBody
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        [VaultLoaden(CWRConstant.Masking + "DiffusionCircle")]
        private static Asset<Texture2D> RingTex = null;

        private int Delay => (int)Projectile.ai[0];
        private float BlastRadius => Projectile.ai[1];
        /// <summary>爆缩后的余帧(冲击环外扩+消散)</summary>
        private const int AfterFrames = 12;
        /// <summary>伤害窗帧数:覆盖波前从中心推进到 BlastRadius 的整段</summary>
        private const int DamageFrames = 8;
        /// <summary>冲击环最终可见半径 = 爆缩半径 × 此系数(环越过判定圈后是无害的消散波)</summary>
        private const float RingOvershoot = 1.35f;

        /// <summary>
        /// 本地帧龄:localAI 逐端计数(OnSpawn/timeLeft 不跨端,反推会在远端错位)。
        /// 各端从收到生成包起步,偏差 ≤2 帧;受害者端的伤害窗与其本地可见相位严格对齐
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

        /// <summary>当前可见半径:生长→收缩→爆缩定格</summary>
        private float VisualRadius() {
            int age = Age;
            if (age >= Delay) {
                return BlastRadius;
            }
            float growEnd = Delay - 8;
            if (age < growEnd) {
                //三次方生长:起小终猛
                float t = age / growEnd;
                return BlastRadius * (0.12f + 0.88f * t * t * (3f - 2f * t));
            }
            //爆缩前收缩拍
            float s = (age - growEnd) / 8f;
            return BlastRadius * MathHelper.Lerp(1f, 0.4f, s);
        }

        /// <summary>涨压 0~1</summary>
        private float Charge => MathHelper.Clamp(Age / MathF.Max(Delay - 8, 1f), 0f, 1f);

        public override void AI() {
            Projectile.localAI[0]++;
            SeaShrimpBubbleRender.PresenceStamp.Stamp();
            int age = Age;
            if (age >= Delay + AfterFrames) {
                Projectile.Kill();
                return;
            }

            //光照:气泡蓝辉
            float lum = age < Delay ? 0.35f : 0.9f;
            Lighting.AddLight(Projectile.Center, 0.12f * lum, 0.25f * lum, 0.5f * lum);

            if (age == Delay) {
                //爆缩帧:声致发光——针点白闪 + 冲击波前 + 就近震屏 + 滤镜微脉冲
                //(脉冲分档:巨泡 0.4 全场第二强,常规 0.15;满档 impact 独留死亡内爆)
                if (!Main.dedServ) {
                    SeaShrimpAbyssScreen.TriggerImpactFrame(BlastRadius >= 180f ? 0.4f : 0.15f);
                    SoundEngine.PlaySound(SoundID.Item94 with { Volume = 0.9f, Pitch = 0.25f, MaxInstances = 3 }, Projectile.Center);
                    SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.5f, Pitch = 0.5f, MaxInstances = 3 }, Projectile.Center);
                    //径向水团锥:速度拉伸的暗水滴甩出去,活得比冲击环久
                    int globs = (int)(10 * MathHelper.Clamp(BlastRadius / 118f, 0.8f, 1.8f));
                    for (int i = 0; i < globs; i++) {
                        Vector2 dir = Main.rand.NextVector2Unit();
                        PRTLoader.NewParticle<PRT_AbyssGlob>(Projectile.Center + dir * 10f,
                            dir * Main.rand.NextFloat(4f, 9.5f),
                            Color.Lerp(SeaShrimpVFX.Deep, SeaShrimpVFX.Body, Main.rand.NextFloat()),
                            Main.rand.NextFloat(0.4f, 0.7f))?.Configure(Main.rand.Next(16, 26), 1.6f);
                    }
                    for (int i = 0; i < 8; i++) {
                        PRTLoader.NewParticle<PRT_AbyssSpark>(Projectile.Center,
                            Main.rand.NextVector2Circular(5f, 5f),
                            SeaShrimpVFX.Glow, Main.rand.NextFloat(0.8f, 1.2f))?.Configure(12);
                    }
                    for (int i = 0; i < 5; i++) {
                        EverdeepVFX.ShedDroplet(Projectile.Center + Main.rand.NextVector2Circular(8f, 8f),
                            Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(2.5f, 5f), 1f);
                    }
                    if (Main.LocalPlayer != null
                        && Vector2.Distance(Main.LocalPlayer.Center, Projectile.Center) < 1200f) {
                        Main.LocalPlayer.CWR()?.GetScreenShake(6f);
                    }
                }
            }
            else if (age < Delay && !Main.dedServ) {
                //生长期:外围水滴被向心吸入,密度∝√涨压,收缩拍截停——吸气静默
                bool quiet = age > Delay - 8;
                if (!quiet && Main.rand.NextFloat() < 0.30f + 0.55f * MathF.Sqrt(Charge)) {
                    float dist = VisualRadius() * Main.rand.NextFloat(1.25f, 1.85f) + 14f;
                    Vector2 rim = Projectile.Center + Main.rand.NextVector2CircularEdge(1f, 1f) * dist;
                    PRTLoader.NewParticle<PRT_AbyssGlob>(rim,
                        (Projectile.Center - rim) * 0.08f,
                        Color.Lerp(SeaShrimpVFX.Deep, SeaShrimpVFX.Body, Main.rand.NextFloat()),
                        Main.rand.NextFloat(0.24f, 0.42f))?.Configure(14, 1.8f);
                }
            }
        }

        public override void OnKill(int timeLeft) {
            //余波:雾环在弹体死后继续存在(余波规则)
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

        /// <summary>伤害窗:仅爆缩后 8 帧,波前推进段</summary>
        public override bool? CanDamage() {
            int age = Age;
            return age >= Delay && age < Delay + DamageFrames ? null : false;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            //圆形判定:逐帧对齐可见冲击波前,封顶在爆缩半径(环越过后是无害消散波)
            float progress = MathHelper.Clamp((Age - Delay) / (float)AfterFrames, 0f, 1f);
            float shockR = MathF.Min(BlastRadius,
                SeaShrimpVFX.CollapseRingRadius(BlastRadius * RingOvershoot, progress));
            Vector2 nearest = new(
                MathHelper.Clamp(Projectile.Center.X, targetHitbox.Left, targetHitbox.Right),
                MathHelper.Clamp(Projectile.Center.Y, targetHitbox.Top, targetHitbox.Bottom));
            return Vector2.Distance(nearest, Projectile.Center) <= shockR;
        }

        bool ISeaShrimpBubbleBody.GetBubbleBody(out SeaShrimpBubbleBodyParams body) {
            int age = Age;
            if (age >= Delay) {
                //爆缩段泡体消亡,交给 PreDraw 的崩爆环
                body = default;
                return false;
            }
            bool shrinking = age > Delay - 8;
            float charge = Charge;
            body = new SeaShrimpBubbleBodyParams {
                Center = Projectile.Center,
                Radius = VisualRadius(),
                //收缩拍膜面被张力压平:静得反常才有崩爆的落差
                Wobble = shrinking ? 0.10f : 0.35f + 0.45f * charge,
                Arm = shrinking ? 1f : charge * 0.8f,
                Burst = 0f,
                Fade = MathHelper.Clamp(age / 6f, 0f, 1f),
                Seed = Projectile.identity,
            };
            return true;
        }

        public override bool PreDraw(ref Color lightColor) {
            int age = Age;
            if (age >= Delay) {
                //崩爆:海虾专属冲击环 + 白热针点急速衰减
                float progress = MathHelper.Clamp((age - Delay) / (float)AfterFrames, 0f, 1f);
                if (SeaShrimpVFX.CollapsePathReady) {
                    SeaShrimpVFX.DrawCollapse(Projectile.Center, BlastRadius * RingOvershoot,
                        progress, Projectile.identity * 0.31f, 1f);
                }
                Texture2D glow = CWRAsset.SoftGlow?.Value;
                float fade = 1f - progress;
                if (glow != null && fade > 0f) {
                    Vector2 pos = Projectile.Center - Main.screenPosition;
                    Main.spriteBatch.Draw(glow, pos, null, new Color(255, 255, 255, 0) * (0.9f * fade), 0f,
                        glow.Size() * 0.5f, BlastRadius * 1.5f / glow.Width * 2f * fade, SpriteEffects.None, 0f);
                    Main.spriteBatch.Draw(glow, pos, null,
                        SeaShrimpVFX.Glow with { A = 0 } * (0.8f * fade), 0f,
                        glow.Size() * 0.5f, BlastRadius * 2.6f / glow.Width * 2f, SpriteEffects.None, 0f);
                }
                return false;
            }

            if (SeaShrimpVFX.BubblePathReady) {
                //生长期泡体由 SeaShrimpBubbleRender 统一批绘
                return false;
            }
            //着色器缺失回退:双环+高光
            Texture2D ring = RingTex?.Value;
            Texture2D glowTex = CWRAsset.SoftGlow?.Value;
            if (ring == null) {
                return false;
            }
            float radius = VisualRadius();
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float wobble = 1f + 0.04f * MathF.Sin(Main.GlobalTimeWrappedHourly * 21f + Projectile.identity);
            float scale = radius * 2f / ring.Width * wobble;
            Main.spriteBatch.Draw(ring, drawPos, null, new Color(150, 200, 255) * 0.85f, 0f,
                ring.Size() * 0.5f, scale, SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(ring, drawPos, null, new Color(40, 70, 130) * 0.35f, 0f,
                ring.Size() * 0.5f, scale * 0.86f, SpriteEffects.None, 0f);
            if (glowTex != null) {
                Main.spriteBatch.Draw(glowTex, drawPos + new Vector2(-radius * 0.34f, -radius * 0.4f), null,
                    new Color(255, 255, 255, 0) * 0.55f, 0f, glowTex.Size() * 0.5f,
                    radius * 0.5f / glowTex.Width * 2f, SpriteEffects.None, 0f);
            }
            return false;
        }
    }
}
