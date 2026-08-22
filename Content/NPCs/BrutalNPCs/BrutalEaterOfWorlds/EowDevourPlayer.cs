using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEaterOfWorlds.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEaterOfWorlds.Rendering;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEaterOfWorlds.States;
using InnoVault.Cinematics;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEaterOfWorlds
{
    /// <summary>
    /// 入腹暗幕滤镜：注册+本地驱动+孤儿兜底。只作用于被吞玩家本机屏幕
    /// </summary>
    internal class EowGorgeFilterSystem : ModSystem
    {
        /// <summary>暗幕滤镜注册名</summary>
        internal const string GorgeFilterName = "CalamityOverhaul:EowGorge";

        /// <summary>暗幕平滑包络(本机)</summary>
        private float darkSmooth;

        public override void Load() {
            if (Main.dedServ) {
                return;
            }
            Filters.Scene[GorgeFilterName] = new Filter(
                new ScreenShaderData("FilterMiniTower")
                    .UseColor(0.015f, 0.02f, 0.008f)
                    .UseOpacity(0f),
                EffectPriority.VeryHigh);
        }

        public override void PostUpdateEverything() {
            if (Main.dedServ) {
                return;
            }

            //目标浓度由本机玩家的被吞相位决定；任何异常(死亡/断投/掉头)都自然衰减归零
            float target = 0f;
            if (Main.LocalPlayer.active) {
                EowDevourPlayer devour = Main.LocalPlayer.GetModPlayer<EowDevourPlayer>();
                target = devour.GorgeDarknessTarget();
            }

            darkSmooth = target > darkSmooth
                ? MathHelper.Lerp(darkSmooth, target, 0.07f)
                : MathHelper.Lerp(darkSmooth, target, 0.11f);

            Filter filter = Filters.Scene[GorgeFilterName];
            if (filter == null) {
                return;
            }
            if (darkSmooth > 0.02f) {
                if (!filter.IsActive()) {
                    Filters.Scene.Activate(GorgeFilterName, Main.LocalPlayer.Center);
                }
                filter.GetShader()
                    .UseOpacity(darkSmooth)
                    .UseTargetPosition(Main.LocalPlayer.Center);
            }
            else if (filter.IsActive()) {
                Filters.Scene.Deactivate(GorgeFilterName);
            }
        }
    }

    /// <summary>
    /// 生吞入腹的玩家侧：被吞玩家自己的客户端负责钉位/锁控/免疫/挤压拍结算/释放弹射。<br/>
    /// 服务器绝不写玩家位置，一切位移与生命结算都发生在此(玩家位置与生命是客户端权威)
    /// </summary>
    internal class EowDevourPlayer : ModPlayer
    {
        #region 数据(全实例字段，禁static per-player)
        /// <summary>抓住我的头 whoAmI(-1无)；释放帧还要回读它的相位判断弹射方式</summary>
        private int grabHeadWho = -1;
        /// <summary>本帧被钉住</summary>
        private bool pinned;
        /// <summary>已结算的挤压拍(严格前进，快照回绕不重放)</summary>
        private int lastBeatConsumed;
        /// <summary>释放后坠落宽限帧(持续清 fallStart)</summary>
        private int fallGraceTicks;
        /// <summary>释放后镜头残留帧(让镜头目送玩家飞出)</summary>
        private int camLingerTicks;
        /// <summary>起手一次性处理已做(钩爪/坐骑/姿态)</summary>
        private bool grabStartHandled;

        internal bool Pinned => pinned;
        #endregion

        #region 检测
        /// <summary>正抓着指定玩家的世吞头(验状态+验接管在场)，无则null</summary>
        private static NPC FindDevourHead(int playerWho, out EowHeadAI headOverride) {
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.type != NPCID.EaterofWorldsHead) {
                    continue;
                }
                if ((int)npc.ai[2] != (int)EowStateIndex.Devour) {
                    continue;
                }
                if (!npc.TryGetOverride<EowHeadAI>(out EowHeadAI overrideInds) || overrideInds == null) {
                    continue;
                }
                if ((int)overrideInds.ai[EowHeadAI.SlotGrabTarget] == playerWho + 1) {
                    headOverride = overrideInds;
                    return npc;
                }
            }
            headOverride = null;
            return null;
        }

        /// <summary>暗幕目标浓度：入地渐暗，腹内最深，携人上冲回亮</summary>
        internal float GorgeDarknessTarget() {
            if (!pinned || grabHeadWho < 0 || grabHeadWho >= Main.maxNPCs) {
                return 0f;
            }
            NPC head = Main.npc[grabHeadWho];
            if (!head.active || !head.TryGetOverride<EowHeadAI>(out EowHeadAI h) || h == null) {
                return 0f;
            }
            return (int)h.ai[EowHeadAI.SlotGrabPhase] switch {
                (int)EowDevourState.GrabSlotPhase.Gorge => 0.55f,
                (int)EowDevourState.GrabSlotPhase.Squeeze => 0.86f,
                (int)EowDevourState.GrabSlotPhase.EjectCarry => 0.45f,
                _ => 0f,
            };
        }
        #endregion

        #region 钉位主循环
        public override void PreUpdateMovement() {
            //只有被吞玩家自己的客户端施加位移与锁控(服务端 myPlayer=255 恒不命中)
            if (Player.whoAmI != Main.myPlayer) {
                return;
            }

            NPC head = FindDevourHead(Player.whoAmI, out EowHeadAI headOverride);
            int phase = headOverride != null ? (int)headOverride.ai[EowHeadAI.SlotGrabPhase] : 0;
            bool grabbedNow = head != null
                && phase >= (int)EowDevourState.GrabSlotPhase.Hold
                && phase <= (int)EowDevourState.GrabSlotPhase.EjectCarry;

            if (grabbedNow) {
                UpdatePinned(head, headOverride);
            }
            else if (pinned) {
                ReleasePin();
            }

            UpdateFallGrace();
        }

        private void UpdatePinned(NPC head, EowHeadAI headOverride) {
            if (!pinned) {
                BeginPin(head, headOverride);
            }
            grabHeadWho = head.whoAmI;

            //钉在口中：位置直贴，速度镜像头速(远端外推与本地积分一致)
            Vector2 mouth = head.Center
                + (head.rotation - MathHelper.PiOver2).ToRotationVector2() * 14f * head.scale;
            Player.Center = mouth;
            Player.velocity = head.velocity;
            Player.fallStart = (int)(Player.position.Y / 16f);

            //锁行为：禁物品/建造/击退，掐断持续引导
            Player.noItems = true;
            Player.noBuilding = true;
            Player.noKnockback = true;
            Player.channel = false;

            //腹内免疫(挤压拍自己开口)，被衔期间不闪烁
            Player.immune = true;
            Player.immuneNoBlink = true;
            if (Player.immuneTime < 2) {
                Player.immuneTime = 2;
            }

            LockLocalControls();

            //挤压拍消费：严格前进，回绕快照不重放
            int beat = (int)headOverride.ai[EowHeadAI.SlotGrabBeat];
            if (beat > lastBeatConsumed) {
                lastBeatConsumed = beat;
                DoSqueezeBeat(head);
            }

            //腹内闷鸣底噪(被吞者独有的听觉层)
            if (Main.GameUpdateCount % 34 == 0) {
                SoundEngine.PlaySound(SoundID.WormDigQuiet with {
                    Volume = 0.75f,
                    Pitch = -0.9f,
                    MaxInstances = 2
                }, Player.Center);
            }
        }

        private void BeginPin(NPC head, EowHeadAI headOverride) {
            pinned = true;
            grabHeadWho = head.whoAmI;
            //对齐当前拍计数，不补结算历史拍
            lastBeatConsumed = (int)headOverride.ai[EowHeadAI.SlotGrabBeat];

            if (!grabStartHandled) {
                grabStartHandled = true;
                //清钩爪、下坐骑、起身(投技硬要求)
                Player.RemoveAllGrapplingHooks();
                if (Player.mount != null && Player.mount.Active) {
                    Player.mount.Dismount(Player);
                }
                Player.StopVanityActions();
            }
        }

        private void LockLocalControls() {
            Player.controlJump = false;
            Player.controlDown = false;
            Player.controlLeft = false;
            Player.controlRight = false;
            Player.controlUp = false;
            Player.controlUseItem = false;
            Player.controlUseTile = false;
            Player.controlThrow = false;
            Player.controlHook = false;
            Player.controlMount = false;
        }
        #endregion

        #region 挤压拍结算
        /// <summary>挤压伤害：百分比+小额固定值，三拍总量恒低于满血(公平阀门)</summary>
        private void DoSqueezeBeat(NPC head) {
            bool brutal = CWRRef.GetDeathMode() || CWRRef.GetBossRushActive();
            float pct = brutal ? 0.15f : 0.12f;
            int flat = brutal ? 10 : 8;
            int dmg = (int)(Player.statLifeMax2 * pct) + flat;

            //腹内免疫为常态，本拍开一帧口子让 Hurt 落地(生命是客户端权威，正确端结算)
            Player.immune = false;
            Player.immuneTime = 0;
            Player.Hurt(PlayerDeathReason.ByNPC(head.whoAmI), dmg, 0,
                cooldownCounter: -1, dodgeable: false, knockback: 0f);

            //被吞者体感：重震+湿压声+酸沫(运镜接管期普通震屏可能失效，走导演器)
            if (CutsceneDirector.CurrentClip is EowDevourCutscene) {
                CutsceneDirector.Shake(Vector2.UnitY, 8f, 0.88f, 16);
            }
            SoundEngine.PlaySound(SoundID.NPCDeath13 with { Pitch = -0.6f, Volume = 1.1f, MaxInstances = 3 }, Player.Center);
            EowMotionFX.SpawnAcidBurst(Player.Center, 1.5f);
        }
        #endregion

        #region 释放
        private void ReleasePin() {
            pinned = false;
            grabStartHandled = false;

            //回读头相位分辨释放方式：破土喷出(带弹射) or 异常断投(温和落地)
            bool launched = false;
            NPC head = grabHeadWho >= 0 && grabHeadWho < Main.maxNPCs ? Main.npc[grabHeadWho] : null;
            if (head != null && head.active && head.type == NPCID.EaterofWorldsHead
                && head.TryGetOverride<EowHeadAI>(out EowHeadAI h) && h != null
                && (int)h.ai[EowHeadAI.SlotGrabPhase] == (int)EowDevourState.GrabSlotPhase.EjectLaunched) {
                launched = true;
            }
            grabHeadWho = -1;

            if (launched && head != null) {
                //喷上天：继承头部分量向上抛射
                float vy = MathHelper.Clamp(head.velocity.Y * 0.4f - 22f, -34f, -18f);
                Player.velocity = new Vector2(head.velocity.X * 0.3f, vy);
                SoundEngine.PlaySound(SoundID.NPCDeath13 with { Pitch = -0.2f, Volume = 1.2f }, Player.Center);
                EowMotionFX.SpawnAcidBurst(Player.Center, 2.2f, -Vector2.UnitY);
                camLingerTicks = 22;
            }
            else {
                //异常断投：轻托一把，镜头立即收
                Player.velocity = new Vector2(0f, -6f);
                camLingerTicks = 0;
            }

            //释放三件套：足额无敌帧+坠落宽限+满翼(公平阀门)
            Player.immuneNoBlink = false;
            Player.SetImmuneTimeForAllTypes(90);
            Player.wingTime = Player.wingTimeMax;
            fallGraceTicks = 130;
            RescueFromGround();
        }

        /// <summary>释放后坠落宽限：持续清坠落起点，防投技赠送摔落伤害</summary>
        private void UpdateFallGrace() {
            if (fallGraceTicks <= 0) {
                return;
            }
            fallGraceTicks--;
            Player.fallStart = (int)(Player.position.Y / 16f);
        }

        /// <summary>被埋进实体块时向上找净空转移(极端地形断投的自救，位置客户端权威)</summary>
        private void RescueFromGround() {
            if (!Collision.SolidCollision(Player.position, Player.width, Player.height)) {
                return;
            }
            int tileX = (int)(Player.Center.X / 16f);
            int startY = (int)(Player.Bottom.Y / 16f);
            for (int up = 0; up < 90; up++) {
                int y = startY - up;
                if (y < 12) {
                    break;
                }
                bool clear = true;
                for (int dx = -1; dx <= 0 && clear; dx++) {
                    for (int dy = 0; dy < 3 && clear; dy++) {
                        Tile tile = Framing.GetTileSafely(tileX + dx, y - dy);
                        if (tile.HasUnactuatedTile && Main.tileSolid[tile.TileType]) {
                            clear = false;
                        }
                    }
                }
                if (clear) {
                    Player.Bottom = new Vector2(Player.Center.X, (y + 1) * 16f);
                    Player.velocity = Vector2.Zero;
                    Player.fallStart = (int)(Player.position.Y / 16f);
                    break;
                }
            }
        }
        #endregion

        #region 运镜启停
        public override void PostUpdate() {
            if (Player.whoAmI != Main.myPlayer) {
                return;
            }

            bool playing = CutsceneDirector.CurrentClip is EowDevourCutscene;

            if (pinned && grabHeadWho >= 0 && grabHeadWho < Main.maxNPCs) {
                NPC head = Main.npc[grabHeadWho];
                if (head.active && !playing) {
                    CutsceneDirector.Play<EowDevourCutscene, NPC>(head, restartSameClip: false);
                }
                return;
            }

            //释放后镜头残留：目送玩家飞出再平滑收束
            if (camLingerTicks > 0) {
                camLingerTicks--;
                return;
            }
            if (playing) {
                CutsceneDirector.Stop();
            }
        }
        #endregion

        #region 异常出口
        /// <summary>死亡兜底：钉住期死亡立即本地解除(死亡玩家不跑 PostUpdate/PreUpdateMovement)</summary>
        public override void UpdateDead() {
            if (Player.whoAmI != Main.myPlayer) {
                return;
            }
            if (pinned) {
                pinned = false;
                grabStartHandled = false;
                grabHeadWho = -1;
                camLingerTicks = 0;
                fallGraceTicks = 0;
            }
            if (CutsceneDirector.CurrentClip is EowDevourCutscene) {
                CutsceneDirector.Stop();
            }
        }

        public override void OnRespawn() => ClearLocalState();

        public override void OnEnterWorld() => ClearLocalState();

        public override void PlayerDisconnect() => ClearLocalState();

        private void ClearLocalState() {
            pinned = false;
            grabStartHandled = false;
            grabHeadWho = -1;
            lastBeatConsumed = 0;
            fallGraceTicks = 0;
            camLingerTicks = 0;
        }
        #endregion
    }
}
