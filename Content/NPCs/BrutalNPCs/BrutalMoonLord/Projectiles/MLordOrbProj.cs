using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Rendering;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Projectiles
{
    /// <summary>
    /// 幻影星球：ai[0]=宿主 whoAmI，ai[1]=模式 0持握齐射/1引力井环绕，ai[2]=模式0的齐射延迟帧。
    /// 持握期贴宿主呼吸，齐射拍各端按宿主目标确定性放飞
    /// </summary>
    internal class MLordOrbProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        private const float LaunchSpeed = 12.5f;
        private const float MaxSpeed = 19f;

        private ref float Timer => ref Projectile.localAI[0];
        private ref float Launched => ref Projectile.localAI[1];
        private NPC Host => ((int)Projectile.ai[0]).TryGetNPC(out NPC n) ? n : null;
        private bool WellMode => Projectile.ai[1] == 1f;

        private Vector2 heldOffset;
        private bool offsetCaptured;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 34;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 640;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        public override void AI() {
            Timer++;
            Projectile.rotation += 0.045f + Projectile.velocity.Length() * 0.004f;
            Lighting.AddLight(Projectile.Center, MLordDirector.Phantasmal.ToVector3() * 0.45f);

            if (WellMode) {
                WellOrbitAI();
            }
            else {
                HeldVolleyAI();
            }

            //相位明灭星屑
            if (!VaultUtils.isServer && Main.rand.NextBool(5)) {
                PRTLoader.NewParticle<PRT_HeavenfallStar>(
                    Projectile.Center + Main.rand.NextVector2Circular(12f, 12f),
                    -Projectile.velocity * 0.1f, MLordDirector.Phantasmal,
                    Main.rand.NextFloat(0.35f, 0.6f))?.Configure(false, Main.rand.Next(10, 18));
            }
        }

        /// <summary>
        /// 持握→齐射：贴宿主呼吸，齐射由权威端裁定写速度并 netUpdate 广播，
        /// 客户端凭"速度非零"识别放飞（避免两端各自预判目标造成弹道分叉）
        /// </summary>
        private void HeldVolleyAI() {
            NPC host = Host;
            int launchDelay = (int)Projectile.ai[2];

            if (Launched == 0f) {
                //客户端：收到权威端速度即视作已放飞
                if (Projectile.velocity.LengthSquared() > 1f) {
                    Launched = 1f;
                    if (!VaultUtils.isServer) {
                        SoundEngine.PlaySound(SoundID.Item125 with { Volume = 0.55f, Pitch = 0.2f, MaxInstances = 6 }, Projectile.Center);
                    }
                    return;
                }

                if (!host.Alives()) {
                    //宿主没了：权威端就地放飞
                    if (!VaultUtils.isClient) {
                        Projectile.velocity = Vector2.UnitY * LaunchSpeed;
                        Projectile.netUpdate = true;
                    }
                    return;
                }

                if (!offsetCaptured) {
                    heldOffset = Projectile.Center - host.Center;
                    offsetCaptured = true;
                }

                //持握呼吸：轻微离心张合
                float breath = 1f + 0.06f * (float)Math.Sin(Timer * 0.11f + Projectile.whoAmI * 0.7f);
                Projectile.Center = host.Center + heldOffset * breath;
                Projectile.velocity = Vector2.Zero;

                //权威端裁定放飞
                if (!VaultUtils.isClient && Timer >= launchDelay) {
                    Launched = 1f;
                    int targetIndex = host.target;
                    Vector2 aim = Vector2.UnitY;
                    if (targetIndex >= 0 && targetIndex < Main.maxPlayers) {
                        Player target = Main.player[targetIndex];
                        if (target.active && !target.dead) {
                            aim = (target.Center + target.velocity * 11f - Projectile.Center).SafeNormalize(Vector2.UnitY);
                        }
                    }
                    Projectile.velocity = aim * LaunchSpeed;
                    Projectile.netUpdate = true;
                    if (!VaultUtils.isServer) {
                        SoundEngine.PlaySound(SoundID.Item125 with { Volume = 0.55f, Pitch = 0.2f, MaxInstances = 6 }, Projectile.Center);
                    }
                }
                return;
            }

            //飞行段复合加速，绝不匀速
            if (Projectile.velocity.Length() < MaxSpeed) {
                Projectile.velocity *= 1.013f;
            }
        }

        /// <summary>引力井环绕：向最近引力井加速，井灭后直线甩出。轨道对初值敏感，权威端周期广播矫偏</summary>
        private void WellOrbitAI() {
            Projectile wellProj = FindNearestWell();
            if (wellProj != null) {
                Vector2 toWell = wellProj.Center - Projectile.Center;
                float dist = Math.Max(toWell.Length(), 60f);
                //平方衰减向心力，近处收紧
                float gravity = MathHelper.Clamp(9000f / (dist * dist) * 6f, 0.08f, 0.8f);
                Projectile.velocity += toWell.SafeNormalize(Vector2.Zero) * gravity;
                if (Projectile.velocity.Length() > 17f) {
                    Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.Zero) * 17f;
                }
                //椭圆轨道混沌敏感：权威端 30 帧一次位置矫偏
                if (!VaultUtils.isClient && (int)Timer % 30 == 0) {
                    Projectile.netUpdate = true;
                }
            }
            else if (Timer > 40f && Projectile.timeLeft > 70) {
                //井没了：甩出后限时消散
                Projectile.timeLeft = 70;
            }
        }

        private Projectile FindNearestWell() {
            int type = ModContent.ProjectileType<MLordGravityWellProj>();
            Projectile best = null;
            float bestDist = float.MaxValue;
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (!p.active || p.type != type) {
                    continue;
                }
                float dist = Projectile.DistanceSQ(p.Center);
                if (dist < bestDist) {
                    bestDist = dist;
                    best = p;
                }
            }
            return best;
        }

        //持握期无伤，出手后判定
        public override bool? CanDamage() {
            if (WellMode) {
                return Timer > 12f ? null : false;
            }
            return Launched == 1f ? null : false;
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            MLordScreenFX.StarBurst(Projectile.Center, 0.55f, 7);
            SoundEngine.PlaySound(SoundID.Item118 with { Volume = 0.4f, Pitch = 0.3f, MaxInstances = 6 }, Projectile.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D glow = CWRAsset.DiffusionCircle?.Value;
            Texture2D star = CWRAsset.StarTexture?.Value;
            if (glow == null || star == null) {
                return false;
            }

            Vector2 screenPos = Projectile.Center - Main.screenPosition;
            float speed = Projectile.velocity.Length();
            float phase = 0.82f + 0.18f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 9f + Projectile.whoAmI * 1.3f);

            //残影拖体（速度门控）：逐段连成收锥光带，段间无断口
            if (speed > 4f) {
                Texture2D soft = CWRAsset.SoftGlow?.Value;
                if (soft != null) {
                    Vector2 prev = Projectile.Center;
                    for (int i = 1; i < Projectile.oldPos.Length; i++) {
                        //trail 缓存未填满前是零向量，画出去会拉一条通向世界原点的巨型光带
                        if (Projectile.oldPos[i] == Vector2.Zero) {
                            break;
                        }
                        Vector2 cur = Projectile.oldPos[i] + Projectile.Size / 2f;
                        Vector2 seg = prev - cur;
                        float segLen = seg.Length();
                        if (segLen > 0.5f) {
                            float fade = 1f - i / (float)Projectile.oldPos.Length;
                            Vector2 mid = (prev + cur) * 0.5f - Main.screenPosition;
                            //软圆点沿段拉伸+半段重叠 → 连续锥形拖带
                            Main.EntitySpriteDraw(soft, mid, null,
                                MLordDirector.DeepViolet with { A = 0 } * (0.34f * fade), seg.ToRotation(),
                                soft.Size() / 2f, new Vector2(segLen * 1.7f / soft.Width, (26f * fade + 5f) / soft.Height),
                                SpriteEffects.None, 0);
                        }
                        prev = cur;
                    }
                }
            }

            //速度各向异性拉伸主体
            float stretch = MathHelper.Clamp(speed * 0.02f, 0f, 0.55f);
            Vector2 bodyScale = new Vector2(0.34f * (1f + stretch), 0.34f * (1f - stretch * 0.4f));
            float bodyRot = speed > 2f ? Projectile.velocity.ToRotation() : Projectile.rotation;

            Main.EntitySpriteDraw(glow, screenPos, null, MLordDirector.DeepViolet with { A = 0 } * (0.85f * phase),
                bodyRot, glow.Size() / 2f, bodyScale * 1.7f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, screenPos, null, MLordDirector.Phantasmal with { A = 0 } * phase,
                bodyRot, glow.Size() / 2f, bodyScale, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(star, screenPos, null, MLordDirector.MoonWhite with { A = 0 } * (0.75f * phase),
                Projectile.rotation, star.Size() / 2f, 0.24f, SpriteEffects.None, 0);
            return false;
        }
    }
}
