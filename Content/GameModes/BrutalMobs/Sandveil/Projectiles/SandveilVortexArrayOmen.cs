using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.Events;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Sandveil.Projectiles
{
    /// <summary>
    /// 沙元素沙龙卷阵预兆：扬手仪式期在朝向目标的一侧铺开扇形三道涌沙标记，
    /// 中央道固定空缺（<see cref="LaneGapIndex"/> 被布点循环真正读取，永远安全）。
    /// ai[0]=来源NPC+1|类型&lt;&lt;8 ai[1]=Pack(阵向,档位)；生成帧锁死锚点与阵向（预告即承诺）。
    /// 幽灵标记与提交发射共用同一套 道循环+落地取样，看到什么就来什么；
    /// 预告期来源死亡则取消（击杀施法者是有效反制）。档位只加每道柱数，缺口测试不变；
    /// 沙暴期缺口加宽（<see cref="CurrentGapPad"/>）=沙隐猎场的公平回款，思路镜像
    /// WastesSandConeTelegraph.CurrentGapHalfAngle，代码独立
    /// </summary>
    internal class SandveilVortexArrayOmen : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>扬手仪式帧数（任务契约 ≥40，档位一律不缩短）</summary>
        internal const int RitualFrames = 44;
        private const int FadeFrames = 10;

        //==== 公平阀门：具名缺口（布点循环真正读取） ====
        /// <summary>道数（扇形三道）</summary>
        internal const int LaneCount = 3;
        /// <summary>中央道空缺索引：布点与幽灵预览循环里被 continue 跳过的那一道，永远安全</summary>
        internal const int LaneGapIndex = 1;
        /// <summary>近道起止距离（像素，沿阵向）</summary>
        private const float LaneNearStart = 100f;
        private const float LaneNearEnd = 200f;
        /// <summary>远道起止距离</summary>
        private const float LaneFarStart = 320f;
        private const float LaneFarEnd = 420f;
        /// <summary>沙暴期缺口加宽量（两侧道各向外让开）：沙隐强度换来的公平回款</summary>
        private const float StormGapPad = 36f;

        /// <summary>当前缺口加宽：读原版天气（全端同步），布点与幽灵同读保持缺口即所见</summary>
        internal static float CurrentGapPad
            => Sandstorm.Happening && Sandstorm.Severity > 0.4f ? StormGapPad : 0f;

        /// <summary>每道柱数（档位 1/2/3，只加密度，缺口测试不变）</summary>
        private static readonly int[] ColumnsPerLaneByTier = [2, 3, 4];
        /// <summary>喷发波按距离外推的每像素延迟帧（读作向外滚的沙浪）</summary>
        private const float DelayPerPixel = 0.08f;
        /// <summary>柱底落地取样：从锚上方 8 瓦向下扫的最大瓦格数</summary>
        private const int ColumnGroundScanTiles = 24;

        //色板参考 DuneStorm 沙漠色板，数值抄色、代码独立
        private static readonly Color SandDeep = new(140, 108, 62);
        private static readonly Color SandBright = new(232, 202, 126, 0);
        private static readonly Color WarnGlow = new(255, 200, 110, 0);

        internal static float Pack(bool dirNegative, int tier)
            => (dirNegative ? 1 : 0) | (Math.Clamp(tier, 1, 3) << 1);

        private int SrcPacked => (int)Projectile.ai[0];
        private int SourceIndex => (SrcPacked & 255) - 1;
        private int SourceType => (SrcPacked >> 8) & 0xFFF;
        private float Dir => ((int)Projectile.ai[1] & 1) != 0 ? -1f : 1f;
        private int Tier => Math.Clamp((int)Projectile.ai[1] >> 1, 1, 3);
        private int TotalLife => RitualFrames + FadeFrames;
        private int Elapsed => TotalLife - Projectile.timeLeft;

        private bool Cancelled {
            get => Projectile.localAI[1] == 1f;
            set => Projectile.localAI[1] = value ? 1f : 0f;
        }

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 600;

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.hostile = false;//纯预告体，伤害经由沙涌柱
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = RitualFrames + FadeFrames;
            Projectile.netImportant = true;
        }

        /// <summary>纯预告体，永不参与伤害</summary>
        public override bool? CanDamage() => false;

        public override bool ShouldUpdatePosition() => false;

        /// <summary>某道的布点距离区间；两侧道贴缺口的一端随沙暴回款外让（中央道仅用于安全巷可视化）</summary>
        private static void LaneSpan(int lane, out float start, out float end) {
            float pad = CurrentGapPad;
            if (lane == 0) {
                start = LaneNearStart;
                end = LaneNearEnd - pad;
                return;
            }
            if (lane == 2) {
                start = LaneFarStart + pad;
                end = LaneFarEnd;
                return;
            }
            //中央道（LaneGapIndex）：布点循环从不到达这里，仅安全巷绘制取用
            start = LaneNearEnd - pad;
            end = LaneFarStart + pad;
        }

        /// <summary>道内第 k 柱的沿向距离</summary>
        private static float ColumnDist(float start, float end, int k, int count)
            => count <= 1 ? (start + end) * 0.5f : MathHelper.Lerp(start, end, k / (count - 1f));

        /// <summary>柱底落地取样：从锚高上方 8 瓦向下找可站立地表（布点与幽灵共用，所见即所落）</summary>
        private bool TryGroundAt(float worldX, out Vector2 basePos) {
            basePos = default;
            int tileX = (int)(worldX / 16f);
            int startY = (int)(Projectile.Center.Y / 16f) - 8;
            for (int dy = 0; dy < ColumnGroundScanTiles; dy++) {
                int tileY = startY + dy;
                if (!WorldGen.InWorld(tileX, tileY, 10)) {
                    return false;
                }
                if (WorldGen.SolidTile(tileX, tileY)) {
                    basePos = new Vector2(tileX * 16f + 8f, tileY * 16f);
                    return true;
                }
            }
            return false;
        }

        public override void AI() {
            if (Projectile.localAI[0] == 0f && !Main.dedServ) {
                Projectile.localAI[0] = 1f;
                SoundEngine.PlaySound(SoundID.Item34 with { Volume = 0.5f, Pitch = 0.4f, MaxInstances = 4 }, Projectile.Center);
            }
            int elapsed = Elapsed;

            //来源校验：沙元素死亡/槽位被新怪复用则取消提交（各端读同步的 npc.active，结论一致）
            if (!Cancelled && elapsed < RitualFrames) {
                if (SourceIndex < 0 || SourceIndex >= Main.maxNPCs || !Main.npc[SourceIndex].active
                    || Main.npc[SourceIndex].type != SourceType) {
                    Cancelled = true;
                }
            }

            if (!Cancelled && elapsed < RitualFrames && !Main.dedServ) {
                float progress = elapsed / (float)RitualFrames;
                //仪式期：两侧道地面渗沙（≤2 粒/帧），随进度增强
                if (Main.rand.NextBool(2)) {
                    int lane = Main.rand.NextBool() ? 0 : 2;
                    LaneSpan(lane, out float start, out float end);
                    float dist = MathHelper.Lerp(start, end, Main.rand.NextFloat());
                    if (TryGroundAt(Projectile.Center.X + Dir * dist, out Vector2 spot)) {
                        Dust seep = Dust.NewDustPerfect(spot + new Vector2(Main.rand.NextFloat(-8f, 8f), 2f),
                            DustID.Sand, new Vector2(0f, -Main.rand.NextFloat(0.8f, 1.6f + 2.5f * progress)),
                            120, default, 0.9f + progress * 0.7f);
                        seep.noGravity = true;
                    }
                }
                //扬手仪式锚：锚点原地起旋的沙尘（沙元素的可见起手）
                if (Main.rand.NextBool(2)) {
                    float swirl = Main.GlobalTimeWrappedHourly * 9f + Main.rand.NextFloat(MathHelper.TwoPi);
                    Dust rite = Dust.NewDustPerfect(Projectile.Center + swirl.ToRotationVector2() * 18f
                        - Vector2.UnitY * Main.rand.NextFloat(0f, 34f * progress),
                        DustID.Sand, new Vector2(-MathF.Sin(swirl) * 1.4f, -1.2f), 110, default, 1f);
                    rite.noGravity = true;
                }
            }

            if (elapsed == RitualFrames && !Cancelled) {
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    Emit();
                }
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Item34 with { Volume = 0.85f, Pitch = -0.15f, MaxInstances = 4 }, Projectile.Center);
                }
            }
        }

        /// <summary>
        /// 提交帧布点：与幽灵预览同一套 道循环+落地取样。
        /// 中央道（<see cref="LaneGapIndex"/>）被 continue 真正跳过=永远安全的逃生巷
        /// </summary>
        private void Emit() {
            int count = ColumnsPerLaneByTier[Tier - 1];
            int columnType = ModContent.ProjectileType<SandveilSurgeColumnProj>();
            for (int lane = 0; lane < LaneCount; lane++) {
                if (lane == LaneGapIndex) {
                    continue;//具名缺口：中央道永远安全
                }
                LaneSpan(lane, out float start, out float end);
                for (int k = 0; k < count; k++) {
                    float dist = ColumnDist(start, end, k, count);
                    if (!TryGroundAt(Projectile.Center.X + Dir * dist, out Vector2 basePos)) {
                        continue;//落不到地的柱位直接空缺（地形洞=天然安全）
                    }
                    int delay = (int)((dist - LaneNearStart) * DelayPerPixel);
                    Projectile.NewProjectile(Projectile.GetSource_FromAI(), basePos, Vector2.Zero,
                        columnType, Projectile.damage, 1f, Main.myPlayer, delay, SrcPacked);
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            int elapsed = Elapsed;
            float fade;
            if (Cancelled) {
                fade = 0.3f * MathHelper.Clamp(1f - elapsed / (float)RitualFrames, 0f, 1f);
            }
            else if (elapsed >= RitualFrames) {
                fade = MathHelper.Clamp(1f - (elapsed - RitualFrames) / (float)FadeFrames, 0f, 1f) * 0.5f;
            }
            else {
                fade = MathHelper.Clamp(elapsed / 10f, 0f, 1f);
            }
            if (fade <= 0.01f) {
                return false;
            }
            float progress = MathHelper.Clamp(elapsed / (float)RitualFrames, 0f, 1f);
            float pulse = 0.7f + 0.3f * MathF.Sin(Main.GlobalTimeWrappedHourly * 15f + Projectile.identity);

            Main.instance.LoadProjectile(ProjectileID.SandBallFalling);
            Texture2D sand = TextureAssets.Projectile[ProjectileID.SandBallFalling].Value;
            Texture2D rim = CWRAsset.Extra_98.Value;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            int count = ColumnsPerLaneByTier[Tier - 1];

            //幽灵柱位标记：与 Emit 同一套循环与取样，虚影即承诺
            for (int lane = 0; lane < LaneCount; lane++) {
                if (lane == LaneGapIndex) {
                    continue;
                }
                LaneSpan(lane, out float start, out float end);
                for (int k = 0; k < count; k++) {
                    float dist = ColumnDist(start, end, k, count);
                    if (!TryGroundAt(Projectile.Center.X + Dir * dist, out Vector2 basePos)) {
                        continue;
                    }
                    Vector2 pos = basePos + new Vector2(0f, 2f) - Main.screenPosition;
                    //暗沙底垫 + 幽灵沙块（升起量随进度），加色警芒收尾
                    Main.EntitySpriteDraw(rim, pos, null, SandDeep * (0.6f * fade), 0f,
                        rim.Size() / 2f, new Vector2(46f / rim.Width, 20f / rim.Height), SpriteEffects.None, 0);
                    float jig = MathF.Sin(Main.GlobalTimeWrappedHourly * 20f + dist * 0.13f);
                    Vector2 ghostPos = pos - new Vector2(-jig * 2f, 4f + 8f * progress);
                    Color ghost = Color.Lerp(lightColor, new Color(226, 196, 120), 0.5f) * (0.7f * fade * progress);
                    Main.EntitySpriteDraw(sand, ghostPos, null, ghost, jig * 0.5f, sand.Size() / 2f,
                        0.55f + 0.35f * progress, SpriteEffects.None, 0);
                    Main.EntitySpriteDraw(glow, pos, null, WarnGlow * (0.45f * fade * pulse * progress), 0f,
                        glow.Size() / 2f, new Vector2(0.5f, 0.22f), SpriteEffects.None, 0);
                }
            }

            //中央安全巷：亮沙缓光铺满缺口区间（指示逃生位，宽度含沙暴回款、与布点同读）
            LaneSpan(LaneGapIndex, out float gapStart, out float gapEnd);
            float gapMid = (gapStart + gapEnd) * 0.5f;
            if (TryGroundAt(Projectile.Center.X + Dir * gapMid, out Vector2 safeSpot)) {
                Vector2 lanePos = safeSpot + new Vector2(0f, -6f) - Main.screenPosition;
                Main.EntitySpriteDraw(glow, lanePos, null, SandBright * (0.4f * fade), 0f,
                    glow.Size() / 2f, new Vector2((gapEnd - gapStart) / glow.Width, 0.3f), SpriteEffects.None, 0);
            }

            //扬手仪式锚：锚点立起的凝沙光柱（越临近提交越高）
            Vector2 anchorPos = Projectile.Center - Main.screenPosition;
            Main.EntitySpriteDraw(glow, anchorPos - new Vector2(0f, 20f + 26f * progress), null,
                SandBright * (0.5f * fade * pulse), MathHelper.PiOver2, glow.Size() / 2f,
                new Vector2(0.5f + 0.5f * progress, 0.4f), SpriteEffects.None, 0);
            return false;
        }

        public override void DrawBehind(int index, List<int> behindNPCsAndTiles, List<int> behindNPCs,
            List<int> behindProjectiles, List<int> overPlayers, List<int> overWiresUI) => behindNPCsAndTiles.Add(index);
    }
}
