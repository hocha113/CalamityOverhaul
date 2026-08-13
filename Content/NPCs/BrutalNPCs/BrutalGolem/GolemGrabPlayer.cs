using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalGolem.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalGolem.Projectiles;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalGolem.States.Fists;
using InnoVault.Cinematics;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalGolem
{
    /// <summary>投技被抓者本端：玩家位置客户端权威，贴合位移/锁控/运镜/保命阀全部只在
    /// Main.myPlayer 的客户端施加；抓取事实读拳 Override ai 同步槽</summary>
    internal class GolemGrabPlayer : ModPlayer
    {
        //以下皆为被抓者本端表现状态，不参与网络
        private int grabFistIndex = -1;
        private int localTimer;
        private Vector2 dragStart;
        private GolemPinKind lastPinKind;
        private bool releaseDone;

        /// <summary>本端是否正被石巨人抓住</summary>
        internal bool GrabbedLocally => grabFistIndex >= 0;

        public override void PostUpdate() {
            if (Player.whoAmI != Main.myPlayer) {
                return;
            }

            NPC fist = FindGrabbingMe();
            bool playing = CutsceneDirector.CurrentClip is GolemGrabCutscene;

            if (fist == null) {
                if (grabFistIndex >= 0) {
                    OnLocalRelease();
                }
                if (playing) {
                    CutsceneDirector.Stop();
                }
                grabFistIndex = -1;
                localTimer = 0;
                return;
            }

            if (grabFistIndex < 0) {
                OnLocalGrabStart();
            }
            grabFistIndex = fist.whoAmI;
            localTimer++;
            ApplyGlue(fist);

            //restartSameClip:false，已播则复用；被更高优先级演出（死亡运镜）占用时锁控由 SetControls 兜底
            if (!playing) {
                CutsceneDirector.Play<GolemGrabCutscene, NPC>(fist, restartSameClip: false);
            }
        }

        /// <summary>死亡不走 PostUpdate：在此清理演出与本地态</summary>
        public override void UpdateDead() {
            if (Player.whoAmI != Main.myPlayer) {
                return;
            }
            if (grabFistIndex >= 0) {
                grabFistIndex = -1;
                localTimer = 0;
                releaseDone = true;
            }
            if (CutsceneDirector.CurrentClip is GolemGrabCutscene) {
                CutsceneDirector.Stop();
            }
        }

        /// <summary>兜底锁控：运镜被更高优先级演出顶掉时仍锁住被抓者输入</summary>
        public override void SetControls() {
            if (Player.whoAmI != Main.myPlayer || grabFistIndex < 0) {
                return;
            }
            Player.controlLeft = false;
            Player.controlRight = false;
            Player.controlUp = false;
            Player.controlDown = false;
            Player.controlJump = false;
            Player.controlUseItem = false;
            Player.controlUseTile = false;
            Player.controlHook = false;
            Player.controlMount = false;
            Player.controlThrow = false;
            Player.controlSmart = false;
        }

        /// <summary>保命阀：投技期间石巨人系伤害不可致死（满血一套不死的硬保证）</summary>
        public override void ModifyHurt(ref Player.HurtModifiers modifiers) {
            if (grabFistIndex < 0 || Player.statLife <= 1) {
                return;
            }
            if (!IsGolemSource(modifiers.DamageSource)) {
                return;
            }
            modifiers.SetMaxDamage(Math.Max(Player.statLife - 1, 1));
        }

        /// <summary>抓取瞬间：斩断位移类挂点，起手顿帧反馈</summary>
        private void OnLocalGrabStart() {
            localTimer = 0;
            releaseDone = false;
            dragStart = Player.Center;
            lastPinKind = GolemPinKind.None;

            if (Player.mount?.Active == true) {
                Player.mount.Dismount(Player);
            }
            Player.RemoveAllGrapplingHooks();
            Player.pulley = false;
            Player.channel = false;
        }

        /// <summary>逐帧贴合：拖拽段本地确定性插值（平滑），钉压/研磨段贴拳同步位置</summary>
        private void ApplyGlue(NPC fist) {
            GolemFistAI fistOverride = GolemFacts.FindOverride<GolemFistAI>(fist);
            if (fistOverride == null) {
                return;
            }

            var kind = (GolemPinKind)(int)fistOverride.ai[GolemAiSlots.FistPinKind];
            Vector2 pin = new(fistOverride.ai[GolemAiSlots.FistPinX], fistOverride.ai[GolemAiSlots.FistPinY]);
            Vector2 normal = GolemFacts.PinNormal(kind);
            if (kind != GolemPinKind.None) {
                lastPinKind = kind;
            }

            Vector2 glue;
            if (kind != GolemPinKind.None && pin.LengthSquared() > 1f
                && localTimer < GolemFistGrabState.DragEnd) {
                //拖拽段：从被抓点二次缓入冲向钉压点，与服务端拳轨迹同形
                float t = MathHelper.Clamp((localTimer - GolemFistGrabState.HitStopEnd)
                    / (float)(GolemFistGrabState.DragEnd - GolemFistGrabState.HitStopEnd), 0f, 1f);
                glue = Vector2.Lerp(dragStart, pin, t * t);
            }
            else {
                //钉压/研磨段：贴住拳内侧
                glue = fist.Center - normal * GolemFistGrabState.PinGap;
            }

            Player.Center = glue;
            Player.velocity = Vector2.Zero;
            //持续压归零坠落计，出手后不吃摔落伤害
            Player.fallStart = (int)(Player.position.Y / 16f);
            Player.channel = false;
            //飞行中的钩爪补斩（抓取前射出的钩子命中会拽人）
            if (Player.grapCount > 0) {
                Player.RemoveAllGrapplingHooks();
            }
        }

        /// <summary>释放瞬间：沿钉面弹出 + 足额无敌帧 + 终结反馈</summary>
        private void OnLocalRelease() {
            if (releaseDone) {
                return;
            }
            releaseDone = true;

            Vector2 normal = GolemFacts.PinNormal(lastPinKind);
            Vector2 tangent = GolemFacts.GrindTangent(lastPinKind);
            Player.velocity = lastPinKind switch {
                GolemPinKind.WallLeft or GolemPinKind.WallRight => normal * 7.5f + new Vector2(0f, -6.5f),
                GolemPinKind.FloorRight or GolemPinKind.FloorLeft => tangent * 5f + new Vector2(0f, -8.5f),
                //无钉面的放弃式松手：轻弹脱身
                _ => new Vector2(0f, -4f),
            };
            Player.SetImmuneTimeForAllTypes(60);
            Player.fallStart = (int)(Player.position.Y / 16f);

            //终结反馈：运镜还在时走导演震，环波兜底
            if (CutsceneDirector.CurrentClip is GolemGrabCutscene) {
                CutsceneDirector.Shake(Vector2.UnitY, 9f, 0.88f, 20);
            }
            GolemScreenEffects.Shake(6f);
            GolemScreenEffects.PushShockRing(Player.Center, 0.9f, 560f);
        }

        /// <summary>正抓着本端玩家的拳，无则 null</summary>
        private NPC FindGrabbingMe() {
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.type != NPCID.GolemFistLeft && npc.type != NPCID.GolemFistRight) {
                    continue;
                }
                if ((int)npc.ai[GolemAiSlots.PartStateSlot] != (int)GolemFistStateIndex.Grab) {
                    continue;
                }
                GolemFistAI fistOverride = GolemFacts.FindOverride<GolemFistAI>(npc);
                if (fistOverride == null
                    || (int)fistOverride.ai[GolemAiSlots.FistGrabTarget] - 1 != Player.whoAmI) {
                    continue;
                }
                return npc;
            }
            return null;
        }

        /// <summary>石巨人家族伤害来源判定（保命阀作用域）</summary>
        private static bool IsGolemSource(PlayerDeathReason source) {
            if (source == null) {
                return false;
            }
            int npcIndex = source.SourceNPCIndex;
            if (npcIndex >= 0 && npcIndex < Main.maxNPCs) {
                int type = Main.npc[npcIndex].type;
                if (type is NPCID.Golem or NPCID.GolemHead or NPCID.GolemHeadFree
                    or NPCID.GolemFistLeft or NPCID.GolemFistRight) {
                    return true;
                }
            }
            int projIndex = source.SourceProjectileLocalIndex;
            if (projIndex >= 0 && projIndex < Main.maxProjectiles) {
                int type = Main.projectile[projIndex].type;
                if (type == ModContent.ProjectileType<GolemGrabRay>()
                    || type == ModContent.ProjectileType<GolemEyeRay>()
                    || type == ModContent.ProjectileType<GolemShockWave>()
                    || type == ModContent.ProjectileType<GolemStoneShrapnel>()
                    || type == ModContent.ProjectileType<GolemSunBolt>()) {
                    return true;
                }
            }
            return false;
        }
    }
}
