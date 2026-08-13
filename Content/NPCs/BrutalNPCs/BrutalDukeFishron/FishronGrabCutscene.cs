using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDukeFishron.Rendering;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDukeFishron.States;
using InnoVault.Cinematics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDukeFishron
{
    /// <summary>
    /// 投技运镜：随被抓玩家扎入涡底（推近），钉住期凝视涡心看鲨影掠过，
    /// 破水后跟飞拉远交还。只在被抓玩家的客户端播放，让位死亡运镜(100)
    /// </summary>
    internal sealed class FishronGrabCutscene : CutsceneClip<NPC>
    {
        public override int Priority => 90;

        public override bool CanPlay(Player player, NPC subject)
            => base.CanPlay(player, subject) && subject != null && subject.active;

        protected override void BuildTimeline(CutsceneTimeline timeline) {
            //时长给大上限：实际由 FishronGrabPlayer 依状态启停
            const int total = 460;
            timeline.Duration = total;
            timeline.Add(new DynamicCameraTrack(0, total, PerFrame));
        }

        private static void PerFrame(CutsceneContext context) {
            if (!context.TryGetSubject(out NPC boss) || !boss.active
                || !boss.TryGetOverride(out DukeFishronAI ov)
                || ov.CurrentState is not FishronVortexGrabState grab) {
                //状态缺席的过渡帧：回落玩家，等待外部 Stop
                context.SetCameraFocus(context.PlayerCenter, 0.08f);
                context.SetCameraZoom(1f, 0.05f);
                return;
            }

            Vector2 heart = FishronGrabFacts.Heart(FishronGrabFacts.ReadAnchor(ov));
            int t = grab.Timer;

            if (t < FishronVortexGrabState.DragEnd) {
                //扎入：镜头跟人坠向涡心，快速推近
                context.SetCameraFocus(heart, 0.09f);
                context.SetCameraZoom(1.28f, 0.05f);
            }
            else if (t < FishronVortexGrabState.DiveStart) {
                //涡底：凝住涡心看三轮掠击，缓慢继续推近
                context.SetCameraFocus(heart + new Vector2(0f, 10f), 0.12f);
                context.SetCameraZoom(1.4f, 0.02f);
            }
            else if (t < FishronVortexGrabState.LaunchTick) {
                //深潜死寂：贴得最近，水面渐平——最安静的一拍
                context.SetCameraFocus(heart + new Vector2(0f, 26f), 0.1f);
                context.SetCameraZoom(1.46f, 0.03f);
            }
            else {
                //破水：跟着被顶飞的玩家拉远交还
                context.SetCameraFocus(context.PlayerCenter, 0.12f);
                context.SetCameraZoom(1.02f, 0.06f);
            }
        }
    }

    /// <summary>
    /// 投技玩家侧：位移与控制锁只由被抓玩家自己的客户端施加（玩家位置客户端权威），
    /// 运镜/滤镜/闷音只在 Main.myPlayer==受害者 时启用；旁观者两不沾。
    /// 抽吸期的吸力也在本地施加——可以逆流游出去
    /// </summary>
    internal class FishronGrabPlayer : ModPlayer
    {
        /// <summary>释放后坠落保护帧：顶飞不该被摔死收尾</summary>
        private int releaseGrace;
        /// <summary>本地已施加破水顶飞</summary>
        private bool flungLocal;
        /// <summary>本地上一帧处于钉住（区分中途夭折与正常破水）</summary>
        private bool pinnedLastFrame;

        /// <summary>运镜期间的震屏转发（导演接管相机后普通震屏无效）</summary>
        internal static void RequestShake(float intensity, int duration) {
            if (VaultUtils.isServer || !CWRServerConfig.Instance.ScreenVibration) {
                return;
            }
            if (CutsceneDirector.CurrentClip is not FishronGrabCutscene) {
                return;
            }
            CutsceneDirector.Shake(Vector2.Zero, intensity, 0.9f, duration);
        }

        /// <summary>正在投技流程的公爵；无则全 null</summary>
        private static DukeFishronAI FindGrabBoss(out NPC boss) {
            boss = null;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.type != NPCID.DukeFishron) {
                    continue;
                }
                //槽位复用等异常取不到覆写时视作无投技（精确索引缺键会抛出）
                if (!npc.TryGetOverride(out DukeFishronAI ov) || ov.CurrentState == null) {
                    continue;
                }
                if (ov.CurrentState is FishronVortexSnareState or FishronVortexGrabState) {
                    boss = npc;
                    return ov;
                }
            }
            return null;
        }

        public override void PreUpdateMovement() {
            //坠落保护：每帧重置落点，被顶上天不折算摔伤
            if (releaseGrace > 0) {
                releaseGrace--;
                Player.fallStart = (int)(Player.position.Y / 16f);
            }

            DukeFishronAI ov = FindGrabBoss(out NPC boss);
            if (ov == null) {
                //抓取途中整个流程蒸发（boss 转死亡演出/退场）：放人时补一口保护
                if (pinnedLastFrame) {
                    Player.SetImmuneTimeForAllTypes(40);
                    releaseGrace = 120;
                }
                flungLocal = false;
                pinnedLastFrame = false;
                return;
            }

            if (ov.CurrentState is FishronVortexSnareState snare) {
                ApplySuction(ov, snare);
                pinnedLastFrame = false;
                return;
            }

            if (ov.CurrentState is FishronVortexGrabState grab) {
                int victim = FishronGrabFacts.ReadVictim(ov);
                Vector2 heart = FishronGrabFacts.Heart(FishronGrabFacts.ReadAnchor(ov));

                //钉住段：位移在每个端对该玩家实例都施加（平滑），锁输入只锁本地
                if (victim == Player.whoAmI && grab.Timer < FishronVortexGrabState.LaunchTick) {
                    ApplyPin(grab, heart);
                    pinnedLastFrame = true;
                    return;
                }

                //钉住结束帧：贴近破水拍（容忍释放包早到几帧）按顶飞处理，过早断投则温和放人
                if (pinnedLastFrame && !flungLocal) {
                    if (grab.Timer >= FishronVortexGrabState.LaunchTick - 8) {
                        flungLocal = true;
                        int seed = (int)ov.ai[FishronGrabFacts.SlotSeed];
                        int side = (seed & 2) == 0 ? 1 : -1;
                        Player.velocity = new Vector2(side * 5f, -27f);
                        Player.SetImmuneTimeForAllTypes(90);
                        releaseGrace = 240;
                    }
                    else {
                        Player.SetImmuneTimeForAllTypes(40);
                        releaseGrace = 120;
                    }
                }
                pinnedLastFrame = false;
            }
        }

        /// <summary>
        /// 抽吸吸力：只对本机自己的玩家施加（位置客户端权威）。
        /// 力度低于起飞/冲刺加速度——决意外游必能脱出，站桩才会被卷
        /// </summary>
        private void ApplySuction(DukeFishronAI ov, FishronVortexSnareState snare) {
            if (Main.dedServ || Player.whoAmI != Main.myPlayer || !Player.Alives() || Player.ghost) {
                return;
            }
            if (snare.Timer < FishronVortexSnareState.SuctionStart
                || snare.Timer >= FishronVortexSnareState.CommitTick) {
                return;
            }
            Vector2 heart = FishronGrabFacts.Heart(FishronGrabFacts.ReadAnchor(ov));
            float dist = Player.Distance(heart);
            if (dist > FishronGrabFacts.SuctionRadius || dist < 8f) {
                return;
            }
            float falloff = 1f - dist / FishronGrabFacts.SuctionRadius;
            falloff = falloff * falloff * (3f - 2f * falloff);
            //末 20 帧收网增压：跟预告锁定同语法，最后一瞬最凶
            float lockBoost = snare.Timer >= FishronVortexSnareState.CommitTick - 20 ? 1.7f : 1f;
            Vector2 pull = (heart - Player.Center).SafeNormalize(Vector2.Zero)
                * (0.62f * falloff * lockBoost);
            Player.velocity += pull;
        }

        /// <summary>钉住位移：卷入段强力牵引，落定后钉在涡心随水沉浮</summary>
        private void ApplyPin(FishronVortexGrabState grab, Vector2 heart) {
            int t = grab.Timer;

            //硬件动作清理：钩爪/坐骑/站离等一切外力锚
            Player.RemoveAllGrapplingHooks();
            if (Player.mount?.Active == true) {
                Player.mount.Dismount(Player);
            }
            Player.StopVanityActions();
            Player.pulley = false;
            Player.noItems = true;
            Player.noBuilding = true;
            Player.noKnockback = true;
            Player.fallStart = (int)(Player.position.Y / 16f);

            if (t <= FishronVortexGrabState.HitStopEnd) {
                //顿帧：世界停一拍
                Player.velocity = Vector2.Zero;
            }
            else if (t <= FishronVortexGrabState.DragEnd) {
                //卷入：强牵引螺旋收向涡心（速度写法保留碰撞安全）
                Vector2 desired = (heart - Player.Center) * 0.16f;
                if (desired.Length() > 26f) {
                    desired = desired.SafeNormalize(Vector2.Zero) * 26f;
                }
                Player.velocity = Vector2.Lerp(Player.velocity, desired, 0.35f);
            }
            else {
                //钉死涡心：确定性沉浮（各端同帧同浮沉）
                Vector2 bob = new((float)Math.Sin(t * 0.13f) * 5f, (float)Math.Sin(t * 0.21f + 1.3f) * 7f);
                Player.Center = Vector2.Lerp(Player.Center, heart + bob, 0.5f);
                Player.velocity = Vector2.Zero;
            }

            //本地专属：锁输入 + 闷音氛围
            if (Player.whoAmI == Main.myPlayer && !Main.dedServ) {
                LockLocalControls();
                //水下闷响：隔水听见他在绕着你转
                if (t > FishronVortexGrabState.DragEnd && t % 46 == 24) {
                    SoundEngine.PlaySound(SoundID.Zombie20 with {
                        Volume = 0.3f,
                        Pitch = -0.85f,
                        MaxInstances = 2
                    }, Player.Center);
                }
                if (t == FishronVortexGrabState.DragEnd - 6) {
                    SoundEngine.PlaySound(SoundID.Drown with { Volume = 0.9f, Pitch = 0.1f }, Player.Center);
                }
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

        public override void PostUpdate() {
            if (Player.whoAmI != Main.myPlayer) {
                return;
            }

            DukeFishronAI ov = FindGrabBoss(out NPC boss);
            bool playing = CutsceneDirector.CurrentClip is FishronGrabCutscene;

            bool wantCutscene = false;
            if (ov?.CurrentState is FishronVortexGrabState grab && boss != null) {
                int victim = FishronGrabFacts.ReadVictim(ov);
                //钉住期以受害者槽为准；破水后凭本地 flung 旗留 30 帧跟飞
                wantCutscene = (victim == Player.whoAmI
                        && grab.Timer < FishronVortexGrabState.LaunchTick)
                    || (flungLocal && grab.Timer < FishronVortexGrabState.LaunchTick + 30);

                //入水滤镜：拖入段渐启，破水即撤（撤由 FX 自身衰减完成）
                if (victim == Player.whoAmI || flungLocal) {
                    int t = grab.Timer;
                    if (t >= FishronVortexGrabState.DragEnd - 12
                        && t < FishronVortexGrabState.LaunchTick) {
                        float veil = MathHelper.Clamp(
                            (t - (FishronVortexGrabState.DragEnd - 12)) / 18f, 0f, 1f);
                        FishronGrabVeilFX.Report(veil);
                    }
                }
            }

            if (wantCutscene && boss != null) {
                if (!playing) {
                    CutsceneDirector.Play<FishronGrabCutscene, NPC>(boss, restartSameClip: false);
                }
            }
            else if (playing) {
                CutsceneDirector.Stop();
            }
        }

        public override void UpdateDead() {
            //死亡帧后 PostUpdate/PreUpdateMovement 不再执行：退出路径必须在这里兜底
            releaseGrace = 0;
            flungLocal = false;
            pinnedLastFrame = false;
            if (Player.whoAmI == Main.myPlayer && !Main.dedServ
                && CutsceneDirector.CurrentClip is FishronGrabCutscene) {
                CutsceneDirector.Stop();
            }
        }

        public override void OnEnterWorld() {
            releaseGrace = 0;
            flungLocal = false;
            pinnedLastFrame = false;
        }
    }
}
