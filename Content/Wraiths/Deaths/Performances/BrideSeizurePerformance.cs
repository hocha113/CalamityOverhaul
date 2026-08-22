using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.CrimsonRendSlashs;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.Wraiths.Deaths.Performances
{
    /// <summary>
    /// 绯嫁夺身「你成了新娘」，迎亲反转。<br/>
    /// 前兆：花轿不请自来，沿干血痕自远处吱呀而至；<br/>
    /// 显形：红绸自轿中探出缚身，把人抬进喜堂，帘合拢罩住；<br/>
    /// 处决：合卺之刻帘缝一线冷烛，闷锣落下，喝下的不是酒；<br/>
    /// 余韵：帘开，空轿带着人远去，只剩一地干花瓣。<br/>
    /// 材质：冷喜（干血/冷烛/空轿帘），复用 BrideCurtain 帘面与干花瓣，不混海蓝重启或替死血臂。
    /// </summary>
    internal sealed class BrideSeizurePerformance : WraithDeathPerformance
    {
        public override int OmenEndFrame => 46;
        public override int ExecuteFrame => 126;
        public override int TotalFrames => 192;

        //喜堂帘面基准尺寸（世界像素），与迎亲仪式同规格
        private const float HallWidth = 380f;
        private const float HallHeight = 460f;
        private const int TrailPointCount = 26;

        private static readonly Color TrailDry = new(96, 12, 18);
        private static readonly Color PetalDry = new(120, 26, 34);
        private static readonly Color PetalDeep = new(86, 16, 22);
        private static readonly Color PetalLit = new(150, 40, 44);
        private static readonly Color SilkDry = new(112, 18, 24);

        private Vector2 from;
        private float seedF;
        private bool routeSet;
        //红绸缚身量 0..1
        private float bindAmount;

        //帘缝冷烛：处决前后短促一线
        private int slitFlash;

        public override void OnBegin() {
            seedF = Seed * 0.37f;
            float side = (Seed & 1) == 0 ? 1f : -1f;
            from = Player.Center + new Vector2(side * 470f, -36f);
            routeSet = true;
            SoundEngine.PlaySound(SoundID.DoorOpen with {
                Pitch = -0.78f,
                Volume = 0.5f,
                MaxInstances = 3,
            }, from);
        }

        public override void Update() {
            if (!routeSet) {
                from = Player.Center + new Vector2(470f, -36f);
                routeSet = true;
            }
            if (slitFlash > 0) {
                slitFlash--;
            }

            switch (Phase) {
                case WraithSeizePhase.Omen:
                    //血痕沿路点染，轿越来越近
                    if (Timer % 3 == 0) {
                        Vector2 pos = TrailPoint(Main.rand.NextFloat(0.08f, 0.95f))
                            + Main.rand.NextVector2Circular(6f, 4f);
                        PRTLoader.NewParticle<PRT_CrimsonBloodStain>(pos,
                            new Vector2(Main.rand.NextFloat(-0.3f, 0.3f),
                                Main.rand.NextFloat(0.2f, 0.9f)),
                            Main.rand.NextBool(3) ? CrimsonRendHitVFX.BloodDeep : TrailDry,
                            Main.rand.NextFloat(0.35f, 0.7f))
                            ?.Configure(Main.rand.Next(10, 18), 0.3f, 0.985f, Main.rand.Next(40, 70));
                    }
                    //轿落座
                    if (Timer == OmenEndFrame - 2) {
                        SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with {
                            Pitch = -0.8f,
                            Volume = 0.4f,
                            MaxInstances = 3,
                        }, Player.Center);
                    }
                    break;

                case WraithSeizePhase.Manifest: {
                    //红绸缚身：前半段绷紧，后半段抬进帘内
                    bindAmount = MathHelper.Clamp(PhaseProgress / 0.45f, 0f, 1f);
                    if (Timer == OmenEndFrame + 3) {
                        SoundEngine.PlaySound(SoundID.Item35 with {
                            Pitch = -0.6f,
                            Volume = 0.3f,
                            MaxInstances = 3,
                        }, Player.Center);
                    }
                    //帘合拢
                    if (PhaseProgress is >= 0.62f and < 0.65f && Timer % 2 == 0) {
                        SoundEngine.PlaySound(SoundID.DoorClosed with {
                            Pitch = -0.66f,
                            Volume = 0.5f,
                            MaxInstances = 3,
                        }, Player.Center);
                    }
                    if (Timer % 8 == 0) {
                        SpawnAsh(Player.Center + Main.rand.NextVector2Circular(120f, 150f), 0.5f);
                    }
                    break;
                }

                case WraithSeizePhase.Linger:
                    //开帘散场：干花瓣与尘缓缓飘出，空轿远去
                    if (Timer == ExecuteFrame + 14) {
                        SoundEngine.PlaySound(SoundID.DoorOpen with {
                            Pitch = -0.5f,
                            Volume = 0.42f,
                            MaxInstances = 3,
                        }, DeathAnchor);
                    }
                    if (Timer % 2 == 0 && PhaseProgress > 0.2f) {
                        Vector2 offset = Main.rand.NextVector2Circular(
                            HallWidth * 0.36f, HallHeight * 0.38f);
                        Vector2 vel = offset.SafeNormalize(Vector2.UnitY)
                            * Main.rand.NextFloat(0.25f, 0.8f);
                        vel.Y += Main.rand.NextFloat(0f, 0.3f);
                        PRTLoader.NewParticle<PRT_BrideDryPetal>(DeathAnchor + offset, vel,
                            Main.rand.NextBool() ? PetalDry : PetalDeep,
                            Main.rand.NextFloat(0.55f, 0.95f))
                            ?.Configure(Main.rand.Next(60, 95));
                        if (Main.rand.NextBool(4)) {
                            SpawnAsh(DeathAnchor + offset * 0.7f, 0.4f);
                        }
                    }
                    break;
            }
        }

        public override void OnExecute() {
            slitFlash = 16;
            //合卺：一声闷锣 + 远处极轻一记铃，喝下的不是酒
            SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with {
                Pitch = -0.55f,
                Volume = 0.62f,
                MaxInstances = 3,
            }, Player.Center);
            SoundEngine.PlaySound(SoundID.Item35 with {
                Pitch = -0.85f,
                Volume = 0.22f,
                MaxInstances = 3,
            }, Player.Center);
            //一圈干花瓣被烛光照亮着散开
            for (int i = 0; i < 12; i++) {
                float angle = MathHelper.TwoPi * i / 12f + Main.rand.NextFloat(-0.2f, 0.2f);
                Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(0.8f, 1.7f);
                vel.Y -= Main.rand.NextFloat(0.2f, 0.7f);
                PRTLoader.NewParticle<PRT_BrideDryPetal>(
                    Player.Center + Main.rand.NextVector2Circular(24f, 30f), vel,
                    Main.rand.NextBool(3) ? PetalLit : PetalDry,
                    Main.rand.NextFloat(0.7f, 1.1f))
                    ?.Configure(Main.rand.Next(70, 105));
            }
            SpawnAsh(Player.Center, 0.7f);
        }

        public override bool HidesPlayer =>
            //帘合拢到散场开帘之间：进了喜堂的人不该被看见
            Phase == WraithSeizePhase.Manifest && PhaseProgress >= 0.62f
            || Phase == WraithSeizePhase.Linger && Timer < ExecuteFrame + 18;

        public override void Draw(SpriteBatch sb) {
            //血迹与红绸走当前批次，帘面自开批次（BrideCurtain shader）
            float trailFade = Phase switch {
                WraithSeizePhase.Omen => MathHelper.Clamp(PhaseProgress * 1.4f, 0f, 1f),
                WraithSeizePhase.Manifest => MathHelper.Clamp(1f - PhaseProgress * 1.6f, 0f, 1f),
                _ => 0f,
            };
            if (trailFade > 0.01f) {
                DrawTrail(sb, trailFade);
            }
            if (bindAmount > 0.01f && !HidesPlayer) {
                DrawSilkBinding(sb);
            }

            sb.End();
            DrawHall(sb);
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointWrap,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);
        }

        /// <summary>红绸缚身：几道自轿向玩家绷紧的干血色绸带，绷紧时收窄发直。</summary>
        private void DrawSilkBinding(SpriteBatch sb) {
            Texture2D pixel = VaultAsset.placeholder2?.Value;
            if (pixel == null) {
                return;
            }
            Rectangle src = new(0, 0, 1, 1);
            Vector2 target = Player.Center;
            Vector2 origin = Vector2.Lerp(from, target, 0.82f);
            const int Ribbons = 3;
            for (int r = 0; r < Ribbons; r++) {
                float phase = seedF * 5.3f + r * 2.1f;
                //绷紧后绸带几乎成直线，松时垂曲
                float slack = (1f - bindAmount) * 44f + 6f;
                const int Segs = 9;
                Vector2 prev = origin;
                for (int i = 1; i <= Segs; i++) {
                    float t = i / (float)Segs;
                    Vector2 baseline = Vector2.Lerp(origin, target, t);
                    Vector2 normal = (target - origin).SafeNormalize(Vector2.UnitX)
                        .RotatedBy(MathHelper.PiOver2);
                    float sag = MathF.Sin(t * MathHelper.Pi) * slack;
                    float wave = MathF.Sin(t * MathHelper.Pi * 1.6f + Timer * 0.09f + phase)
                        * slack * 0.35f;
                    Vector2 pos = baseline + normal * (wave + (r - 1) * 9f) + new Vector2(0f, sag);
                    Vector2 delta = pos - prev;
                    float len = delta.Length();
                    if (len > 0.6f) {
                        float width = MathHelper.Lerp(6.5f, 3.2f, bindAmount)
                            * MathF.Sin(t * MathHelper.Pi) + 1.4f;
                        sb.Draw(pixel, prev - Main.screenPosition, src,
                            SilkDry * (0.85f * MathHelper.Clamp(bindAmount * 1.4f, 0f, 1f)),
                            delta.ToRotation(), new Vector2(0f, 0.5f),
                            new Vector2(len, width), SpriteEffects.None, 0f);
                    }
                    prev = pos;
                }
            }
        }

        /// <summary>确定性血痕路径点：起点到玩家的缓波折线，随 seed 定相。</summary>
        private Vector2 TrailPoint(float t01) {
            Vector2 to = Player.dead ? DeathAnchor : Player.Center;
            Vector2 delta = to - from;
            Vector2 normal = delta.SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2);
            float envelope = MathF.Sin(t01 * MathHelper.Pi);
            float wave = MathF.Sin(t01 * MathHelper.TwoPi * 1.2f + seedF * 7.1f) * 26f * envelope;
            return Vector2.Lerp(from, to, t01) + normal * wave;
        }

        private void DrawTrail(SpriteBatch sb, float fade) {
            Texture2D pixel = VaultAsset.placeholder2?.Value;
            if (pixel == null) {
                return;
            }
            Rectangle src = new(0, 0, 1, 1);
            Vector2 prev = TrailPoint(0f);
            for (int i = 1; i < TrailPointCount; i++) {
                float t01 = i / (float)(TrailPointCount - 1);
                Vector2 next = TrailPoint(t01);
                Vector2 seg = next - prev;
                float len = seg.Length();
                if (len >= 1f) {
                    float width = MathHelper.Lerp(1.4f, 3.2f, t01);
                    Color color = TrailDry * (fade * MathHelper.Lerp(0.28f, 0.6f, t01));
                    sb.Draw(pixel, prev - Main.screenPosition, src, color,
                        seg.ToRotation(), Vector2.Zero, new Vector2(len, width),
                        SpriteEffects.None, 0f);
                }
                prev = next;
            }
        }

        //---- 喜堂帘面：与迎亲同一套 shader 参数，姿态按夺身相位反算 ----

        private void ResolveHallPose(out Vector2 center, out float scale, out float close,
            out float candle, out float slit, out float fade) {
            Vector2 anchor = Player.dead ? DeathAnchor : Player.Center;
            switch (Phase) {
                case WraithSeizePhase.Omen: {
                    //轿至：闭帘小轿沿血痕靠近
                    float k = PhaseProgress;
                    float ease = k * k * (3f - 2f * k);
                    center = Vector2.Lerp(from, anchor, ease);
                    scale = MathHelper.Lerp(0.34f, 0.46f, k);
                    close = 0.88f;
                    candle = 0f;
                    slit = 0f;
                    fade = MathHelper.Clamp(Timer / 8f, 0f, 1f);
                    return;
                }
                case WraithSeizePhase.Manifest: {
                    float k = PhaseProgress;
                    center = anchor;
                    //帘先张口受人，再合拢罩住；烛环随合拢点起
                    float grow = MathHelper.Clamp(k * 1.9f, 0f, 1f);
                    scale = MathHelper.Lerp(0.46f, 1f, grow * grow * (3f - 2f * grow));
                    close = k < 0.32f
                        ? MathHelper.Lerp(0.88f, 0.28f, k / 0.32f)
                        : MathHelper.Lerp(0.28f, 1f, (k - 0.32f) / 0.68f);
                    candle = k * k;
                    slit = 0f;
                    fade = 1f;
                    return;
                }
                default: {
                    //合卺一线冷烛 → 开帘 → 空轿远去
                    float k = PhaseProgress;
                    float open = MathHelper.Clamp((k - 0.12f) / 0.42f, 0f, 1f);
                    open = open * open * (3f - 2f * open);
                    //空轿带着人退回来处
                    float depart = MathHelper.Clamp((k - 0.4f) / 0.6f, 0f, 1f);
                    center = Vector2.Lerp(DeathAnchor, Vector2.Lerp(DeathAnchor, from, 0.55f),
                        depart * depart);
                    scale = MathHelper.Lerp(1f, 0.38f, depart);
                    close = MathHelper.Lerp(1f, 0.34f, open);
                    candle = MathHelper.Clamp(1f - k * 1.2f, 0f, 1f);
                    slit = slitFlash > 0 ? slitFlash / 16f : 0f;
                    fade = 1f - MathHelper.Clamp((k - 0.55f) / 0.45f, 0f, 1f);
                    return;
                }
            }
        }

        private void DrawHall(SpriteBatch sb) {
            ResolveHallPose(out Vector2 center, out float scale, out float close,
                out float candle, out float slit, out float fade);
            if (fade <= 0.01f) {
                return;
            }
            Texture2D white = VaultAsset.placeholder2?.Value;
            if (white == null) {
                return;
            }
            Effect effect = EffectLoader.BrideCurtain?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            float w = HallWidth * scale;
            float h = HallHeight * scale;
            Rectangle dest = new(
                (int)(center.X - Main.screenPosition.X - w * 0.5f),
                (int)(center.Y - Main.screenPosition.Y - h * 0.5f),
                (int)w, (int)h);

            if (effect != null && noise != null) {
                sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                    SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone,
                    null, Main.GameViewMatrix.TransformationMatrix);
                effect.Parameters["uNoiseTex"]?.SetValue(noise);
                effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
                effect.Parameters["uSeed"]?.SetValue(seedF);
                effect.Parameters["uFade"]?.SetValue(fade);
                effect.Parameters["uClose"]?.SetValue(close);
                effect.Parameters["uCandle"]?.SetValue(candle);
                effect.Parameters["uSlit"]?.SetValue(slit);
                effect.Parameters["uAspect"]?.SetValue(HallWidth / HallHeight);
                effect.CurrentTechnique = effect.Techniques["TechRig"];
                effect.CurrentTechnique.Passes[0].Apply();
                sb.Draw(white, dest, Color.White);
                sb.End();
                return;
            }

            DrawHallFallback(sb, dest, white, close, candle, slit, fade, scale);
        }

        /// <summary>着色器缺席时的简笔帘面：竖条褶皱 + 烛点 + 帘缝，不落纯黑矩形。</summary>
        private void DrawHallFallback(SpriteBatch sb, Rectangle dest, Texture2D pixel,
            float close, float candle, float slit, float fade, float scale) {
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);
            const int Strips = 9;
            float halfW = dest.Width * 0.5f;
            float panelW = halfW * close;
            for (int side = 0; side < 2; side++) {
                float dir = side == 0 ? 1f : -1f;
                float edgeX = side == 0 ? dest.X : dest.Right;
                for (int i = 0; i < Strips; i++) {
                    float k = i / (float)(Strips - 1);
                    float stripW = panelW / Strips;
                    float x = edgeX + dir * stripW * i;
                    float fold = 0.55f + 0.45f * MathF.Sin(k * 9.2f + seedF * 5f + side * 2.1f);
                    float ragged = 0.86f + 0.13f * MathF.Sin(k * 17f + seedF * 11f);
                    Color cloth = Color.Lerp(new Color(46, 12, 18), new Color(84, 18, 26), fold)
                        * (fade * 0.92f);
                    sb.Draw(pixel, new Vector2(side == 0 ? x : x - stripW, dest.Y),
                        new Rectangle(0, 0, 1, 1), cloth, 0f, Vector2.Zero,
                        new Vector2(stripW + 1f, dest.Height * ragged), SpriteEffects.None, 0f);
                }
            }
            sb.End();

            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);
            Texture2D glow = CWRAsset.StarGlow01?.Value;
            if (glow != null && candle > 0.01f) {
                for (int i = 0; i < 5; i++) {
                    float cx = MathHelper.Lerp(0.16f, 0.84f, i / 4f);
                    float cy = 0.74f + 0.04f * MathF.Cos((cx - 0.5f) * 3.4f);
                    Vector2 pos = new(dest.X + dest.Width * cx, dest.Y + dest.Height * cy);
                    float breath = 0.8f + 0.2f * MathF.Sin(
                        Main.GlobalTimeWrappedHourly * 2.3f + i * 1.7f);
                    sb.Draw(glow, pos, null,
                        new Color(232, 162, 78, 0) * (candle * fade * 0.7f * breath), 0f,
                        glow.Size() * 0.5f, 0.10f * scale, SpriteEffects.None, 0f);
                }
            }
            if (slit > 0.01f) {
                sb.Draw(pixel, new Vector2(dest.Center.X - 1f, dest.Y + dest.Height * 0.18f),
                    new Rectangle(0, 0, 1, 1), new Color(255, 232, 200, 0) * (slit * fade * 0.85f),
                    0f, Vector2.Zero, new Vector2(3f, dest.Height * 0.64f), SpriteEffects.None, 0f);
            }
            sb.End();
        }

        private static void SpawnAsh(Vector2 pos, float scale) {
            PRTLoader.NewParticle<PRT_CrimsonSmoke>(pos,
                new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), Main.rand.NextFloat(-0.5f, -0.1f)),
                default, scale * Main.rand.NextFloat(0.8f, 1.2f))
                ?.Configure(Main.rand.Next(26, 40),
                    new Color(66, 20, 24), new Color(28, 10, 14));
        }

        public override Vector2 CameraFocus => Phase switch {
            //轿至期镜头稍向来处偏，让人看见轿在靠近
            WraithSeizePhase.Omen => Vector2.Lerp(Player?.Center ?? DeathAnchor, from, 0.22f),
            WraithSeizePhase.Linger => DeathAnchor,
            _ => Player?.Center ?? DeathAnchor,
        };

        public override float CameraZoom => Phase switch {
            WraithSeizePhase.Omen => 1.08f,
            WraithSeizePhase.Manifest => MathHelper.Lerp(1.16f, 1.3f, PhaseProgress),
            WraithSeizePhase.Linger => 1.1f,
            _ => 1f,
        };

        //冷喜不喧闹：只有落轿、合帘与合卺各一颤
        public override float ShakeIntensity => slitFlash > 0 ? slitFlash * 0.32f : 0f;

        public override void UpdatePlayerMotion() {
            if (Player == null || Player.dead) {
                return;
            }
            //缚身后被"抬着走"：先急减速再钉死
            Player.velocity *= bindAmount > 0.3f ? 0.35f : 0.6f;
            if (Timer > 8 && bindAmount > 0.3f) {
                Player.velocity = Vector2.Zero;
            }
            Player.fallStart = (int)(Player.position.Y / 16f);
        }
    }
}
