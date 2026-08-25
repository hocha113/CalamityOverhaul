using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.Bestiary;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework.Graphics;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.NPCs.Elites
{
    /// <summary>
    /// 提灯巡守（L2 牢狱层，WAVE2-ENEMIES §3.1）：灯锥扫到你就举灯鸣警、全层怪向你涌来。
    /// 状态机：0 巡逻（灯锥常显，入锥累计 30f 宽限）→ 1 鸣警（75f，三环三响，
    /// 窗口内击杀=警报流产）→ 2 追缉（×1.6 追击 + 活警报器浓度 + 即时增援已在鸣警尾拍发出）
    /// → 3 熄灯撤防（180f 解除信号）→ 回巡逻（30s 冷却）。
    /// 联机：入锥裁决/鸣警计时/增援生成全服务器，ai[0..3] 过线，各端从 ai 重放灯锥/三环节拍；
    /// 出生 alpha 目标恒 0。材质=锈铁+灯油火：灯随步摆 / 锥内浮尘发亮 / 鸣警灯焰白热过曝一拍即回暖。
    /// </summary>
    internal class LanternWarden : EliteModNPC
    {
        public override string Texture => "Terraria/Images/NPC_" + NPCID.RustyArmoredBonesSword;

        //==================== 参数（建议值，验收再调）====================

        private const int StatePatrol = 0;
        private const int StateAlarm = 1;
        private const int StateChase = 2;
        private const int StateStanddown = 3;

        /// <summary>灯锥半径（px）</summary>
        internal const float ConeRange = 340f;
        /// <summary>灯锥半角余弦（半角约 25°）</summary>
        private const float ConeHalfCos = 0.906f;
        /// <summary>灯锥轴线下俯角（弧度，前下 25°）</summary>
        private const float ConePitch = 0.436f;
        /// <summary>入锥宽限帧：满 30f 才鸣警，撤出即衰减</summary>
        private const int GraceFrames = 30;
        /// <summary>鸣警总帧</summary>
        private const int AlarmFrames = 75;
        /// <summary>追缉硬上限 / 断视脱战帧</summary>
        private const int ChaseFrames = 600;
        private const int LoseSightFrames = 360;
        /// <summary>撤防帧 / 警报冷却（30s）</summary>
        private const int StanddownFrames = 180;
        private const int AlarmCooldown = 1800;

        private const float PatrolSpeed = 1.3f;
        private const float ChaseSpeed = 2.3f;

        /// <summary>灯油火主色（暖橙）</summary>
        internal static readonly Color LampWarm = new(255, 180, 90);
        private static readonly Color LampCore = new(255, 230, 170);
        /// <summary>老铁锈褐（drawColor 乘色）</summary>
        private static readonly Color RustMul = new(205, 150, 105);

        /// <summary>转身锁（防抖，各端本地步态用）</summary>
        private int turnLock;

        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = Main.npcFrameCount[NPCID.RustyArmoredBonesSword];
            NPCID.Sets.SpecificDebuffImmunity[Type][BuffID.Confused] = true;
        }

        public override void SetDefaults() {
            NPC.width = 18;
            NPC.height = 40;
            NPC.damage = 34;
            NPC.defense = 12;
            NPC.lifeMax = 260;
            NPC.knockBackResist = 0.25f;
            NPC.aiStyle = -1;
            NPC.npcSlots = 1.5f;
            NPC.value = 40000f;
            NPC.HitSound = SoundID.NPCHit2;
            NPC.DeathSound = SoundID.NPCDeath2;
            AnimationType = NPCID.RustyArmoredBonesSword;
        }

        public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry) {
            bestiaryEntry.Info.AddRange([
                BestiaryDatabaseNPCsPopulator.CommonTags.SpawnConditions.Biomes.TheDungeon,
                new FlavorTextBestiaryInfoElement("Mods.CalamityOverhaul.NPCs.LanternWarden.Bestiary"),
            ]);
        }

        //==================== 投放（§4：L2 主投 0.10；L6 二现已被裁决 §1-11 砍掉，本波 L6/L7 零投放）====================

        public override float SpawnChance(NPCSpawnInfo spawnInfo) {
            if (!DungeonworldEliteDirector.CommonSpawnGate(spawnInfo, Type)) {
                return 0f;
            }
            return DungeonworldEliteDirector.BandIndexForRow(spawnInfo.SpawnTileY) == 1 ? 0.10f : 0f;
        }

        //==================== AI ====================

        public override void AI() {
            HealAlpha(0);
            AmbientClock++;
            if (StateEdge()) {
                PlayStateEdgeCue();
            }
            ServerSyncPacer();

            if (NPC.direction == 0) {
                NPC.direction = 1;
            }

            switch ((int)State) {
                case StatePatrol:
                    UpdatePatrol();
                    break;
                case StateAlarm:
                    UpdateAlarm();
                    break;
                case StateChase:
                    UpdateChase();
                    break;
                default:
                    UpdateStanddown();
                    break;
            }

            NPC.spriteDirection = NPC.direction;
            DoLanternLight();
            DoConeDust();
        }

        private void PlayStateEdgeCue() {
            switch ((int)State) {
                case StateAlarm:
                    //灯焰拔高的点火声
                    SoundEngine.PlaySound(SoundID.Item20 with { Volume = 0.6f, Pitch = 0.3f, MaxInstances = 3 }, NPC.Center);
                    break;
                case StateChase:
                    SoundEngine.PlaySound(SoundID.NPCHit2 with { Volume = 0.7f, Pitch = -0.5f, MaxInstances = 3 }, NPC.Center);
                    break;
                case StateStanddown:
                    //解除的哑钟
                    SoundEngine.PlaySound(SoundID.Item35 with { Volume = 0.3f, Pitch = -0.6f, MaxInstances = 2 }, NPC.Center);
                    break;
            }
        }

        //==================== 巡逻 ====================

        private void UpdatePatrol() {
            WalkGait(PatrolSpeed, 0.06f, turnAtLedge: true);

            //警报冷却记帧（服务器裁决用，表现不读它）
            if (!VaultUtils.isClient && StackCount > 0f) {
                StackCount--;
            }

            //入锥裁决只在服务器，每 10 tick 一次/每玩家：距离+夹角+通视
            if (VaultUtils.isClient || (int)AmbientClock % 10 != 0 || StackCount > 0f) {
                return;
            }
            bool seen = false;
            Vector2 axis = ConeAxis();
            foreach (Player player in Main.ActivePlayers) {
                if (player.dead) {
                    continue;
                }
                Vector2 toPlayer = player.Center - LanternPos();
                float dist = toPlayer.Length();
                if (dist > ConeRange || dist < 1f) {
                    continue;
                }
                if (Vector2.Dot(toPlayer / dist, axis) < ConeHalfCos) {
                    continue;
                }
                if (!Collision.CanHitLine(NPC.position, NPC.width, NPC.height,
                    player.position, player.width, player.height)) {
                    continue;
                }
                seen = true;
                break;
            }
            float old = StateParam;
            if (seen) {
                StateParam = Math.Min(GraceFrames + 10, StateParam + 10);
            }
            else {
                StateParam = Math.Max(0, StateParam - 10);
            }
            if (StateParam != old) {
                NPC.netUpdate = true;
            }
            if (StateParam >= GraceFrames) {
                ChangeState(StateAlarm);
            }
        }

        //==================== 鸣警（75f，窗口内被击杀=警报流产）====================

        private void UpdateAlarm() {
            StateTimer++;
            NPC.velocity.X *= 0.8f;
            int t = (int)StateTimer;

            //三响三环，节拍固定可背；严格前进沿防回卷重播
            if (t >= RingBeatFrame(0) && BeatForward(1)) {
                SoundEngine.PlaySound(SoundID.Item35 with { Volume = 0.8f, Pitch = -0.15f, MaxInstances = 3 }, NPC.Center);
            }
            if (t >= RingBeatFrame(1) && BeatForward(2)) {
                SoundEngine.PlaySound(SoundID.Item35 with { Volume = 0.85f, Pitch = 0.05f, MaxInstances = 3 }, NPC.Center);
            }
            if (t >= RingBeatFrame(2) && BeatForward(3)) {
                SoundEngine.PlaySound(SoundID.Item35 with { Volume = 0.9f, Pitch = 0.25f, MaxInstances = 3 }, NPC.Center);
            }

            if (VaultUtils.isClient || t < AlarmFrames) {
                return;
            }
            //鸣警成功当拍：即时增援保底通道（EditSpawnRate 只是浓度阀，可能来得慢）
            NPC.TargetClosest(faceTarget: true);
            if (NPC.HasValidTarget) {
                SpawnReinforcements(Main.player[NPC.target]);
            }
            ChangeState(StateChase);
        }

        /// <summary>三环节拍帧：12/37/62</summary>
        internal static int RingBeatFrame(int index) => 12 + index * 25;

        //==================== 追缉（活警报器）====================

        private void UpdateChase() {
            StateTimer++;

            //服务器每帧通报浓度（追缉结束后 Director 里自然残留 8s）
            if (!VaultUtils.isClient) {
                DungeonworldEliteDirector.ReportAlarmChase(NPC.whoAmI, NPC.Center);
            }

            NPC.TargetClosest(faceTarget: true);
            if (!NPC.HasValidTarget) {
                if (!VaultUtils.isClient) {
                    ChangeState(StateStanddown);
                }
                return;
            }
            Player target = Main.player[NPC.target];

            //追击步态 + 小跳
            float dx = target.Center.X - NPC.Center.X;
            if (Math.Abs(dx) > 8f) {
                NPC.direction = Math.Sign(dx);
            }
            WalkGait(ChaseSpeed, 0.12f, turnAtLedge: false);
            bool standing = NPC.velocity.Y == 0f;
            if (standing) {
                if (NPC.collideX || (target.Bottom.Y < NPC.Top.Y - 48f && Math.Abs(dx) < 140f)) {
                    NPC.velocity.Y = -7.6f;
                }
                else if ((int)StateTimer % 55 == 0) {
                    NPC.velocity.Y = -4.5f;
                }
            }

            //断视/超时脱战（服务器裁决）
            if (VaultUtils.isClient) {
                return;
            }
            if ((int)AmbientClock % 10 == 0) {
                bool sight = Collision.CanHitLine(NPC.position, NPC.width, NPC.height,
                    target.position, target.width, target.height);
                StateParam = sight ? 0f : StateParam + 10f;
            }
            if (StateParam >= LoseSightFrames || StateTimer >= ChaseFrames) {
                ChangeState(StateStanddown);
            }
        }

        //==================== 熄灯撤防 ====================

        private void UpdateStanddown() {
            StateTimer++;
            NPC.velocity.X *= 0.85f;
            if (!VaultUtils.isClient && StateTimer >= StanddownFrames) {
                ChangeState(StatePatrol);
                StackCount = AlarmCooldown;
            }
        }

        //==================== 步态（最笨 fighter：撞墙/临崖转身，可读性优先于聪明）====================

        private void WalkGait(float maxSpeed, float accel, bool turnAtLedge) {
            if (turnLock > 0) {
                turnLock--;
            }
            NPC.velocity.X += accel * NPC.direction;
            NPC.velocity.X = MathHelper.Clamp(NPC.velocity.X, -maxSpeed, maxSpeed);

            bool standing = NPC.velocity.Y == 0f;
            if (!standing || turnLock > 0) {
                return;
            }
            bool turn = NPC.collideX;
            if (!turn && turnAtLedge) {
                int probeX = (int)((NPC.Center.X + NPC.direction * (NPC.width / 2 + 8)) / 16f);
                int probeY = (int)(NPC.Bottom.Y / 16f);
                turn = !StandableTile(probeX, probeY) && !StandableTile(probeX, probeY + 1);
            }
            if (turn) {
                NPC.direction = -NPC.direction;
                NPC.velocity.X = 0f;
                turnLock = 12;
            }
        }

        private static bool StandableTile(int x, int y) {
            if (!WorldGen.InWorld(x, y, 5)) {
                return false;
            }
            Tile tile = Main.tile[x, y];
            return tile.HasUnactuatedTile && (Main.tileSolid[tile.TileType] || Main.tileSolidTop[tile.TileType]);
        }

        //==================== 增援（服务器）：屏幕外最近墙沿落 2 只本层原版怪 ====================

        private void SpawnReinforcements(Player target) {
            int[] pool = [NPCID.AngryBones, NPCID.AngryBonesBig, NPCID.AngryBonesBigMuscle, NPCID.AngryBonesBigHelmet];
            int spawned = 0;
            for (int side = -1; side <= 1; side += 2) {
                if (!TryFindPerch(target, side, out Point tile)) {
                    continue;
                }
                int type = pool[Main.rand.Next(pool.Length)];
                int idx = NPC.NewNPC(NPC.GetSource_FromAI(), tile.X * 16 + 8, tile.Y * 16, type);
                if (idx >= 0 && idx < Main.maxNPCs) {
                    spawned++;
                }
            }
            //两侧都找不到落点：退化为巡守脚边出（警报必须有后果）
            for (; spawned < 2; spawned++) {
                int type = pool[Main.rand.Next(pool.Length)];
                NPC.NewNPC(NPC.GetSource_FromAI(), (int)NPC.Center.X + (spawned == 0 ? -32 : 32), (int)NPC.Bottom.Y, type);
            }
        }

        /// <summary>目标一侧约 62 格（略出屏）扫墙沿：脚下可站 + 上方 3 格净空</summary>
        private static bool TryFindPerch(Player target, int side, out Point tile) {
            int baseX = (int)(target.Center.X / 16f) + side * 62;
            int baseY = (int)(target.Center.Y / 16f);
            for (int step = 0; step < 8; step++) {
                int x = baseX + side * step * 2;
                if (x < 12 || x > Main.maxTilesX - 12) {
                    break;
                }
                for (int dy = -14; dy <= 14; dy++) {
                    int y = baseY + dy;
                    if (y < 12 || y >= Main.maxTilesY - 12) {
                        continue;
                    }
                    if (!WorldGen.SolidTile(x, y)) {
                        continue;
                    }
                    if (Collision.SolidTiles(x - 1, x + 1, y - 4, y - 1)) {
                        continue;
                    }
                    tile = new Point(x, y);
                    return true;
                }
            }
            tile = default;
            return false;
        }

        //==================== 表现：灯光 / 锥内浮尘 ====================

        /// <summary>灯锥轴线：行进方向前下 25°</summary>
        private Vector2 ConeAxis()
            => new Vector2(NPC.direction * MathF.Cos(ConePitch), MathF.Sin(ConePitch));

        private Vector2 LanternPos() {
            if ((int)State == StateAlarm) {
                return NPC.Center + new Vector2(NPC.direction * 2f, -24f);
            }
            return NPC.Center + new Vector2(NPC.direction * 9f, 1f);
        }

        /// <summary>灯焰强度：状态×计时的确定函数（各端一致）</summary>
        private float FlameLevel() {
            int t = (int)StateTimer;
            switch ((int)State) {
                case StateAlarm:
                    return t < 4 ? 1.6f : MathHelper.Lerp(1.2f, 1.0f, Math.Min(1f, (t - 4) / 20f));
                case StateChase:
                    return 0.95f;
                case StateStanddown:
                    return MathHelper.Lerp(0.9f, 0.5f, Math.Min(1f, t / (float)StanddownFrames));
                default:
                    float level = 0.62f + 0.05f * MathF.Sin(AmbientClock * 0.05f + Seed);
                    if (StateParam > 0f) {
                        //宽限期灯焰双闪（潜行玩家的撤出窗口提示）
                        level *= 0.72f + 0.28f * MathF.Sin(AmbientClock * 1.2f);
                    }
                    return level;
            }
        }

        private void DoLanternLight() {
            float level = FlameLevel();
            Vector2 pos = LanternPos();
            Lighting.AddLight(pos, 0.95f * level, 0.66f * level, 0.34f * level);
        }

        /// <summary>锥内浮尘发亮（各端本地，巡逻态专属；Dust 不占 PRT 预算）</summary>
        private void DoConeDust() {
            if (Main.dedServ || (int)State != StatePatrol || (int)AmbientClock % 2 != 0) {
                return;
            }
            Vector2 axis = ConeAxis();
            Vector2 perp = new(-axis.Y, axis.X);
            float d = Main.rand.NextFloat(50f, ConeRange * 0.92f);
            Vector2 p = LanternPos() + axis * d + perp * Main.rand.NextFloat(-0.3f, 0.3f) * d;
            Dust dust = Dust.NewDustPerfect(p, DustID.Torch, axis * 0.4f, 150, default, Main.rand.NextFloat(0.6f, 1.0f));
            dust.noGravity = true;
        }

        //==================== 掉落 ====================

        public override void ModifyNPCLoot(NPCLoot npcLoot) {
            npcLoot.Add(new CommonDrop(ItemID.GoldenKey, 100, 1, 1, 15));
            npcLoot.Add(new CommonDrop(ItemID.HunterPotion, 4));
            npcLoot.Add(new CommonDrop(ItemID.ChainLantern, 10));
        }

        public override void HitEffect(NPC.HitInfo hit) {
            if (Main.dedServ) {
                return;
            }
            int n = NPC.life <= 0 ? 12 : 3;
            for (int i = 0; i < n; i++) {
                Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.Bone,
                    hit.HitDirection * 1.5f, -1f);
            }
        }

        //==================== 绘制：本体走原版管线（GetAlpha 压锈色），灯/锥/环在 PostDraw ====================

        public override Color? GetAlpha(Color drawColor)
            => drawColor.MultiplyRGB(RustMul) * NPC.Opacity;

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            //上游实体批状态泄漏自愈（netcode 7.2）：以已知默认态重开一次
            BeginDefault(spriteBatch);
            return true;
        }

        public override void PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            DrawLanternBody(spriteBatch, screenPos, drawColor);
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) {
                return;
            }
            BeginAdditive(spriteBatch);
            DrawFlameGlow(spriteBatch, glow);
            if ((int)State == StatePatrol) {
                DrawCone(spriteBatch, glow);
            }
            if ((int)State == StateAlarm) {
                DrawAlarmRings(spriteBatch, glow);
            }
            BeginDefault(spriteBatch);
        }

        /// <summary>吊灯本体（默认批）：随步伐摆锤式摆动，鸣警举过头</summary>
        private void DrawLanternBody(SpriteBatch sb, Vector2 screenPos, Color drawColor) {
            Main.instance.LoadItem(ItemID.ChainLantern);
            Texture2D lantern = TextureAssets.Item[ItemID.ChainLantern]?.Value;
            if (lantern == null) {
                return;
            }
            float rot = LanternRotation();
            Vector2 anchor = LanternPos();
            //origin 取贴图顶部中点，灯体绕提手摆动
            Vector2 origin = new(lantern.Width * 0.5f, 2f);
            sb.Draw(lantern, anchor - screenPos, null, drawColor.MultiplyRGB(new Color(220, 190, 150)) * NPC.Opacity,
                rot, origin, 0.85f, SpriteEffects.None, 0f);
        }

        private float LanternRotation() {
            if ((int)State == StateAlarm) {
                return MathF.Sin(AmbientClock * 0.8f + Seed) * 0.06f;
            }
            float swing = 0.16f + 0.10f * Math.Min(1f, Math.Abs(NPC.velocity.X) / ChaseSpeed);
            return MathF.Sin(AmbientClock * 0.09f + Seed) * swing;
        }

        /// <summary>灯焰双层辉光（加色批：强度写进色值整体，A 随乘法收缩）</summary>
        private void DrawFlameGlow(SpriteBatch sb, Texture2D glow) {
            float level = FlameLevel();
            Vector2 flamePos = LanternPos() + new Vector2(0f, 14f).RotatedBy(LanternRotation());
            Vector2 gOrigin = glow.Size() * 0.5f;
            //鸣警首拍白热过曝，一拍即回暖（法 4：暖材质白只走短过曝）
            Color main = (int)State == StateAlarm && (int)StateTimer < 4
                ? Color.Lerp(LampWarm, Color.White, 0.75f) : LampWarm;
            sb.Draw(glow, flamePos - Main.screenPosition, null, main * (0.55f * level), 0f,
                gOrigin, new Vector2(26f * 2f / glow.Width), SpriteEffects.None, 0f);
            sb.Draw(glow, flamePos - Main.screenPosition, null, LampCore * (0.45f * level), 0f,
                gOrigin, new Vector2(11f * 2f / glow.Width), SpriteEffects.None, 0f);
        }

        /// <summary>灯锥：轴向三段渐隐渐宽的柔光条（各端从 direction 与 ai 推导同一形状）</summary>
        private void DrawCone(SpriteBatch sb, Texture2D glow) {
            Vector2 axis = ConeAxis();
            float rot = axis.ToRotation();
            Vector2 src = LanternPos();
            Vector2 gOrigin = glow.Size() * 0.5f;
            float strength = FlameLevel();
            Color coneCol = LampWarm;
            if (StateParam > 0f && (int)State == StatePatrol) {
                //宽限期锥色偏白+闪烁（与灯焰双闪同源）
                coneCol = Color.Lerp(LampWarm, Color.White, Math.Min(0.5f, StateParam / GraceFrames * 0.5f));
            }
            //三段：距离 65/170/285，宽 26/62/104，亮度衰减；视觉末端 ~345px ≥ 判定半径 340px（公平性）
            float[] dist = [65f, 170f, 285f];
            float[] wide = [26f, 62f, 104f];
            float[] lum = [0.30f, 0.20f, 0.11f];
            for (int i = 0; i < 3; i++) {
                Vector2 p = src + axis * dist[i];
                sb.Draw(glow, p - Main.screenPosition, null, coneCol * (lum[i] * strength), rot,
                    gOrigin, new Vector2(120f * 2f / glow.Width, wide[i] * 2f / glow.Height), SpriteEffects.None, 0f);
            }
        }

        /// <summary>警报三环：从三响节拍展开的光环脉冲（timer 确定函数，回卷不重播由绘制无副作用天然保证）</summary>
        private void DrawAlarmRings(SpriteBatch sb, Texture2D glow) {
            int t = (int)StateTimer;
            Vector2 gOrigin = glow.Size() * 0.5f;
            for (int i = 0; i < 3; i++) {
                int start = RingBeatFrame(i);
                if (t < start || t > start + 40) {
                    continue;
                }
                float p = (t - start) / 40f;
                float radius = 20f + p * 230f;
                float alpha = (1f - p) * 0.45f;
                sb.Draw(glow, NPC.Center - Main.screenPosition, null, LampWarm * alpha, 0f,
                    gOrigin, new Vector2(radius * 2f / glow.Width), SpriteEffects.None, 0f);
            }
        }
    }
}
