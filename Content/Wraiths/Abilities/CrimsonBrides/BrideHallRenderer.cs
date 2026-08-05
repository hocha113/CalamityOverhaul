using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.CrimsonRendSlashs;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.Wraiths.Abilities.CrimsonBrides
{
    /// <summary>
    /// 迎亲仪式的本地演出宿主：血迹路径、花轿/喜堂帘面、相位音效与粒子节拍。<br/>
    /// 纯表现层，不保存、不参与判定；权威状态在 <see cref="CrimsonBrideRitePlayer"/>。
    /// </summary>
    internal sealed class BrideHallRenderer : RenderHandle
    {
        //喜堂帘面基准尺寸（世界像素）
        private const float HallWidth = 380f;
        private const float HallHeight = 460f;
        private const int TrailPointCount = 26;

        //冷喜干血调色：比替死的活血更沉、更干
        private static readonly Color TrailDry = new(96, 12, 18);
        private static readonly Color PetalDry = new(120, 26, 34);
        private static readonly Color PetalDeep = new(86, 16, 22);
        private static readonly Color PetalLit = new(150, 40, 44);

        private sealed class HallFx
        {
            public uint Revision;
            public Vector2 From;
            public float SeedF;
            //已消费的最大节拍帧：网络快照回卷时不重放锣声与一次性爆点
            public int LastBeat;
        }

        //按玩家槽位存放的表现簿记，客户端专用
        private static readonly Dictionary<int, HallFx> fxByPlayer = [];

        public override float Weight => 1.23f;

        private struct HallPose
        {
            public Vector2 Center;
            public float Scale;
            public float Close;
            public float Candle;
            public float Slit;
            public float Fade;
            public float TrailFade;
        }

        internal static void OnRiteStarted(Player player, CrimsonBrideRitePlayer rite) {
            if (Main.dedServ || player == null) {
                return;
            }
            EnsureFx(player, rite);
        }

        private static HallFx EnsureFx(Player player, CrimsonBrideRitePlayer rite) {
            if (!fxByPlayer.TryGetValue(player.whoAmI, out HallFx fx)
                || fx.Revision != rite.RiteRevision) {
                float side = (rite.RiteSeed & 1) == 0 ? 1f : -1f;
                fx = new HallFx {
                    Revision = rite.RiteRevision,
                    From = player.Center + new Vector2(side * 470f, -36f),
                    SeedF = rite.RiteSeed * 0.37f,
                };
                fxByPlayer[player.whoAmI] = fx;
            }
            return fx;
        }

        /// <summary>由仪式推进逐帧调用（仅图形端）：音效节拍与粒子。</summary>
        internal static void OnRiteTick(Player player, CrimsonBrideRitePlayer rite) {
            if (Main.dedServ || player == null || rite.RiteTimer <= 0) {
                return;
            }
            HallFx fx = EnsureFx(player, rite);
            int t = rite.RiteTimer;
            bool freshBeat = t > fx.LastBeat;
            fx.LastBeat = Math.Max(fx.LastBeat, t);

            if (freshBeat) {
                PlayBeatCues(player, fx, t);
            }

            //轿至：血迹沿路点染（干血为主，几乎不飞溅）
            if (t <= CrimsonBrideRestart.PhaseArriveEnd && t % 3 == 0) {
                Vector2 pos = TrailPoint(fx, player.Center,
                    Main.rand.NextFloat(0.08f, 0.95f))
                    + Main.rand.NextVector2Circular(6f, 4f);
                PRTLoader.NewParticle<PRT_CrimsonBloodStain>(pos,
                    new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), Main.rand.NextFloat(0.2f, 0.9f)),
                    Main.rand.NextBool(3) ? CrimsonRendHitVFX.BloodDeep : TrailDry,
                    Main.rand.NextFloat(0.35f, 0.7f))
                    ?.Configure(Main.rand.Next(10, 18), 0.3f, 0.985f, Main.rand.Next(40, 70));
            }

            //迎入：帘影落位时挤出少量干尘
            if (t > CrimsonBrideRestart.PhaseArriveEnd
                && t <= CrimsonBrideRestart.PhaseWelcomeEnd && t % 8 == 0) {
                SpawnAsh(player.Center + Main.rand.NextVector2Circular(120f, 150f), 0.5f);
            }

            //合卺：一圈干花瓣被烛光照亮着散开
            if (t == CrimsonBrideRestart.RestoreFrame && freshBeat) {
                for (int i = 0; i < 10; i++) {
                    float angle = MathHelper.TwoPi * i / 10f + Main.rand.NextFloat(-0.2f, 0.2f);
                    Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(0.8f, 1.7f);
                    vel.Y -= Main.rand.NextFloat(0.2f, 0.7f);
                    PRTLoader.NewParticle<PRT_BrideDryPetal>(
                        player.Center + Main.rand.NextVector2Circular(24f, 30f), vel,
                        Main.rand.NextBool(3) ? PetalLit : PetalDry,
                        Main.rand.NextFloat(0.7f, 1.1f))
                        ?.Configure(Main.rand.Next(70, 105));
                }
                if (Main.LocalPlayer?.active == true
                    && Vector2.DistanceSquared(Main.LocalPlayer.Center, player.Center)
                        < 1400f * 1400f) {
                    Main.LocalPlayer.CWR()?.GetScreenShake(5f);
                }
            }

            //散场：帘开后干花瓣与尘缓缓飘出
            if (t > CrimsonBrideRestart.HideEnd && t <= CrimsonBrideRestart.TotalFrames - 4
                && t % 2 == 0) {
                Vector2 offset = Main.rand.NextVector2Circular(HallWidth * 0.36f, HallHeight * 0.38f);
                Vector2 vel = offset.SafeNormalize(Vector2.UnitY) * Main.rand.NextFloat(0.25f, 0.8f);
                vel.Y += Main.rand.NextFloat(0f, 0.3f);
                PRTLoader.NewParticle<PRT_BrideDryPetal>(player.Center + offset, vel,
                    Main.rand.NextBool() ? PetalDry : PetalDeep,
                    Main.rand.NextFloat(0.55f, 0.95f))
                    ?.Configure(Main.rand.Next(60, 95));
                if (Main.rand.NextBool(4)) {
                    SpawnAsh(player.Center + offset * 0.7f, 0.4f);
                }
            }
        }

        private static void SpawnAsh(Vector2 pos, float scale) {
            PRTLoader.NewParticle<PRT_CrimsonSmoke>(pos,
                new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), Main.rand.NextFloat(-0.5f, -0.1f)),
                default, scale * Main.rand.NextFloat(0.8f, 1.2f))
                ?.Configure(Main.rand.Next(26, 40),
                    new Color(66, 20, 24), new Color(28, 10, 14));
        }

        private static void PlayBeatCues(Player player, HallFx fx, int t) {
            //木轿吱呀出发
            if (t == 2) {
                SoundEngine.PlaySound(SoundID.DoorOpen with {
                    Pitch = -0.78f, Volume = 0.5f, MaxInstances = 3,
                }, fx.From);
            }
            //轿落座
            else if (t == CrimsonBrideRestart.PhaseArriveEnd) {
                SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with {
                    Pitch = -0.8f, Volume = 0.35f, MaxInstances = 3,
                }, player.Center);
            }
            //帘幕合拢
            else if (t == CrimsonBrideRestart.HideStart + 1) {
                SoundEngine.PlaySound(SoundID.DoorClosed with {
                    Pitch = -0.66f, Volume = 0.5f, MaxInstances = 3,
                }, player.Center);
            }
            //合卺：一声闷锣，远处极轻一记铃
            else if (t == CrimsonBrideRestart.RestoreFrame) {
                SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with {
                    Pitch = -0.55f, Volume = 0.55f, MaxInstances = 3,
                }, player.Center);
                SoundEngine.PlaySound(SoundID.Item35 with {
                    Pitch = -0.85f, Volume = 0.2f, MaxInstances = 3,
                }, player.Center);
            }
            //开帘散场
            else if (t == CrimsonBrideRestart.HideEnd) {
                SoundEngine.PlaySound(SoundID.DoorOpen with {
                    Pitch = -0.5f, Volume = 0.42f, MaxInstances = 3,
                }, player.Center);
            }
        }

        public override void UpdateBySystem(int index) {
            if (Main.gameMenu) {
                fxByPlayer.Clear();
                return;
            }
            //清掉已结束或玩家失效的簿记
            if (fxByPlayer.Count == 0) {
                return;
            }
            List<int> stale = null;
            foreach (int who in fxByPlayer.Keys) {
                Player player = who >= 0 && who < Main.maxPlayers ? Main.player[who] : null;
                if (player?.active != true
                    || !player.TryGetModPlayer(out CrimsonBrideRitePlayer rite)
                    || rite.RiteTimer <= 0) {
                    (stale ??= []).Add(who);
                }
            }
            if (stale != null) {
                foreach (int who in stale) {
                    fxByPlayer.Remove(who);
                }
            }
        }

        public override void EndEntityDraw(SpriteBatch spriteBatch, Main main,
            GraphicsDevice graphicsDevice, RenderTarget2D screenSwap) {
            if (Main.dedServ || fxByPlayer.Count == 0) {
                return;
            }

            foreach (KeyValuePair<int, HallFx> pair in fxByPlayer) {
                Player player = Main.player[pair.Key];
                if (player?.active != true
                    || !player.TryGetModPlayer(out CrimsonBrideRitePlayer rite)
                    || rite.RiteTimer <= 0) {
                    continue;
                }
                HallPose pose = ComputePose(player, rite, pair.Value);
                if (pose.TrailFade > 0.01f) {
                    DrawTrail(spriteBatch, pair.Value, player.Center, pose.TrailFade);
                }
                if (pose.Fade > 0.01f) {
                    DrawHall(spriteBatch, pair.Value, in pose);
                }
            }
        }

        // ===== 相位姿态 =====

        private static HallPose ComputePose(Player player, CrimsonBrideRitePlayer rite, HallFx fx) {
            int t = rite.RiteTimer;
            HallPose pose = new() {
                Center = player.Center,
                Scale = 1f,
                Close = 1f,
                Candle = 1f,
                Slit = 0f,
                Fade = 1f,
                TrailFade = 0f,
            };

            if (t <= CrimsonBrideRestart.PhaseArriveEnd) {
                //轿至：闭帘小轿沿血迹靠近
                float k = t / (float)CrimsonBrideRestart.PhaseArriveEnd;
                float ease = k * k * (3f - 2f * k);
                pose.Center = Vector2.Lerp(fx.From, player.Center, ease);
                pose.Scale = MathHelper.Lerp(0.34f, 0.46f, k);
                pose.Close = 0.88f;
                pose.Candle = 0f;
                pose.Fade = MathHelper.Clamp(t / 8f, 0f, 1f);
                pose.TrailFade = MathHelper.Clamp(k * 1.4f, 0f, 1f);
                return pose;
            }
            if (t <= CrimsonBrideRestart.PhaseWelcomeEnd) {
                //迎入：帘先张开受人，再合拢罩住；烛环随合拢点起
                float k = (t - CrimsonBrideRestart.PhaseArriveEnd)
                    / (float)(CrimsonBrideRestart.PhaseWelcomeEnd - CrimsonBrideRestart.PhaseArriveEnd);
                float grow = MathHelper.Clamp(k * 1.8f, 0f, 1f);
                pose.Scale = MathHelper.Lerp(0.46f, 1f, grow * grow * (3f - 2f * grow));
                pose.Close = k < 0.3f
                    ? MathHelper.Lerp(0.88f, 0.30f, k / 0.3f)
                    : MathHelper.Lerp(0.30f, 1f, (k - 0.3f) / 0.7f);
                pose.Candle = k * k;
                pose.TrailFade = MathHelper.Clamp(1f - k * 1.6f, 0f, 1f);
                return pose;
            }
            if (t <= CrimsonBrideRestart.PhaseUnionEnd) {
                //合卺：闭帘，帘缝里一线冷烛光
                float k = (t - CrimsonBrideRestart.PhaseWelcomeEnd)
                    / (float)(CrimsonBrideRestart.PhaseUnionEnd - CrimsonBrideRestart.PhaseWelcomeEnd);
                float pulse = MathF.Sin(k * MathHelper.Pi);
                pose.Slit = pulse * pulse;
                return pose;
            }
            //散场：开帘、熄烛、帘面化去
            float departK = (t - CrimsonBrideRestart.PhaseUnionEnd)
                / (float)(CrimsonBrideRestart.TotalFrames - CrimsonBrideRestart.PhaseUnionEnd);
            float open = departK * departK * (3f - 2f * departK);
            pose.Close = 1f - open;
            pose.Candle = MathHelper.Clamp(1f - departK * 1.3f, 0f, 1f);
            pose.Fade = 1f - MathHelper.Clamp((departK - 0.45f) / 0.55f, 0f, 1f);
            pose.Center = player.Center - new Vector2(0f, 8f * departK * departK);
            return pose;
        }

        // ===== 血迹路径 =====

        /// <summary>确定性路径点：起点到玩家的缓波折线，随 seed 定相，不逐帧抖动。</summary>
        private static Vector2 TrailPoint(HallFx fx, Vector2 to, float t01) {
            Vector2 from = fx.From;
            Vector2 delta = to - from;
            Vector2 normal = delta.SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2);
            float envelope = MathF.Sin(t01 * MathHelper.Pi);
            float wave = MathF.Sin(t01 * MathHelper.TwoPi * 1.2f + fx.SeedF * 7.1f) * 26f * envelope;
            return Vector2.Lerp(from, to, t01) + normal * wave;
        }

        private static void DrawTrail(SpriteBatch sb, HallFx fx, Vector2 to, float fade) {
            Texture2D pixel = VaultAsset.placeholder2?.Value;
            if (pixel == null) {
                return;
            }
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);
            Vector2 prev = TrailPoint(fx, to, 0f);
            for (int i = 1; i < TrailPointCount; i++) {
                float t01 = i / (float)(TrailPointCount - 1);
                Vector2 next = TrailPoint(fx, to, t01);
                Vector2 seg = next - prev;
                float len = seg.Length();
                if (len >= 1f) {
                    //靠近喜堂一端渐宽渐浓，像被拖进门的血痕
                    float width = MathHelper.Lerp(1.4f, 3.2f, t01);
                    Color color = TrailDry * (fade * MathHelper.Lerp(0.28f, 0.6f, t01));
                    sb.Draw(pixel, prev - Main.screenPosition, new Rectangle(0, 0, 1, 1),
                        color, seg.ToRotation(), Vector2.Zero,
                        new Vector2(len, width), SpriteEffects.None, 0f);
                }
                prev = next;
            }
            sb.End();
        }

        // ===== 喜堂帘面 =====

        private static void DrawHall(SpriteBatch sb, HallFx fx, in HallPose pose) {
            Effect effect = EffectLoader.BrideCurtain?.Value;
            Texture2D white = VaultAsset.placeholder2?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (white == null) {
                return;
            }

            float w = HallWidth * pose.Scale;
            float h = HallHeight * pose.Scale;
            Rectangle dest = new(
                (int)(pose.Center.X - Main.screenPosition.X - w * 0.5f),
                (int)(pose.Center.Y - Main.screenPosition.Y - h * 0.5f),
                (int)w, (int)h);

            if (effect != null && noise != null) {
                sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                    SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone,
                    null, Main.GameViewMatrix.TransformationMatrix);
                effect.Parameters["uNoiseTex"]?.SetValue(noise);
                effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
                effect.Parameters["uSeed"]?.SetValue(fx.SeedF);
                effect.Parameters["uFade"]?.SetValue(pose.Fade);
                effect.Parameters["uClose"]?.SetValue(pose.Close);
                effect.Parameters["uCandle"]?.SetValue(pose.Candle);
                effect.Parameters["uSlit"]?.SetValue(pose.Slit);
                effect.Parameters["uAspect"]?.SetValue(HallWidth / HallHeight);
                effect.CurrentTechnique = effect.Techniques["TechRig"];
                effect.CurrentTechnique.Passes[0].Apply();
                sb.Draw(white, dest, Color.White);
                sb.End();
                return;
            }

            DrawHallFallback(sb, fx, in pose, dest, white);
        }

        /// <summary>着色器缺失时的简笔帘面：竖条褶皱 + 烛点 + 帘缝，不落纯黑矩形。</summary>
        private static void DrawHallFallback(SpriteBatch sb, HallFx fx, in HallPose pose,
            Rectangle dest, Texture2D pixel) {
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);
            const int strips = 9;
            float halfW = dest.Width * 0.5f;
            float panelW = halfW * pose.Close;
            for (int side = 0; side < 2; side++) {
                float dir = side == 0 ? 1f : -1f;
                float edgeX = side == 0 ? dest.X : dest.Right;
                for (int i = 0; i < strips; i++) {
                    float k = i / (float)(strips - 1);
                    float stripW = panelW / strips;
                    float x = edgeX + dir * stripW * i;
                    //褶皱明暗与破口错落，避免整块矩形
                    float fold = 0.55f + 0.45f * MathF.Sin(k * 9.2f + fx.SeedF * 5f + side * 2.1f);
                    float ragged = 0.86f + 0.13f * MathF.Sin(k * 17f + fx.SeedF * 11f);
                    Color cloth = Color.Lerp(new Color(46, 12, 18), new Color(84, 18, 26), fold)
                        * (pose.Fade * 0.92f);
                    sb.Draw(pixel, new Vector2(side == 0 ? x : x - stripW, dest.Y),
                        new Rectangle(0, 0, 1, 1), cloth, 0f, Vector2.Zero,
                        new Vector2(stripW + 1f, dest.Height * ragged), SpriteEffects.None, 0f);
                }
            }
            sb.End();

            //烛点与帘缝走加色
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);
            Texture2D glow = CWRAsset.StarGlow01?.Value;
            if (glow != null && pose.Candle > 0.01f) {
                for (int i = 0; i < 5; i++) {
                    float cx = MathHelper.Lerp(0.16f, 0.84f, i / 4f);
                    float cy = 0.74f + 0.04f * MathF.Cos((cx - 0.5f) * 3.4f);
                    Vector2 pos = new(dest.X + dest.Width * cx, dest.Y + dest.Height * cy);
                    float breath = 0.8f + 0.2f * MathF.Sin(Main.GlobalTimeWrappedHourly * 2.3f + i * 1.7f);
                    Color flame = new Color(232, 162, 78, 0) * (pose.Candle * pose.Fade * 0.7f * breath);
                    sb.Draw(glow, pos, null, flame, 0f, glow.Size() * 0.5f,
                        0.10f * pose.Scale, SpriteEffects.None, 0f);
                }
            }
            if (pose.Slit > 0.01f) {
                Texture2D pixel2 = pixel;
                Color slit = new Color(255, 232, 200, 0) * (pose.Slit * pose.Fade * 0.85f);
                sb.Draw(pixel2, new Vector2(dest.Center.X - 1f, dest.Y + dest.Height * 0.18f),
                    new Rectangle(0, 0, 1, 1), slit, 0f, Vector2.Zero,
                    new Vector2(3f, dest.Height * 0.64f), SpriteEffects.None, 0f);
            }
            sb.End();
        }
    }
}
