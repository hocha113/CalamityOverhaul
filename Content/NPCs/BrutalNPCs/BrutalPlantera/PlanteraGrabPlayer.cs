using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalPlantera.States;
using InnoVault.Cinematics;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalPlantera
{
    /// <summary>
    /// 绞藤飨宴被抓者本地侧：Terraria玩家位置是客户端权威，
    /// 拖拽/钉口/弹射位移、锁输入、免疫与分拍结算伤害全部只在
    /// 被抓玩家自己的客户端施加(从同步的Boss ai推导启停)；
    /// 运镜同样只在被抓者本机播放，旁观者不被接管
    /// </summary>
    internal class PlanteraGrabPlayer : ModPlayer
    {
        /// <summary>释放弹射的无敌帧(覆盖上升+大部分回落)</summary>
        private const int ReleaseInvince = 120;
        /// <summary>中途脱手(boss死/打断)的怜悯无敌</summary>
        private const int MercyInvince = 60;

        private int lastSubPhase = -1;
        private int grabTimer;
        private bool wasPinned;

        /// <summary>投技运镜期间的震屏(运镜接管相机后普通震屏可能失效)</summary>
        internal static void RequestShake(float intensity, int duration) {
            if (VaultUtils.isServer || !CWRClientConfig.Instance.ScreenVibration) {
                return;
            }
            if (CutsceneDirector.CurrentClip is not PlanteraVineFeastCutscene) {
                return;
            }
            CutsceneDirector.Shake(Vector2.Zero, intensity, 0.88f, duration);
        }

        public override void PreUpdateMovement() {
            //位移权威在被抓者本地：只处理自己
            if (Player.whoAmI != Main.myPlayer) {
                return;
            }

            NPC boss = PlanteraVineFeastState.FindFeastBoss();
            int sub = boss != null ? PlanteraVineFeastState.GrabSubPhase(boss) : -1;
            int victim = boss != null ? PlanteraVineFeastState.GrabVictim(boss) : -1;
            bool grabbed = boss != null && victim == Player.whoAmI
                && sub >= PlanteraVineFeastState.SubDrag && sub <= PlanteraVineFeastState.SubSpit;

            if (!grabbed) {
                //异常出口(boss死亡/换状态/掉线目标切换)：钉身期被放开给怜悯无敌
                if (wasPinned) {
                    Player.SetImmuneTimeForAllTypes(MercyInvince);
                }
                ResetLocal();
                return;
            }

            if (sub != lastSubPhase) {
                //刚被缠住的一次性处理
                if (lastSubPhase < PlanteraVineFeastState.SubDrag) {
                    OnGrabBegin();
                }
                lastSubPhase = sub;
                grabTimer = 0;
            }
            else {
                grabTimer++;
            }

            //弹射之后控制权已交还，不再钉身
            bool pinned = sub != PlanteraVineFeastState.SubSpit
                || grabTimer < PlanteraVineFeastState.SpitYeetTick;

            if (pinned) {
                ApplyPinnedFrame();
                wasPinned = true;
            }

            switch (sub) {
                case PlanteraVineFeastState.SubDrag:
                    UpdateDragMotion(boss);
                    break;
                case PlanteraVineFeastState.SubChew:
                    UpdateChewMotion(boss);
                    break;
                default:
                    UpdateSpitMotion(boss);
                    break;
            }
        }

        /// <summary>被缠瞬间：断钩爪/下坐骑/断滑轮/停手上动作</summary>
        private void OnGrabBegin() {
            Player.RemoveAllGrapplingHooks();
            if (Player.mount != null && Player.mount.Active) {
                Player.mount.Dismount(Player);
            }
            Player.pulley = false;
            Player.channel = false;
            wasPinned = false;
        }

        /// <summary>钉身每帧：免疫外来伤害(分拍伤害自己绕行)+禁道具+锁输入+防摔清账</summary>
        private void ApplyPinnedFrame() {
            Player.SetImmuneTimeForAllTypes(2);
            Player.immuneNoBlink = true;
            Player.noItems = true;
            Player.noBuilding = true;
            Player.noKnockback = true;
            Player.fallStart = (int)(Player.position.Y / 16f);
            LockLocalControls();
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

        /// <summary>拖拽：顿帧定身→荆棘颠簸中被收线拽向巨口</summary>
        private void UpdateDragMotion(NPC boss) {
            if (grabTimer < PlanteraVineFeastState.HitStopTime) {
                Player.velocity = Vector2.Zero;
                return;
            }

            Vector2 maw = PlanteraVineFeastState.MawWorld(boss);
            Vector2 to = maw - Player.Center;
            if (to.Length() < 56f) {
                //已到嘴边贴住等咀嚼
                Player.velocity = to * 0.35f;
                return;
            }

            float t = MathHelper.Clamp((grabTimer - PlanteraVineFeastState.HitStopTime)
                / (float)(PlanteraVineFeastState.DragTime - PlanteraVineFeastState.HitStopTime), 0f, 1f);
            float speed = MathHelper.Lerp(8f, 26f, t * t);
            Vector2 dir = to.SafeNormalize(Vector2.Zero);
            //垂向小颠簸：拖过荆棘的糙感
            Vector2 bump = dir.RotatedBy(MathHelper.PiOver2) * (float)Math.Sin(grabTimer * 0.55f) * 2.2f;
            Player.velocity = dir * speed + bump;

            //荆棘刮擦分拍(轻伤)
            if (grabTimer == PlanteraVineFeastState.ScrapeTickA
                || grabTimer == PlanteraVineFeastState.ScrapeTickB) {
                ApplyScriptedHit(boss, 0.12f);
                RequestShake(3f, 8);
            }
        }

        /// <summary>咀嚼：钉在巨口，拍内被缓缓拖出再猛拽入；咬合帧结算</summary>
        private void UpdateChewMotion(NPC boss) {
            int beatTick = grabTimer % PlanteraVineFeastState.BitePeriod;
            int biteIndex = grabTimer / PlanteraVineFeastState.BitePeriod;

            //拍内口距：张口缓推出→咬合猛拽入→余韵回位
            float mawDist;
            if (beatTick < PlanteraVineFeastState.BiteSnapTick) {
                mawDist = MathHelper.Lerp(PlanteraVineFeastState.MawHoldDist,
                    PlanteraVineFeastState.MawHoldDist + 26f,
                    beatTick / (float)PlanteraVineFeastState.BiteSnapTick);
            }
            else if (beatTick < PlanteraVineFeastState.BiteSnapTick + 4) {
                mawDist = PlanteraVineFeastState.MawHoldDist - 12f;
            }
            else {
                mawDist = MathHelper.Lerp(PlanteraVineFeastState.MawHoldDist - 12f,
                    PlanteraVineFeastState.MawHoldDist,
                    (beatTick - PlanteraVineFeastState.BiteSnapTick - 4f)
                    / (PlanteraVineFeastState.BitePeriod - PlanteraVineFeastState.BiteSnapTick - 4f));
            }

            Player.Center = boss.Center + PlanteraVineFeastState.MawDir(boss) * mawDist;
            Player.velocity = Vector2.Zero;

            //窗口硬界：服务端推进迟到时本地不多咬第四口
            if (beatTick == PlanteraVineFeastState.BiteSnapTick
                && grabTimer < PlanteraVineFeastState.ChewTime) {
                ApplyScriptedHit(boss, 0.5f);
                RequestShake(5f + biteIndex * 1.5f, 10);
                //第三口孢子毒雾喷面：中毒由被抓者本地入账(客户端权威+原版buff同步)
                if (biteIndex == 2) {
                    Player.AddBuff(BuffID.Poisoned, 480);
                }
            }
        }

        /// <summary>吐飞：压缩期继续钉口，弹射帧终结一击+抛出+释放无敌</summary>
        private void UpdateSpitMotion(NPC boss) {
            if (grabTimer < PlanteraVineFeastState.SpitYeetTick) {
                float squeeze = grabTimer / (float)PlanteraVineFeastState.SpitYeetTick;
                Player.Center = boss.Center + PlanteraVineFeastState.MawDir(boss)
                    * MathHelper.Lerp(PlanteraVineFeastState.MawHoldDist, 38f, squeeze);
                Player.velocity = Vector2.Zero;
                return;
            }

            if (grabTimer == PlanteraVineFeastState.SpitYeetTick) {
                //终结一击(最重)→连壳抛飞→足额无敌盖过Hurt自带
                ApplyScriptedHit(boss, 0.65f);
                Vector2 spitDir = new Vector2(Math.Sign(Player.Center.X - boss.Center.X) * 0.62f, -1f)
                    .SafeNormalize(-Vector2.UnitY);
                if (spitDir.X == 0f) {
                    spitDir = new Vector2(0.62f, -1f).SafeNormalize(-Vector2.UnitY);
                }
                Player.velocity = spitDir * 21f;
                Player.fallStart = (int)(Player.position.Y / 16f);
                Player.SetImmuneTimeForAllTypes(ReleaseInvince);
                RequestShake(8f, 14);
                wasPinned = false;
            }
        }

        /// <summary>
        /// 分拍结算：请求伤害钳到"当前血-1"以内，防御只会再降
        /// 满血玩家绝不会被一套投技处死；走Boss免疫槽绕过钉身期通用免疫
        /// </summary>
        private void ApplyScriptedHit(NPC boss, float scale) {
            int raw = (int)(boss.defDamage * scale);
            int request = Math.Min(raw, Player.statLife - 1);
            if (request < 5) {
                return;
            }
            Player.hurtCooldowns[ImmunityCooldownID.Bosses] = 0;
            Player.immune = false;
            Player.Hurt(PlayerDeathReason.ByNPC(boss.whoAmI), request,
                Math.Sign(Player.Center.X - boss.Center.X),
                cooldownCounter: ImmunityCooldownID.Bosses, knockback: 0f);
        }

        private void ResetLocal() {
            lastSubPhase = -1;
            grabTimer = 0;
            wasPinned = false;
        }

        /// <summary>运镜启停：仅被抓者本机，从NPC同步态推导(Deerclops模式)</summary>
        public override void PostUpdate() {
            if (Player.whoAmI != Main.myPlayer) {
                return;
            }

            NPC boss = PlanteraVineFeastState.FindFeastBoss();
            int sub = boss != null ? PlanteraVineFeastState.GrabSubPhase(boss) : -1;
            bool mine = boss != null
                && PlanteraVineFeastState.GrabVictim(boss) == Player.whoAmI
                && sub >= PlanteraVineFeastState.SubDrag && sub <= PlanteraVineFeastState.SubSpit;
            bool playing = CutsceneDirector.CurrentClip is PlanteraVineFeastCutscene;

            if (mine && !playing) {
                CutsceneDirector.Play<PlanteraVineFeastCutscene, NPC>(boss, restartSameClip: false);
            }
            else if (!mine && playing) {
                CutsceneDirector.Stop();
            }
        }

        /// <summary>死亡(DoT等)时的本地清理：停运镜清残余</summary>
        public override void UpdateDead() {
            if (Player.whoAmI == Main.myPlayer
                && CutsceneDirector.CurrentClip is PlanteraVineFeastCutscene) {
                CutsceneDirector.Stop();
            }
            ResetLocal();
        }

        public override void OnRespawn() => ResetLocal();

        public override void OnEnterWorld() => ResetLocal();

        public override void PlayerDisconnect() => ResetLocal();
    }
}
