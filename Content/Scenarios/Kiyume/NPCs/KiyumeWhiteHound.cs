using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Scenarios.Kiyume.Gen;
using CalamityOverhaul.Content.Scenarios.Kiyume.Stealth;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Kiyume.NPCs
{
    /// <summary>
    /// 白毛望乡犬（P2 计划书点子 11，压仓）：低概率通体灰白的犬，站在远处高地/屋脊
    /// 看着你，不攻击、打不着、不出声；走近或任意攻击命中其位置即化雾，在场到时自然化雾。
    /// 山神的狗，凶兆亦是庇护。<br/>
    /// 联机合同：ai[0]=状态（0=Watch 1=Fade）ai[1]=状态计时 ai[2]=在场预算（生成时权威滚定）；
    /// 化雾触发、被看见计数、庇护登记全在权威端，客户端从 ai[] 重放演出。<br/>
    /// 庇护链路：被 <see cref="KiyumeStealthSense.ObservedByAnyPlayer"/> 连续看见 ≥60t 坐实
    /// witnessed，化雾散尽时置位 <see cref="HomewardGraceUntilTideRise"/>；
    /// 犬导演目标数消费此旗标压 0（当夜犬群不出），潮位过 0.5 上穿沿由导演清零
    /// （镜像 <see cref="KiyumeHound.RecruitHoldUntilTideRise"/> 消费法）；会话复位在本文件。<br/>
    /// 材质：狼帧 + KikasaHound.fx 实体态，uEdgeTint 换灰白、uEyeGlow 恒 0.12；
    /// shader 体面是湿墨近黑（vc 只能压不能提），整体提亮走 CPU 灰白叠层
    /// </summary>
    internal class KiyumeWhiteHound : ModNPC
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>
        /// 望乡庇护旗标：见过白犬的当夜犬群不出。化雾散尽时置位（须已被看见坐实），
        /// 犬导演在目标数计算处消费（压 0、场上恶犬渐次 Fade），潮位 0.5 上穿沿清零。
        /// 世界级状态、只在权威端写，static 合法；会话复位见 <see cref="KiyumeWhiteHoundSystem"/>
        /// </summary>
        internal static bool HomewardGraceUntilTideRise;

        //──── 导演泵状态（世界级会话状态，权威端独写） ────

        /// <summary>泵冷却（tick），入梦即满装：首次出现不早于 ≈7.5 分钟</summary>
        private static int pumpCooldown;
        /// <summary>抽签已中、正在找点（找点失败静默保持，下轮巡检再试）</summary>
        private static bool seeking;

        //──── ai[0] 状态位 ────
        internal const int StateWatch = 0;
        internal const int StateFade = 1;

        /// <summary>ai[0]：状态</summary>
        private ref float State => ref NPC.ai[0];
        /// <summary>ai[1]：状态计时</summary>
        private ref float StateTimer => ref NPC.ai[1];
        /// <summary>ai[2]：在场预算（tick，生成时权威滚定后恒定）</summary>
        private ref float StayBudget => ref NPC.ai[2];

        //──── 绘制校准（狼帧链与 KiyumeHound 同源，只换配色） ────
        private const float HoundScale = 1.18f;
        private static readonly Vector2 EyeAnchor = new(0.17f, 0.38f);
        /// <summary>通体灰白（uEdgeTint 与提亮叠层共用）</summary>
        private static readonly Color BodyTint = new(200, 200, 210);
        /// <summary>双目恒定冷弱辉光（不随状态变化，它没有情绪）</summary>
        private const float WhiteEyeGlow = 0.12f;
        /// <summary>灰白叠层强度（整体提亮）</summary>
        private const float BodyLiftStrength = 0.55f;

        //──── 权威端字段（不入同步：登记只在权威端裁决） ────

        /// <summary>被看见的连续计时（断看清零）</summary>
        private int seenTicks;
        /// <summary>被看见坐实（连续 ≥60t，一经坐实不回撤）</summary>
        private bool witnessed;

        //──── 各端本地演出字段 ────

        private int frame = 3;

        private float Seed => NPC.whoAmI * 0.613f;
        private bool Authority => !VaultUtils.isClient;

        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = 1;
            //图鉴一律 Hide（裁决 14）；不进 KiyumeHoundTypes 注册表（它不出声，P5 犬位扫描不该看它）
            NPCID.Sets.NPCBestiaryDrawModifiers hide = new() { Hide = true };
            NPCID.Sets.NPCBestiaryDrawOffset.Add(Type, hide);
            NPCID.Sets.MustAlwaysDraw[Type] = true;
        }

        public override void SetDefaults() {
            NPC.width = 64;
            NPC.height = 34;
            //伤害恒 0 + 打不着：山神的狗不参与任何攻防
            NPC.damage = 0;
            NPC.defense = 0;
            NPC.lifeMax = 600;
            NPC.knockBackResist = 0f;
            NPC.aiStyle = -1;
            NPC.noGravity = false;
            NPC.noTileCollide = false;
            NPC.value = 0;
            NPC.npcSlots = 0f;
            //friendly：召唤物不自动扑它，化雾只回应玩家本人的走近与射向它的攻击
            NPC.friendly = true;
            NPC.dontTakeDamage = true;
            //无 HitSound/DeathSound：安静是它的全部语言
        }

        //导演管生死，化雾是唯一离场：不参与原版远离 despawn
        public override bool CheckActive() => false;

        //==================== AI ====================

        public override void AI() {
            //鬼梦门控：绝不泄漏到主世界与其他子世界（此路径不登记庇护）
            if (!KiyumeWorld.Active) {
                NPC.active = false;
                return;
            }
            //出生透明度显式清零（VFX 缺陷②）：绘制全接管不读 NPC.alpha，这里兜底防全局层误读
            NPC.alpha = 0;
            NPC.damage = 0;

            //出生初始化：预算未随生成写入（异常生成）时兜底取下限
            if (NPC.localAI[0] == 0f) {
                NPC.localAI[0] = 1f;
                if (StayBudget < KiyumeHoundMetrics.WhiteHoundStayMinTicks) {
                    StayBudget = KiyumeHoundMetrics.WhiteHoundStayMinTicks;
                }
            }

            bool authority = Authority;
            //被看见计数（权威端，Watch 与 Fade 全程累计：走近看它化雾也算见到）
            if (authority) {
                if (KiyumeStealthSense.ObservedByAnyPlayer(
                    NPC.Hitbox, KiyumeHoundMetrics.WhiteHoundFogBlind)) {
                    if (++seenTicks >= KiyumeHoundMetrics.WhiteHoundSeenGateTicks) {
                        witnessed = true;
                    }
                }
                else {
                    seenTicks = 0;
                }
            }

            if ((int)State == StateFade) {
                UpdateFade(authority);
            }
            else {
                UpdateWatch(authority);
            }

            ServerSyncPacer();
            if (!Main.dedServ) {
                UpdatePresentation();
            }
        }

        //静立远望：面向最近玩家，不动；走近 / 攻击命中其位置 / 在场到时 → 化雾
        private void UpdateWatch(bool authority) {
            StateTimer++;
            NPC.velocity.X = 0f;
            Player near = NearestPlayer(out float nearDist);
            if (near != null) {
                Face(MathF.Sign(near.Center.X - NPC.Center.X));
            }
            if (!authority) {
                return;
            }
            //不惊不怒，就是走了：三个触发同一条离场路
            if (nearDist < KiyumeHoundMetrics.WhiteHoundApproachPx
                || AttackTouches()
                || StateTimer >= StayBudget) {
                EnterFade();
            }
        }

        //化雾退场：原地散作雾，不走远（它本来就在告别）
        private void UpdateFade(bool authority) {
            StateTimer++;
            NPC.velocity.X = 0f;
            if (authority && StateTimer >= KiyumeHoundMetrics.WhiteHoundFadeTicks) {
                if (witnessed) {
                    //望乡庇护登记：见到它的当夜犬群不出（导演目标数消费，潮位上穿沿清零）
                    HomewardGraceUntilTideRise = true;
                }
                NPC.active = false;
                if (VaultUtils.isServer) {
                    NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, NPC.whoAmI);
                }
            }
        }

        private void EnterFade() {
            //化雾起点接住尚未凝实的部分，避免溶解度跳变（凝现中被走近的场合）
            float carry = Dissolve01();
            State = StateFade;
            StateTimer = KiyumeHoundMetrics.WhiteHoundFadeTicks * carry;
            NPC.netUpdate = true;
        }

        //任意攻击命中其位置：友方带伤弹幕擦过外扩犬框即算（近战要走到 340px 内，走近门已覆盖）
        private bool AttackTouches() {
            Rectangle box = NPC.Hitbox;
            box.Inflate(KiyumeHoundMetrics.WhiteHoundStrikeInflatePx,
                KiyumeHoundMetrics.WhiteHoundStrikeInflatePx);
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.friendly && proj.damage > 0 && proj.Hitbox.Intersects(box)) {
                    return true;
                }
            }
            return false;
        }

        private void Face(int dir) {
            if (dir != 0) {
                NPC.direction = dir;
                NPC.spriteDirection = dir;
            }
        }

        private Player NearestPlayer(out float dist) {
            Player best = null;
            dist = float.MaxValue;
            foreach (Player player in Main.ActivePlayers) {
                if (player.dead || player.ghost) {
                    continue;
                }
                float d = Vector2.Distance(player.Center, NPC.Center);
                if (d < dist) {
                    dist = d;
                    best = player;
                }
            }
            return best;
        }

        //低频重发 SyncNPC 钳住计时漂移（KiyumeHound.ServerSyncPacer 同款，静物用更疏的节拍）
        private void ServerSyncPacer(int interval = 48) {
            if (!VaultUtils.isServer) {
                return;
            }
            if (++NPC.localAI[1] >= interval) {
                NPC.localAI[1] = 0f;
                NPC.netUpdate = true;
            }
        }

        //==================== 导演泵（由 KiyumeHoundDirector 巡检锚区调用，20t 一拍，权威端） ====================

        /// <summary>会话复位（OnWorldLoad/Unload）：冷却满装、旗标清零</summary>
        internal static void ResetSession() {
            HomewardGraceUntilTideRise = false;
            pumpCooldown = KiyumeHoundMetrics.WhiteHoundCooldownTicks;
            seeking = false;
        }

        /// <summary>
        /// 白犬泵：27000t 冷却 → 1/3 抽签 → 找点生成（全场至多 1，找点失败静默下拍再试）。
        /// 调用方合同：仅权威端、仅 KiyumeWorld.Active、每 DirectorCheckTicks 一拍
        /// </summary>
        internal static void DirectorPump() {
            if (pumpCooldown > 0) {
                pumpCooldown -= KiyumeHoundMetrics.DirectorCheckTicks;
                return;
            }
            if (!seeking) {
                if (!Main.rand.NextBool(KiyumeHoundMetrics.WhiteHoundLotteryChance)) {
                    pumpCooldown = KiyumeHoundMetrics.WhiteHoundCooldownTicks;
                    return;
                }
                seeking = true;
            }
            //全场至多 1：冷却远长于在场预算，此查是硬保证不是常态路径
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.ModNPC is KiyumeWhiteHound) {
                    seeking = false;
                    pumpCooldown = KiyumeHoundMetrics.WhiteHoundCooldownTicks;
                    return;
                }
            }
            if (TrySpawn()) {
                seeking = false;
                pumpCooldown = KiyumeHoundMetrics.WhiteHoundCooldownTicks;
            }
        }

        //找点：锚定随机活人，700~1000px 环带，高地/屋脊优先（前半程硬性要求高于玩家），
        //探地可站、距所有玩家 ≥480px、距最近恶犬 ≥1600px（不与恶犬同屏）
        private static bool TrySpawn() {
            Player anchor = null;
            int alive = 0;
            foreach (Player player in Main.ActivePlayers) {
                if (player.dead || player.ghost) {
                    continue;
                }
                if (Main.rand.Next(++alive) == 0) {
                    anchor = player;
                }
            }
            if (anchor == null) {
                return false;
            }
            for (int attempt = 0; attempt < KiyumeHoundMetrics.WhiteHoundSpawnAttempts; attempt++) {
                //前半程只收明显高于玩家的高地/屋脊，后半程放宽到略低于玩家
                float groundGateY = anchor.Center.Y
                    + (attempt < KiyumeHoundMetrics.WhiteHoundSpawnAttempts / 2
                        ? -KiyumeHoundMetrics.WhiteHoundElevatePx
                        : KiyumeHoundMetrics.WhiteHoundElevatePx);
                int side = Main.rand.NextBool() ? -1 : 1;
                float dist = Main.rand.NextFloat(
                    KiyumeHoundMetrics.WhiteHoundSpawnMinPx, KiyumeHoundMetrics.WhiteHoundSpawnMaxPx);
                int col = (int)((anchor.Center.X + side * dist) / 16f);
                //西不入湖、东不出图（恶犬导演同款界）
                if (col < KiyumeHoundMetrics.LowTideBandLeftCol + 4
                    || col > KiyumeMetrics.Width - 40) {
                    continue;
                }
                //从玩家头顶上方向下探地：先碰到屋脊就站屋脊
                if (!TryFindGround(col, anchor.Center.Y - 320f, out int floorRow)) {
                    continue;
                }
                float groundY = floorRow * 16f;
                if (groundY > groundGateY || !Standable(col, floorRow)) {
                    continue;
                }
                var bottom = new Vector2(col * 16f + 8f, groundY);
                if (TooCloseToAnyone(bottom - new Vector2(0f, 17f))) {
                    continue;
                }
                //在场预算生成时权威滚定，随 ai[2] 过线
                int stay = Main.rand.Next(KiyumeHoundMetrics.WhiteHoundStayMinTicks,
                    KiyumeHoundMetrics.WhiteHoundStayMaxTicks + 1);
                int idx = NPC.NewNPC(new EntitySource_WorldEvent(), (int)bottom.X, (int)bottom.Y,
                    ModContent.NPCType<KiyumeWhiteHound>(), ai2: stay);
                if (idx < 0 || idx >= Main.maxNPCs) {
                    return false;
                }
                if (VaultUtils.isServer) {
                    NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, idx);
                }
                CWRMod.Instance.Logger.Info($"[Kiyume] white hound appeared col={col}");
                return true;
            }
            return false;
        }

        private static bool TooCloseToAnyone(Vector2 body) {
            foreach (Player player in Main.ActivePlayers) {
                if (!player.dead && !player.ghost
                    && Vector2.Distance(player.Center, body)
                        < KiyumeHoundMetrics.WhiteHoundPlayerClearPx) {
                    return true;
                }
            }
            //不与恶犬同屏：山神的狗不与凶犬同框
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.ModNPC is KiyumeHound
                    && Vector2.Distance(npc.Center, body)
                        < KiyumeHoundMetrics.WhiteHoundHoundClearPx) {
                    return true;
                }
            }
            return false;
        }

        //从起始高度向下探第一块实心（KiyumeHoundShade 探地同法，屋脊天然可命中）
        private static bool TryFindGround(int col, float fromY, out int floorRow) {
            int tileY = (int)(fromY / 16f);
            for (int i = 0; i < 60; i++) {
                int y = tileY + i;
                if (!WorldGen.InWorld(col, y, 20)) {
                    break;
                }
                Tile tile = Framing.GetTileSafely(col, y);
                if (tile.HasTile && Main.tileSolid[tile.TileType]
                    && !Main.tileSolidTop[tile.TileType]) {
                    floorRow = y;
                    return true;
                }
            }
            floorRow = 0;
            return false;
        }

        //探地可站（恶犬导演 TrySpawnAt 同款）：体格净空 + 脚下两列实心支撑 + 不踩水
        private static bool Standable(int col, int floorRow) {
            if (floorRow < 60 || floorRow > Main.maxTilesY - 60) {
                return false;
            }
            for (int dx = -2; dx <= 1; dx++) {
                for (int dy = 1; dy <= 3; dy++) {
                    Tile tile = Framing.GetTileSafely(col + dx, floorRow - dy);
                    if (tile.HasTile && Main.tileSolid[tile.TileType]
                        && !Main.tileSolidTop[tile.TileType]) {
                        return false;
                    }
                }
            }
            for (int dx = -1; dx <= 0; dx++) {
                Tile support = Framing.GetTileSafely(col + dx, floorRow);
                if (!support.HasTile || !Main.tileSolid[support.TileType]
                    || Main.tileSolidTop[support.TileType]) {
                    return false;
                }
            }
            return Framing.GetTileSafely(col, floorRow - 1).LiquidAmount == 0;
        }

        //==================== 演出（各端本地） ====================

        /// <summary>化雾度：凝现 1→0（60t），化雾 0→1（90t）；驻望恒 0</summary>
        internal float Dissolve01() {
            if ((int)State == StateFade) {
                return MathHelper.Clamp(
                    StateTimer / KiyumeHoundMetrics.WhiteHoundFadeTicks, 0f, 1f);
            }
            return 1f - MathHelper.Clamp(
                StateTimer / KiyumeHoundMetrics.WhiteHoundEmergeTicks, 0f, 1f);
        }

        private void UpdatePresentation() {
            //帧 3 定格 + 偶尔换 4 的呼吸感：由计时确定性驱动，各端同拍
            if ((int)State == StateWatch) {
                int phase = ((int)StateTimer + NPC.whoAmI * 37) % 230;
                frame = phase < 14 ? 4 : 3;
            }
            else {
                frame = 3;
            }

            //凝现/化雾的灰白雾息（黑犬同手法，换冷灰）
            bool misting = (int)State == StateFade
                || StateTimer < KiyumeHoundMetrics.WhiteHoundEmergeTicks;
            if (misting && Main.rand.NextBool(4)) {
                Dust wisp = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height, DustID.Smoke,
                    Main.rand.NextFloat(-0.4f, 0.4f), Main.rand.NextFloat(-0.7f, -0.1f),
                    170, new Color(186, 188, 196), Main.rand.NextFloat(0.9f, 1.5f));
                wisp.noGravity = true;
            }
        }

        //==================== 绘制（全接管：狼帧 + KikasaHound.fx 实体态，灰白配色） ====================

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            //上游批状态自愈（netcode 7.2）
            BeginDefault(spriteBatch);
            Main.instance.LoadNPC(NPCID.Wolf);
            Texture2D tex = TextureAssets.Npc[NPCID.Wolf].Value;
            if (tex == null) {
                return false;
            }
            int frameCount = Main.npcFrameCount[NPCID.Wolf];
            int frameH = tex.Height / frameCount;
            int safeFrame = Math.Clamp(frame, 0, frameCount - 1);
            //源矩形上下各内缩 1px + shader 帧界钳制，双通道防帧表渗色
            var source = new Rectangle(0, safeFrame * frameH + 1, tex.Width, frameH - 2);
            float height = source.Height * HoundScale;
            var center = new Vector2(NPC.Center.X,
                NPC.Bottom.Y + 2f + NPC.gfxOffY - height * 0.5f);
            var origin = new Vector2(source.Width * 0.5f, source.Height * 0.5f);
            bool faceRight = NPC.spriteDirection > 0;
            SpriteEffects flip = faceRight ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            float dissolve = Dissolve01();

            Effect hound = EffectLoader.KikasaHound?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (hound == null || noise == null) {
                //着色器缺编：灰白剪影回退（透明度显式给值，化雾降级为降透明）
                spriteBatch.Draw(tex, center - screenPos, source,
                    BodyTint * (0.85f * (1f - dissolve)), 0f, origin, HoundScale, flip, 0f);
                return false;
            }

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);
            GraphicsDevice gd = Main.instance.GraphicsDevice;
            gd.Textures[1] = noise;
            gd.SamplerStates[1] = SamplerState.LinearWrap;

            hound.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            hound.Parameters["uSeed"]?.SetValue(Seed);
            hound.Parameters["uUvRect"]?.SetValue(new Vector4(
                0f, source.Y / (float)tex.Height, 1f, source.Height / (float)tex.Height));
            hound.Parameters["uTexel"]?.SetValue(new Vector2(1f / tex.Width, 1f / tex.Height));
            hound.Parameters["uAspect"]?.SetValue(tex.Width / (float)source.Height);
            hound.Parameters["uFlipH"]?.SetValue(faceRight ? 1f : 0f);
            hound.Parameters["uFlipV"]?.SetValue(0f);
            hound.Parameters["uMode"]?.SetValue(1f);
            hound.Parameters["uSeamGate"]?.SetValue(0f);
            hound.Parameters["uWobble"]?.SetValue(0.010f);
            hound.Parameters["uEyeGlow"]?.SetValue(WhiteEyeGlow);
            hound.Parameters["uEyeAnchor"]?.SetValue(EyeAnchor);
            hound.Parameters["uDissolve"]?.SetValue(dissolve);
            hound.Parameters["uEdgeTint"]?.SetValue(BodyTint.ToVector3());
            hound.CurrentTechnique = hound.Techniques["TechHound"];
            hound.CurrentTechnique.Passes[0].Apply();

            //vc.a 恒 1：来去全走 uDissolve，不降 alpha（化雾不是调透明度）
            spriteBatch.Draw(tex, center - screenPos, source, Color.White,
                0f, origin, HoundScale, SpriteEffects.None, 0f);

            BeginDefault(spriteBatch);
            gd.Textures[1] = null;

            //整体提亮：湿墨体面上罩一层灰白狼相（shader 输出近黑，vc 只能压不能提，提亮走叠层）
            spriteBatch.Draw(tex, center - screenPos, source,
                BodyTint * (BodyLiftStrength * (1f - dissolve)), 0f, origin, HoundScale, flip, 0f);

#if DEBUG
            Utils.DrawBorderString(spriteBatch,
                $"白犬 状态 {(int)State}  见 {seenTicks}{(witnessed ? " 坐实" : "")}",
                NPC.Top - screenPos + new Vector2(-34f, -32f),
                Color.LightGoldenrodYellow, 0.7f);
#endif
            return false;
        }

        private static void BeginDefault(SpriteBatch sb) {
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }
    }

    /// <summary>白犬会话复位（ShouldSave=false 每次进梦全新，静态残留=幽灵庇护/幽灵冷却）</summary>
    internal class KiyumeWhiteHoundSystem : ModSystem
    {
        public override void OnWorldLoad() => KiyumeWhiteHound.ResetSession();
        public override void OnWorldUnload() => KiyumeWhiteHound.ResetSession();
    }
}
