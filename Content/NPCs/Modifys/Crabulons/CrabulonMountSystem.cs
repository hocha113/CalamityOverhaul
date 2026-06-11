using CalamityOverhaul.Common;
using System;
using Terraria;
using Terraria.Audio;

namespace CalamityOverhaul.Content.NPCs.Modifys.Crabulons
{
    /// <summary>
    /// 菌生蟹骑乘系统。
    /// 多人同步模型：骑乘期间运动权威是骑手玩家（走原版玩家同步管线，60帧平滑），
    /// 蟹在所有端每帧吸附到骑手锚点上，自身不再进行任何物理模拟，
    /// 因此各端的蟹位置都是同一条已同步数据流（骑手位置）的纯函数，不会发散。
    /// 骑手的运动接管见<see cref="CrabulonMountPlayer"/>
    /// </summary>
    internal class CrabulonMountSystem
    {
        private readonly NPC npc;
        private readonly ModifyCrabulon owner;

        public CrabulonMountSystem(NPC npc, ModifyCrabulon owner) {
            this.npc = npc;
            this.owner = owner;
        }

        //获取骑乘位置
        public Vector2 GetMountPosition() {
            float yOffset = owner.ai[9] > 0 ? owner.ai[9] : npc.gfxOffY;
            return npc.Top + new Vector2(0, yOffset);
        }

        //骑乘时蟹箱体的左上角位置，以骑手为锚点（npc.Top对齐骑手中心）
        public static Vector2 GetAttachedBoxPosition(Player rider, NPC npc) {
            return rider.Center - new Vector2(npc.width / 2f, 0f);
        }

        //本端解除骑乘状态，不发包，用于网络通知和异常兜底
        public void ForceDismount() {
            bool wasMount = owner.Mount || owner.MountACrabulon;
            owner.Mount = false;
            owner.MountACrabulon = false;
            owner.localAI[5] = 0f;
            //恢复蟹的物理模拟
            npc.noGravity = false;
            npc.noTileCollide = false;
            if (wasMount) {
                owner.DontMount = CrabulonConstants.DismountCooldown;
            }
            if (owner.CrabulonPlayer != null) {
                owner.CrabulonPlayer.IsMount = false;
                owner.CrabulonPlayer.MountCrabulon = null;
            }
        }

        //主动下马，骑手端发起并广播
        public void Dismount() {
            bool isRider = owner.Owner.Alives() && owner.Owner.whoAmI == Main.myPlayer;

            if (isRider) {
                owner.Owner.fullRotation = 0;
                owner.Owner.velocity.Y -= 5;//只允许骑手端修改自己的速度
            }

            ForceDismount();

            if (isRider) {
                owner.SendNetWork();
            }
        }

        //处理骑乘AI
        public bool ProcessMountAI() {
            if (owner.DontMount > 0) {
                owner.DontMount--;
            }

            if (!owner.Mount) {
                return HandleMountRequest();
            }

            return HandleMountedAttach();
        }

        //处理上马请求
        private bool HandleMountRequest() {
            if (owner.CrabulonPlayer != null) {
                owner.CrabulonPlayer.MountCrabulon = null;
                owner.CrabulonPlayer.IsMount = false;
            }

            //上马只由骑手端发起，其余端通过状态同步得知
            if (ShouldStartMount()) {
                owner.MountACrabulon = true;
                owner.SendNetWork();
                PlayMountSound();
            }

            //吸附动画只在骑手端推进，完成后广播Mount状态，避免各端用不同位置各自判定完成时机
            if (owner.MountACrabulon && owner.Owner.whoAmI == Main.myPlayer) {
                ProcessMountAnimation();
            }

            return true;
        }

        //判断是否应该开始上马
        private bool ShouldStartMount() {
            return owner.Owner.whoAmI == Main.myPlayer
                && owner.SaddleItem.Alives()
                && owner.DontMount <= 0
                && !owner.MountACrabulon
                && owner.hoverNPC
                && owner.rightPressed;
        }

        //播放上马音效
        private void PlayMountSound() {
            if (VaultUtils.isServer) {
                return;
            }

            SoundEngine.PlaySound(CWRSound.ToMount with {
                PitchRange = (-0.1f, 0.1f),
                Volume = Main.rand.NextFloat(0.6f, 0.8f)
            }, owner.Owner.Center);
        }

        //处理上马吸附动画（仅骑手端，操作的是本地玩家自身，天然客户端权威）
        private void ProcessMountAnimation() {
            owner.Owner.RemoveAllGrapplingHooks();
            owner.Owner.mount.Dismount(owner.Owner);

            Vector2 toMount = GetMountPosition() - owner.Owner.Center;
            owner.Owner.velocity = toMount.SafeNormalize(Vector2.Zero) * 8;

            owner.Owner.CWR().IsRotatingDuringDash = true;
            owner.Owner.CWR().RotationDirection = Math.Sign(owner.Owner.velocity.X);
            owner.Owner.CWR().PendingDashRotSpeedMode = 0.06f;
            owner.Owner.CWR().PendingDashVelocity = owner.Owner.velocity;

            if (++owner.localAI[5] > CrabulonConstants.MountTimeout || toMount.Length() < owner.Owner.width) {
                CompleteMountProcess();
            }
        }

        //完成上马流程
        private void CompleteMountProcess() {
            owner.localAI[5] = 0f;
            owner.Mount = true;
            owner.MountACrabulon = false;
            owner.Owner.velocity = Vector2.Zero;

            if (owner.CrabulonPlayer != null) {
                owner.CrabulonPlayer.MountCrabulon = owner;
            }

            owner.SendNetWork();
        }

        //骑乘状态：蟹在所有端吸附到骑手身上
        private bool HandleMountedAttach() {
            //鞍具被移除时自动下马
            if (!owner.SaddleItem.Alives()) {
                Dismount();
                return false;
            }

            if (owner.CrabulonPlayer != null) {
                CrabulonPlayer.CloseDuringDash(owner.Owner);
                owner.CrabulonPlayer.MountCrabulon = owner;
                owner.CrabulonPlayer.IsMount = true;
            }

            //蟹不参与任何物理：穿墙防卡由骑手侧的箱体约束保证（CrabulonMountPlayer）
            npc.noGravity = true;
            npc.noTileCollide = true;

            //通过velocity吸附到骑手锚点，引擎积分后位置精确贴合，
            //velocity同时天然等于本帧位移（即骑手速度），动画系统可直接复用
            Vector2 targetPos = GetAttachedBoxPosition(owner.Owner, npc);
            npc.velocity = targetPos - npc.position;

            //起身动画残留帧
            if (owner.ai[9] > 0) {
                owner.ai[9]--;
            }

            UpdateMountAnimation();

            if (CheckDismountInput()) {
                Dismount();
            }

            return false;
        }

        //更新骑乘动画，输入源是骑手速度（各端均已平滑同步）
        private void UpdateMountAnimation() {
            float horizontalSpeed = owner.Owner.velocity.X;
            npc.ai[0] = Math.Abs(horizontalSpeed) > 0.1f ? 1f : 0f;
            if (Math.Abs(owner.Owner.velocity.Y) > 1f) {
                npc.ai[0] = 3f;
            }

            if (owner.dontTurnTo <= 0f && horizontalSpeed != 0f) {
                npc.spriteDirection = npc.direction = Math.Sign(horizontalSpeed);
            }
        }

        //检查下马输入
        private bool CheckDismountInput() {
            return owner.Owner.whoAmI == Main.myPlayer && owner.hoverNPC && owner.rightPressed;
        }
    }
}
