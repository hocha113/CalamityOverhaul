using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Rendering;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.States;
using CalamityOverhaul.Content.TimeFreezes;
using InnoVault.Cinematics;
using InnoVault.GameSystem;
using InnoVault.PRT;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight
{
    /// <summary>光绫缚舞运镜：只在受缚玩家本端播放，对齐EmpressLightBindWaltzState节拍表</summary>
    internal sealed class EmpressGrabCutscene : CutsceneClip<NPC>
    {
        /// <summary>运镜总长：掷出后跟拍受缚者飞行一小段再交还镜头</summary>
        internal const int ClipTime = 250;

        //低于死亡运镜(100)：她中途进死亡演出时由死亡运镜顶替
        public override int Priority => 80;

        public override bool CanPlay(Player player, NPC subject)
            => base.CanPlay(player, subject) && subject != null && subject.active;

        protected override void BuildTimeline(CutsceneTimeline timeline) {
            timeline.Duration = ClipTime;
            const int Throw = EmpressLightBindWaltzState.BurstTick;

            //缚定与剑舞：焦点压在受缚者与女皇之间偏受缚者；掷出后跟拍飞行
            timeline
                .Add(CameraFocusTrack.Follow(0, Throw, DuetFocus, Vector2.Zero, 0.09f))
                .Add(CameraFocusTrack.Follow(Throw, ClipTime - Throw,
                    context => context.PlayerCenter, new Vector2(0f, -30f), 0.07f));

            //变焦：捕获顿帧快推近→剑舞缓吐→终唱再推→爆绽回落
            timeline
                .Add(new CameraZoomTrack(0, 14, 1f, 1.42f, 0.1f, CutsceneEase.CubicOut))
                .Add(new CameraZoomTrack(14, EmpressLightBindWaltzState.FinaleStart - 14, 1.42f, 1.3f, 0.03f))
                .Add(new CameraZoomTrack(EmpressLightBindWaltzState.FinaleStart,
                    Throw - EmpressLightBindWaltzState.FinaleStart, 1.3f, 1.48f, 0.05f))
                .Add(new CameraZoomTrack(Throw, ClipTime - Throw, 1.48f, 1f, 0.05f, CutsceneEase.CubicOut));

            //锁操作到掷出为止：飞行段把身体还给玩家
            timeline.Add(new InputLockTrack(0, Throw, CutsceneInputLockFlags.All));
        }

        //演出主体失效时回退受缚者中心，防镜头瞬移
        private static Vector2 DuetFocus(CutsceneContext context)
            => context.TryGetSubject(out NPC boss) && boss.active
                ? Vector2.Lerp(context.PlayerCenter, boss.Center, 0.32f)
                : context.PlayerCenter;
    }

    /// <summary>
    /// 光绫缚舞受缚玩家端：Terraria玩家位置是客户端权威，悬空定身/锁输入/脚本化落伤/掷出
    /// 全部由受缚者自己的客户端施加（读同步来的NPC缚舞态），旁观者只经实体同步看到结果
    /// </summary>
    internal class EmpressGrabPerformancePlayer : ModPlayer
    {
        //剑舞落伤比例（占最大生命，直伤不吃防御；昼形态加严）
        private const float PassFractionNight = 0.11f;
        private const float PassFractionDay = 0.13f;
        private const float FinaleFractionNight = 0.17f;
        private const float FinaleFractionDay = 0.22f;

        private bool bound;
        /// <summary>本轮已掷出/中断，等她离开缚舞态后才允许再次受缚</summary>
        private bool completed;
        private int age;
        private Vector2 anchor;
        private float riseHeight;

        /// <summary>正以缚舞态擒住该玩家的女皇，无则null（各端可用，读同步数据）</summary>
        internal static NPC FindGrabbingEmpress(int playerIndex) {
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.type != NPCID.HallowBoss) {
                    continue;
                }
                if ((int)npc.ai[2] != (int)EmpressStateIndex.LightBindWaltz || npc.target != playerIndex) {
                    continue;
                }
                //确认接管在场：原版女皇的ai[2]是攻击杂项，可能撞值
                if (!npc.TryGetOverride(out Dictionary<Type, NPCOverride> overrides)
                    || !overrides.TryGetValue(typeof(EmpressOfLightAI), out NPCOverride raw)
                    || raw is not EmpressOfLightAI) {
                    continue;
                }
                return npc;
            }
            return null;
        }

        /// <summary>受缚期间锁全部操作输入并禁持物（仅本人客户端有意义）</summary>
        public override void SetControls() {
            if (!bound) {
                return;
            }
            Player.controlLeft = Player.controlRight = false;
            Player.controlUp = Player.controlDown = false;
            Player.controlJump = false;
            Player.controlUseItem = Player.controlUseTile = false;
            Player.controlHook = Player.controlMount = false;
            Player.controlThrow = false;
            Player.noItems = true;
        }

        public override void PostUpdate() {
            if (Player.whoAmI != Main.myPlayer) {
                return;
            }

            NPC boss = FindGrabbingEmpress(Player.whoAmI);

            if (!bound) {
                if (boss == null) {
                    completed = false;
                }
                else if (!completed) {
                    BeginBind();
                }
                return;
            }

            //异常出口：她离开缚舞态（超时/死亡演出/离场）→就地解缚
            if (boss == null) {
                ReleaseEarly();
                return;
            }

            //她被时停冻住：整场处刑暂停，人保持悬吊，节拍不走
            if (TimeFreezeSystem.IsFrozen(boss)) {
                PinBody();
                return;
            }

            age++;

            //保底超时：无论如何不许把人永远吊着
            if (age > EmpressLightBindWaltzState.TotalTime + 90) {
                ReleaseEarly();
                return;
            }

            //终结：辐光爆绽落伤+掷出，交还身体
            if (age == EmpressLightBindWaltzState.BurstTick) {
                FinishAndThrow(boss);
                return;
            }

            //剑舞三拍：刃光擦身的瞬间落伤（本端结算，原版受伤包自动同步）
            bool day = NPC.ShouldEmpressBeEnraged();
            for (int k = 0; k < EmpressLightBindWaltzState.PassCount; k++) {
                if (age == EmpressLightBindWaltzState.PassHitTick(k)) {
                    ApplyGrabHurt(boss, day ? PassFractionDay : PassFractionNight, k % 2 == 0 ? 1 : -1);
                    break;
                }
            }

            PinBody();

            //运镜只在受缚者本端起播；被更高优先级演出压住时重试，终唱后不再补播（防时停错拍重播）
            if (age < EmpressLightBindWaltzState.FinaleStart
                && CutsceneDirector.CurrentClip is not EmpressGrabCutscene
                && Player.Distance(boss.Center) < 2600f) {
                CutsceneDirector.Play<EmpressGrabCutscene, NPC>(boss, restartSameClip: false);
            }
        }

        /// <summary>受缚期间死亡：立即解除本地缚定并收回运镜</summary>
        public override void UpdateDead() {
            if (Player.whoAmI != Main.myPlayer || !bound) {
                return;
            }
            bound = false;
            completed = true;
            StopClipIfOurs();
        }

        /// <summary>缚定起点：锚定当前位，斩断钩爪坐骑等位移挂点</summary>
        private void BeginBind() {
            bound = true;
            completed = false;
            age = 0;
            anchor = Player.Center;
            //头顶有实体物则少升一点，防被抬进物块
            riseHeight = Collision.CanHitLine(Player.Center, 1, 1, Player.Center - new Vector2(0f, 60f), 1, 1) ? 46f : 8f;
            if (Player.mount?.Active == true) {
                Player.mount.Dismount(Player);
            }
            Player.RemoveAllGrapplingHooks();
            Player.velocity = Vector2.Zero;
            //捕获即净场：残余光笼弹的清弹包在途时先兜住这段真空（日间形态弹幕即死级）
            Player.SetImmuneTimeForAllTypes(30);
            Player.immune = true;
        }

        /// <summary>悬空定身：缓升到吊点后随光绫微漾，重力与击退全部无效</summary>
        private void PinBody() {
            float rise = riseHeight * VaultUtils.EaseOutCubic(MathHelper.Clamp(age / 12f, 0f, 1f));
            Vector2 sway = EmpressMotion.Breathing(Player.whoAmI * 0.77f, 2.2f);
            Player.Center = anchor + new Vector2(sway.X * 0.4f, -rise + sway.Y * 0.3f);
            Player.velocity = Vector2.Zero;
            Player.fallStart = (int)(Player.position.Y / 16f);
            Player.RemoveAllGrapplingHooks();
        }

        /// <summary>终结拍：终伤落定后向她的反侧掷出，给足无敌帧与翅膀余量</summary>
        private void FinishAndThrow(NPC boss) {
            bound = false;
            completed = true;
            bool day = NPC.ShouldEmpressBeEnraged();
            ApplyGrabHurt(boss, day ? FinaleFractionDay : FinaleFractionNight, 0);

            float dirX = anchor.X >= boss.Center.X ? 1f : -1f;
            Player.velocity = new Vector2(dirX * 10.5f, -7f);
            Player.SetImmuneTimeForAllTypes(90);
            Player.immune = true;
            Player.wingTime = Player.wingTimeMax;
            Player.fallStart = (int)(Player.position.Y / 16f);
            //对拍的运镜留到自然结束跟拍飞行；错拍重播的运镜（时停打断产物）立即收掉防锁输入过界
            if (CutsceneDirector.CurrentClip is EmpressGrabCutscene
                && CutsceneDirector.CurrentTick < EmpressLightBindWaltzState.BurstTick - 20) {
                CutsceneDirector.Stop();
            }
        }

        /// <summary>提前解缚：清残余速度、给无敌帧、收回运镜</summary>
        private void ReleaseEarly() {
            bound = false;
            completed = true;
            Player.velocity = Vector2.Zero;
            Player.SetImmuneTimeForAllTypes(60);
            Player.immune = true;
            Player.fallStart = (int)(Player.position.Y / 16f);
            StopClipIfOurs();
        }

        private static void StopClipIfOurs() {
            if (CutsceneDirector.CurrentClip is EmpressGrabCutscene) {
                CutsceneDirector.Stop();
            }
        }

        /// <summary>
        /// 脚本化落伤：占最大生命比例的定额直伤（不吃防御，可被闪避），
        /// 铁律，投技永不处死，任何一击至多打到剩1血
        /// </summary>
        private void ApplyGrabHurt(NPC boss, float fraction, int hitDirection) {
            int damage = Math.Max((int)(Player.statLifeMax2 * fraction), 60);
            damage = Math.Min(damage, Math.Max(Player.statLife - 1, 0));
            if (damage <= 0) {
                return;
            }
            Player.HurtInfo info = new() {
                DamageSource = PlayerDeathReason.ByNPC(boss.whoAmI),
                SourceDamage = damage,
                Damage = damage,
                HitDirection = hitDirection,
                Knockback = 0f,
                Dodgeable = true,
                PvP = false,
                CooldownCounter = ImmunityCooldownID.Bosses,
            };
            Player.Hurt(info);

            //受创的贴身光屑（本端补一层，世界侧闪光由缚舞状态负责）
            if (!Main.dedServ) {
                for (int i = 0; i < 6; i++) {
                    float hue = Main.rand.NextFloat();
                    PRTLoader.NewParticle<PRT_EmpressSpark>(Player.Center + Main.rand.NextVector2Circular(14f, 20f),
                        VaultUtils.RandVr(1.5f, 5f), EmpressMotion.Prism(hue, 0.7f),
                        Main.rand.NextFloat(0.6f, 1f))?.Configure(14, hue);
                }
            }
        }
    }
}
