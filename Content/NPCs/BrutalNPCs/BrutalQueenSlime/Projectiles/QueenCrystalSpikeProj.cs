using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.Core;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.Projectiles
{
    /// <summary>
    /// 水晶尖刺：皇后重做后的主力弹幕，本体=原版蓝晶刺贴图(实体遮挡)+棱彩加色缀层。<br/>
    /// 出生有材质化生长段(无伤害)，生长本身即预告；ai[0]=模式 ai[1]=模式参数 ai[2]=色相种子。
    /// </summary>
    internal class QueenCrystalSpikeProj : ModProjectile, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        internal enum Mode : int
        {
            /// <summary>瞄准直刺：出生锁向，释放后复利续力</summary>
            Aimed = 0,
            /// <summary>天落刺：材质化后重力急坠(ai[1]=释放前额外悬帧)</summary>
            Rain = 1,
            /// <summary>绽放刺：悬停成花(ai[1]=悬帧数)后一帧全速外射</summary>
            Burst = 2,
            /// <summary>圆舞刺：外扩减速→凝滞(无伤)→向心收拢</summary>
            Converge = 3,
        }

        internal const int SpikeDamage = 30;

        /// <summary>材质化生长帧数，此间无伤害不碰地——生长即预告</summary>
        internal const int MaterializeTime = 8;

        private const int ConvergeOutTime = 24;
        private const int ConvergeFreezeTime = 30;
        private const float RainMaxFall = 26f;
        private const float BurstLaunchSpeed = 13.5f;

        private Mode ProjMode => (Mode)(int)Projectile.ai[0];
        private float ModeParam => Projectile.ai[1];
        private float HueSeed => Projectile.ai[2];
        private ref float Timer => ref Projectile.localAI[0];

        /// <summary>绽放刺总预备帧(材质化+悬停)</summary>
        internal static int BurstHangTotal(int hangExtra) => MaterializeTime + hangExtra;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 16;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 420;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        public override void AI() {
            Timer++;

            //首个本地帧：捕获出生速度(远端从生成包得到同值，各端一致)
            if (Timer == 1) {
                Projectile.localAI[1] = Projectile.velocity.Length();
                if (ProjMode == Mode.Converge) {
                    //圆舞刺记录出生原点(此刻仍在出生点)
                    Projectile.localAI[1] = Projectile.Center.X;
                    Projectile.localAI[2] = Projectile.Center.Y;
                }
                if (!VaultUtils.isServer) {
                    Main.instance.LoadProjectile(ProjectileID.QueenSlimeMinionBlueSpike);
                }
            }

            switch (ProjMode) {
                case Mode.Aimed:
                    UpdateAimed();
                    break;
                case Mode.Rain:
                    UpdateRain();
                    break;
                case Mode.Burst:
                    UpdateBurst();
                    break;
                case Mode.Converge:
                    UpdateConverge();
                    break;
            }

            //贴图朝上，指向速度方向
            if (Projectile.velocity.Length() > 0.1f) {
                Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            }

            Lighting.AddLight(Projectile.Center, QueenMotion.PrismHue(HueSeed).ToVector3() * 0.32f);

            //飞行期尾向光尘(速度门控)
            if (!VaultUtils.isServer && Projectile.velocity.Length() > 6f && Main.rand.NextBool(3)) {
                Dust d = Dust.NewDustPerfect(Projectile.Center - Projectile.velocity * 0.4f, DustID.TintableDust,
                    -Projectile.velocity * 0.12f, 150, QueenMotion.GetQueenDustColor(), 1.05f);
                d.noGravity = true;
            }
        }

        /// <summary>瞄准直刺：材质化间 15% 速慢推(公平阀)，释放全速+复利续力</summary>
        private void UpdateAimed() {
            float spawnSpeed = Projectile.localAI[1];
            if (Timer <= MaterializeTime) {
                Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.UnitY) * spawnSpeed * 0.15f;
                return;
            }
            if (Timer == MaterializeTime + 1) {
                Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.UnitY) * spawnSpeed;
                PlayLaunchCue(0.5f);
            }
            //复利续力，上限1.4倍
            if (Projectile.velocity.Length() < spawnSpeed * 1.4f) {
                Projectile.velocity *= 1.012f;
            }
            Projectile.tileCollide = Timer > MaterializeTime + 6;
        }

        /// <summary>天落刺：悬于空中生长，释放后重力急坠</summary>
        private void UpdateRain() {
            int hold = MaterializeTime + (int)ModeParam;
            if (Timer <= hold) {
                Projectile.velocity = new Vector2(0f, 0.4f);
                return;
            }
            if (Timer == hold + 1) {
                PlayLaunchCue(0.35f);
            }
            Projectile.velocity.Y += 0.52f;
            if (Projectile.velocity.Y > RainMaxFall) {
                Projectile.velocity.Y = RainMaxFall;
            }
            Projectile.tileCollide = Timer > hold + 5;
        }

        /// <summary>绽放刺：悬停成花(方向即出生方向)，悬满一帧全速外射</summary>
        private void UpdateBurst() {
            int hang = BurstHangTotal((int)ModeParam);
            if (Timer <= hang) {
                //悬停微漂，保方向信息
                Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.UnitY) * 0.42f;
                return;
            }
            if (Timer == hang + 1) {
                Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.UnitY) * BurstLaunchSpeed;
                PlayLaunchCue(0.55f);
            }
            if (Projectile.velocity.Length() < BurstLaunchSpeed * 1.35f) {
                Projectile.velocity *= 1.014f;
            }
            Projectile.tileCollide = Timer > hang + 7;
        }

        /// <summary>圆舞刺：外扩减速→凝滞→向心收拢，穿过原点不折返</summary>
        private void UpdateConverge() {
            if (Timer <= ConvergeOutTime) {
                Projectile.velocity *= 0.915f;
            }
            else if (Timer <= ConvergeOutTime + ConvergeFreezeTime) {
                Projectile.velocity *= 0.7f;
                //凝滞末拍轻微回缩预备
                if (Timer == ConvergeOutTime + ConvergeFreezeTime - 4) {
                    Vector2 origin = new Vector2(Projectile.localAI[1], Projectile.localAI[2]);
                    Projectile.velocity = (Projectile.Center - origin).SafeNormalize(Vector2.UnitY) * 1.6f;
                }
            }
            else if (Timer == ConvergeOutTime + ConvergeFreezeTime + 1) {
                Vector2 origin = new Vector2(Projectile.localAI[1], Projectile.localAI[2]);
                Projectile.velocity = (origin - Projectile.Center).SafeNormalize(Vector2.UnitY) * 11.5f;
                PlayLaunchCue(0.45f);
            }
            else {
                Projectile.tileCollide = true;
            }
        }

        private void PlayLaunchCue(float volume) {
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item27 with { Volume = volume, Pitch = 0.55f, MaxInstances = 5 }, Projectile.Center);
            }
        }

        /// <summary>生长段与圆舞凝滞段无伤害，伤害窗对齐视觉</summary>
        public override bool? CanDamage() {
            if (Timer <= MaterializeTime) {
                return false;
            }
            return ProjMode switch {
                Mode.Burst when Timer <= BurstHangTotal((int)ModeParam) => false,
                Mode.Rain when Timer <= MaterializeTime + (int)ModeParam => false,
                Mode.Converge when Timer > ConvergeOutTime && Timer <= ConvergeOutTime + ConvergeFreezeTime => false,
                _ => null,
            };
        }

        public override bool OnTileCollide(Vector2 oldVelocity) => true;

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            QueenMotion.CrystalShatterBurst(Projectile.Center, 0.42f, HueSeed, playSound: false);
            SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.3f, Pitch = 0.5f, MaxInstances = 4 }, Projectile.Center);
        }

        /// <summary>生长进度 0~1(绘制缩放)</summary>
        private float GrowP => MathHelper.Clamp(Timer / (float)MaterializeTime, 0f, 1f);

        /// <summary>凝滞/悬停期的闪烁强度</summary>
        private float HoldTwinkle() {
            bool holding = ProjMode switch {
                Mode.Burst => Timer <= BurstHangTotal((int)ModeParam),
                Mode.Converge => Timer > ConvergeOutTime && Timer <= ConvergeOutTime + ConvergeFreezeTime,
                Mode.Rain => Timer <= MaterializeTime + (int)ModeParam,
                _ => false,
            };
            return holding ? 0.72f + 0.28f * (float)Math.Sin(Timer * 0.55f + Projectile.whoAmI) : 1f;
        }

        /// <summary>本体：原版蓝晶刺贴图(实体遮挡)+同材质残影链</summary>
        public override bool PreDraw(ref Color lightColor) {
            Texture2D spike = TextureAssets.Projectile[ProjectileID.QueenSlimeMinionBlueSpike].Value;
            Rectangle rect = spike.Frame();
            Vector2 origin = rect.Size() / 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Color hue = QueenMotion.PrismHue(HueSeed);
            //实体染色：留 alpha 保遮挡，向棱彩微调
            Color bodyColor = Color.Lerp(Color.White, hue, 0.35f);
            bodyColor = Color.Lerp(bodyColor, lightColor, 0.25f);
            float grow = GrowP;
            float twinkle = HoldTwinkle();
            float scale = Projectile.scale * (0.45f + 0.55f * grow);

            //残影链：同贴图缩淡重画(材质一致，横轴比≈0.85)
            if (Projectile.velocity.Length() > 5f) {
                for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                    if (Projectile.oldPos[i] == Vector2.Zero) {
                        continue;
                    }
                    Vector2 ghostPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                    float fade = 1f - i / (float)Projectile.oldPos.Length;
                    Main.EntitySpriteDraw(spike, ghostPos, rect, bodyColor * (0.42f * fade), Projectile.rotation,
                        origin, scale * (0.6f + 0.3f * fade), SpriteEffects.None, 0);
                }
            }

            //本体
            Main.EntitySpriteDraw(spike, drawPos, rect, bodyColor * twinkle, Projectile.rotation,
                origin, scale, SpriteEffects.None, 0);
            return false;
        }

        /// <summary>棱彩缀层(真加色批)：核心微光+晶面星芒，占比压在本体之下</summary>
        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Texture2D star = CWRAsset.StarGlow01.Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Color hue = QueenMotion.PrismHue(HueSeed);
            float grow = GrowP;
            float twinkle = HoldTwinkle();

            //速度拉伸的核心微光(体积感衬底)
            float speed = Projectile.velocity.Length();
            float stretch = MathHelper.Clamp(speed * 0.035f, 0f, 0.6f);
            Vector2 glowScale = new Vector2(0.24f - stretch * 0.06f, 0.24f + stretch * 0.3f) * grow;
            float glowRot = speed > 0.5f ? Projectile.velocity.ToRotation() - MathHelper.PiOver2 : Projectile.rotation;
            spriteBatch.Draw(glow, drawPos, null, hue * (0.55f * twinkle * grow), glowRot,
                glow.Size() / 2f, glowScale, SpriteEffects.None, 0f);

            //晶面星芒(生长期更亮，读作"正在成形")
            float starGlint = grow < 1f ? 1.25f : 0.7f;
            spriteBatch.Draw(star, drawPos, null, hue * (starGlint * twinkle * 0.8f),
                Projectile.rotation + Timer * 0.05f, star.Size() / 2f, 0.24f * grow, SpriteEffects.None, 0f);
        }
    }
}
