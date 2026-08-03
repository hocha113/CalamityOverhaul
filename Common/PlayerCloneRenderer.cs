using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Common
{
    /// <summary>
    /// Reusable player-clone renderer. Call <see cref="Prepare"/> once for each source player,
    /// then draw any number of frozen poses from that prepared appearance.
    /// </summary>
    internal class PlayerCloneRenderer : ICWRLoader
    {
        private static Player dummy;
        private static bool tintActive;
        private static Color activeTint;

        void ICWRLoader.UnLoadData() {
            dummy = null;
            tintActive = false;
            activeTint = default;
        }

        public static void Prepare(Player owner) {
            if (owner == null || Main.dedServ) {
                return;
            }

            dummy ??= new Player();
            dummy.isDisplayDollOrInanimate = true;
            dummy.CopyVisuals(owner);
            dummy.ResetEffects();
            dummy.skinVariant = owner.skinVariant;
        }

        public static void DrawPrepared(Vector2 position, Color tint, int direction,
            Rectangle bodyFrame, Rectangle legFrame,
            float fullRotation = 0f, Vector2 fullRotationOrigin = default, float gravDir = 1f) {
            if (dummy == null || Main.dedServ) {
                return;
            }

            ApplyPose(position, direction, gravDir, bodyFrame, legFrame,
                fullRotation, fullRotationOrigin);
            tintActive = true;
            activeTint = tint;
            try {
                Main.PlayerRenderer.DrawPlayer(Main.Camera, dummy, dummy.position,
                    dummy.fullRotation, dummy.fullRotationOrigin);
            } finally {
                tintActive = false;
            }
        }

        public static void Draw(Player owner, Vector2 position, Color tint, int direction,
            Rectangle bodyFrame, Rectangle legFrame,
            float fullRotation = 0f, Vector2 fullRotationOrigin = default, float gravDir = 1f) {
            if (owner == null || Main.dedServ) {
                return;
            }

            Prepare(owner);
            DrawPrepared(position, tint, direction, bodyFrame, legFrame,
                fullRotation, fullRotationOrigin, gravDir);
        }

        public static void DrawPreparedNatural(Vector2 position, int direction, float gravDir,
            Rectangle bodyFrame, Rectangle legFrame,
            float fullRotation = 0f, Vector2 fullRotationOrigin = default) {
            if (dummy == null || Main.dedServ) {
                return;
            }

            ApplyPose(position, direction, gravDir, bodyFrame, legFrame,
                fullRotation, fullRotationOrigin);
            Main.PlayerRenderer.DrawPlayer(Main.Camera, dummy, dummy.position,
                dummy.fullRotation, dummy.fullRotationOrigin);
        }

        private static void ApplyPose(Vector2 position, int direction, float gravDir,
            Rectangle bodyFrame, Rectangle legFrame,
            float fullRotation, Vector2 fullRotationOrigin) {
            dummy.position = position;
            dummy.velocity = Vector2.Zero;
            dummy.direction = direction >= 0 ? 1 : -1;
            dummy.gravDir = gravDir >= 0f ? 1f : -1f;
            dummy.bodyFrame = bodyFrame;
            dummy.legFrame = legFrame;
            dummy.fullRotation = fullRotation;
            dummy.fullRotationOrigin = fullRotationOrigin;
            dummy.heldProj = -1;
            dummy.itemAnimation = 0;
            dummy.itemTime = 0;
        }

        internal static bool TryGetActiveTint(Player player, out Color tint) {
            tint = activeTint;
            return tintActive && ReferenceEquals(player, dummy);
        }
    }

    internal sealed class PlayerCloneTintPlayer : ModPlayer
    {
        public override void TransformDrawData(ref PlayerDrawSet drawInfo) {
            if (!PlayerCloneRenderer.TryGetActiveTint(drawInfo.drawPlayer, out Color tint)) {
                return;
            }

            int shader = ContentSamples.CommonlyUsedContentSamples.ColorOnlyShaderIndex;
            for (int i = 0; i < drawInfo.DrawDataCache.Count; i++) {
                DrawData data = drawInfo.DrawDataCache[i];
                data.color = tint;
                data.shader = shader;
                drawInfo.DrawDataCache[i] = data;
            }
        }
    }
}
