using CalamityOverhaul.Content.Items.Magic.Everdeeps;
using CalamityOverhaul.Content.Items.Melee.Abyssrends;
using CalamityOverhaul.Content.NPCs.SeaShrimp.Core;
using CalamityOverhaul.Content.NPCs.SeaShrimp.Kinematics;
using CalamityOverhaul.Content.NPCs.SeaShrimp.Rendering;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using InnoVault.Trails;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.SeaShrimp.Projectiles
{
    /// <summary>
    /// 巨型雷泡：泡泡大炮的主体。生长期挂在 boss 钳口无害膨胀（内部电流游走即预告），
    /// 拍击帧被权威端写入速度与飞行帧数，直线飞向拍出瞬间记录的玩家位置 A（不追踪），
    /// 到点崩爆散出一圈带电小泡接链爆。伤害窗=飞行本体+崩爆波前。
    /// ai[0]=链 id（<see cref="SeaShrimpSparkBubble.MakeChainId"/>，内含 boss whoAmI），
    /// ai[1]=0 生长/1 飞行，ai[2]=飞行总帧数（权威端拍击时写）
    /// </summary>
    internal class SeaShrimpVoltBubble : SeaShrimpModProjectile, ISeaShrimpBubbleBody
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        [VaultLoaden(CWRConstant.Masking + "ThunderTrail")]
        private static Asset<Texture2D> ThunderTex = null;

        /// <summary>生长帧数（f12 生成 → f44 拍出）</summary>
        private const int GrowFrames = 32;
        /// <summary>崩爆余帧（冲击环外扩+消散）</summary>
        private const int AfterFrames = 12;
        /// <summary>伤害窗帧数：波前推进段</summary>
        private const int DamageFrames = 8;
        private const float RingOvershoot = 1.35f;
        /// <summary>钳口悬泡点：沿 boss 头前向的距离 px（泡后缘让开头部遮挡）</summary>
        internal const float MuzzleOffset = 150f;

        private int ChainId => (int)Projectile.ai[0];
        private int OwnerWho => SeaShrimpSparkBubble.ChainOwner(ChainId);
        private bool Launched => Projectile.ai[1] >= 1f;
        private int FlightFrames => (int)Projectile.ai[2];

        /// <summary>本地总龄：逐端计数</summary>
        private int Age => (int)Projectile.localAI[0];
        /// <summary>飞行龄：拍出后逐端计数（各端从收包起步，偏差 ≤2f）</summary>
        private int FlightAge => (int)Projectile.localAI[1];
        private bool Bursting => Launched && FlightAge >= FlightFrames;

        /// <summary>内部电流：两条膜内游走短弧（纯本地表现）</summary>
        private readonly ThunderTrail[] innerArcs = new ThunderTrail[2];
        /// <summary>一次性起爆闩：对本地计数偏差也稳</summary>
        private bool burstFired;

        public override void SetDefaults() {
            Projectile.width = 60;
            Projectile.height = 60;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.aiStyle = -1;
            Projectile.timeLeft = 400;
        }

        public override bool ShouldUpdatePosition() => Launched;

        /// <summary>当前可见半径：生长三次方 → 飞行满径 → 崩爆前定格</summary>
        private float VisualRadius() {
            if (Launched) {
                return SeaShrimpDirector.VoltBubbleRadius;
            }
            float t = MathHelper.Clamp(Age / (float)GrowFrames, 0f, 1f);
            float s = t * t * (3f - 2f * t);
            return MathHelper.Lerp(24f, SeaShrimpDirector.VoltBubbleRadius, s);
        }

        public override void AI() {
            Projectile.localAI[0]++;
            SeaShrimpBubbleRender.PresenceStamp.Stamp();

            if (Bursting) {
                Projectile.velocity = Vector2.Zero;
                Projectile.localAI[1]++;
                if (!burstFired) {
                    burstFired = true;
                    OnBurst();
                }
                if (FlightAge >= FlightFrames + AfterFrames) {
                    Projectile.Kill();
                }
                return;
            }

            float radius = VisualRadius();
            float lum = 0.4f + 0.4f * MathHelper.Clamp(radius / SeaShrimpDirector.VoltBubbleRadius, 0f, 1f);
            Lighting.AddLight(Projectile.Center, 0.14f * lum, 0.3f * lum, 0.55f * lum);

            if (!Launched) {
                //生长期：挂在 boss 钳口；主人不在大炮招内（被全局转移打断）→ 无主泡消散
                if (OwnerWho < 0 || OwnerWho >= Main.maxNPCs) {
                    Projectile.Kill();
                    return;
                }
                NPC owner = Main.npc[OwnerWho];
                if (!owner.active || owner.ModNPC is not SeaShrimpBoss boss
                    || (int)owner.ai[3] != (int)SeaShrimpStateIndex.BubbleCannon) {
                    Projectile.Kill();
                    return;
                }
                Projectile.Center = owner.Center
                    + boss.Locomotion.Heading.ToRotationVector2() * MuzzleOffset;
                Projectile.velocity = Vector2.Zero;
            }
            else {
                Projectile.localAI[1]++;
                //飞行段：撞地提前崩爆（确定性输入，各端同帧）
                Vector2 nose = Projectile.Center + Projectile.velocity.SafeNormalize(Vector2.Zero) * radius * 0.7f;
                if (ShrimpTerrain.SolidAt(nose)) {
                    Projectile.ai[2] = FlightAge;
                }
                //飞行拖沫：速度撕下的水膜碎滴（本地表现）
                if (!Main.dedServ && Main.GameUpdateCount % 3 == 0) {
                    EverdeepVFX.ShedDroplet(Projectile.Center - Projectile.velocity.SafeNormalize(Vector2.Zero) * radius * 0.8f
                        + Main.rand.NextVector2Circular(radius * 0.4f, radius * 0.4f),
                        -Projectile.velocity * 0.08f, 0.9f);
                }
            }

            //膜面电火花：电流游走的外泄（本地表现）
            if (!Main.dedServ && Main.rand.NextFloat() < 0.35f) {
                Vector2 rim = Projectile.Center + Main.rand.NextVector2CircularEdge(1f, 1f) * radius * 0.85f;
                PRTLoader.NewParticle<PRT_AbyssSpark>(rim, Main.rand.NextVector2Circular(1.5f, 1.5f),
                    SeaShrimpBubbleArc.ArcColor, Main.rand.NextFloat(0.35f, 0.65f))?.Configure(9);
            }
        }

        /// <summary>崩爆帧：白闪 + 冲击波前 + 全屏微脉冲 + 散出一圈带电小泡</summary>
        private void OnBurst() {
            if (!Main.dedServ) {
                SeaShrimpAbyssScreen.TriggerImpactFrame(0.3f);
                SoundEngine.PlaySound(SoundID.Item94 with { Volume = 1f, Pitch = 0.1f, MaxInstances = 3 }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.6f, Pitch = 0.3f, MaxInstances = 3 }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.8f, Pitch = -0.1f, MaxInstances = 3 }, Projectile.Center);
                //径向水团锥 + 电火花：崩爆动量甩出去，活得比冲击环久
                for (int i = 0; i < 14; i++) {
                    Vector2 dir = Main.rand.NextVector2Unit();
                    PRTLoader.NewParticle<PRT_AbyssGlob>(Projectile.Center + dir * 14f,
                        dir * Main.rand.NextFloat(5f, 11f),
                        Color.Lerp(SeaShrimpVFX.Deep, SeaShrimpVFX.Body, Main.rand.NextFloat()),
                        Main.rand.NextFloat(0.45f, 0.75f))?.Configure(Main.rand.Next(18, 28), 1.7f);
                }
                for (int i = 0; i < 10; i++) {
                    PRTLoader.NewParticle<PRT_AbyssSpark>(Projectile.Center,
                        Main.rand.NextVector2Circular(6f, 6f),
                        SeaShrimpBubbleArc.ArcColor, Main.rand.NextFloat(0.8f, 1.3f))?.Configure(14);
                }
                for (int i = 0; i < 6; i++) {
                    EverdeepVFX.ShedDroplet(Projectile.Center + Main.rand.NextVector2Circular(10f, 10f),
                        Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(3f, 6f), 1f);
                }
                if (Main.LocalPlayer != null
                    && Vector2.Distance(Main.LocalPlayer.Center, Projectile.Center) < 1300f) {
                    Main.LocalPlayer.CWR()?.GetScreenShake(7f);
                }
            }

            //散射带电小泡：角序单圈、速度交替内外错落（"无数"的杂乱感），
            //错帧起爆让链弧沿圆周蔓延成一圈电网；爆心先登记为链头（仅权威端，生成包广播）
            if (VaultUtils.isClient) {
                return;
            }
            SeaShrimpSparkBubble.RegisterBurst(ChainId, Projectile.Center);
            NPC owner = OwnerWho >= 0 && OwnerWho < Main.maxNPCs ? Main.npc[OwnerWho] : null;
            int damage = owner != null && owner.active
                ? SeaShrimpDirector.ScaleProjectileDamage(owner, SeaShrimpDirector.SparkBubbleDamage)
                : Projectile.damage;
            int count = SeaShrimpDirector.SparkBubbleCount;
            float baseAngle = Main.rand.NextFloat(MathHelper.TwoPi);
            for (int n = 0; n < count; n++) {
                Vector2 dir = (baseAngle + MathHelper.TwoPi * n / count
                    + Main.rand.NextFloat(-0.1f, 0.1f)).ToRotationVector2();
                //初速交替内外圈：积分散射半径 ≈ 340~460px，稳稳飞出崩爆可见环（~405px）
                float speed = SeaShrimpDirector.SparkScatterSpeed + (n % 2) * 3.2f + Main.rand.NextFloat(0.8f);
                Projectile.NewProjectile(Projectile.GetSource_FromAI(),
                    Projectile.Center + dir * 24f, dir * speed,
                    ModContent.ProjectileType<SeaShrimpSparkBubble>(), damage, 1f, Main.myPlayer,
                    SeaShrimpDirector.SparkBurstBase + n * SeaShrimpDirector.SparkBurstStep,
                    ChainId, 24f + (n % 3) * 3f);
            }
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 6; i++) {
                PRTLoader.NewParticle<PRT_GhostRainMist>(
                    Projectile.Center + Main.rand.NextVector2Circular(
                        SeaShrimpDirector.VoltBlastRadius * 0.5f, SeaShrimpDirector.VoltBlastRadius * 0.5f),
                    new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), -Main.rand.NextFloat(0.2f, 0.7f)),
                    new Color(120, 190, 245) * 0.5f, Main.rand.NextFloat(0.8f, 1.2f))
                    ?.Configure(Main.rand.Next(36, 60));
            }
        }

        /// <summary>伤害窗：飞行本体（速度门）+ 崩爆波前 8f；生长期无害</summary>
        public override bool? CanDamage() {
            if (Bursting) {
                return FlightAge < FlightFrames + DamageFrames ? null : false;
            }
            return Launched && Projectile.velocity.Length() > 10f ? null : false;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Vector2 nearest = new(
                MathHelper.Clamp(Projectile.Center.X, targetHitbox.Left, targetHitbox.Right),
                MathHelper.Clamp(Projectile.Center.Y, targetHitbox.Top, targetHitbox.Bottom));
            if (Bursting) {
                float progress = MathHelper.Clamp((FlightAge - FlightFrames) / (float)AfterFrames, 0f, 1f);
                float shockR = MathF.Min(SeaShrimpDirector.VoltBlastRadius,
                    SeaShrimpVFX.CollapseRingRadius(SeaShrimpDirector.VoltBlastRadius * RingOvershoot, progress));
                return Vector2.Distance(nearest, Projectile.Center) <= shockR;
            }
            return Vector2.Distance(nearest, Projectile.Center) <= VisualRadius() * 0.92f;
        }

        bool ISeaShrimpBubbleBody.GetBubbleBody(out SeaShrimpBubbleBodyParams body) {
            if (Bursting) {
                body = default;
                return false;
            }
            float grow = MathHelper.Clamp(Age / (float)GrowFrames, 0f, 1f);
            body = new SeaShrimpBubbleBodyParams {
                Center = Projectile.Center,
                Radius = VisualRadius(),
                //生长期膜面随涨压绷紧发颤，飞行期速度再拉一档
                Wobble = Launched ? 0.7f : 0.35f + 0.4f * grow,
                Arm = Launched ? 0.95f : grow * 0.75f,
                Burst = 0f,
                Fade = MathHelper.Clamp(Age / 6f, 0f, 1f),
                Seed = Projectile.identity,
            };
            return true;
        }

        /// <summary>确定性膜内点：identity+槽位+时间片哈希转角度（各端一致，不掷随机）</summary>
        private Vector2 InnerPoint(int slot, int timeSlice, float radius) {
            float h = MathF.Sin(Projectile.identity * 3.7f + slot * 17.31f + timeSlice * 7.13f) * 43758.5453f;
            float angle = (h - MathF.Floor(h)) * MathHelper.TwoPi;
            float r = 0.45f + 0.35f * MathF.Abs(MathF.Sin(h * 0.31f));
            return Projectile.Center + angle.ToRotationVector2() * radius * r;
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Bursting) {
                //崩爆：海虾冲击环 + 白热电闪急衰
                float progress = MathHelper.Clamp((FlightAge - FlightFrames) / (float)AfterFrames, 0f, 1f);
                if (SeaShrimpVFX.CollapsePathReady) {
                    SeaShrimpVFX.DrawCollapse(Projectile.Center, SeaShrimpDirector.VoltBlastRadius * RingOvershoot,
                        progress, Projectile.identity * 0.29f, 1f);
                }
                Texture2D glowTex = CWRAsset.SoftGlow?.Value;
                float fade = 1f - progress;
                if (glowTex != null && fade > 0f) {
                    Vector2 pos = Projectile.Center - Main.screenPosition;
                    Main.spriteBatch.Draw(glowTex, pos, null, new Color(255, 255, 255, 0) * (0.95f * fade), 0f,
                        glowTex.Size() * 0.5f,
                        SeaShrimpDirector.VoltBlastRadius * 1.4f / glowTex.Width * 2f * fade, SpriteEffects.None, 0f);
                    Main.spriteBatch.Draw(glowTex, pos, null,
                        SeaShrimpBubbleArc.ArcColor with { A = 0 } * (0.8f * fade), 0f,
                        glowTex.Size() * 0.5f,
                        SeaShrimpDirector.VoltBlastRadius * 2.4f / glowTex.Width * 2f, SpriteEffects.None, 0f);
                }
                return false;
            }

            float radius = VisualRadius();

            //内部电流：两条膜内游走短弧，端点每 6f 换位（水膜批绘之上的带电声明）
            if (ThunderTex?.Value != null && radius > 40f) {
                int slice = (int)(Main.GameUpdateCount / 6);
                for (int arc = 0; arc < innerArcs.Length; arc++) {
                    Vector2 from = InnerPoint(arc * 2, slice + arc, radius);
                    Vector2 to = InnerPoint(arc * 2 + 1, slice - arc, radius);
                    Vector2 dir = to - from;
                    Vector2 perp = dir.SafeNormalize(Vector2.UnitY).RotatedBy(MathHelper.PiOver2);
                    Vector2[] points = new Vector2[8];
                    for (int i = 0; i < points.Length; i++) {
                        float t = i / (float)(points.Length - 1);
                        float envelope = MathF.Sin(t * MathHelper.Pi);
                        float wave = MathF.Sin(Main.GlobalTimeWrappedHourly * 14f + t * 9f + arc * 2.6f)
                            * radius * 0.14f * envelope;
                        points[i] = from + dir * t + perp * wave;
                    }
                    if (innerArcs[arc] == null) {
                        innerArcs[arc] = new ThunderTrail(ThunderTex, f => 8f + 5f * MathF.Sin(f * MathHelper.Pi),
                            _ => SeaShrimpBubbleArc.ArcColor, _ => 0.85f) {
                            CanDraw = true,
                            UseNonOrAdd = true,
                            PartitionPointCount = 2,
                        };
                        innerArcs[arc].SetRange((0, 6));
                        innerArcs[arc].SetExpandWidth(3);
                    }
                    innerArcs[arc].BasePositions = points;
                    if (Main.GameUpdateCount % 3 == 0) {
                        innerArcs[arc].RandomThunder();
                    }
                    innerArcs[arc].DrawThunder(Main.instance.GraphicsDevice);
                }
            }

            //中心电辉：闪烁的带电核（批绘水膜之上）
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow != null) {
                float flicker = 0.5f + 0.35f * MathF.Sin(Main.GlobalTimeWrappedHourly * 26f + Projectile.identity);
                Main.spriteBatch.Draw(glow, Projectile.Center - Main.screenPosition, null,
                    SeaShrimpBubbleArc.ArcColor with { A = 0 } * (flicker * 0.5f), 0f,
                    glow.Size() * 0.5f, radius * 1.1f / glow.Width * 2f, SpriteEffects.None, 0f);
            }
            return false;
        }
    }
}
