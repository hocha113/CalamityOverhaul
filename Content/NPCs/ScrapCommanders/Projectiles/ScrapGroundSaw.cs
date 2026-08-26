using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.ScrapCommanders.Projectiles
{
    /// <summary>
    /// 脱链地锯犬·回旋镖：去程贴地滚向玩家一侧（撞墙跳一下翻越、磨地甩火星），
    /// 滚够路程后升空回程斜切，沿途二次判定，回到统帅锯臂上咔哒归位。
    /// ai[0]=滚动方向，ai[1]=统帅 whoAmI（回收锚）
    /// </summary>
    internal class ScrapGroundSaw : ScrapModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int LifeFrames = 170;
        /// <summary>去程滚地的帧数，之后转回程</summary>
        private const int RollFrames = 85;
        private float RollDir => Projectile.ai[0];
        private NPC Boss => Main.npc[(int)Projectile.ai[1]];
        private bool Returning => Projectile.localAI[0] != 0f;

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.aiStyle = -1;
            Projectile.timeLeft = LifeFrames;
        }

        public override void AI() {
            //==================== 回程斜切 ====================
            if (Returning) {
                NPC boss = Boss;
                if (boss == null || !boss.active) {
                    //统帅没了就地熄转
                    Projectile.velocity *= 0.94f;
                    Projectile.rotation += RollDir * 0.2f;
                    if (Projectile.timeLeft > 20) {
                        Projectile.timeLeft = 20;
                    }
                    return;
                }
                Vector2 home = boss.ModNPC is ScrapCommander owner
                    ? owner.GetArmPos(ScrapCommander.ArmSaw) : boss.Center;
                Vector2 want = (home - Projectile.Center).SafeNormalize(Vector2.UnitY) * 14.5f;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, want, 0.08f);
                Projectile.rotation += RollDir * 0.55f;
                if (!Main.dedServ && Projectile.timeLeft % 3 == 0) {
                    PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                        -Projectile.velocity.RotatedBy(Main.rand.NextFloat(-0.4f, 0.4f)) * 0.2f,
                        new Color(255, 150, 58) * 0.7f, Main.rand.NextFloat(0.4f, 0.7f))
                        ?.Configure(false, Main.rand.Next(8, 12));
                }
                //归臂：咔哒收锯
                if (Vector2.Distance(Projectile.Center, home) < 66f) {
                    Terraria.Audio.SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.5f, Pitch = 0.25f, MaxInstances = 2 }, home);
                    Projectile.Kill();
                }
                return;
            }
            //到点起跳回程：脱地升空
            if (Projectile.timeLeft <= LifeFrames - RollFrames) {
                Projectile.localAI[0] = 1f;
                Projectile.tileCollide = false;
                Projectile.velocity = new Vector2(Projectile.velocity.X * 0.5f, -9f);
                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item22 with { Volume = 0.5f, Pitch = 0.5f, MaxInstances = 2 }, Projectile.Center);
                return;
            }

            //==================== 去程贴地滚 ====================
            Projectile.velocity.X = MathHelper.Clamp(
                Projectile.velocity.X + RollDir * 0.12f, -10.5f, 10.5f);
            Projectile.velocity.Y = MathF.Min(Projectile.velocity.Y + 0.5f, 16f);
            //转速跟着地速走
            Projectile.rotation += Projectile.velocity.X * 0.06f + RollDir * 0.18f;

            bool grounded = Projectile.velocity.Y == 0f;
            if (!Main.dedServ && grounded) {
                if (Projectile.timeLeft % 3 == 0) {
                    PRTLoader.NewParticle<PRT_Spark>(
                        Projectile.Center + new Vector2(-RollDir * 12f, 16f),
                        new Vector2(-RollDir * Main.rand.NextFloat(2f, 5f), -Main.rand.NextFloat(1f, 3f)),
                        Color.Lerp(new Color(255, 150, 58), Color.White, Main.rand.NextFloat(0.4f)),
                        Main.rand.NextFloat(0.5f, 0.9f))?.Configure(true, Main.rand.Next(8, 14));
                }
                //磨地剥落：锯口啃出的地皮碎屑
                if (Projectile.timeLeft % 7 == 0) {
                    PRTLoader.NewParticle<PRT_SHPCHeavySpall>(
                        Projectile.Center + new Vector2(-RollDir * 10f, 14f),
                        new Vector2(-RollDir * Main.rand.NextFloat(1.5f, 3f), -Main.rand.NextFloat(2f, 4f)),
                        new Color(255, 150, 58), Main.rand.NextFloat(0.35f, 0.55f))
                        ?.Configure(new Color(96, 64, 46), Main.rand.Next(16, 26), 0.32f);
                }
            }
            if (Projectile.timeLeft % 24 == 0) {
                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item22 with {
                    Volume = 0.3f,
                    Pitch = 0.3f,
                    MaxInstances = 2
                }, Projectile.Center);
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            //撞墙跳一下翻越
            if (Projectile.velocity.X != oldVelocity.X && MathF.Abs(oldVelocity.X) > 0.5f) {
                Projectile.velocity.X = oldVelocity.X;
                Projectile.velocity.Y = -7.5f;
            }
            //落地滚
            if (Projectile.velocity.Y != oldVelocity.Y && oldVelocity.Y > 1f) {
                Projectile.velocity.Y = 0f;
            }
            return false;
        }

        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadNPC(NPCID.PrimeSaw);
            Texture2D tex = TextureAssets.Npc[NPCID.PrimeSaw]?.Value;
            if (tex == null) {
                return false;
            }
            int frameCount = Main.npcFrameCount[NPCID.PrimeSaw];
            int frameIndex = (int)(Main.GlobalTimeWrappedHourly * 20f + Projectile.identity) % frameCount;
            int frameH = tex.Height / frameCount;
            Rectangle frame = new(0, frameH * frameIndex, tex.Width, frameH);
            float fade = MathHelper.Clamp(Projectile.timeLeft / 20f, 0f, 1f);

            //转动热涂抹：身后两张递减的加色鬼影（预乘批 A=0 加色技巧）
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow != null) {
                for (int j = 1; j <= 2; j++) {
                    Vector2 back = Projectile.Center - Projectile.velocity * (j * 1.8f);
                    Main.spriteBatch.Draw(glow, back - Main.screenPosition, null,
                        new Color(255, 130, 48, 0) * (0.3f * fade / j), 0f,
                        glow.Size() * 0.5f, new Vector2(46f / glow.Width * 2f), SpriteEffects.None, 0f);
                }
            }

            Color tint = lightColor.MultiplyRGB(new Color(214, 158, 118)) * fade;
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, frame, tint,
                Projectile.rotation, frame.Size() * 0.5f, 0.62f, SpriteEffects.None, 0);
            return false;
        }

        public override void OnKill(int timeLeft) {
            ScrapVfx.MetalExplosion(Projectile.Center, 0.55f);
        }
    }
}
