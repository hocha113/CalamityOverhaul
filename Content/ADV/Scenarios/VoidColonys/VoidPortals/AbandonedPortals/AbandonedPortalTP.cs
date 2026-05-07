using CalamityOverhaul.Common;
using InnoVault.TileProcessors;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.ADV.Scenarios.VoidColonys.VoidPortals.AbandonedPortals
{
    internal class AbandonedPortalTP : TileProcessor
    {
        public override int TargetTileID => ModContent.TileType<AbandonedPortalTile>();

        [VaultLoaden("CalamityOverhaul/Assets/ADV/VoidColony/AbandonedPortal")]
        private static Microsoft.Xna.Framework.Graphics.Texture2D AbandonedPortalTex;

        private const float InteractRangePixels = 520f;
        private const string SaveStateKey = "RepairState";
        private const string SaveTimerKey = "RepairTimer";

        public byte RepairStateByte;
        public int RepairTimer;

        private float hoverStrength;
        private float hoverSeed;

        internal enum RepairState : byte
        {
            Broken,
            Repairing,
            Repaired,
        }

        internal RepairState State {
            get => (RepairState)RepairStateByte;
            private set => RepairStateByte = (byte)value;
        }

        internal bool CanTeleport => State == RepairState.Repaired;

        internal float RepairProgress => State switch {
            RepairState.Repairing => MathHelper.Clamp(RepairTimer / (float)AbandonedPortalSession.RepairDurationFrames, 0f, 1f),
            RepairState.Repaired => 1f,
            _ => 0f,
        };

        internal Vector2 WorldCenter => PosInWorld + new Vector2(AbandonedPortalTile.Width * 8f, AbandonedPortalTile.Height * 8f);
        internal Vector2 PortalMouthCenter => PosInWorld + new Vector2(AbandonedPortalTile.Width * 16 * 0.55f, AbandonedPortalTile.Height * 16 * 0.46f);

        public override void SetProperty() {
            hoverSeed = Main.rand.NextFloat() * 100f;
            AbandonedPortalSession.CurrentPortal ??= this;
        }

        public override void OnKill() {
            if (AbandonedPortalSession.CurrentPortal == this) {
                AbandonedPortalSession.Close();
            }
        }

        public override void Update() {
            if (VoidColony.Active) {
                if (AbandonedPortalSession.CurrentPortal == this) {
                    AbandonedPortalSession.Close();
                }
                return;
            }

            if (AbandonedPortalSession.CurrentPortal != this) {
                AbandonedPortalSession.CurrentPortal = this;
            }

            UpdateRepair();
            UpdateLocalHoverAndInteract();
        }

        internal void StartRepair() {
            if (State != RepairState.Broken) return;
            State = RepairState.Repairing;
            RepairTimer = 0;
            SendData();
            SoundEngine.PlaySound(SoundID.Item93 with { Volume = 0.65f, Pitch = -0.25f }, WorldCenter);
        }

        internal void StartTransport(Player player) {
            if (!CanTeleport || player == null || !player.active) return;
            AbandonedPortalSession.RequestClose();
            player.GetModPlayer<VoidTransportPlayer>().StartTransport(PortalMouthCenter, VoidColony.Enter);
        }

        private void UpdateRepair() {
            if (State != RepairState.Repairing) return;

            RepairTimer++;
            if (!Main.dedServ && RepairTimer % 12 == 0) {
                SpawnRepairSpark();
            }

            if (RepairTimer >= AbandonedPortalSession.RepairDurationFrames) {
                RepairTimer = AbandonedPortalSession.RepairDurationFrames;
                State = RepairState.Repaired;
                SendData();
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.75f, Pitch = -0.4f }, WorldCenter);
                }
            }
        }

        private void UpdateLocalHoverAndInteract() {
            if (Main.netMode == NetmodeID.Server) return;

            Player local = Main.LocalPlayer;
            if (local == null || !local.active || local.dead) {
                hoverStrength = 0f;
                return;
            }

            bool panelOpen = AbandonedPortalSession.IsOpen;
            bool canHover = !Main.gameMenu
                && !panelOpen
                && !local.mouseInterface
                && local.Center.DistanceSQ(WorldCenter) < InteractRangePixels * InteractRangePixels;

            Rectangle aabb = new((int)PosInWorld.X, (int)PosInWorld.Y, AbandonedPortalTile.Width * 16, AbandonedPortalTile.Height * 16);
            bool mouseOver = canHover && aabb.Contains((int)Main.MouseWorld.X, (int)Main.MouseWorld.Y);

            float target = mouseOver ? 1f : 0f;
            hoverStrength = MathHelper.Lerp(hoverStrength, target, 0.18f);
            if (Math.Abs(hoverStrength - target) < 0.005f) hoverStrength = target;

            if (!mouseOver) return;

            local.CWR().DontUseItemTime = 2;
            local.mouseInterface = true;
            local.cursorItemIconEnabled = false;
            local.cursorItemIconID = ItemID.None;
        }

        public override void FrontDraw(SpriteBatch spriteBatch) {
            Texture2D tex = AbandonedPortalTex;
            if (tex == null) return;

            Vector2 drawPos = PosInWorld - Main.screenPosition;
            drawPos.Y += 4;
            float repairGlow = RepairProgress;

            Color drawColor = Lighting.GetColor(Position.X + AbandonedPortalTile.Width / 2, Position.Y + AbandonedPortalTile.Height / 2);
            spriteBatch.Draw(tex, drawPos, drawColor);

            if (repairGlow > 0.02f) {
                Color glow = Color.Lerp(new Color(80, 180, 220, 0), new Color(255, 150, 80, 0), repairGlow);
                float pulse = 0.18f + MathF.Sin(Main.GlobalTimeWrappedHourly * 4f) * 0.06f;
                spriteBatch.Draw(tex, drawPos, glow * (pulse + repairGlow * 0.28f));
            }

            if (hoverStrength > 0.01f && !AbandonedPortalSession.IsOpen) {
                DrawHoverOutline(spriteBatch, tex, drawPos, hoverStrength);
            }
        }

        private void DrawHoverOutline(SpriteBatch sb, Texture2D tex, Vector2 drawPos, float strength) {
            Effect shader = EffectLoader.SignalTowerHoverOutline?.Value;
            if (shader == null) return;

            shader.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            shader.Parameters["intensity"]?.SetValue(MathHelper.Clamp(strength, 0f, 1f));
            shader.Parameters["texelSize"]?.SetValue(new Vector2(1f / tex.Width, 1f / tex.Height));
            shader.Parameters["seed"]?.SetValue(hoverSeed);

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, shader, Main.GameViewMatrix.TransformationMatrix);
            sb.Draw(tex, drawPos, Color.White);
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        private void SpawnRepairSpark() {
            Vector2 basePos = PortalMouthCenter + Main.rand.NextVector2Circular(90f, 70f);
            Dust d = Dust.NewDustPerfect(basePos, DustID.Electric, Main.rand.NextVector2Circular(1.6f, 1.6f), 80,
                Color.Lerp(Color.Cyan, Color.OrangeRed, Main.rand.NextFloat()), Main.rand.NextFloat(0.8f, 1.35f));
            d.noGravity = true;
        }

        public override void SendData(ModPacket data) {
            data.Write(RepairStateByte);
            data.Write(RepairTimer);
        }

        public override void ReceiveData(BinaryReader reader, int whoAmI) {
            RepairStateByte = reader.ReadByte();
            RepairTimer = reader.ReadInt32();
        }

        public override void SaveData(TagCompound tag) {
            tag[SaveStateKey] = RepairStateByte;
            tag[SaveTimerKey] = RepairTimer;
        }

        public override void LoadData(TagCompound tag) {
            RepairStateByte = tag.GetByte(SaveStateKey);
            RepairTimer = Math.Clamp(tag.GetInt(SaveTimerKey), 0, AbandonedPortalSession.RepairDurationFrames);
            if (RepairTimer >= AbandonedPortalSession.RepairDurationFrames) {
                State = RepairState.Repaired;
            }
        }
    }
}
