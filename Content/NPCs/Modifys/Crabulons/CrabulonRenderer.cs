using CalamityOverhaul.Content.Items.Tools;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.Modifys.Crabulons
{
    /// <summary>
    /// 菌生蟹渲染系统
    /// </summary>
    internal class CrabulonRenderer
    {
        private readonly NPC npc;
        private readonly ModifyCrabulon owner;
        private Player mountPlayerClone;

        public CrabulonRenderer(NPC npc, ModifyCrabulon owner) {
            this.npc = npc;
            this.owner = owner;
        }

        //绘制前处理
        public bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (owner.Mount && owner.Owner != null) {
                DrawMountedPlayer();
            }

            if (owner.DyeItemID > 0) {
                npc.BeginDyeEffectForWorld(owner.DyeItemID);
            }

            if (owner.ai[9] > 0) {
                npc.gfxOffY = owner.ai[9];
            }

            return true;
        }

        //绘制后处理
        public bool PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (owner.DyeItemID > 0) {
                npc.EndDyeEffectForWorld();
            }

            if (owner.SaddleItem.Alives()) {
                DrawSaddle(spriteBatch, drawColor);
            }

            return true;
        }

        //绘制骑乘的玩家
        private void DrawMountedPlayer() {
            if (owner.CrabulonPlayer == null || !owner.CrabulonPlayer.IsMount) {
                return;
            }

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(
                SpriteSortMode.Deferred,
                BlendState.AlphaBlend,
                SamplerState.PointWrap,
                null,
                Main.Rasterizer,
                null,
                Main.GameViewMatrix.ZoomMatrix
            );

            mountPlayerClone = (Player)owner.Owner.Clone();
            ConfigureMountedPlayer();
            DrawPlayerOnMount();
            UpdateHeldProjectile();
            RestorePlayerRotation();

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(
                SpriteSortMode.Deferred,
                BlendState.AlphaBlend,
                SamplerState.PointWrap,
                null,
                Main.Rasterizer,
                null,
                Main.GameViewMatrix.ZoomMatrix
            );
        }

        //配置骑乘玩家
        private void ConfigureMountedPlayer() {
            mountPlayerClone.fullRotation = npc.rotation + MathHelper.PiOver2;
            mountPlayerClone.fullRotationOrigin = mountPlayerClone.Size / 2f;
            mountPlayerClone.Center = owner.MountSystem.GetMountPosition();

            if (mountPlayerClone.itemAnimation <= 0) {
                mountPlayerClone.headFrame.Y = 0;
                mountPlayerClone.bodyFrame.Y = 0;
            }
        }

        //绘制骑乘玩家
        private void DrawPlayerOnMount() {
            Main.PlayerRenderer.DrawPlayer(
                Main.Camera,
                mountPlayerClone,
                mountPlayerClone.position,
                mountPlayerClone.bodyRotation,
                mountPlayerClone.fullRotationOrigin
            );
        }

        //更新手持弹幕
        private void UpdateHeldProjectile() {
            ModifyCrabulon.mountPlayerHeldProj = mountPlayerClone.heldProj;
            if (ModifyCrabulon.mountPlayerHeldProj.TryGetProjectile(out var heldProj)) {
                Vector2 gfxOffYByPlayer = new(0, -mountPlayerClone.gfxOffY);
                heldProj.Center = mountPlayerClone.Center + ModifyCrabulon.mountPlayerHeldPosOffset + gfxOffYByPlayer;
            }
        }

        //恢复玩家旋转
        private void RestorePlayerRotation() {
            mountPlayerClone.fullRotation = 0;
        }

        //绘制鞍具
        private void DrawSaddle(SpriteBatch spriteBatch, Color drawColor) {
            npc.BeginDyeEffectForWorld(owner.SaddleItem.CWR().DyeItemID);

            Vector2 drawPos = npc.Top + new Vector2(0, 16 + npc.gfxOffY) - Main.screenPosition;
            Vector2 origin = MushroomSaddle.MushroomSaddlePlace.Size() / 2;

            spriteBatch.Draw(
                MushroomSaddle.MushroomSaddlePlace.Value,
                drawPos,
                null,
                drawColor,
                npc.rotation,
                origin,
                1f,
                SpriteEffects.None,
                0
            );

            npc.EndDyeEffectForWorld();
        }
    }
}
