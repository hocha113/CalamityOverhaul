using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BloomsandSerpents.Projectiles
{
    /// <summary>
    /// 风暴标记：祭舞召唤的沙尘暴预告实体（BSS 沙语言：旋沙收束 + 隆隆 + 转环）。
    /// 自然到期（timeLeft 走完）由权威端生成原版 657 沙尘暴本体——原版 658 标记
    /// 自产的本体伤害硬编码 30/22 对本档位超模，走本标记可自控伤害并把寿命钳短。
    /// 被转阶段清弹 Kill 掉的标记不爆，安静退场（镜像 BssBreachOmen 契约）。
    /// ai[0] = 预告帧数；ai[1] = 本体伤害（已换算）；ai[2] = 本体寿命钳制。
    /// </summary>
    internal class BssStormMark : BssModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.SandBallFalling;

        private int TotalFrames => (int)Projectile.ai[0];
        private int NadoDamage => (int)Projectile.ai[1];
        private int NadoLife => (int)Projectile.ai[2];
        private float Progress => TotalFrames > 0 ? 1f - Projectile.timeLeft / (float)TotalFrames : 1f;

        public override void SetDefaults() {
            Projectile.width = 26;
            Projectile.height = 26;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 66;
        }

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                if (TotalFrames > 0) {
                    Projectile.timeLeft = TotalFrames;
                }
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Item60 with { Volume = 0.55f, MaxInstances = 4 }, Projectile.Center);
                }
            }

            float p = Progress;

            if (Main.dedServ) {
                return;
            }
            //旋沙收束：环带向轴心旋进（凭空聚风的预告主体）
            int count = 1 + (int)(p * 3f);
            for (int i = 0; i < count; i++) {
                if (!Main.rand.NextBool(2)) {
                    continue;
                }
                float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                float r = MathHelper.Lerp(150f, 34f, p) * Main.rand.NextFloat(0.75f, 1.2f);
                Vector2 pos = Projectile.Center + ang.ToRotationVector2() * r;
                Vector2 tangent = (ang + MathHelper.PiOver2).ToRotationVector2();
                Dust d = Dust.NewDustPerfect(pos, DustID.Sand,
                    tangent * Main.rand.NextFloat(2.5f, 4.5f) + (Projectile.Center - pos) * 0.03f,
                    120, default, Main.rand.NextFloat(0.9f, 1.4f));
                d.noGravity = true;
            }
            //上下拉出的风柱丝（沙尘暴身形的预演）
            if (Main.rand.NextBool(3)) {
                Dust w = Dust.NewDustPerfect(
                    Projectile.Center + new Vector2(Main.rand.NextFloat(-16f, 16f), Main.rand.NextFloat(-160f, 160f) * p),
                    DustID.Sand, new Vector2(0f, -Main.rand.NextFloat(1f, 3f)),
                    140, default, Main.rand.NextFloat(0.7f, 1f));
                w.noGravity = true;
            }
            //隆隆节拍加密
            int gap = p > 0.6f ? 11 : 17;
            if (Projectile.timeLeft % gap == 0) {
                SoundEngine.PlaySound(SoundID.WormDig with { Volume = 0.35f + 0.3f * p, Pitch = -0.3f + 0.3f * p, MaxInstances = 4 },
                    Projectile.Center);
                BssVfx.Shake(Projectile.Center, 0.8f + 1.8f * p, 800f);
            }
        }

        /// <summary>自然到期：各端本地起风演出，权威端生成 657 本体（自控伤害 + 寿命钳短）</summary>
        public override void OnKill(int timeLeft) {
            if (timeLeft > 0) {
                return; //被清场阀杀掉的标记安静退场
            }

            if (!Main.dedServ) {
                BssVfx.SandBurst(Projectile.Center + new Vector2(0f, 40f), 1.4f);
                SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.6f, Pitch = -0.35f, MaxInstances = 3 }, Projectile.Center);
                BssVfx.Shake(Projectile.Center, 4f, 1100f);
                for (int i = 0; i < 16; i++) {
                    Dust d = Dust.NewDustPerfect(
                        Projectile.Center + new Vector2(Main.rand.NextFloat(-20f, 20f), Main.rand.NextFloat(-90f, 90f)),
                        DustID.Sand, new Vector2(Main.rand.NextFloat(-2f, 2f), -Main.rand.NextFloat(2f, 6f)),
                        90, default, Main.rand.NextFloat(1.1f, 1.6f));
                    d.noGravity = true;
                }
            }

            if (VaultUtils.isClient) {
                return;
            }
            int idx = Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center,
                Vector2.Zero, ProjectileID.SandnadoHostile, Math.Max(NadoDamage, 1), 3f, Main.myPlayer);
            if (idx >= 0 && idx < Main.maxProjectiles && NadoLife > 0) {
                Main.projectile[idx].timeLeft = NadoLife;
                Main.projectile[idx].netUpdate = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            //转环收束：三颗沙球绕轴旋进（可见实体预告，不只靠尘雾）
            Main.instance.LoadProjectile(ProjectileID.SandBallFalling);
            Texture2D tex = TextureAssets.Projectile[ProjectileID.SandBallFalling].Value;
            Vector2 origin = tex.Size() * 0.5f;
            float p = Progress;
            float spin = Main.GlobalTimeWrappedHourly * (3f + p * 6f);
            float radius = MathHelper.Lerp(56f, 14f, p);
            for (int i = 0; i < 3; i++) {
                float ang = spin + MathHelper.TwoPi * i / 3f;
                Vector2 pos = Projectile.Center + ang.ToRotationVector2() * radius
                    + new Vector2(0f, MathF.Sin(spin * 1.6f + i * 2.1f) * 22f * (1f - p));
                Color tint = lightColor.MultiplyRGB(BssVfx.SandWarm) * (0.55f + 0.45f * p);
                Main.EntitySpriteDraw(tex, pos - Main.screenPosition, null, tint,
                    ang, origin, 0.9f + 0.5f * p, SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
