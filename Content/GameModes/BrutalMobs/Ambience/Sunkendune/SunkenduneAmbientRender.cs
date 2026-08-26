using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Sunkendune
{
    /// <summary>
    /// 「尘窖」的屏幕层绘制（镜像 DungeonworldAmbientRender 的结构惯例）：
    /// 顶部石缝渗漏细沙流（连续细沙帘 + 稀疏沙粒尘）与「甲虫惊群」地面剪影。
    /// 沙是漫反射材质：全部乘本地光照亮度，黑暗处如实不可见；
    /// 暗色剪影用真 alpha 的 Extra_98 承载（加色批画不出暗形）。自开自收 AlphaBlend 批，无 RT 槽
    /// </summary>
    internal sealed class SunkenduneAmbientRender : RenderHandle
    {
        /// <summary>槽位分配权重 1.74</summary>
        public override float Weight => 1.74f;

        private const int MaxTrickles = 4;
        private const int MaxBeetles = 10;
        /// <summary>沙帘本体色（乘光照后使用）</summary>
        private static readonly Color SandTint = new(210, 182, 124);
        /// <summary>甲虫剪影色</summary>
        private static readonly Color BeetleTint = new(30, 24, 16);

        /// <summary>当前活跃细沙流数（喂给沙粒摩擦声循环）</summary>
        internal static int ActiveTrickleCount;

        private struct Trickle
        {
            internal bool Active;
            internal Vector2 Anchor;
            internal float Len;
            internal int Life;
            internal int MaxLife;
            internal float Phase;
            internal float WidthScale;
            internal int GrainTimer;
            internal int PuffTimer;
            internal float FloorY;
            internal float LightLum;
            internal int LightTimer;
        }

        private struct Beetle
        {
            internal bool Active;
            internal Vector2 Pos;
            internal float VelX;
            internal int Life;
            internal int MaxLife;
            internal float Seed;
            internal float Scale;
            internal int KickTimer;
        }

        private static readonly Trickle[] trickles = new Trickle[MaxTrickles];
        private static readonly Beetle[] beetles = new Beetle[MaxBeetles];
        private static int trickleSpawnIn;
        private static int beetleRollIn;

        //==================== 逻辑更新 ====================

        public override void UpdateBySystem(int index) {
            if (Main.dedServ || Main.gameMenu || Main.gamePaused) {
                return;
            }
            float presence = SunkenduneAmbience.Presence;
            if (presence < 0.02f) {
                //离场清空，防跨世界残留世界坐标
                for (int i = 0; i < trickles.Length; i++) {
                    trickles[i].Active = false;
                }
                for (int i = 0; i < beetles.Length; i++) {
                    beetles[i].Active = false;
                }
                ActiveTrickleCount = 0;
                return;
            }

            UpdateTrickles(presence);
            UpdateBeetles();
        }

        //==================== 细沙流 ====================

        private static void UpdateTrickles(float presence) {
            int count = 0;
            for (int i = 0; i < trickles.Length; i++) {
                if (!trickles[i].Active) {
                    continue;
                }
                ref Trickle t = ref trickles[i];
                t.Life++;
                //出屏过远或到寿即收
                if (t.Life >= t.MaxLife
                    || Math.Abs(t.Anchor.X - Main.screenPosition.X - Main.screenWidth * 0.5f) > Main.screenWidth
                    || Math.Abs(t.Anchor.Y - Main.screenPosition.Y - Main.screenHeight * 0.5f) > Main.screenHeight) {
                    t.Active = false;
                    continue;
                }
                count++;

                //光照缓存（漫反射材质，黑暗处如实变暗）
                if (--t.LightTimer <= 0) {
                    t.LightTimer = 15;
                    Color light = Lighting.GetColor((int)(t.Anchor.X / 16f), (int)(t.Anchor.Y / 16f) + 2);
                    t.LightLum = (light.R + light.G + light.B) / 765f;
                }

                //稀疏沙粒尘：连续沙帘之上的颗粒感（预算约 7 粒/秒/流）
                if (--t.GrainTimer <= 0) {
                    t.GrainTimer = Main.rand.Next(7, 11);
                    Dust grain = Dust.NewDustPerfect(
                        t.Anchor + new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), 2f),
                        DustID.Sand, new Vector2(0f, Main.rand.NextFloat(2.4f, 4.2f)),
                        120, default, Main.rand.NextFloat(0.65f, 0.95f));
                    grain.noGravity = false;
                }

                //落点微尘：沙流触地的余绪
                if (--t.PuffTimer <= 0) {
                    t.PuffTimer = Main.rand.Next(30, 44);
                    Dust puff = Dust.NewDustPerfect(
                        new Vector2(t.Anchor.X + Main.rand.NextFloat(-4f, 4f), t.FloorY - 2f),
                        DustID.Sand, new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), -Main.rand.NextFloat(0.2f, 0.7f)),
                        140, default, 0.7f);
                    puff.noGravity = true;
                }
            }
            ActiveTrickleCount = count;

            //补充：数量随在场强度走
            if (--trickleSpawnIn > 0) {
                return;
            }
            trickleSpawnIn = Main.rand.Next(40, 70);
            //常态密度预算：满强度也只保 3 条流（粒子合计 ≤40/s 量级）
            int cap = 1 + (int)(presence * 2f);
            if (count < cap) {
                TrySpawnTrickle();
            }
        }

        //在屏内向下找"石缝"：沙类实心瓦、下方有足够净空
        private static void TrySpawnTrickle() {
            int x = (int)(Main.screenPosition.X / 16f) + Main.rand.Next(-4, Main.screenWidth / 16 + 5);
            int top = (int)(Main.screenPosition.Y / 16f) - 6;
            for (int dy = 0; dy < 36; dy++) {
                int y = top + dy;
                if (!WorldGen.InWorld(x, y, 10)) {
                    return;
                }
                if (!WorldGen.SolidTile(x, y)) {
                    continue;
                }
                if (!SunkendunePlayer.IsSandFamily(Main.tile[x, y].TileType)) {
                    return;
                }
                //净空：缝下至少 6 格空气
                int clear = 0;
                while (clear < 18 && WorldGen.InWorld(x, y + 1 + clear, 10)
                    && !WorldGen.SolidTile(x, y + 1 + clear)) {
                    clear++;
                }
                if (clear < 6) {
                    return;
                }
                for (int i = 0; i < trickles.Length; i++) {
                    if (trickles[i].Active) {
                        continue;
                    }
                    float len = Math.Min(clear * 16f - 6f, Main.rand.NextFloat(110f, 240f));
                    trickles[i] = new Trickle {
                        Active = true,
                        Anchor = new Vector2(x * 16f + 8f, (y + 1) * 16f),
                        Len = len,
                        Life = 0,
                        MaxLife = Main.rand.Next(360, 660),
                        Phase = Main.rand.NextFloat(MathHelper.TwoPi),
                        WidthScale = Main.rand.NextFloat(0.7f, 1.15f),
                        GrainTimer = Main.rand.Next(6),
                        PuffTimer = Main.rand.Next(20, 40),
                        FloorY = (y + 1) * 16f + Math.Min(clear * 16f, len + 40f),
                        LightLum = 0.3f,
                        LightTimer = 0,
                    };
                    return;
                }
                return;
            }
        }

        //==================== 甲虫惊群（纯氛围，本机各自演出）====================

        private static void UpdateBeetles() {
            for (int i = 0; i < beetles.Length; i++) {
                if (!beetles[i].Active) {
                    continue;
                }
                ref Beetle b = ref beetles[i];
                b.Life++;
                if (b.Life >= b.MaxLife) {
                    BurrowAway(ref b);
                    continue;
                }
                b.Pos.X += b.VelX * (0.85f + 0.3f * MathF.Sin(b.Life * 0.55f + b.Seed));

                //贴地：脚下重新吸附地表；悬空或撞墙则钻沙离场
                int tx = (int)(b.Pos.X / 16f);
                int ty = (int)(b.Pos.Y / 16f);
                if (!WorldGen.InWorld(tx, ty, 10) || WorldGen.SolidTile(tx, ty - 1)) {
                    BurrowAway(ref b);
                    continue;
                }
                bool grounded = false;
                for (int dy = 0; dy < 4; dy++) {
                    if (WorldGen.SolidTile(tx, ty + dy)) {
                        b.Pos.Y = (ty + dy) * 16f;
                        grounded = true;
                        break;
                    }
                }
                if (!grounded) {
                    BurrowAway(ref b);
                    continue;
                }

                //足下踢沙
                if (--b.KickTimer <= 0) {
                    b.KickTimer = Main.rand.Next(10, 16);
                    Dust kick = Dust.NewDustPerfect(b.Pos + new Vector2(0f, -1f), DustID.Sand,
                        new Vector2(-b.VelX * 0.12f, -Main.rand.NextFloat(0.3f, 0.8f)),
                        140, default, 0.6f);
                    kick.noGravity = true;
                }
            }

            //低频惊群：45~90 秒一窝，玩家踏实地面时才起
            if (--beetleRollIn > 0) {
                return;
            }
            beetleRollIn = Main.rand.Next(2700, 5400);
            Player player = Main.LocalPlayer;
            if (SunkenduneAmbience.Presence < 0.6f || player.velocity.Y != 0f) {
                return;
            }
            TrySpawnSwarm(player);
        }

        private static void BurrowAway(ref Beetle b) {
            b.Active = false;
            for (int j = 0; j < 2; j++) {
                Dust dust = Dust.NewDustPerfect(b.Pos, DustID.Sand,
                    new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), -Main.rand.NextFloat(0.4f, 1f)),
                    130, default, 0.7f);
                dust.noGravity = true;
            }
        }

        private static void TrySpawnSwarm(Player player) {
            int dir = Main.rand.NextBool() ? 1 : -1;
            float startX = player.Center.X + dir * Main.rand.NextFloat(280f, 440f);
            int tx = (int)(startX / 16f);
            int ty = (int)(player.Bottom.Y / 16f) - 3;
            for (int dy = 0; dy < 10; dy++) {
                if (!WorldGen.InWorld(tx, ty + dy, 10)) {
                    return;
                }
                if (!WorldGen.SolidTile(tx, ty + dy)) {
                    continue;
                }
                if (!SunkendunePlayer.IsSandFamily(Main.tile[tx, ty + dy].TileType)) {
                    return;
                }
                float groundY = (ty + dy) * 16f;
                int swarm = Main.rand.Next(5, 9);
                int seated = 0;
                for (int i = 0; i < beetles.Length && seated < swarm; i++) {
                    if (beetles[i].Active) {
                        continue;
                    }
                    beetles[i] = new Beetle {
                        Active = true,
                        Pos = new Vector2(startX + dir * -seated * Main.rand.NextFloat(9f, 16f), groundY),
                        VelX = -dir * Main.rand.NextFloat(2.6f, 4.2f),
                        Life = 0,
                        MaxLife = Main.rand.Next(150, 240),
                        Seed = Main.rand.NextFloat(10f),
                        Scale = Main.rand.NextFloat(0.8f, 1.2f),
                        KickTimer = Main.rand.Next(10),
                    };
                    seated++;
                }
                if (seated > 0) {
                    SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.28f, Pitch = 0.45f, MaxInstances = 3 },
                        new Vector2(startX, groundY));
                }
                return;
            }
        }

        //==================== 绘制 ====================

        public override void EndEntityDraw(SpriteBatch spriteBatch, Main main
            , GraphicsDevice graphicsDevice, RenderTarget2D screenSwap) {
            if (Main.dedServ || Main.gameMenu) {
                return;
            }
            float presence = SunkenduneAmbience.Presence;
            if (presence < 0.02f) {
                return;
            }
            bool anyTrickle = false;
            for (int i = 0; i < trickles.Length; i++) {
                if (trickles[i].Active) {
                    anyTrickle = true;
                    break;
                }
            }
            bool anyBeetle = false;
            for (int i = 0; i < beetles.Length; i++) {
                if (beetles[i].Active) {
                    anyBeetle = true;
                    break;
                }
            }
            if (!anyTrickle && !anyBeetle) {
                return;
            }

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);
            if (anyTrickle) {
                DrawTrickles(spriteBatch, presence);
            }
            if (anyBeetle) {
                DrawBeetles(spriteBatch);
            }
            spriteBatch.End();
        }

        //细沙帘：3 段行进包络（顶端自石缝生出、末端散逸归零，防两端平切）
        private static void DrawTrickles(SpriteBatch sb, float presence) {
            Texture2D tex = CWRAsset.Extra_98?.Value;
            if (tex == null || tex.IsDisposed) {
                return;
            }
            Vector2 orig = tex.Size() / 2f;
            for (int i = 0; i < trickles.Length; i++) {
                if (!trickles[i].Active) {
                    continue;
                }
                ref Trickle t = ref trickles[i];
                //生命首尾淡入淡出
                float lifeEnv = Math.Min(t.Life / 24f, 1f)
                    * MathHelper.Clamp((t.MaxLife - t.Life) / 40f, 0f, 1f);
                float lum = MathHelper.Clamp(0.12f + 0.88f * t.LightLum, 0f, 1f);
                Color lit = new Color(
                    (byte)(SandTint.R * lum), (byte)(SandTint.G * lum), (byte)(SandTint.B * lum));

                //石缝暗口（真 alpha 暗形，锚住"从缝里漏出来"）
                sb.Draw(tex, t.Anchor - Main.screenPosition + new Vector2(0f, -2f), null,
                    new Color(26, 20, 12) * (0.5f * lifeEnv), 0f, orig,
                    new Vector2(0.3f, 0.09f), SpriteEffects.None, 0f);

                float segLen = t.Len * 0.42f;
                for (int s = 0; s < 3; s++) {
                    //段中心沿流向匀速下行循环
                    float head = (t.Life * 9f + s * t.Len / 3f) % t.Len;
                    float posEnv = MathF.Pow(MathF.Sin(MathHelper.Pi * head / t.Len), 0.7f);
                    float sway = MathF.Sin(t.Life * 0.05f + t.Phase + s * 2.1f) * 1.6f * (head / t.Len);
                    Vector2 pos = t.Anchor + new Vector2(sway, head) - Main.screenPosition;
                    float alpha = 0.38f * lifeEnv * posEnv * presence;
                    if (alpha < 0.01f) {
                        continue;
                    }
                    sb.Draw(tex, pos, null, lit * alpha, 0f, orig,
                        new Vector2(0.2f * t.WidthScale, segLen / 47f), SpriteEffects.None, 0f);
                }
            }
        }

        //甲虫剪影：暗色小梭形贴地窜行，亮度随本地光照（黑暗处如实看不见）
        private static void DrawBeetles(SpriteBatch sb) {
            Texture2D tex = CWRAsset.Extra_98?.Value;
            if (tex == null || tex.IsDisposed) {
                return;
            }
            Vector2 orig = tex.Size() / 2f;
            for (int i = 0; i < beetles.Length; i++) {
                if (!beetles[i].Active) {
                    continue;
                }
                ref Beetle b = ref beetles[i];
                Color light = Lighting.GetColor((int)(b.Pos.X / 16f), (int)(b.Pos.Y / 16f) - 1);
                float lum = (light.R + light.G + light.B) / 765f;
                float fade = Math.Min(b.Life / 14f, 1f)
                    * MathHelper.Clamp((b.MaxLife - b.Life) / 20f, 0f, 1f);
                float alpha = MathHelper.Clamp(lum * 1.4f, 0.08f, 0.85f) * fade;
                if (alpha < 0.02f) {
                    continue;
                }
                //疾走的碎步起伏与摆头
                float bob = -MathF.Abs(MathF.Sin(b.Life * 0.9f + b.Seed)) * 1.2f;
                float wig = MathF.Sin(b.Life * 1.3f + b.Seed) * 0.09f;
                Vector2 pos = b.Pos + new Vector2(0f, bob - 2f) - Main.screenPosition;
                sb.Draw(tex, pos, null, BeetleTint * alpha, MathHelper.PiOver2 + wig, orig,
                    new Vector2(0.24f, 0.17f) * b.Scale, SpriteEffects.None, 0f);
            }
        }
    }
}
