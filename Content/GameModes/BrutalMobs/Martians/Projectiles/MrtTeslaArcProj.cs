using InnoVault.Trails;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Martians.Projectiles
{
    /// <summary>
    /// 特斯拉电弧链：ai[0]/ai[1]=两端 NPC 索引，ai[2]=typeA*1000+typeB（索引+类型双校验，槽位复用不冒充）。
    /// 预热 <see cref="ArcWarmupFrames"/>（≥40）帧内细弧可见但无判定；通电后弧线=判定线；
    /// 两端拉开 <see cref="ArcBreakDistance"/> 或任一端失效即断链快速消散（判定随功率门同帧关闭）。
    /// 反制：击杀任一端、等两端自行走远、或不站在两个特斯拉源的连线上
    /// </summary>
    internal class MrtTeslaArcProj : ModProjectile
    {
        //豁免声明：闪电弧属「本身是光」的豁免类（镜像 BOSS-REWORK 契约 4.1 的信徒裁定），加色纯光合法，弹体遮挡像素要求不适用，实体感由两端 NPC 本体承载
        public override string Texture => CWRConstant.VaultPlaceholder2;

        [VaultLoaden(CWRConstant.Masking + "ThunderTrail")]
        private static Asset<Texture2D> ThunderTex = null;

        /// <summary>预热帧（任务要求 ≥40，各档位一律不缩短）</summary>
        internal const int ArcWarmupFrames = 42;
        /// <summary>通电持续帧</summary>
        internal const int ArcLiveFrames = 240;
        /// <summary>消散帧</summary>
        internal const int ArcFadeFrames = 10;
        /// <summary>总寿命（NPC 侧租约按此计）</summary>
        internal const int TotalLifeFrames = ArcWarmupFrames + ArcLiveFrames + ArcFadeFrames;
        /// <summary>断链距离：两端超过此距即断（结链距离在 MartiansNPC 侧更小，构成迟滞防临界抖动）</summary>
        internal const float ArcBreakDistance = 560f;
        /// <summary>满功率时的判定半宽（Colliding 按功率缩放读取）</summary>
        private const float ArcHitHalfWidth = 15f;
        /// <summary>判定功率门：可见形态与判定共用同一 power 值</summary>
        private const float DamagePowerGate = 0.75f;
        private const int ArcPointCount = 12;

        internal static readonly Color ArcBlue = new(120, 210, 255);

        private ref float Age => ref Projectile.localAI[0];
        /// <summary>消散段起点的功率快照（断链时从当前亮度淡出，不回跳）</summary>
        private ref float PeakPower => ref Projectile.localAI[1];

        private ThunderTrail mainTrail;
        private ThunderTrail coreTrail;
        private readonly Vector2[] arcPoints = new Vector2[ArcPointCount];
        /// <summary>0~1 当前功率，判定门与全部绘制读同一个值</summary>
        private float power;

        /// <summary>端点解析：索引+类型双校验，任一不符视为断链</summary>
        private NPC ResolveEnd(int index, int expectedType) {
            if (index < 0 || index >= Main.maxNPCs) {
                return null;
            }
            NPC npc = Main.npc[index];
            return npc.active && npc.type == expectedType ? npc : null;
        }

        private NPC EndA => ResolveEnd((int)Projectile.ai[0], (int)Projectile.ai[2] / 1000);
        private NPC EndB => ResolveEnd((int)Projectile.ai[1], (int)Projectile.ai[2] % 1000);

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 800;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 24;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalLifeFrames + 30;
            Projectile.netImportant = true;
        }

        public override void AI() {
            NPC endA = EndA;
            NPC endB = EndB;
            bool linked = endA != null && endB != null
                && Vector2.DistanceSquared(endA.Center, endB.Center) <= ArcBreakDistance * ArcBreakDistance;

            int fadeStart = TotalLifeFrames - ArcFadeFrames;
            if (!linked && Age < fadeStart) {
                //断链（端点被杀/失效/拉距）：立即进入消散段，判定随功率门同帧关闭
                Age = fadeStart;
            }

            if (Age == 0f && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item93 with { Volume = 0.45f, Pitch = -0.35f, MaxInstances = 5 }, Projectile.Center);
            }
            if ((int)Age == ArcWarmupFrames && linked && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.6f, Pitch = 0.15f, MaxInstances = 3 }, Projectile.Center);
            }

            if (endA != null && endB != null) {
                Projectile.Center = (endA.Center + endB.Center) / 2f;
            }

            //功率曲线：细弧预热 → 快速升压 → 满功率 → 消散（从峰值快照淡出）
            if (Age < fadeStart) {
                if (Age < ArcWarmupFrames) {
                    power = 0.08f + 0.24f * (Age / ArcWarmupFrames);
                }
                else {
                    float surge = MathHelper.Clamp((Age - ArcWarmupFrames) / 8f, 0f, 1f);
                    power = MathHelper.Lerp(0.32f, 1f, VaultUtils.EaseOutCubic(surge));
                }
                PeakPower = Math.Max(PeakPower, power);
            }
            else {
                power = PeakPower * MathHelper.Clamp(1f - (Age - fadeStart) / ArcFadeFrames, 0f, 1f);
            }

            Age++;
            if (Age >= TotalLifeFrames) {
                Projectile.Kill();
                return;
            }
            if (VaultUtils.isServer || endA == null || endB == null) {
                return;
            }

            //客户端表现：电弧路径 + 沿线光照 + 低频火花（≤1 粒/帧）
            BuildArcPath(endA.Center + new Vector2(0f, endA.gfxOffY), endB.Center + new Vector2(0f, endB.gfxOffY));
            for (int i = 0; i < 4; i++) {
                Lighting.AddLight(Vector2.Lerp(endA.Center, endB.Center, i / 3f), ArcBlue.ToVector3() * (0.4f * power));
            }
            if (power > 0.6f && Main.rand.NextBool(3)) {
                Vector2 sparkPos = Vector2.Lerp(endA.Center, endB.Center, Main.rand.NextFloat());
                Dust dust = Dust.NewDustPerfect(sparkPos, DustID.MartianSaucerSpark,
                    Main.rand.NextVector2Circular(2.5f, 2.5f), 0, default, Main.rand.NextFloat(0.8f, 1.3f));
                dust.noGravity = true;
            }
        }

        /// <summary>两端间采样电弧路径（数组复用，无每帧分配）</summary>
        private void BuildArcPath(Vector2 start, Vector2 end) {
            Vector2 dir = end - start;
            Vector2 perp = dir.SafeNormalize(Vector2.UnitY).RotatedBy(MathHelper.PiOver2);
            float waveSeed = Main.GlobalTimeWrappedHourly * 8f + Projectile.identity;

            for (int i = 0; i < ArcPointCount; i++) {
                float t = i / (float)(ArcPointCount - 1);
                //两端钉死在单位上，中段正弦摆动
                float envelope = (float)Math.Sin(t * MathHelper.Pi);
                float wave = (float)Math.Sin(waveSeed + t * 9f) * 11f * envelope * power;
                arcPoints[i] = start + dir * t + perp * wave;
            }

            if (mainTrail == null) {
                mainTrail = new ThunderTrail(ThunderTex, GetMainWidth, GetMainColor, GetArcAlpha) {
                    CanDraw = true,
                    UseNonOrAdd = true,
                    PartitionPointCount = 3,
                };
                mainTrail.SetRange((0, 8));
                mainTrail.SetExpandWidth(5);

                coreTrail = new ThunderTrail(ThunderTex, GetCoreWidth, GetCoreColor, GetArcAlpha) {
                    CanDraw = true,
                    UseNonOrAdd = true,
                    PartitionPointCount = 2,
                };
                coreTrail.SetRange((0, 4));
                coreTrail.SetExpandWidth(2);
            }

            mainTrail.BasePositions = arcPoints;
            coreTrail.BasePositions = arcPoints;
            if ((int)Age % 3 == 0) {
                mainTrail.RandomThunder();
                coreTrail.RandomThunder();
            }
        }

        private float GetMainWidth(float factor) => (10f + 6f * (float)Math.Sin(factor * MathHelper.Pi)) * power;
        private float GetCoreWidth(float factor) => (4f + 2.5f * (float)Math.Sin(factor * MathHelper.Pi)) * power;
        private Color GetMainColor(float factor) => ArcBlue;
        private Color GetCoreColor(float factor) => Color.White;
        private float GetArcAlpha(float factor) => power;

        /// <summary>预热/消散无伤：帧门（≥42）与功率门双保险，功率门即可见形态门</summary>
        public override bool? CanDamage()
            => Age >= ArcWarmupFrames && Age < TotalLifeFrames - ArcFadeFrames && power >= DamagePowerGate ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            NPC endA = EndA;
            NPC endB = EndB;
            if (endA == null || endB == null) {
                return false;
            }
            float collisionPoint = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                endA.Center, endB.Center, ArcHitHalfWidth * 2f * power, ref collisionPoint);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            //命中方本机结算，原生同步
            target.AddBuff(BuffID.Electrified, 90);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (power <= 0.02f) {
                return false;
            }

            mainTrail?.DrawThunder(Main.instance.GraphicsDevice);
            coreTrail?.DrawThunder(Main.instance.GraphicsDevice);

            //两端连接节点辉光（辉光敷料，实体感由端点 NPC 本体提供）
            NPC endA = EndA;
            NPC endB = EndB;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Color glowColor = ArcBlue with { A = 0 };
            float pulse = 1f + 0.18f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 22f + Projectile.identity);
            if (endA != null) {
                Main.EntitySpriteDraw(glow, endA.Center + new Vector2(0f, endA.gfxOffY) - Main.screenPosition, null,
                    glowColor * (0.85f * power), 0f, glow.Size() / 2f, 0.5f * power * pulse, SpriteEffects.None, 0);
            }
            if (endB != null) {
                Main.EntitySpriteDraw(glow, endB.Center + new Vector2(0f, endB.gfxOffY) - Main.screenPosition, null,
                    glowColor * (0.85f * power), 0f, glow.Size() / 2f, 0.5f * power * pulse, SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
