using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEyeOfCthulhu;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEyeOfCthulhu.Core;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Accessories.BrutalRelics.EyeOfCthulhu
{
    /// <summary>
    /// 血雾之瞳随身演出锚点：本身无伤害，承载跨端可见的液态血演出。<br/>
    /// 视觉母题：本体就是血。突进时本体隐去、位置上是一团顺速度拉长的血核，背风端不断拉丝断珠，
    /// 迎风面甩出溅片；急刹时血核前段带惯性溅向前方、本体重新凝形。免死重凝走同一套语言。<br/>
    /// owner 端生成后经原生弹幕同步；时间轴按本端 AI 帧数推进，拉丝/溅片/血块都是本端局部列表。<br/>
    /// ai[0]=模式：0 突进（蓄力起手即生成）/ 1 消隐点炸开 + 血核飞向新位置 / 2 重凝落点；ai[1]=总寿命(帧)；
    /// velocity=方向（ShouldUpdatePosition=false，只当同步载体）
    /// </summary>
    internal class BloodfogVeilProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        internal int Mode => (int)Projectile.ai[0];
        internal float TotalLife => Math.Max(Projectile.ai[1], 1f);
        /// <summary>时间轴帧号，首帧 AI 与玩家突进 elapsed=0 同帧</summary>
        private int Frame => (int)Projectile.localAI[1] - 1;
        private Vector2 DashDir => Projectile.velocity.SafeNormalize(Vector2.UnitX * Main.player[Projectile.owner].direction);

        #region 突进时间轴（帧，与 BloodfogIrisPlayer 的突进相位对齐）
        /// <summary>0..2 反向预备：血丝被向后拽出</summary>
        private const int WindupFrames = BloodfogIrisPlayer.DashWindupFrames;
        /// <summary>起步帧：本体隐去、血核出现、冠状溅射</summary>
        private const int LaunchFrame = BloodfogIrisPlayer.DashWindupFrames;
        /// <summary>急刹首帧：血核前段溅出、本体凝形</summary>
        private const int BrakeFrame = BloodfogIrisPlayer.DashWindupFrames + BloodfogIrisPlayer.DashTravelFrames;
        /// <summary>凝形收缩团持续帧</summary>
        private const int ReformFrames = 4;
        private const int DashQuietFrame = BrakeFrame + ReformFrames;
        #endregion

        #region 重凝时间轴（帧）
        /// <summary>血核从消隐点飞到新位置的帧数</summary>
        private const int CometFrames = 6;
        /// <summary>落点：到位帧本体显形 + 前向溅射</summary>
        private const int LandFrame = CometFrames;
        private const int LandQuietFrame = LandFrame + ReformFrames;
        #endregion

        #region 元素模型（本端局部，随弹幕生灭）
        /// <summary>拉丝：根跟着血核背风端走、尾留在世界里；拉到极限或到龄就断成一串血珠</summary>
        private sealed class Ligament
        {
            public Vector2 Root;
            public Vector2 Tail;
            /// <summary>根相对血核中心的横向偏移 px</summary>
            public float PerpOffset;
            /// <summary>根落在血核中心后方多少 px</summary>
            public float BackOffset;
            /// <summary>根部宽 px</summary>
            public float Width;
            public float MaxLen;
            public float Phase;
            public float Amp;
            public float Freq;
            public int Age;
            /// <summary>附着最大帧数，超过即断</summary>
            public int MaxAge;
            public int Beads;
            public float Seed;
            /// <summary>悬丝：根固定、尾受重力下垂（雾态滴血 / 消隐点残丝）</summary>
            public bool Hanging;
            /// <summary>预备丝：尾被向后拽，起步帧甩回</summary>
            public bool Windup;
        }

        /// <summary>溅片：自源点扇开的一片薄血，展开→变薄→撕洞→只剩液舌，撕到一半沿舌尖放血珠</summary>
        private sealed class Sheet
        {
            public Vector2 Pos;
            public Vector2 Vel;
            public Vector2 Dir;
            public float Size;
            public float Seed;
            public int Age;
            public int Life;
            public float Decel;
            /// <summary>true 宽扇(起步/炸开) false 窄扇(途中迎风/落地)</summary>
            public bool Wide;
            public bool DropsDone;
        }

        /// <summary>血块：炸开时甩出的大团，带重力，寿尽碎成血珠</summary>
        private sealed class Chunk
        {
            public Vector2 Pos;
            public Vector2 Vel;
            public float Len;
            public float Wid;
            public float Seed;
            public int Age;
            public int Life;
        }

        private readonly List<Ligament> ligaments = new(24);
        private readonly List<Sheet> sheets = new(16);
        private readonly List<Chunk> chunks = new(8);

        /// <summary>血核本帧中心/朝向/速度（模式 0 跟玩家，模式 1 沿飞行路径），供拉丝根与绘制共用</summary>
        private Vector2 massCenter;
        private Vector2 massDir = Vector2.UnitX;
        private Vector2 massVel;
        private bool massActive;
        /// <summary>起步过冲：血核前伸量，逐帧 ×0.5</summary>
        private float overshoot;
        #endregion

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 64;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
            Projectile.alpha = 255;
            //伏击窗口全程存在，晚入场玩家也要收到
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            int frame = (int)Projectile.localAI[1];
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                Projectile.timeLeft = (int)TotalLife;
            }
            Projectile.localAI[1]++;
            overshoot *= 0.5f;

            Player owner = Main.player[Projectile.owner];
            if (Mode == 1) {
                UpdateBurst(owner, frame);
                SimulateElements();
                return;
            }

            if (!owner.active || owner.dead) {
                Projectile.Kill();
                return;
            }
            Projectile.Center = owner.Center;

            //雾态视觉计时：各端(含服务端)每帧点亮，
            //服务端由 PostUpdateEquips 读它压仇恨，客户端驱动本体褪色
            BloodfogIrisPlayer mp = owner.GetModPlayer<BloodfogIrisPlayer>();
            mp.VeilVisualTimer = 2;

            if (Mode == 0) {
                UpdateDash(owner, mp, frame);
            }
            else {
                UpdateLanding(owner, mp, frame);
            }
            SimulateElements();
        }

        #region 突进：预备拽丝 → 起步冠溅 → 途中血核拉丝 → 急刹前溅凝形 → 雾态滴血
        private void UpdateDash(Player owner, BloodfogIrisPlayer mp, int frame) {
            Vector2 dir = DashDir;
            Vector2 perp = dir.RotatedBy(MathHelper.PiOver2);
            bool client = !VaultUtils.isServer;

            if (frame < LaunchFrame) {
                //反向预备：本体仍在，背风侧血丝被向后拽出、越拽越长（拉弓）
                massActive = false;
                massDir = dir;
                massCenter = owner.Center;
                if (!client) {
                    return;
                }
                if (frame == 0) {
                    SoundEngine.PlaySound(SoundID.Item103 with { Volume = 0.5f, Pitch = -0.5f }, owner.Center);
                    BloodfogIrisFX.FineMist(owner.Center - dir * 10f, -dir, 6, 2.5f, 0.8f);
                }
                for (int i = 0; i < 2; i++) {
                    Vector2 root = owner.Center - dir * Main.rand.NextFloat(4f, 10f) + perp * Main.rand.NextFloat(-14f, 14f);
                    ligaments.Add(new Ligament {
                        Root = root,
                        Tail = root - dir * Main.rand.NextFloat(6f, 16f),
                        Width = Main.rand.NextFloat(2.2f, 3.6f),
                        MaxLen = 200f,
                        Phase = Main.rand.NextFloat(MathHelper.TwoPi),
                        Amp = Main.rand.NextFloat(1f, 2.5f),
                        Freq = Main.rand.NextFloat(1f, 2f),
                        MaxAge = 60,
                        Beads = Main.rand.Next(5, 8),
                        Seed = Main.rand.NextFloat(),
                        Windup = true
                    });
                }
                return;
            }

            massVel = owner.velocity;
            massDir = dir;
            massCenter = owner.Center;
            massActive = frame < BrakeFrame;

            if (frame == LaunchFrame) {
                //起步：本体隐去、血核过冲、预备丝整批甩回成顺冲刺飞的血珠、迎风冠状溅射
                mp.HideBodyTimer = 2;
                overshoot = 12f;
                SnapAll(dir * 9f);
                LaunchSplash(owner.Center, dir);
                return;
            }

            if (frame < BrakeFrame) {
                mp.HideBodyTimer = 2;
                if (!client) {
                    return;
                }
                //背风端拉丝：每帧一根，偶数帧两根
                int pulls = frame % 2 == 0 ? 2 : 1;
                for (int i = 0; i < pulls; i++) {
                    SpawnPulledLigament();
                }
                //背风甩珠 + 细血雾 + 速度线
                BloodfogIrisFX.Spray(massCenter - dir * 24f, -dir, 2, 0.7f, 2f, 6f, 0.7f, 1.3f, 16, 26, 0.3f, 0.98f, 10f);
                BloodfogIrisFX.FineMist(massCenter - dir * 30f, -dir, 2, 2f, 0.9f);
                BloodfogIrisFX.SpeedStreaks(massCenter - dir * 20f, dir, 1, 26f);
                //迎风小溅片，左右交替
                if (frame % 2 == 1) {
                    float side = (frame / 2) % 2 == 0 ? 1f : -1f;
                    SpawnSheet(massCenter + dir * 26f, dir.RotatedBy(side * Main.rand.NextFloat(0.9f, 1.3f)),
                        Main.rand.NextFloat(7f, 10f), 0.78f, Main.rand.NextFloat(42f, 56f), Main.rand.Next(7, 10), wide: false);
                }
                return;
            }

            if (frame == BrakeFrame) {
                //急刹：本体当帧显形，血核前段带惯性溅向前方，所有拉丝断
                mp.HideBodyTimer = 0;
                SnapAll(dir * 6f);
                BrakeSplash(owner.Center, dir, 1f);
                return;
            }

            //雾态：偶尔滴血、偶尔一根悬丝
            if (client && EocMotion.OnScreen(owner.Center, 400f)) {
                if (frame % 7 == 0) {
                    BloodfogIrisFX.Drip(owner);
                }
                if (frame % 31 == 5) {
                    SpawnHangingLigament(owner.Center + new Vector2(Main.rand.NextFloat(-8f, 8f), Main.rand.NextFloat(0f, 16f)), 28f, 44f);
                }
            }
        }

        /// <summary>起步冠状溅射：5 片宽扇从迎风端甩向前侧方 + 反冲血雾锥 + 速度线 + 湿吼 + 方向震屏</summary>
        private void LaunchSplash(Vector2 center, Vector2 dir) {
            EocMotion.Shake(center, 4.2f, 9, dir);
            if (VaultUtils.isServer) {
                return;
            }
            Vector2 front = center + dir * 14f;
            for (int i = 0; i < 4; i++) {
                float side = i % 2 == 0 ? 1f : -1f;
                float ang = side * Main.rand.NextFloat(0.55f, 1.25f);
                SpawnSheet(front, dir.RotatedBy(ang), Main.rand.NextFloat(10f, 16f), 0.8f, Main.rand.NextFloat(90f, 130f), Main.rand.Next(10, 14), wide: true);
            }
            SpawnSheet(front, dir.RotatedBy(Main.rand.NextFloat(-0.25f, 0.25f)), Main.rand.NextFloat(12f, 15f), 0.8f, Main.rand.NextFloat(60f, 80f), Main.rand.Next(8, 11), wide: false);
            BloodfogIrisFX.Spray(center - dir * 10f, -dir, 18, 0.85f, 4f, 12f, 1f, 1.9f, 22, 38, 0.3f, 0.985f, 16f);
            BloodfogIrisFX.FineMist(center - dir * 16f, -dir, 12, 5f, 0.9f);
            BloodfogIrisFX.SpeedStreaks(center, dir, 3, 22f);
            BloodfogIrisFX.WetBurstSound(center, 0.95f, heavy: false);
        }

        /// <summary>急刹/落地前溅：3 片窄扇顺方向溅出 + 前向血珠锥 + 湿裂响 + 短震</summary>
        private void BrakeSplash(Vector2 center, Vector2 dir, float strength) {
            EocMotion.Shake(center, 3f * strength, 7, dir);
            if (VaultUtils.isServer) {
                return;
            }
            Vector2 front = center + dir * 16f;
            for (int i = 0; i < 3; i++) {
                float ang = (i - 1) * 0.42f + Main.rand.NextFloat(-0.12f, 0.12f);
                SpawnSheet(front, dir.RotatedBy(ang), Main.rand.NextFloat(9f, 13f) * strength, 0.8f,
                    Main.rand.NextFloat(70f, 100f) * strength, Main.rand.Next(9, 13), wide: false);
            }
            BloodfogIrisFX.Spray(front, dir, (int)(14 * strength), 0.55f, 6f, 14f, 0.9f, 1.7f, 20, 34, 0.32f, 0.98f, 12f);
            BloodfogIrisFX.FineMist(front + dir * 4f, dir, 8, 4f, 0.7f);
            SoundEngine.PlaySound(SoundID.NPCHit13 with { Volume = 0.8f * strength, Pitch = -0.2f }, center);
            SoundEngine.PlaySound(SoundID.Item74 with { Volume = 0.4f * strength, Pitch = 0.25f }, center);
        }
        #endregion

        #region 重凝：消隐点炸开 → 血核飞向新位置 → 落地前溅凝形
        /// <summary>消隐点弹幕：首帧躯体炸成血，随后血核沿路径飞向玩家现位，沿途拉丝落珠</summary>
        private void UpdateBurst(Player owner, int frame) {
            Vector2 from = Projectile.Center;
            Vector2 to = owner.active ? owner.Center : from;
            Vector2 path = to - from;
            float len = path.Length();
            Vector2 dir = len > 1f ? path / len : Vector2.UnitX;

            if (frame == 0) {
                massDir = dir;
                massCenter = from;
                BodyBurst(from);
            }

            if (frame <= CometFrames && len > 12f) {
                //血核：快出慢到（先甩出去再在落点前减速）
                float t = MathHelper.Clamp((frame + 1f) / (CometFrames + 1f), 0f, 1f);
                float p = 1f - MathF.Pow(1f - t, 2.2f);
                Vector2 prev = frame == 0 ? from : massCenter;
                massCenter = from + path * p;
                massDir = dir;
                massVel = massCenter - prev;
                massActive = frame < CometFrames;
                if (!VaultUtils.isServer && frame < CometFrames) {
                    SpawnPulledLigament();
                    SpawnPulledLigament();
                    BloodfogIrisFX.Spray(massCenter - dir * 20f, -dir, 3, 0.8f, 1.5f, 5f, 0.7f, 1.3f, 16, 26, 0.3f, 0.98f, 10f);
                    BloodfogIrisFX.FineMist(massCenter - dir * 26f, -dir, 2, 2f, 0.9f);
                    BloodfogIrisFX.SpeedStreaks(massCenter - dir * 16f, dir, 1, 20f);
                }
                return;
            }
            massActive = false;
        }

        /// <summary>躯体炸开：6 片宽扇全向 + 全向血珠 + 4 大血块 + 4 根悬丝 + 表皮碎屑 + 湿爆音</summary>
        private void BodyBurst(Vector2 pos) {
            Lighting.AddLight(pos, EocMotion.Arterial.ToVector3() * 1.4f);
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 6; i++) {
                Vector2 dir = (MathHelper.TwoPi * i / 6f + Main.rand.NextFloat(-0.3f, 0.3f)).ToRotationVector2();
                SpawnSheet(pos + dir * 8f, dir, Main.rand.NextFloat(8f, 13f), 0.82f, Main.rand.NextFloat(80f, 110f), Main.rand.Next(10, 15), wide: true);
            }
            BloodfogIrisFX.Spray(pos, Vector2.UnitX, 24, MathHelper.Pi, 3f, 12f, 1f, 2.1f, 24, 44, 0.34f, 0.985f, 8f);
            BloodfogIrisFX.FineMist(pos, Vector2.UnitX, 10, 4f, MathHelper.Pi, 14f);
            for (int i = 0; i < 4; i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(3f, 7f);
                vel.Y -= 2f;
                chunks.Add(new Chunk {
                    Pos = pos + Main.rand.NextVector2Circular(10f, 14f),
                    Vel = vel,
                    Len = Main.rand.NextFloat(18f, 26f),
                    Wid = Main.rand.NextFloat(11f, 16f),
                    Seed = Main.rand.NextFloat(),
                    Life = Main.rand.Next(26, 40)
                });
            }
            for (int i = 0; i < 4; i++) {
                SpawnHangingLigament(pos + new Vector2(Main.rand.NextFloat(-12f, 12f), Main.rand.NextFloat(-14f, 14f)), 30f, 50f);
            }
            for (int i = 0; i < 7; i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 7f);
                vel.Y -= 2.2f;
                PRTLoader.NewParticle<PRT_EocSkinShred>(pos + Main.rand.NextVector2Circular(16f, 24f), vel,
                    Color.Lerp(EocMotion.Arterial, EocMotion.VenousDark, Main.rand.NextFloat()),
                    Main.rand.NextFloat(0.8f, 1.4f))?.Configure(Main.rand.Next(26, 44));
            }
            SoundEngine.PlaySound(SoundID.Zombie3 with { Volume = 0.75f, Pitch = -0.45f }, pos);
            BloodfogIrisFX.WetBurstSound(pos, 1.3f, heavy: true);
        }

        /// <summary>落点弹幕：血核到位前本体隐去、外圈血珠汇入；到位帧显形 + 前溅；之后雾态滴血</summary>
        private void UpdateLanding(Player owner, BloodfogIrisPlayer mp, int frame) {
            Vector2 dir = DashDir;
            massActive = false;
            massDir = dir;
            massCenter = owner.Center;
            bool client = !VaultUtils.isServer;

            if (frame < LandFrame) {
                mp.HideBodyTimer = 2;
                if (client && frame % 2 == 0) {
                    BloodfogIrisFX.Converge(owner.Center, 90f, 2, 6);
                }
                return;
            }
            if (frame == LandFrame) {
                mp.HideBodyTimer = 0;
                BrakeSplash(owner.Center, dir, 0.85f);
                if (client) {
                    SoundEngine.PlaySound(SoundID.Item103 with { Volume = 0.7f, Pitch = -0.25f }, owner.Center);
                    SoundEngine.PlaySound(SoundID.Zombie2 with { Volume = 0.55f, Pitch = -0.6f }, owner.Center);
                }
                return;
            }
            if (client && EocMotion.OnScreen(owner.Center, 400f)) {
                if (frame % 7 == 3) {
                    BloodfogIrisFX.Drip(owner);
                }
                if (frame % 29 == 11) {
                    SpawnHangingLigament(owner.Center + new Vector2(Main.rand.NextFloat(-8f, 8f), Main.rand.NextFloat(0f, 16f)), 28f, 44f);
                }
            }
        }
        #endregion

        #region 元素生成 / 模拟 / 断裂
        /// <summary>从血核背风端拉一根丝：根随血核走，尾留在原地，被拉到极限就断</summary>
        private void SpawnPulledLigament() {
            if (VaultUtils.isServer || ligaments.Count >= 22) {
                return;
            }
            float perpOff = Main.rand.NextFloat(-9f, 9f);
            float back = Main.rand.NextFloat(18f, 30f);
            Vector2 perp = massDir.RotatedBy(MathHelper.PiOver2);
            Vector2 root = massCenter - massDir * back + perp * perpOff;
            ligaments.Add(new Ligament {
                Root = root,
                Tail = root - massDir * Main.rand.NextFloat(2f, 8f),
                PerpOffset = perpOff,
                BackOffset = back,
                Width = Main.rand.NextFloat(2.4f, 5f),
                MaxLen = Main.rand.NextFloat(70f, 120f),
                Phase = Main.rand.NextFloat(MathHelper.TwoPi),
                Amp = Main.rand.NextFloat(3f, 7f),
                Freq = Main.rand.NextFloat(1.5f, 3f),
                MaxAge = Main.rand.Next(6, 10),
                Beads = Main.rand.Next(7, 10),
                Seed = Main.rand.NextFloat()
            });
        }

        /// <summary>悬丝：根固定在 pos，尾受重力下垂，拉到 minLen~maxLen 之间某个长度就断成血珠落下</summary>
        private void SpawnHangingLigament(Vector2 pos, float minLen, float maxLen) {
            if (VaultUtils.isServer || ligaments.Count >= 22) {
                return;
            }
            ligaments.Add(new Ligament {
                Root = pos,
                Tail = pos + new Vector2(Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(3f, 8f)),
                Width = Main.rand.NextFloat(1.8f, 3.2f),
                MaxLen = Main.rand.NextFloat(minLen, maxLen),
                Phase = Main.rand.NextFloat(MathHelper.TwoPi),
                Amp = Main.rand.NextFloat(0.4f, 1.2f),
                Freq = Main.rand.NextFloat(0.6f, 1.2f),
                MaxAge = 90,
                Beads = Main.rand.Next(5, 8),
                Seed = Main.rand.NextFloat(),
                Hanging = true
            });
        }

        private void SpawnSheet(Vector2 source, Vector2 dir, float speed, float decel, float size, int life, bool wide) {
            if (VaultUtils.isServer || sheets.Count >= 16) {
                return;
            }
            sheets.Add(new Sheet {
                Pos = source,
                Vel = dir * speed,
                Dir = dir,
                Size = size,
                Seed = Main.rand.NextFloat(),
                Life = life,
                Decel = decel,
                Wide = wide
            });
        }

        /// <summary>丝上某点位置：t=0 尾（自由端）→ 1 根；自由端横向摆动大、根端小</summary>
        private Vector2 BeadPos(Ligament l, float t, out Vector2 axis) {
            Vector2 span = l.Root - l.Tail;
            float len = span.Length();
            axis = len > 0.01f ? span / len : massDir;
            Vector2 n = axis.RotatedBy(MathHelper.PiOver2);
            float wave = MathF.Sin(l.Phase + t * l.Freq * MathHelper.TwoPi + l.Age * 0.55f) * l.Amp * (1f - t) * MathF.Min(1f, len / 40f);
            return Vector2.Lerp(l.Tail, l.Root, t) + n * wave;
        }

        private static float BeadWidth(Ligament l, float t) => l.Width * MathHelper.Lerp(0.3f, 1f, t);

        /// <summary>断丝：每颗珠段变成一颗带重力的血珠，根侧带上血核速度的一部分</summary>
        private void SnapLigament(Ligament l, Vector2 rootVel) {
            if (VaultUtils.isServer) {
                return;
            }
            int n = Math.Max(l.Beads, 2);
            for (int k = 0; k < n; k++) {
                float t = k / (float)(n - 1);
                Vector2 pos = BeadPos(l, t, out Vector2 axis);
                Vector2 side = axis.RotatedBy(MathHelper.PiOver2) * MathF.Sin(l.Phase + t * l.Freq * MathHelper.TwoPi) * l.Amp * 0.12f;
                Vector2 vel = rootVel * t + side + Main.rand.NextVector2Circular(0.7f, 0.7f);
                float scale = MathHelper.Clamp(BeadWidth(l, t) / 3.2f, 0.45f, 1.5f);
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(pos, vel,
                    Color.Lerp(EocMotion.VenousDark, EocMotion.Arterial, Main.rand.NextFloat(0.3f, 1f)), scale)?
                    .Configure(Main.rand.Next(16, 28), 0.3f, 0.985f);
            }
        }

        private void SnapAll(Vector2 rootVel) {
            for (int i = ligaments.Count - 1; i >= 0; i--) {
                SnapLigament(ligaments[i], rootVel);
            }
            ligaments.Clear();
        }

        /// <summary>血块碎成几颗血珠</summary>
        private static void BurstChunk(Chunk c) {
            if (VaultUtils.isServer) {
                return;
            }
            int n = Main.rand.Next(3, 6);
            for (int i = 0; i < n; i++) {
                Vector2 vel = c.Vel * 0.4f + Main.rand.NextVector2Unit() * Main.rand.NextFloat(1f, 4f);
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(c.Pos + Main.rand.NextVector2Circular(c.Wid * 0.4f, c.Wid * 0.4f), vel,
                    Color.Lerp(EocMotion.VenousDark, EocMotion.Arterial, Main.rand.NextFloat()), Main.rand.NextFloat(0.8f, 1.5f))?
                    .Configure(Main.rand.Next(16, 28), 0.32f, 0.985f);
            }
        }

        /// <summary>溅片展开进度：前 45% 寿命快速扇开</summary>
        private static float SheetSpread(Sheet s)
            => VaultUtils.EaseOutCubic(MathHelper.Clamp((s.Age + 1f) / (s.Life * 0.45f), 0f, 1f));

        /// <summary>溅片撕裂进度：30% 寿命起撕，寿尽撕净</summary>
        private static float SheetTear(Sheet s)
            => MathF.Pow(MathHelper.Clamp((s.Age - s.Life * 0.3f) / (s.Life * 0.7f), 0f, 1f), 1.3f);

        /// <summary>每帧推进全部局部元素；服务端不留任何元素</summary>
        private void SimulateElements() {
            if (Main.dedServ) {
                ligaments.Clear();
                sheets.Clear();
                chunks.Clear();
                return;
            }
            Vector2 perp = massDir.RotatedBy(MathHelper.PiOver2);

            for (int i = ligaments.Count - 1; i >= 0; i--) {
                Ligament l = ligaments[i];
                l.Age++;
                if (l.Hanging) {
                    l.Tail.Y += 0.9f + l.Age * 0.08f;
                }
                else if (l.Windup) {
                    l.Tail -= massDir * 2.6f;
                }
                else if (massActive) {
                    l.Root = massCenter - massDir * l.BackOffset + perp * l.PerpOffset;
                }
                else {
                    SnapLigament(l, massVel * 0.3f);
                    ligaments.RemoveAt(i);
                    continue;
                }
                float len = Vector2.Distance(l.Root, l.Tail);
                if (len > l.MaxLen || l.Age > l.MaxAge) {
                    SnapLigament(l, l.Hanging || l.Windup ? Vector2.Zero : massVel * 0.3f);
                    ligaments.RemoveAt(i);
                }
            }

            for (int i = sheets.Count - 1; i >= 0; i--) {
                Sheet s = sheets[i];
                s.Age++;
                s.Pos += s.Vel;
                s.Vel *= s.Decel;
                //撕到一半：舌尖甩珠
                if (!s.DropsDone && SheetTear(s) > 0.45f) {
                    s.DropsDone = true;
                    int n = Main.rand.Next(3, 6);
                    float halfSpan = (s.Wide ? WideSpan : NarrowSpan) * 0.5f;
                    for (int k = 0; k < n; k++) {
                        Vector2 tipDir = s.Dir.RotatedBy(Main.rand.NextFloat(-halfSpan, halfSpan) * 0.8f);
                        Vector2 tip = s.Pos + tipDir * (s.Size * 0.5f * Main.rand.NextFloat(0.5f, 0.78f)) + s.Dir * s.Size * 0.16f;
                        PRTLoader.NewParticle<PRT_HeartcarverDroplet>(tip, s.Vel * 0.6f + tipDir * Main.rand.NextFloat(1f, 3f),
                            Color.Lerp(EocMotion.Arterial, EocMotion.BrightBlood, Main.rand.NextFloat()), Main.rand.NextFloat(0.7f, 1.3f))?
                            .Configure(Main.rand.Next(16, 28), 0.3f, 0.985f);
                    }
                }
                if (s.Age >= s.Life) {
                    sheets.RemoveAt(i);
                }
            }

            for (int i = chunks.Count - 1; i >= 0; i--) {
                Chunk c = chunks[i];
                c.Age++;
                c.Vel.Y += 0.3f;
                c.Vel.X *= 0.985f;
                c.Pos += c.Vel;
                bool hit = Collision.SolidCollision(c.Pos - new Vector2(c.Wid * 0.5f), (int)c.Wid, (int)c.Wid);
                if (c.Age >= c.Life || hit) {
                    BurstChunk(c);
                    chunks.RemoveAt(i);
                }
            }
        }
        #endregion

        #region 绘制：一次 Immediate 批画完全部液态血 quad（丝 → 血块 → 血核 → 溅片 → 凝形团）
        /// <summary>宽扇角(起步/炸开)与窄扇角(途中迎风/落地)，与 .fx 的 ≤2.6 上限相容</summary>
        private const float WideSpan = 1.7f;
        private const float NarrowSpan = 1.15f;

        /// <summary>血核团簇（血核坐标系：x 迎风、y 横向）：核 / 钝前帽 / 三颗背风卫星；wob=紊乱抖幅 px</summary>
        private static readonly (Vector2 off, float len, float wid, float wob)[] ClumpCore = [
            (new Vector2(4f, 0f), 62f, 24f, 2f),
            (new Vector2(28f, 0f), 24f, 16f, 1.5f),
        ];
        private static readonly (Vector2 off, float len, float wid, float wob)[] ClumpSatellites = [
            (new Vector2(-18f, -8f), 28f, 11f, 4f),
            (new Vector2(-24f, 9f), 22f, 9f, 4f),
            (new Vector2(-34f, -1f), 18f, 7f, 5f),
        ];

        private float SeedBase => Projectile.whoAmI * 0.37f;

        /// <summary>凝形收缩团：急刹/落地那一拍血收回身体，1.3 倍身形缩到 0.85 并淡出</summary>
        private float ReformAlpha(int frame, out float scale) {
            int start = Mode == 0 ? BrakeFrame : Mode == 2 ? LandFrame : int.MaxValue;
            int k = frame - start;
            scale = 1f;
            if (k < 0 || k >= ReformFrames) {
                return 0f;
            }
            float p = (k + 0.5f) / ReformFrames;
            scale = MathHelper.Lerp(1.3f, 0.85f, p);
            return 0.9f * (1f - p);
        }

        public override bool PreDraw(ref Color lightColor) {
            int f = Frame;
            if (f < 0) {
                return false;
            }
            Player owner = Main.player[Projectile.owner];
            SpriteBatch sb = Main.spriteBatch;
            float reformAlpha = ReformAlpha(f, out float reformScale);
            bool anyQuads = massActive || sheets.Count > 0 || ligaments.Count > 0 || chunks.Count > 0 || reformAlpha > 0f;

            if (anyQuads) {
                if (BloodQuadBatch.Begin(sb, out Effect blob, out Effect sheetFx)) {
                    DrawLigaments(sb, blob);
                    DrawChunks(sb, blob);
                    if (massActive) {
                        DrawMass(sb, blob, f);
                    }
                    DrawSheets(sb, sheetFx);
                    if (reformAlpha > 0f && owner.active) {
                        BloodQuadBatch.BlobGroup(blob, 0.6f, 0.25f, 0.8f, 0.6f);
                        BloodQuadBatch.DrawBlob(sb, owner.Center + new Vector2(0f, 2f), -Vector2.UnitY, 46f * reformScale, 26f * reformScale, SeedBase + 0.5f, reformAlpha);
                    }
                    BloodQuadBatch.End(sb);
                }
                else {
                    DrawFallback(sb);
                }
            }

            if (Mode != 1 && owner.active && !owner.dead
                && owner.TryGetModPlayer(out BloodfogIrisPlayer mp) && mp.HideBodyTimer <= 0) {
                DrawPupilGlint(owner, mp, f);
            }
            return false;
        }

        /// <summary>血核：核与钝帽一组（热、亮），三颗卫星一组（暗、小）；起步过冲前伸并顺速度拉长</summary>
        private void DrawMass(SpriteBatch sb, Effect blob, int frame) {
            Vector2 perp = massDir.RotatedBy(MathHelper.PiOver2);
            float stretch = 1f + overshoot / 12f * 0.35f;
            float speedStretch = MathHelper.Clamp(massVel.Length() / 26f, 0.6f, 1.15f);
            stretch *= speedStretch;

            BloodQuadBatch.BlobGroup(blob, 0.85f, 1f, 1.05f, 1f);
            for (int k = 0; k < ClumpCore.Length; k++) {
                (Vector2 off, float len, float wid, float wob) = ClumpCore[k];
                Vector2 jitter = new Vector2(MathF.Sin(frame * 1.9f + k * 2.1f), MathF.Cos(frame * 2.3f + k * 1.3f)) * wob;
                Vector2 local = off + jitter;
                local.X = local.X * stretch + overshoot * (k == 1 ? 1f : 0.4f);
                Vector2 pos = massCenter + massDir * local.X + perp * local.Y;
                BloodQuadBatch.DrawBlob(sb, pos, massDir, len * stretch, wid, SeedBase + k * 0.173f, 1f);
            }

            BloodQuadBatch.BlobGroup(blob, 1f, 0.8f, 0.45f, 1.2f);
            for (int k = 0; k < ClumpSatellites.Length; k++) {
                (Vector2 off, float len, float wid, float wob) = ClumpSatellites[k];
                Vector2 jitter = new Vector2(MathF.Sin(frame * 1.7f + k * 2.9f + 1f), MathF.Cos(frame * 2.6f + k * 1.7f)) * wob;
                Vector2 local = off + jitter;
                local.X *= stretch;
                Vector2 pos = massCenter + massDir * local.X + perp * local.Y;
                BloodQuadBatch.DrawBlob(sb, pos, massDir, len * stretch, wid, SeedBase + 0.31f + k * 0.173f, 0.95f);
            }
        }

        /// <summary>拉丝：每根是一串重叠的小血团，根粗尾细、自由端摆动；断裂后由 SnapLigament 变成血珠</summary>
        private void DrawLigaments(SpriteBatch sb, Effect blob) {
            if (ligaments.Count == 0) {
                return;
            }
            BloodQuadBatch.BlobGroup(blob, 0.7f, 0.6f, 0.35f, 1f);
            foreach (Ligament l in ligaments) {
                int n = Math.Max(l.Beads, 2);
                float len = Vector2.Distance(l.Root, l.Tail);
                if (len < 2f) {
                    continue;
                }
                float spacing = len / (n - 1);
                for (int k = 0; k < n; k++) {
                    float t = k / (float)(n - 1);
                    Vector2 pos = BeadPos(l, t, out Vector2 axis);
                    Vector2 next = BeadPos(l, MathF.Min(t + 1f / (n - 1), 1f), out _);
                    Vector2 tangent = k == n - 1 ? axis : (next - pos).SafeNormalize(axis);
                    float wid = BeadWidth(l, t);
                    float beadLen = MathF.Max(spacing * 1.35f, wid * 1.2f);
                    BloodQuadBatch.DrawBlob(sb, pos, tangent, beadLen, wid, l.Seed + k * 0.07f, 0.95f);
                }
            }
        }

        /// <summary>溅片按宽/窄扇分两组，减少 Apply</summary>
        private void DrawSheets(SpriteBatch sb, Effect sheetFx) {
            if (sheets.Count == 0) {
                return;
            }
            for (int pass = 0; pass < 2; pass++) {
                bool wide = pass == 0;
                bool any = false;
                foreach (Sheet s in sheets) {
                    if (s.Wide != wide) {
                        continue;
                    }
                    if (!any) {
                        BloodQuadBatch.SheetGroup(sheetFx, wide ? WideSpan : NarrowSpan);
                        any = true;
                    }
                    float life = s.Age / (float)s.Life;
                    float alpha = 1f - MathF.Pow(life, 3f) * 0.5f;
                    BloodQuadBatch.DrawSheet(sb, s.Pos, s.Dir, s.Size, s.Seed, SheetSpread(s), SheetTear(s), alpha);
                }
            }
        }

        /// <summary>血块：顺速度拉长的中团，寿末淡出</summary>
        private void DrawChunks(SpriteBatch sb, Effect blob) {
            if (chunks.Count == 0) {
                return;
            }
            BloodQuadBatch.BlobGroup(blob, 0.9f, 0.5f, 0.7f, 1f);
            foreach (Chunk c in chunks) {
                float speed = c.Vel.Length();
                Vector2 dir = speed > 0.1f ? c.Vel / speed : Vector2.UnitX;
                float alpha = 1f - MathF.Pow(c.Age / (float)c.Life, 3f);
                BloodQuadBatch.DrawBlob(sb, c.Pos, dir, c.Len * (1f + MathF.Min(speed, 10f) * 0.05f), c.Wid, c.Seed, alpha);
            }
        }

        /// <summary>缺 fxc 简笔：血核与拉丝都用 Extra_98 真 alpha 梭形盖章，杜绝无形演出</summary>
        private void DrawFallback(SpriteBatch sb) {
            Texture2D bead = CWRAsset.Extra_98.Value;
            Vector2 origin = bead.Size() * 0.5f;
            if (massActive) {
                float rot = massDir.ToRotation() + MathHelper.PiOver2;
                for (int k = 0; k < 3; k++) {
                    Vector2 pos = massCenter - massDir * (k * 14f);
                    float s = 1.1f - k * 0.25f;
                    sb.Draw(bead, pos - Main.screenPosition, null, EocMotion.Arterial * 0.9f, rot, origin, new Vector2(0.5f * s, 1.3f * s), SpriteEffects.None, 0f);
                }
            }
            foreach (Ligament l in ligaments) {
                int n = Math.Max(l.Beads, 2);
                for (int k = 0; k < n; k++) {
                    float t = k / (float)(n - 1);
                    Vector2 pos = BeadPos(l, t, out Vector2 axis);
                    float w = BeadWidth(l, t) / 12f;
                    sb.Draw(bead, pos - Main.screenPosition, null, EocMotion.VenousDark * 0.9f, axis.ToRotation() + MathHelper.PiOver2, origin, new Vector2(w, w * 1.8f), SpriteEffects.None, 0f);
                }
            }
            foreach (Chunk c in chunks) {
                sb.Draw(bead, c.Pos - Main.screenPosition, null, EocMotion.Arterial * 0.9f, c.Vel.ToRotation() + MathHelper.PiOver2, origin, new Vector2(c.Wid / 12f, c.Len / 42f), SpriteEffects.None, 0f);
            }
        }

        /// <summary>雾中瞳光：小、常亮微光 + 间歇一闪；突进冷却期微暗；本体隐身时不画</summary>
        private void DrawPupilGlint(Player owner, BloodfogIrisPlayer mp, int frame) {
            float bloom = VaultUtils.EaseOutCubic(MathHelper.Clamp((frame + 1) / 10f, 0f, 1f));
            float fade = MathHelper.Clamp(Projectile.timeLeft / 24f, 0f, 1f);
            float blink = MathF.Pow(MathF.Max(MathF.Sin(
                (float)Main.timeForVisualEffects * 0.11f + Projectile.whoAmI * 1.7f), 0f), 7f);
            float glow = (0.2f + blink * 0.8f) * bloom * fade;
            //可见冷却：突进转好前瞳光收敛(计时仅所有者端非零，远端不暗)
            if (mp.DashCooldown > 0) {
                glow *= 0.6f;
            }
            if (glow < 0.03f) {
                return;
            }

            Texture2D soft = CWRAsset.SoftGlow.Value;
            Texture2D flare = CWRAsset.StarFlare02.Value;
            Vector2 pos = owner.Center + new Vector2(owner.direction * 7f, -5f) - Main.screenPosition;
            //黑底贴图在预乘批里走 A=0 加色
            Color glintColor = EocMotion.IrisRed with { A = 0 };
            Main.spriteBatch.Draw(soft, pos, null, glintColor * (glow * 0.8f), 0f,
                soft.Size() / 2f, 0.4f * glow + 0.15f, SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(flare, pos, null, glintColor * (glow * 0.65f),
                Main.GlobalTimeWrappedHourly * 1.4f, flare.Size() / 2f, 0.11f * glow + 0.03f, SpriteEffects.None, 0f);
        }
        #endregion
    }

    /// <summary>
    /// 伏击印记：伏击命中点炸开的红色裂瞳，owner 命中钩子里生成、
    /// 经弹幕同步各端可见；纯演出无伤害
    /// </summary>
    internal class BloodfogAmbushMark : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        internal const int Life = 34;
        private float Progress => 1f - Projectile.timeLeft / (float)Life;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 8;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Life;
            Projectile.alpha = 255;
        }

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                PlayBurst();
            }
            Lighting.AddLight(Projectile.Center, EocMotion.IrisRed.ToVector3() * 0.8f * (1f - Progress));
        }

        /// <summary>命中拍：全向血珠 + 瞳色电花 + 湿裂响 + 血闪 + 震屏</summary>
        private void PlayBurst() {
            EocMotion.Shake(Projectile.Center, 5f, 10);
            if (VaultUtils.isServer) {
                return;
            }
            Lighting.AddLight(Projectile.Center, EocMotion.Arterial.ToVector3() * 1.2f);
            SoundEngine.PlaySound(SoundID.NPCHit13 with { Volume = 0.9f, Pitch = 0.22f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Item103 with { Volume = 0.75f, Pitch = -0.12f }, Projectile.Center);

            BloodfogIrisFX.Spray(Projectile.Center, Vector2.UnitX, 22, MathHelper.Pi, 4f, 12f, 1f, 1.9f, 20, 36, 0.32f, 0.984f, 6f);
            BloodfogIrisFX.FineMist(Projectile.Center, Vector2.UnitX, 8, 3f, MathHelper.Pi, 8f);
            for (int i = 0; i < 8; i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(3f, 8f);
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center, vel,
                    EocMotion.IrisRed, Main.rand.NextFloat(0.8f, 1.3f))?.Configure(false, Main.rand.Next(10, 18));
            }
            if (EocMotion.OnScreen(Projectile.Center)) {
                BloodfogScreenFX.PushFlash(0.3f);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            float progress = Progress;
            SpriteBatch sb = Main.spriteBatch;
            Vector2 center = Projectile.Center - Main.screenPosition;
            Effect effect = EffectLoader.BRelicIrisMark?.Value;
            if (effect == null) {
                DrawFallbackSigil(sb, center, progress);
                return false;
            }

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            effect.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * 0.03f);
            effect.Parameters["uProgress"]?.SetValue(progress);
            effect.Parameters["uIntensity"]?.SetValue(1f);
            //噪声显式绑 s1(shader 内 register(s1))
            GraphicsDevice gd = Main.graphics.GraphicsDevice;
            gd.Textures[1] = CWRAsset.PerlinNoise.Value;
            gd.SamplerStates[1] = SamplerState.LinearWrap;
            effect.CurrentTechnique.Passes[0].Apply();

            Texture2D pixel = VaultAsset.placeholder2.Value;
            float side = 300f * (0.85f + 0.35f * VaultUtils.EaseOutCubic(MathHelper.Clamp(progress * 2.4f, 0f, 1f)));
            Vector2 scale = new(side / pixel.Width, side / pixel.Height);
            sb.Draw(pixel, center, null, Color.White, 0f, pixel.Size() / 2f, scale, SpriteEffects.None, 0f);

            sb.End();
            gd.Textures[1] = null;
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            return false;
        }

        /// <summary>缺 fxc 简笔：血环扩张 + 竖瞳暗芯 + 星芒，杜绝无形演出</summary>
        private void DrawFallbackSigil(SpriteBatch sb, Vector2 center, float progress) {
            float fade = 1f - VaultUtils.EaseInQuad(progress);
            //DiffusionCircle 真 alpha，可正常染色
            Texture2D disc = CWRAsset.DiffusionCircle.Value;
            float discScale = (60f + progress * 90f) * 2f / disc.Width;
            sb.Draw(disc, center, null, EocMotion.Arterial * (0.7f * fade), 0f,
                disc.Size() / 2f, discScale, SpriteEffects.None, 0f);
            //Extra_98 真 alpha，允许画暗竖瞳
            Texture2D dark = CWRAsset.Extra_98.Value;
            Vector2 slitScale = new(0.12f, 0.9f * (0.4f + progress * 0.6f));
            sb.Draw(dark, center, null, EocMotion.VenousDark * (0.85f * fade), 0f,
                dark.Size() / 2f, slitScale, SpriteEffects.None, 0f);
            //星芒走 A=0 加色
            Texture2D flare = CWRAsset.StarFlare02.Value;
            sb.Draw(flare, center, null, (EocMotion.IrisRed with { A = 0 }) * fade,
                progress * 1.6f, flare.Size() / 2f, 0.3f * (0.5f + progress), SpriteEffects.None, 0f);
        }
    }
}
