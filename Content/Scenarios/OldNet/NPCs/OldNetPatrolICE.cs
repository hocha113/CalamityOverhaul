using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Scenarios.OldNet.Gen;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.OldNet.NPCs
{
    /// <summary>
    /// 巡逻 ICE：旧网的常驻哨兵。锚点区间匀速往返悬浮，前向视锥充能侦测
    /// （慢速通过=潜行可绕），目击完成 → 玩家噪音 +15 并引来猎杀小队。
    /// 可击杀但高防高血，击杀本身是高噪决策（+20）；无掉落
    /// 安静最优路线不该被刷怪收益污染。零贴图程序化绘制
    /// </summary>
    internal class OldNetPatrolICE : ModNPC
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //ai[2] 状态位
        private const int StatePatrol = 0;
        private const int StateLunge = 1;
        private const int StateCooldown = 2;

        /// <summary>ai[0]：巡逻锚点世界 X（director 生成时写入）</summary>
        private ref float AnchorX => ref NPC.ai[0];
        /// <summary>ai[1]：巡逻方向 ±1</summary>
        private ref float Dir => ref NPC.ai[1];
        /// <summary>ai[2]：状态（巡逻/冲撞/冷却）</summary>
        private ref float State => ref NPC.ai[2];
        /// <summary>ai[3]：状态计时</summary>
        private ref float StateTimer => ref NPC.ai[3];
        /// <summary>侦测充能（本地表现+裁决一体，MP 化时移交服务器 TODO）</summary>
        private ref float DetectCharge => ref NPC.localAI[0];
        private ref float AmbientClock => ref NPC.localAI[1];

        private float Seed => NPC.whoAmI * 0.917f;

        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = 1;
            NPCID.Sets.NPCBestiaryDrawModifiers hide = new() { Hide = true };
            NPCID.Sets.NPCBestiaryDrawOffset.Add(Type, hide);
            NPCID.Sets.MustAlwaysDraw[Type] = true;
        }

        public override void SetDefaults() {
            NPC.width = 34;
            NPC.height = 34;
            //平时零伤害，只在冲撞窗口设回（Wraith 门控惯例）
            NPC.damage = 0;
            NPC.defense = OldNetMetrics.PatrolDefense;
            NPC.lifeMax = OldNetMetrics.PatrolLife;
            NPC.knockBackResist = 0f;
            NPC.aiStyle = -1;
            NPC.noGravity = true;
            //巡逻贴地形飞，不穿墙
            NPC.noTileCollide = false;
            NPC.value = 0;
            NPC.npcSlots = 0.5f;
            NPC.HitSound = SoundID.NPCHit4;
            NPC.DeathSound = SoundID.NPCDeath14;
        }

        public override void AI() {
            //旧网门控：绝不泄漏到主世界与其他子世界
            if (!OldNetWorld.Active) {
                NPC.active = false;
                return;
            }
            AmbientClock++;
            if (AnchorX <= 0f) {
                AnchorX = NPC.Center.X;
                Dir = Main.rand.NextBool() ? 1f : -1f;
            }

            NPC.TargetClosest(faceTarget: false);
            Player player = Main.player[NPC.target];
            bool hasTarget = player != null && player.active && !player.dead;

            switch ((int)State) {
                case StateLunge:
                    UpdateLunge(player, hasTarget);
                    break;
                case StateCooldown:
                    NPC.damage = 0;
                    UpdatePatrolMotion();
                    if (--StateTimer <= 0f) {
                        State = StatePatrol;
                        NPC.netUpdate = true;
                    }
                    break;
                default:
                    NPC.damage = 0;
                    UpdatePatrolMotion();
                    if (hasTarget) {
                        UpdateDetection(player);
                    }
                    break;
            }

            //哨兵冷光
            float glow = (int)State == StateLunge ? 0.9f
                : DetectCharge > 0f ? 0.5f + DetectCharge / OldNetMetrics.PatrolDetectChargeTicks * 0.4f
                : 0.3f;
            Lighting.AddLight(NPC.Center, 0.05f * glow, 0.22f * glow, 0.26f * glow);
        }

        //──── 巡逻：锚点 ± 区间往返，贴地形悬浮 ────

        private void UpdatePatrolMotion() {
            int tier = LocalTier();
            float speed = OldNetMetrics.PatrolSpeed
                * (tier >= 1 ? OldNetMetrics.PatrolAlertSpeedMul : 1f);

            float range = OldNetMetrics.PatrolRangeCols * 16f;
            if (NPC.Center.X > AnchorX + range && Dir > 0f) {
                Dir = -1f;
            }
            else if (NPC.Center.X < AnchorX - range && Dir < 0f) {
                Dir = 1f;
            }
            //横向撞墙就掉头（地形起伏兜底）
            if (MathF.Abs(NPC.velocity.X) < 0.05f && MathF.Abs(NPC.oldVelocity.X) > 0.5f) {
                Dir = -Dir;
            }
            NPC.velocity.X = MathHelper.Lerp(NPC.velocity.X, Dir * speed, 0.08f);

            //向下采样地表，维持悬浮高度
            float targetY = ProbeHoverY();
            NPC.velocity.Y = MathHelper.Clamp((targetY - NPC.Center.Y) * 0.04f, -2.5f, 2.5f);

            NPC.direction = NPC.spriteDirection = Dir >= 0f ? 1 : -1;
        }

        //从自身位置向下找首块实心，返回悬浮目标世界 Y
        private float ProbeHoverY() {
            int col = (int)(NPC.Center.X / 16f);
            int startRow = Math.Max((int)(NPC.Center.Y / 16f) - 4, OldNetMetrics.BorderThick);
            for (int y = startRow; y < Main.maxTilesY - OldNetMetrics.BorderThick; y++) {
                Tile tile = Framing.GetTileSafely(col, y);
                if (tile.HasTile && Main.tileSolid[tile.TileType]) {
                    return y * 16f - OldNetMetrics.PatrolHoverHeight;
                }
            }
            return NPC.Center.Y;
        }

        //──── 侦测：前向视锥 + 半径充能，潜行绕过 ────

        private void UpdateDetection(Player player) {
            int tier = LocalTier();
            float radius = OldNetMetrics.PatrolDetectRadius;
            if (tier >= 1) {
                radius *= OldNetMetrics.PatrolAlertRadiusMul;
            }
            //慢速通过 = 潜行；清剿波期间潜行失效
            if (!OldNetICEDirector.CleanupWaveActive
                && player.velocity.Length() < OldNetMetrics.PatrolSneakSpeedGate) {
                radius *= OldNetMetrics.PatrolSneakRadiusMul;
            }

            Vector2 toPlayer = player.Center - NPC.Center;
            float dist = toPlayer.Length();
            bool inCone = dist < radius
                && Vector2.Dot(toPlayer.SafeNormalize(Vector2.Zero), new Vector2(NPC.direction, 0f)) > 0.25f
                && Collision.CanHitLine(NPC.position, NPC.width, NPC.height,
                    player.position, player.width, player.height);

            if (!inCone) {
                //脱出视锥即清零：潜行的容错就是硬的
                DetectCharge = 0f;
                return;
            }

            DetectCharge++;
            if ((int)DetectCharge % 18 == 1 && !Main.dedServ) {
                SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.4f, Pitch = 0.6f }, NPC.Center);
            }
            if (DetectCharge < OldNetMetrics.PatrolDetectChargeTicks) {
                return;
            }

            //目击完成：点亮玩家 + 引来猎杀，随后进入冲撞窗口
            DetectCharge = 0f;
            OldNetPlayer.Get(player).AddNoise(OldNetMetrics.NoiseSpotted);
            OldNetICEDirector.NotifySpotted(player);
            State = StateLunge;
            StateTimer = OldNetMetrics.PatrolLungeTicks;
            NPC.netUpdate = true;
            if (!Main.dedServ) {
                SoundEngine.PlaySound(CWRSound.FaultOccurred with { Volume = 0.55f, Pitch = 0.2f }, NPC.Center);
            }
        }

        //──── 冲撞窗口：接触伤害唯一生效期 ────

        private void UpdateLunge(Player player, bool hasTarget) {
            NPC.damage = OldNetMetrics.PatrolContactDamage;
            if (hasTarget) {
                Vector2 dir = (player.Center - NPC.Center).SafeNormalize(Vector2.UnitX * Dir);
                NPC.velocity = Vector2.Lerp(NPC.velocity, dir * 6f, 0.08f);
                NPC.direction = NPC.spriteDirection = NPC.velocity.X >= 0f ? 1 : -1;
            }
            if (--StateTimer <= 0f || !hasTarget) {
                State = StateCooldown;
                StateTimer = OldNetMetrics.PatrolRedetectCooldown;
                NPC.damage = 0;
                NPC.netUpdate = true;
            }
        }

        private static int LocalTier() {
            //M1 单人：本机玩家即威胁源；MP 化时改为区域最高档 TODO
            return Main.LocalPlayer?.active == true
                ? OldNetPlayer.Get(Main.LocalPlayer).NoiseTier : 0;
        }

        public override void OnKill() {
            //打死巡逻是高噪决策：击杀者噪音 +20
            int idx = NPC.lastInteraction;
            Player killer = idx >= 0 && idx < Main.maxPlayers ? Main.player[idx] : null;
            if (killer?.active != true) {
                killer = Main.LocalPlayer;
            }
            if (killer?.active == true) {
                OldNetPlayer.Get(killer).AddNoise(OldNetMetrics.NoisePatrolKill);
            }
        }

        public override void HitEffect(NPC.HitInfo hit) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < (NPC.life <= 0 ? 16 : 4); i++) {
                Dust dust = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height,
                    DustID.Electric, Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-2f, 2f));
                dust.noGravity = true;
                dust.scale = Main.rand.NextFloat(0.6f, 1.1f);
            }
        }

        //──── 程序化绘制：菱形哨体 + 前向扫描线 + 充能进度 ────

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D px = VaultAsset.placeholder2?.Value;
            if (px == null || px.IsDisposed) {
                return false;
            }
            Vector2 center = NPC.Center - screenPos;
            Vector2 origin = new(px.Width * 0.5f, px.Height * 0.5f);
            float t = Main.GlobalTimeWrappedHourly;
            float bob = MathF.Sin(t * 1.7f + Seed) * 2f;
            center.Y += bob;

            bool lunging = (int)State == StateLunge;
            float chargeFrac = MathHelper.Clamp(DetectCharge / OldNetMetrics.PatrolDetectChargeTicks, 0f, 1f);
            Color coldBody = new(24, 60, 68);
            Color accent = lunging ? new Color(235, 64, 44)
                : Color.Lerp(new Color(0, 220, 255), new Color(255, 170, 60), chargeFrac);

            Vector2 Size(float w, float h) => new(w / px.Width, h / px.Height);

            //外缘暗壳（斜置正方形读作菱晶哨体）
            spriteBatch.Draw(px, center, null, coldBody, MathHelper.PiOver4 + t * 0.2f,
                origin, Size(20f, 20f), SpriteEffects.None, 0f);
            //中层旋转框
            spriteBatch.Draw(px, center, null, accent * 0.6f, -t * 0.6f + Seed,
                origin, Size(12f, 12f), SpriteEffects.None, 0f);
            //横向天线杆
            spriteBatch.Draw(px, center, null, accent * 0.5f, 0f,
                origin, Size(30f, 1.5f), SpriteEffects.None, 0f);
            //核芯
            spriteBatch.Draw(px, center, null, Color.White * 0.85f, MathHelper.PiOver4,
                origin, Size(4.5f, 4.5f), SpriteEffects.None, 0f);

            //前向扫描线束：视锥的可读化
            float faceDir = NPC.direction;
            for (int i = -1; i <= 1; i++) {
                float ang = i * 0.16f + MathF.Sin(t * 2.5f + Seed) * 0.05f;
                Vector2 rayDir = new Vector2(faceDir, 0f).RotatedBy(ang);
                float rayLen = 42f + chargeFrac * 26f;
                Vector2 rayCenter = center + rayDir * rayLen * 0.5f;
                spriteBatch.Draw(px, rayCenter, null, accent * (0.16f + chargeFrac * 0.25f),
                    rayDir.ToRotation(), origin, Size(rayLen, 1f), SpriteEffects.None, 0f);
            }

            //眼芯辉光（A=0 亮层）
            Texture2D glowTex = CWRAsset.SoftGlow?.Value;
            if (glowTex != null) {
                Color glowCol = accent * (0.45f + chargeFrac * 0.4f + (lunging ? 0.3f : 0f));
                glowCol.A = 0;
                spriteBatch.Draw(glowTex, center, null, glowCol, 0f,
                    glowTex.Size() * 0.5f, 0.24f, SpriteEffects.None, 0f);
            }

            //头顶充能条：被盯上的可读性阀
            if (chargeFrac > 0.01f && !lunging) {
                Vector2 barTl = center + new Vector2(-15f, -30f);
                spriteBatch.Draw(px, barTl, null, new Color(10, 20, 24) * 0.85f, 0f,
                    Vector2.Zero, Size(30f, 3f), SpriteEffects.None, 0f);
                spriteBatch.Draw(px, barTl, null, accent, 0f,
                    Vector2.Zero, Size(30f * chargeFrac, 3f), SpriteEffects.None, 0f);
            }
            return false;
        }
    }
}
