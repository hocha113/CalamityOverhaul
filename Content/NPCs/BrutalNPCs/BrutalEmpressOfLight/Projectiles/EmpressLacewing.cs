using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Rendering;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Projectiles
{
    /// <summary>
    /// 光蝶：飘向目标玩家过去两秒的平均位置（一直动它就落在你身后，一停就围上来），扑翼时无害；
    /// 她每隔一小节合掌，光蝶冻成琉璃一小节，碰到受伤，前一拍会先闪白。本体=原版棱彩草蛉贴图。
    /// ai[0]=目标玩家 ai[1]=宿主 whoAmI ai[2]=出生小节序号（合掌奇偶由它推）
    /// </summary>
    internal class EmpressLacewing : ModProjectile, IEmpressAttack
    {
        public override string Texture => "Terraria/Images/NPC_" + NPCID.EmpressButterfly;

        public EmpressScorchTier ScorchTier => EmpressScorchTier.Medium;
        public float FeedbackIntensity => 0.8f;

        private const float MaxSpeed = 5.5f;
        private const float Accel = 0.14f;
        private const float Separation = 40f;
        private const int MaxBars = 12;

        private int TargetIndex => (int)Projectile.ai[0];
        private NPC Host => ((int)Projectile.ai[1]).TryGetNPC(out NPC n) ? n : null;
        private int SpawnBar => (int)Projectile.ai[2];
        /// <summary>localAI[0..1]=平均位置</summary>
        private Vector2 Average {
            get => new(Projectile.localAI[0], Projectile.localAI[1]);
            set { Projectile.localAI[0] = value.X; Projectile.localAI[1] = value.Y; }
        }

        private bool frozen;
        private bool scattering;
        private bool preFlashVisual;
        private float flap;
        private float scatterFade = 1f;
        private float Seed => Projectile.identity * 0.61f;
        private float Hue => (Projectile.identity * 0.083f) % 1f;
        private static float DayBlend => VaultUtils.isServer ? 0f : EmpressDayDrive.Intensity;

        private static readonly List<Projectile> swarm = new();

        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 600;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 26;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = EmpressTempo.BarFrames * MaxBars;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        private Player Target => TargetIndex >= 0 && TargetIndex < Main.maxPlayers && Main.player[TargetIndex].Alives() ? Main.player[TargetIndex] : null;

        public override void AI() {
            NPC host = Host;
            if (host == null || !host.active) {
                Projectile.Kill();
                return;
            }
            Player target = Target;
            if (Average == Vector2.Zero) {
                Average = target?.Center ?? Projectile.Center;
            }

            //宿主离开蝶群态 → 四散升空淡出（余韵，不瞬灭）
            if (!scattering && (int)host.ai[2] != (int)EmpressStateIndex.Lacewing && (int)host.ai[2] != (int)EmpressStateIndex.Finale) {
                scattering = true;
            }
            if (scattering) {
                frozen = false;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, new Vector2(MathF.Sin(Seed + Projectile.timeLeft * 0.05f) * 2f, -3.5f), 0.04f);
                scatterFade = Math.Max(scatterFade - 0.02f, 0f);
                if (scatterFade <= 0f) {
                    Projectile.Kill();
                    return;
                }
                Flap();
                return;
            }

            //合掌奇偶：出生后第 1、3、5… 个小节冻住
            bool wasFrozen = frozen;
            frozen = false;
            bool preFlash = false;
            if (EmpressTempo.TryGet(host, out int barFrame, out int barIndex)) {
                int elapsedBars = barIndex - SpawnBar;
                frozen = elapsedBars >= 1 && elapsedBars % 2 == 1;
                preFlash = elapsedBars >= 0 && elapsedBars % 2 == 0 && barFrame >= EmpressTempo.BeatFrames * 2;
            }
            if (frozen && !wasFrozen) {
                OnFreeze();
            }
            if (!frozen && wasFrozen) {
                OnThaw();
            }

            if (frozen) {
                Projectile.velocity *= 0.6f;
                Lighting.AddLight(Projectile.Center, EmpressMotion.FormRim(Hue, DayBlend, 0.6f).ToVector3() * 0.6f);
                return;
            }

            //追平均位置：2 秒时间常数的滞后跟随
            if (target != null) {
                Average = Vector2.Lerp(Average, target.Center, 1f / 120f);
            }
            Vector2 toAvg = Average - Projectile.Center;
            Vector2 desired = toAvg.SafeNormalize(Vector2.Zero) * MathF.Min(MaxSpeed, toAvg.Length() * 0.05f);
            Vector2 steer = desired - Projectile.velocity;
            Projectile.velocity += steer.SafeNormalize(Vector2.Zero) * MathF.Min(steer.Length(), Accel);
            //扑翼的飘：横向正弦 + 竖向随翅频起伏
            float t = Main.GameUpdateCount * 0.05f + Seed;
            Projectile.velocity += new Vector2(MathF.Sin(t * 0.7f) * 0.06f, MathF.Cos(t * 1.9f) * 0.05f);
            if (Projectile.velocity.Length() > MaxSpeed) {
                Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.Zero) * MaxSpeed;
            }
            swarm.Add(Projectile);
            Flap();

            if (!VaultUtils.isServer) {
                Color light = EmpressMotion.FormColor(Hue, DayBlend, 0.62f);
                Lighting.AddLight(Projectile.Center, light.ToVector3() * 0.3f);
                //花粉般的细光屑
                if (Main.rand.NextBool(8)) {
                    PRTLoader.NewParticle<PRT_EmpressSpark>(Projectile.Center, -Projectile.velocity * 0.2f + Main.rand.NextVector2Circular(0.5f, 0.5f),
                        EmpressMotion.FormRim(Hue, DayBlend, 0.62f) * 0.7f, Main.rand.NextFloat(0.3f, 0.55f))?.Configure(20, Hue, DayBlend);
                }
                if (preFlash && Main.rand.NextBool(3)) {
                    PRTLoader.NewParticle<PRT_EmpressSpark>(Projectile.Center + Main.rand.NextVector2Circular(10f, 10f), Vector2.Zero,
                        Color.White, Main.rand.NextFloat(0.4f, 0.6f))?.Configure(8, Hue, DayBlend);
                }
            }
            preFlashVisual = preFlash;
        }

        private void Flap() {
            flap += 0.28f + Projectile.velocity.Length() * 0.03f;
            Projectile.spriteDirection = Projectile.velocity.X >= 0f ? 1 : -1;
            Projectile.rotation = Projectile.velocity.X * 0.05f;
        }

        private void OnFreeze() {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.25f, Pitch = 0.3f, MaxInstances = 2 }, Projectile.Center);
            EmpressMotion.SparkBurst(Projectile.Center, -Vector2.UnitY, 3, 1f, 3f, DayBlend, MathHelper.Pi);
        }

        private void OnThaw() {
            if (VaultUtils.isServer) {
                return;
            }
            PRTLoader.NewParticle<PRT_EmpressRipple>(Projectile.Center, Vector2.Zero, Color.White, 0.25f)?.Configure(10, Hue, DayBlend);
        }

        /// <summary>同类互斥：40px 内推开，蝶群有体积</summary>
        internal static void Separate() {
            for (int i = 0; i < swarm.Count; i++) {
                for (int j = i + 1; j < swarm.Count; j++) {
                    Vector2 d = swarm[i].Center - swarm[j].Center;
                    float len = d.Length();
                    if (len > 1e-4f && len < Separation) {
                        Vector2 push = d / len * (1f - len / Separation) * 0.35f;
                        swarm[i].velocity += push;
                        swarm[j].velocity -= push;
                    }
                }
            }
            swarm.Clear();
        }

        public override bool? CanDamage() => frozen && !scattering ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            return Vector2.Distance(Projectile.Center, targetHitbox.ClosestPointInRect(Projectile.Center)) <= 22f;
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            EmpressMotion.SparkBurst(Projectile.Center, -Vector2.UnitY, 4, 1f, 3f, DayBlend, MathHelper.Pi);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            int frames = Math.Max(Main.npcFrameCount[NPCID.EmpressButterfly], 1);
            int frameH = tex.Height / frames;
            int frame = frozen ? 0 : (int)(flap / 4f) % frames;
            Rectangle src = new(0, frame * frameH, tex.Width, frameH);
            Vector2 origin = src.Size() / 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            SpriteEffects fx = Projectile.spriteDirection < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            float day = DayBlend;
            Color core = EmpressMotion.FormColor(Hue, day, 0.7f) * scatterFade;
            Color rim = EmpressMotion.FormRim(Hue, day, 0.6f) * scatterFade;
            Color dark = EmpressMotion.FormDark(day) * scatterFade;
            bool preFlash = preFlashVisual && !frozen;

            if (frozen) {
                //琉璃蝶：翅膀不动，暗底 + 光谱边 + 白金体 + 折光高光
                Main.spriteBatch.Draw(tex, drawPos, src, dark, Projectile.rotation, origin, 1.3f, fx, 0f);
                Main.spriteBatch.Draw(tex, drawPos, src, rim, Projectile.rotation, origin, 1.15f, fx, 0f);
                Main.spriteBatch.Draw(tex, drawPos, src, core, Projectile.rotation, origin, 1f, fx, 0f);
                float glint = 0.5f + 0.5f * MathF.Sin(Main.GlobalTimeWrappedHourly * 7f + Seed);
                Main.spriteBatch.Draw(tex, drawPos, src, (Color.White with { A = 0 }) * (0.3f + 0.4f * glint), Projectile.rotation, origin, 0.9f, fx, 0f);
                return false;
            }

            //活蝶：原色偏形态色，翅缘一线光谱；预闪时整只发白
            Color body = Color.Lerp(Color.White, core, 0.55f);
            if (preFlash) {
                body = Color.Lerp(body, Color.White, 0.8f);
            }
            Main.spriteBatch.Draw(tex, drawPos, src, rim * 0.8f, Projectile.rotation, origin, 1.12f, fx, 0f);
            Main.spriteBatch.Draw(tex, drawPos, src, body, Projectile.rotation, origin, 1f, fx, 0f);
            if (preFlash) {
                Main.spriteBatch.Draw(tex, drawPos, src, (Color.White with { A = 0 }) * 0.6f, Projectile.rotation, origin, 1f, fx, 0f);
            }
            return false;
        }
    }
}
