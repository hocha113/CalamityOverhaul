using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicCataclysm.Projectiles
{
    /// <summary>
    /// 燎原（地狱叉灾变）：贴地锚三段演出。<br/>
    /// 蓄势 40t：锚点地面火线向两侧蔓延（亮线 telegraph，无伤）；<br/>
    /// 爆发 150t：三波火浪自锚点沿地向两侧横扫（波前判定矩形随波推进，与焰墙同源绘制），
    /// 波间自天而降火雨（原版狱火弹 0.4 倍，owner 端生成）；<br/>
    /// 余韵 120t：焚野烬滩（全带 0.3 倍多跳 + 狱火），地面烬火粒子。<br/>
    /// 波前位置是相位帧的确定函数，各端同判同绘
    /// </summary>
    internal class GsInfernoForkWildfireDirector : GsCataclysmDirectorProj
    {
        public override int OmenTicks => 40;
        public override int MainTicks => 150;
        public override int AftermathTicks => 120;
        protected override int HitTickRate => 20;
        protected override float TickDamageMul => 1f;

        internal static readonly Color FireBright = new(255, 214, 128);
        internal static readonly Color FireMain = new(255, 122, 42);
        internal static readonly Color FireDeep = new(140, 44, 16);

        /// <summary>火浪单侧扫掠范围</summary>
        private const float WaveRange = 430f;
        /// <summary>单波时长（150 / 3）</summary>
        private const int WaveTicks = 50;
        /// <summary>波前判定半宽 / 焰墙可见半宽</summary>
        private const float FrontHalf = 24f;
        /// <summary>焰墙高</summary>
        private const float WallHeight = 92f;

        /// <summary>锚点地表 y（tile 各端一致，逐帧重算结果确定）</summary>
        private float GroundY => FindGroundY(new Vector2(Projectile.Center.X, Projectile.Center.Y - 32f));

        /// <summary>当前波前离锚点的距离（相位帧确定函数）</summary>
        private static float FrontDist(int tInWave)
            => WaveRange * VaultUtils.EaseOutQuad(MathHelper.Clamp(tInWave / (float)WaveTicks, 0f, 1f));

        //==================== 蓄势：火线蔓延 ====================

        protected override void OmenUpdate(int t) {
            if (t == 0 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item74 with { Volume = 0.9f, Pitch = -0.3f }, Projectile.Center);
            }
            if (VaultUtils.isServer) {
                return;
            }
            //telegraph：火线自锚点向两侧爬（进度确定函数，粒子预算 ≤2/帧）
            float reach = WaveRange * t / OmenTicks;
            float gy = GroundY;
            if (t % 2 == 0) {
                float at = Main.rand.NextFloat(-reach, reach);
                PRTLoader.NewParticle<PRT_Spark>(new Vector2(Projectile.Center.X + at, gy - 4f),
                    new Vector2(0f, -Main.rand.NextFloat(0.4f, 1.1f)), FireMain,
                    Main.rand.NextFloat(0.2f, 0.32f))?.Configure(false, Main.rand.Next(8, 14));
            }
            Lighting.AddLight(new Vector2(Projectile.Center.X, gy - 10f), FireMain.ToVector3() * 0.5f);
        }

        //==================== 爆发：三波火浪 + 火雨 ====================

        protected override void MainUpdate(int t) {
            int tInWave = t % WaveTicks;
            float gy = GroundY;
            if (tInWave == 0 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item45 with { Volume = 0.85f, Pitch = -0.2f, MaxInstances = 3 }, Projectile.Center);
                PRTLoader.NewParticle<PRT_Light>(new Vector2(Projectile.Center.X, gy - 20f),
                    Vector2.Zero, FireBright, 0.2f)?.Configure(10, 0.85f);
            }

            //波间火雨：owner 端每 12t 两滴，落点是相位帧的确定函数
            if (OwnerSide && t % 12 == 6) {
                for (int i = 0; i < 2; i++) {
                    float fx = Projectile.Center.X + ((t * 37 + i * 211) % (int)(WaveRange * 2f)) - WaveRange;
                    Vector2 from = new(fx, gy - 420f);
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), from,
                        new Vector2(0f, 9f), ProjectileID.InfernoFriendlyBolt,
                        ScaledDamage(0.4f), 1.5f, Projectile.owner);
                }
            }

            if (VaultUtils.isServer) {
                return;
            }
            //波前焰舌：贴着两个波前腾起（判定与可见同源）
            float d = FrontDist(tInWave);
            for (int s = -1; s <= 1; s += 2) {
                Vector2 front = new(Projectile.Center.X + s * d, gy);
                Lighting.AddLight(front - new Vector2(0f, 30f), FireMain.ToVector3() * 0.6f);
                if (Main.GameUpdateCount % 2 == 0) {
                    PRTLoader.NewParticle<PRT_HellFlame>(front + new Vector2(Main.rand.NextFloat(-FrontHalf, FrontHalf), -Main.rand.NextFloat(0f, 20f)),
                        new Vector2(s * Main.rand.NextFloat(0.4f, 1.2f), -Main.rand.NextFloat(1.5f, 3.2f)),
                        Main.rand.NextBool() ? FireMain : FireDeep, Main.rand.NextFloat(0.4f, 0.65f));
                }
            }
        }

        //==================== 余韵：焚野烬滩 ====================

        protected override void AftermathUpdate(int t) {
            if (VaultUtils.isServer) {
                return;
            }
            float gy = GroundY;
            //烬火粒子：全带零星升腾，随余韵渐稀
            int interval = 3 + t / 30;
            if (t % interval == 0) {
                float at = Main.rand.NextFloat(-WaveRange, WaveRange);
                PRTLoader.NewParticle<PRT_Light>(new Vector2(Projectile.Center.X + at, gy - Main.rand.NextFloat(2f, 14f)),
                    new Vector2(0f, -Main.rand.NextFloat(0.3f, 0.9f)), FireMain,
                    Main.rand.NextFloat(0.05f, 0.1f))?.Configure(Main.rand.Next(16, 28), 0.7f);
            }
            Lighting.AddLight(new Vector2(Projectile.Center.X, gy - 10f),
                FireMain.ToVector3() * 0.4f * (1f - t / (float)AftermathTicks));
        }

        //==================== 判定：波前矩形 / 烬滩条带 ====================

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float gy = GroundY;
            if (Phase == 1) {
                //波前判定：两个随波推进的立墙矩形
                int tInWave = (Elapsed - OmenTicks) % WaveTicks;
                float d = FrontDist(tInWave);
                for (int s = -1; s <= 1; s += 2) {
                    Rectangle wall = new((int)(Projectile.Center.X + s * d - FrontHalf),
                        (int)(gy - WallHeight), (int)(FrontHalf * 2f), (int)(WallHeight + 8f));
                    if (wall.Intersects(targetHitbox)) {
                        return true;
                    }
                }
                return false;
            }
            if (Phase == 2) {
                //烬滩条带：全带低矮判定
                Rectangle strip = new((int)(Projectile.Center.X - WaveRange),
                    (int)(gy - 36f), (int)(WaveRange * 2f), 44);
                return strip.Intersects(targetHitbox);
            }
            return false;
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            //波前全额、烬滩三成
            modifiers.FinalDamage *= Phase == 2 ? 0.3f : 0.85f;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.OnFire3, Phase == 2 ? 120 : 240);
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 3; i++) {
                    PRTLoader.NewParticle<PRT_HellFlame>(target.Center + Main.rand.NextVector2Circular(9f, 9f),
                        Main.rand.NextVector2Circular(1.6f, 1.6f) - new Vector2(0f, 1.4f),
                        FireMain, Main.rand.NextFloat(0.4f, 0.6f));
                }
            }
        }

        //==================== 绘制：telegraph 火线 / 焰墙 / 烬滩 ====================

        public override bool PreDraw(ref Color lightColor) {
            Texture2D glow = CWRAsset.SoftGlow.Value;
            SpriteBatch sb = Main.spriteBatch;
            float gy = GroundY;
            Vector2 anchorScreen = new(Projectile.Center.X - Main.screenPosition.X, gy - Main.screenPosition.Y);
            int elapsed = Elapsed;

            if (Phase == 0) {
                //telegraph：地面亮线随蓄势爬长，快到点时呼吸加急
                float reach = WaveRange * elapsed / OmenTicks;
                float urgency = 0.5f + 0.5f * MathF.Sin(elapsed * (0.2f + elapsed * 0.008f));
                for (int i = 0; i < 7; i++) {
                    float at = MathHelper.Lerp(-reach, reach, i / 6f);
                    sb.Draw(glow, anchorScreen + new Vector2(at, 0f), null,
                        FireMain with { A = 0 } * (0.35f * urgency), 0f, glow.Size() / 2f,
                        new Vector2(0.5f, 0.12f), SpriteEffects.None, 0f);
                }
                return false;
            }

            if (Phase == 1) {
                //焰墙：两个波前的三层辉墙（Hash01 定相闪烁，绘制零随机）
                int tInWave = (elapsed - OmenTicks) % WaveTicks;
                float d = FrontDist(tInWave);
                float flick = 0.85f + 0.15f * MathF.Sin(Main.GlobalTimeWrappedHourly * 21f + Hash01(3) * 6f);
                for (int s = -1; s <= 1; s += 2) {
                    Vector2 front = anchorScreen + new Vector2(s * d, -WallHeight * 0.4f);
                    sb.Draw(glow, front, null, FireDeep with { A = 0 } * (0.55f * flick), 0f,
                        glow.Size() / 2f, new Vector2(0.55f, 1.15f), SpriteEffects.None, 0f);
                    sb.Draw(glow, front, null, FireMain with { A = 0 } * (0.6f * flick), 0f,
                        glow.Size() / 2f, new Vector2(0.38f, 0.9f), SpriteEffects.None, 0f);
                    sb.Draw(glow, front + new Vector2(0f, WallHeight * 0.22f), null,
                        FireBright with { A = 0 } * (0.5f * flick), 0f,
                        glow.Size() / 2f, new Vector2(0.24f, 0.5f), SpriteEffects.None, 0f);
                }
                //扫过的余燃带
                float burned = MathF.Max(d - 60f, 0f);
                if (burned > 10f) {
                    sb.Draw(glow, anchorScreen, null, FireDeep with { A = 0 } * 0.3f, 0f,
                        glow.Size() / 2f, new Vector2(burned / glow.Width * 2f, 0.2f), SpriteEffects.None, 0f);
                }
                return false;
            }

            //烬滩：全带暗红余辉渐熄
            int tAfter = elapsed - OmenTicks - MainTicks;
            float fade = 1f - tAfter / (float)AftermathTicks;
            sb.Draw(glow, anchorScreen, null, FireDeep with { A = 0 } * (0.4f * fade), 0f,
                glow.Size() / 2f, new Vector2(WaveRange * 2f / glow.Width, 0.24f), SpriteEffects.None, 0f);
            sb.Draw(glow, anchorScreen, null, FireMain with { A = 0 } * (0.22f * fade), 0f,
                glow.Size() / 2f, new Vector2(WaveRange * 1.6f / glow.Width, 0.14f), SpriteEffects.None, 0f);
            return false;
        }
    }
}
