using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDeerclops.Core;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDeerclops.Projectiles
{
    /// <summary>
    /// 暗影之手。ai[0]=0视界掠袭/1压边驱赶 ai[1]=掠袭航向|驱赶目标 ai[2]=预兆帧；
    /// 掠袭：视界边缘外成形→红芒预兆→直线掠过；驱赶：从更远处把脱域者推回领域
    /// </summary>
    internal class DeerShadowHandProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.InsanityShadowHostile;

        private const float SweepSpeed = 26f;
        private const float SweepRange = 2500f;
        private const int FadeTime = 16;

        private bool IsBorderMode => Projectile.ai[0] == 1f;
        private float LaneAngle => Projectile.ai[1];
        private int TelegraphTime => Math.Max((int)Projectile.ai[2], 10);

        private ref float Elapsed => ref Projectile.localAI[0];

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 1600;

        public override void SetDefaults() {
            Projectile.width = 46;
            Projectile.height = 46;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 400;
            Projectile.alpha = 255;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        public override void AI() {
            Elapsed += 1f;
            int t = (int)Elapsed;

            if (IsBorderMode) {
                BorderAI(t);
            }
            else {
                SweepAI(t);
            }
        }

        #region 视界掠袭
        private void SweepAI(int t) {
            Vector2 lane = LaneAngle.ToRotationVector2();

            if (t == 1 && !Main.dedServ) {
                SoundEngine.PlaySound(SoundID.NPCDeath6 with { Volume = 0.32f, Pitch = -0.7f, MaxInstances = 5 }, Projectile.Center);
            }

            if (t < TelegraphTime) {
                //成形与蓄势：缓慢向后蜷缩(反向预备)
                Projectile.hostile = false;
                float p = t / (float)TelegraphTime;
                Projectile.alpha = (int)MathHelper.Lerp(255f, 70f, MathHelper.Clamp(p * 1.6f, 0f, 1f));
                float coil = (float)Math.Pow(p, 6) * 3.2f;
                Projectile.position -= lane * coil;
                Projectile.rotation = LaneAngle;

                //暗影渗出
                if (!Main.dedServ && Main.rand.NextBool(3)) {
                    Dust dust = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(20f, 20f),
                        DustID.Shadowflame, lane * Main.rand.NextFloat(0.5f, 1.5f), 140, default, Main.rand.NextFloat(0.9f, 1.5f));
                    dust.noGravity = true;
                }
                return;
            }

            if (t == TelegraphTime) {
                Projectile.velocity = lane * SweepSpeed;
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.DD2_GhastlyGlaiveImpactGhost with { Volume = 0.75f, Pitch = -0.25f, MaxInstances = 5 }, Projectile.Center);
                }
            }

            //掠袭中
            Projectile.hostile = true;
            Projectile.alpha = 40;
            Projectile.rotation = Projectile.velocity.ToRotation();

            float traveled = (t - TelegraphTime) * SweepSpeed;
            if (traveled > SweepRange && Projectile.timeLeft > FadeTime) {
                Projectile.timeLeft = FadeTime;
            }
            if (Projectile.timeLeft <= FadeTime) {
                Projectile.hostile = false;
                Projectile.alpha = (int)MathHelper.Lerp(255f, 40f, Projectile.timeLeft / (float)FadeTime);
            }

            if (!Main.dedServ && Main.rand.NextBool(2)) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(16f, 16f),
                    DustID.Shadowflame, -Projectile.velocity * 0.08f, 160, default, Main.rand.NextFloat(0.8f, 1.4f));
                dust.noGravity = true;
            }
        }
        #endregion

        #region 压边驱赶
        private void BorderAI(int t) {
            Projectile.hostile = t > 12;
            Projectile.alpha = (int)MathHelper.Lerp(255f, 70f, MathHelper.Clamp(t / 20f, 0f, 1f));

            int targetIdx = (int)Projectile.ai[1];
            Player target = targetIdx >= 0 && targetIdx < Main.maxPlayers ? Main.player[targetIdx] : null;

            if (t == 1 && !Main.dedServ) {
                SoundEngine.PlaySound(SoundID.NPCDeath6 with { Volume = 0.4f, Pitch = -0.55f, MaxInstances = 5 }, Projectile.Center);
            }

            //前90帧缓转追踪，之后锁直线(可甩)
            if (t < 90 && target != null && target.active && !target.dead) {
                Vector2 want = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX) * 7f;
                float angle = Projectile.velocity.ToRotation().AngleTowards(want.ToRotation(), 0.024f);
                float speed = MathHelper.Lerp(Projectile.velocity.Length(), 7f, 0.05f);
                Projectile.velocity = angle.ToRotationVector2() * speed;
            }
            Projectile.rotation = Projectile.velocity.ToRotation();

            if (Projectile.timeLeft <= FadeTime) {
                Projectile.hostile = false;
                Projectile.alpha = (int)MathHelper.Lerp(255f, 70f, Projectile.timeLeft / (float)FadeTime);
            }

            if (!Main.dedServ && Main.rand.NextBool(3)) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(14f, 14f),
                    DustID.Shadowflame, -Projectile.velocity * 0.1f, 150, default, Main.rand.NextFloat(0.8f, 1.3f));
                dust.noGravity = true;
            }
        }
        #endregion

        #region 绘制
        public override Color? GetAlpha(Color lightColor) {
            //暗影体不吃环境光，走自发暗紫
            return new Color(255, 255, 255, 255) * Projectile.Opacity;
        }

        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadProjectile(ProjectileID.InsanityShadowHostile);
            Texture2D tex = TextureAssets.Projectile[ProjectileID.InsanityShadowHostile].Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = tex.Size() / 2f;

            //本体朝向：向左时翻面
            float rot = Projectile.rotation;
            SpriteEffects fx = SpriteEffects.None;
            if (Math.Cos(rot) < 0) {
                rot += MathHelper.Pi;
                fx = SpriteEffects.FlipHorizontally;
            }

            int t = (int)Elapsed;
            bool dashing = !IsBorderMode && t >= TelegraphTime && Projectile.timeLeft > FadeTime;

            //掠袭拖影
            if (dashing) {
                for (int i = 1; i <= 4; i++) {
                    Vector2 ghostPos = drawPos - Projectile.velocity * (i * 0.55f);
                    Color ghostColor = DeerclopsMotion.ShadowViolet with { A = 0 } * (0.34f * (1f - i / 5f)) * Projectile.Opacity;
                    Main.EntitySpriteDraw(tex, ghostPos, null, ghostColor, rot, origin, Projectile.scale * (1f - i * 0.04f), fx, 0);
                }
            }

            //暗紫辉边(衬在本体下)
            Color aura = DeerclopsMotion.ShadowViolet with { A = 0 } * (0.5f * Projectile.Opacity);
            Main.EntitySpriteDraw(tex, drawPos, null, aura, rot, origin, Projectile.scale * 1.12f, fx, 0);

            //本体
            Main.EntitySpriteDraw(tex, drawPos, null, Projectile.GetAlpha(lightColor), rot, origin, Projectile.scale, fx, 0);

            //预兆期掌心红芒(可读威胁锚点)
            if (!IsBorderMode && t < TelegraphTime) {
                float p = t / (float)TelegraphTime;
                float pulse = 0.6f + 0.4f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 16f + Projectile.identity);
                Texture2D glow = CWRAsset.SoftGlow.Value;
                Color warn = DeerclopsMotion.GazeRed with { A = 0 } * (p * pulse * 0.9f);
                Main.EntitySpriteDraw(glow, drawPos, null, warn, 0f, glow.Size() / 2f, 0.32f * (0.5f + p * 0.5f), SpriteEffects.None, 0);
            }

            return false;
        }
        #endregion

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 8; i++) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Shadowflame,
                    Main.rand.NextVector2Circular(3f, 3f), 130, default, Main.rand.NextFloat(1f, 1.6f));
                dust.noGravity = true;
            }
        }

        #region 生成助手
        /// <summary>服务端生成一只掠袭手：从lane起点沿angle扫过；返回弹幕索引</summary>
        internal static int SpawnSweepHand(NPC npc, Vector2 start, float angle, int telegraph, int damage) {
            if (VaultUtils.isClient) {
                return -1;
            }
            return Projectile.NewProjectile(npc.GetSource_FromAI(), start, Vector2.Zero,
                ModContent.ProjectileType<DeerShadowHandProj>(), damage, 0f, Main.myPlayer,
                0f, angle, telegraph);
        }

        /// <summary>服务端生成一只压边手：从玩家背离boss一侧扑来，把人往领域里赶</summary>
        internal static void SpawnBorderHand(NPC npc, Player player) {
            if (VaultUtils.isClient) {
                return;
            }
            Vector2 away = (player.Center - npc.Center).SafeNormalize(Vector2.UnitX);
            Vector2 spawnPos = player.Center + away * 430f + away.RotatedBy(MathHelper.PiOver2) * Main.rand.NextFloat(-120f, 120f);
            Vector2 vel = (player.Center - spawnPos).SafeNormalize(Vector2.UnitX) * 6.5f;
            bool death = CWRRef.GetDeathMode() || CWRRef.GetBossRushActive();
            Projectile.NewProjectile(npc.GetSource_FromAI(), spawnPos, vel,
                ModContent.ProjectileType<DeerShadowHandProj>(), death ? 15 : 12, 0f, Main.myPlayer,
                1f, player.whoAmI, 0f);
        }
        #endregion
    }
}
