using CalamityOverhaul.Common;
using System;
using Terraria;
using Terraria.Audio;

namespace CalamityOverhaul.Content.NPCs.Modifys.Crabulons
{
    /// <summary>骑乘，骑手权威，蟹吸附；见MountPlayer</summary>
    internal class CrabulonMountSystem
    {
        private readonly NPC npc;
        private readonly ModifyCrabulon owner;

        public CrabulonMountSystem(NPC npc, ModifyCrabulon owner) {
            this.npc = npc;
            this.owner = owner;
        }

        public Vector2 GetMountPosition() {
            float yOffset = owner.ai[9] > 0 ? owner.ai[9] : npc.gfxOffY;
            return npc.Top + new Vector2(0, yOffset);
        }

        /// <summary>蟹箱左上，锚骑手中心</summary>
        public static Vector2 GetAttachedBoxPosition(Player rider, NPC npc) {
            return rider.Center - new Vector2(npc.width / 2f, 0f);
        }

        //本端下马不发包
        public void ForceDismount() {
            bool wasMount = owner.Mount || owner.MountACrabulon;
            owner.Mount = false;
            owner.MountACrabulon = false;
            owner.localAI[5] = 0f;
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

        //骑手端发起广播
        public void Dismount() {
            bool isRider = owner.Owner.Alives() && owner.Owner.whoAmI == Main.myPlayer;

            if (isRider) {
                owner.Owner.fullRotation = 0;
                owner.Owner.velocity.Y -= 5;//仅骑手改自身速
            }

            ForceDismount();

            if (isRider) {
                owner.SendNetWork();
            }
        }

        public bool ProcessMountAI() {
            if (owner.DontMount > 0) {
                owner.DontMount--;
            }

            if (!owner.Mount) {
                return HandleMountRequest();
            }

            return HandleMountedAttach();
        }

        private bool HandleMountRequest() {
            if (owner.CrabulonPlayer != null) {
                owner.CrabulonPlayer.MountCrabulon = null;
                owner.CrabulonPlayer.IsMount = false;
            }

            //上马仅骑手发起
            if (ShouldStartMount()) {
                owner.MountACrabulon = true;
                owner.SendNetWork();
                PlayMountSound();
            }

            //吸附动画骑手推进后广播
            if (owner.MountACrabulon && owner.Owner.whoAmI == Main.myPlayer) {
                ProcessMountAnimation();
            }

            return true;
        }

        private bool ShouldStartMount() {
            return owner.Owner.whoAmI == Main.myPlayer
                && owner.SaddleItem.Alives()
                && owner.DontMount <= 0
                && !owner.MountACrabulon
                && !owner.uiCommandOpen//开环右键取消，不上马
                && owner.hoverNPC
                && owner.rightPressed;
        }

        private void PlayMountSound() {
            if (VaultUtils.isServer) {
                return;
            }

            SoundEngine.PlaySound(CWRSound.ToMount with {
                PitchRange = (-0.1f, 0.1f),
                Volume = Main.rand.NextFloat(0.6f, 0.8f)
            }, owner.Owner.Center);
        }

        //仅骑手端
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

        private bool HandleMountedAttach() {
            //卸鞍自动下马
            if (!owner.SaddleItem.Alives()) {
                Dismount();
                return false;
            }

            if (owner.CrabulonPlayer != null) {
                CrabulonPlayer.CloseDuringDash(owner.Owner);
                owner.CrabulonPlayer.MountCrabulon = owner;
                owner.CrabulonPlayer.IsMount = true;
            }

            //蟹无物理，箱体约束见MountPlayer
            npc.noGravity = true;
            npc.noTileCollide = true;

            //velocity吸附锚
            Vector2 targetPos = GetAttachedBoxPosition(owner.Owner, npc);
            npc.velocity = targetPos - npc.position;

            //起身残留帧
            if (owner.ai[9] > 0) {
                owner.ai[9]--;
            }

            UpdateMountAnimation();

            if (CheckDismountInput()) {
                Dismount();
            }

            return false;
        }

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

        private bool CheckDismountInput() {
            return owner.Owner.whoAmI == Main.myPlayer && owner.hoverNPC && owner.rightPressed;
        }
    }
}
