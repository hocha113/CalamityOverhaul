using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalWallOfFlesh.Rendering;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalWallOfFlesh.Projectiles
{
    /// <summary>
    /// 血肉尖刺：血泊预告→骨刺喷发→保持→缩回。
    /// ai[0]=-1自地板向上/+1自顶板向下 ai[1]=波序(演出错相)。
    /// 生成位置即锚点(地表/顶板)
    /// </summary>
    internal class WofFleshSpikeProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        private const int TelegraphTime = 24;
        private const int ThrustTime = 8;
        private const int HoldTime = 26;
        private const int RetractTime = 18;
        private const int TotalTime = TelegraphTime + ThrustTime + HoldTime + RetractTime;
        private const float MaxLength = 300f;
        private const float SpikeWidth = 34f;

        private ref float Timer => ref Projectile.localAI[0];
        /// <summary>喷发方向：-1朝上(地板刺) +1朝下(顶板刺)</summary>
        private float ThrustSign => Projectile.ai[0] > 0f ? 1f : -1f;

        /// <summary>当前刺长</summary>
        private float length;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 30;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalTime + 10;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        public override void AI() {
            Timer++;

            if (Timer <= TelegraphTime) {
                length = 0f;
                //预告：血泊蠕动+碎屑顶起
                if (!VaultUtils.isServer && WofMotionFX.OnScreen(Projectile.Center, 100f)) {
                    if (Timer % 4 == 0) {
                        PRTLoader.NewParticle<PRT_HeartcarverDroplet>(
                            Projectile.Center + new Vector2(Main.rand.NextFloat(-SpikeWidth, SpikeWidth), 0f),
                            new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), -ThrustSign * Main.rand.NextFloat(1.5f, 3f)),
                            WofMotionFX.BloodMid, Main.rand.NextFloat(0.7f, 1.1f))?.Configure(Main.rand.Next(14, 22), 0.32f);
                    }
                    Lighting.AddLight(Projectile.Center, WofMotionFX.BloodHot.ToVector3() * (0.4f * Timer / TelegraphTime));
                }
                return;
            }

            int sinceThrust = (int)Timer - TelegraphTime;
            if (sinceThrust <= ThrustTime) {
                //喷发：极锐缓出
                float t = sinceThrust / (float)ThrustTime;
                length = MaxLength * (1f - (float)Math.Pow(1f - t, 5));
                if (sinceThrust == 1 && !VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.NPCHit18 with { Pitch = -0.4f, Volume = 0.9f, MaxInstances = 5 }, Projectile.Center);
                    SoundEngine.PlaySound(SoundID.Item17 with { Pitch = -0.3f, Volume = 0.7f, MaxInstances = 5 }, Projectile.Center);
                    WofMotionFX.SpawnBloodBurst(Projectile.Center, 0.8f, new Vector2(0f, -ThrustSign));
                    WofMotionFX.CameraPunch(Projectile.Center, 2.6f, 8, "WofSpikeErupt", new Vector2(0f, -ThrustSign));
                }
                return;
            }
            if (sinceThrust <= ThrustTime + HoldTime) {
                length = MaxLength;
                return;
            }

            //缩回
            float retractT = (sinceThrust - ThrustTime - HoldTime) / (float)RetractTime;
            length = MaxLength * (1f - retractT * retractT);
            if (length < 4f) {
                length = 0f;
            }
        }

        /// <summary>只在喷发与保持期造成伤害</summary>
        public override bool? CanDamage() {
            int sinceThrust = (int)Timer - TelegraphTime;
            return sinceThrust > 0 && sinceThrust <= ThrustTime + HoldTime ? null : false;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (length < 8f) {
                return false;
            }
            Vector2 tip = Projectile.Center - Vector2.UnitY * ThrustSign * length;
            float p = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                Projectile.Center, tip, SpikeWidth * 0.8f, ref p);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D spike = CWRAsset.Extra_98.Value;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Vector2 anchorScreen = Projectile.Center - Main.screenPosition;
            float up = -ThrustSign;

            //预告血泊：压扁的暗血光斑+呼吸
            if (Timer <= TelegraphTime) {
                float t = Timer / (float)TelegraphTime;
                float breathe = 1f + 0.12f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 22f + Projectile.ai[1] * 2f);
                Color poolCol = WofMotionFX.BloodHot with { A = 0 } * (0.55f * t);
                Main.EntitySpriteDraw(glow, anchorScreen, null, poolCol, 0f, glow.Size() / 2f,
                    new Vector2(2.4f, 0.6f) * breathe * t, SpriteEffects.None, 0);
                //警戒线：即将喷发的竖向细光
                if (t > 0.55f) {
                    Color warn = WofMotionFX.BloodHot with { A = 0 } * ((t - 0.55f) / 0.45f * 0.5f);
                    Main.EntitySpriteDraw(glow, anchorScreen + new Vector2(0f, up * MaxLength * 0.5f), null, warn, 0f,
                        glow.Size() / 2f, new Vector2(0.35f, MaxLength / glow.Height * 1.1f), SpriteEffects.None, 0);
                }
                return false;
            }

            if (length < 4f) {
                return false;
            }

            //刺体：暗肉鞘+骨白芯，双层收锥
            float rot = up > 0 ? 0f : MathHelper.Pi;
            Vector2 mid = anchorScreen + new Vector2(0f, up * length * 0.5f);
            float lenScale = length / spike.Height;
            //肉鞘
            Main.EntitySpriteDraw(spike, mid, null, WofMotionFX.BloodDark, rot, spike.Size() / 2f,
                new Vector2(SpikeWidth / spike.Width * 1.5f, lenScale * 1.05f), SpriteEffects.None, 0);
            //骨白芯(尖端更亮)
            Main.EntitySpriteDraw(spike, mid + new Vector2(0f, up * length * 0.08f), null,
                new Color(214, 176, 160), rot, spike.Size() / 2f,
                new Vector2(SpikeWidth / spike.Width * 0.7f, lenScale * 0.92f), SpriteEffects.None, 0);
            //根部血口
            Main.EntitySpriteDraw(glow, anchorScreen, null, WofMotionFX.BloodHot with { A = 0 } * 0.5f, 0f,
                glow.Size() / 2f, new Vector2(1.8f, 0.5f), SpriteEffects.None, 0);
            return false;
        }
    }
}
