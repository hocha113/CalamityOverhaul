using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Rendering;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Projectiles
{
    /// <summary>
    /// 光痕：光记得你在哪里。出生在玩家脚下，两小节内是半透明的你（无害，脚下金光往上涨），
    /// 第一拍凝成一柄琉璃剑（原版泰拉棱镜剑贴图）立在原地一小节，碰到受伤，再在第一拍碎成四颗光球。
    /// ai[0]=受害玩家索引 ai[1]=宿主 whoAmI。规则：别回到两秒前站过的地方
    /// </summary>
    internal class EmpressEcho : ModProjectile, IEmpressAttack
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.EmpressBlade;

        public EmpressScorchTier ScorchTier => EmpressScorchTier.Light;
        public float FeedbackIntensity => 0.7f;

        internal const int SoftFrames = EmpressTempo.BarFrames * 2;
        internal const int HardFrames = EmpressTempo.BarFrames;
        internal const int TotalFrames = SoftFrames + HardFrames;

        private int Victim => (int)Projectile.ai[0];
        private NPC Host => ((int)Projectile.ai[1]).TryGetNPC(out NPC n) ? n : null;
        private ref float Timer => ref Projectile.localAI[0];
        private bool Hard => Timer >= SoftFrames;
        private float Hue => (Projectile.identity * 0.163f) % 1f;
        private static float DayBlend => VaultUtils.isServer ? 0f : EmpressDayDrive.Intensity;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 800;

        public override void SetDefaults() {
            Projectile.width = 30;
            Projectile.height = 70;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalFrames;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        public override void AI() {
            NPC host = Host;
            if (host == null || !host.active) {
                Projectile.Kill();
                return;
            }
            Timer++;
            Projectile.velocity = Vector2.Zero;

            if (!VaultUtils.isServer) {
                float day = DayBlend;
                Color rim = EmpressMotion.FormRim(Hue, day, 0.6f);
                Lighting.AddLight(Projectile.Center, EmpressMotion.FormColor(Hue, day, 0.6f).ToVector3() * (Hard ? 0.7f : 0.25f));

                //凝固前一拍闪白 + 收束火花（屏息拍）
                if (Timer >= SoftFrames - EmpressTempo.BeatFrames && Timer < SoftFrames && Main.rand.NextBool(2)) {
                    Vector2 spawn = Projectile.Center + Main.rand.NextVector2CircularEdge(50f, 70f);
                    PRTLoader.NewParticle<PRT_EmpressSpark>(spawn, (Projectile.Center - spawn) * 0.12f, rim,
                        Main.rand.NextFloat(0.5f, 0.9f))?.Configure(12, Hue, day);
                }
                if (Timer == SoftFrames) {
                    OnHarden(rim, day);
                }
                //琉璃期偶发折光
                if (Hard && Main.rand.NextBool(6)) {
                    PRTLoader.NewParticle<PRT_EmpressSpark>(Projectile.Center + Main.rand.NextVector2Circular(12f, 30f),
                        new Vector2(0f, -Main.rand.NextFloat(0.5f, 1.5f)), rim, Main.rand.NextFloat(0.4f, 0.7f))?.Configure(14, Hue, day);
                }
            }
        }

        private void OnHarden(Color rim, float day) {
            SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.6f, Pitch = -0.25f, MaxInstances = 3 }, Projectile.Center);
            PRTLoader.NewParticle<PRT_EmpressRipple>(Projectile.Center, Vector2.Zero, Color.White, 0.55f)?.Configure(14, Hue, day);
            EmpressMotion.SparkBurst(Projectile.Center, -Vector2.UnitY, 8, 2f, 6f, day, 1.2f);
        }

        public override void OnKill(int timeLeft) {
            //碎：四颗光球朝斜向飞出（权威端），玻璃碎屑（各端）
            if (!VaultUtils.isClient && timeLeft <= 1 && Host is NPC host) {
                int damage = host.GetAttackDamage_ForProjectiles(40, 26);
                for (int i = 0; i < 4; i++) {
                    Vector2 dir = (MathHelper.PiOver4 + MathHelper.PiOver2 * i).ToRotationVector2();
                    EmpressCast.Bolt(host, Projectile.Center, dir * 4.5f, damage, EmpressBoltMode.Straight);
                }
            }
            if (VaultUtils.isServer) {
                return;
            }
            float day = DayBlend;
            SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.7f, Pitch = 0.1f, MaxInstances = 3 }, Projectile.Center);
            for (int i = 0; i < 14; i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 7f) + new Vector2(0f, -2f);
                PRTLoader.NewParticle<PRT_EmpressSpark>(Projectile.Center + Main.rand.NextVector2Circular(12f, 30f), vel,
                    EmpressMotion.FormRim(Hue + i * 0.07f, day, 0.62f), Main.rand.NextFloat(0.6f, 1.1f))?.Configure(20, Hue, day);
            }
        }

        public override bool? CanDamage() => Hard ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Rectangle blade = new((int)Projectile.Center.X - 15, (int)Projectile.Center.Y - 36, 30, 72);
            return blade.Intersects(targetHitbox);
        }

        public override bool PreDraw(ref Color lightColor) {
            float day = DayBlend;
            Color core = EmpressMotion.FormColor(Hue, day, 0.68f);
            Color rim = EmpressMotion.FormRim(Hue, day, 0.58f);
            Color dark = EmpressMotion.FormDark(day);
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            if (!Hard) {
                DrawGhost(day, core, rim);
                return false;
            }

            //琉璃剑：原版泰拉棱镜剑贴图竖立（贴图朝右上，转 -45° 立起），暗底→光谱边→白金体
            Texture2D blade = TextureAssets.Projectile[Type].Value;
            Vector2 origin = blade.Size() / 2f;
            float rot = -MathHelper.PiOver4;
            float settle = MathHelper.Clamp((Timer - SoftFrames) / 6f, 0f, 1f);
            float scale = 1.1f * (0.8f + 0.2f * settle);
            Main.spriteBatch.Draw(blade, drawPos, null, dark, rot, origin, scale * 1.25f, SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(blade, drawPos, null, rim, rot, origin, scale * 1.12f, SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(blade, drawPos, null, core, rot, origin, scale, SpriteEffects.None, 0f);
            //折光高光沿刃身来回扫
            float glint = 0.5f + 0.5f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 6f + Projectile.identity);
            Main.spriteBatch.Draw(blade, drawPos, null, (Color.White with { A = 0 }) * (0.25f + 0.35f * glint * settle), rot, origin, scale * 0.9f, SpriteEffects.None, 0f);
            return false;
        }

        /// <summary>半透明的你：原版玩家渲染器画一份残像，脚下金光按凝固进度往上涨</summary>
        private void DrawGhost(float day, Color core, Color rim) {
            float t = MathHelper.Clamp(Timer / (float)SoftFrames, 0f, 1f);
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Vector2 feet = Projectile.Center + new Vector2(0f, 32f) - Main.screenPosition;
            //脚下光池，随凝固进度长高（沙漏在装满）
            float rise = 0.15f + 0.85f * t;
            Main.spriteBatch.Draw(glow, feet, null, (rim with { A = 0 }) * (0.35f + 0.35f * t), 0f, new Vector2(glow.Width / 2f, glow.Height),
                new Vector2(0.9f, 1.6f * rise), SpriteEffects.None, 0f);

            if (Victim < 0 || Victim >= Main.maxPlayers) {
                return;
            }
            Player player = Main.player[Victim];
            if (!player.active || player.dead) {
                return;
            }
            //透明度 0.85→0.4，凝固前一拍再猛地实一下
            float shadow = MathHelper.Lerp(0.85f, 0.4f, t);
            if (Timer >= SoftFrames - EmpressTempo.BeatFrames) {
                shadow = MathHelper.Lerp(shadow, 0.15f, (Timer - (SoftFrames - EmpressTempo.BeatFrames)) / EmpressTempo.BeatFrames);
            }
            Vector2 topLeft = Projectile.Center - player.Size / 2f;
            Main.PlayerRenderer.DrawPlayer(Main.Camera, player, topLeft, 0f, player.fullRotationOrigin, shadow);
        }
    }
}
