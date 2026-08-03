using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniDismembers;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniOmokages;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.UI;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.Tutorial
{
    internal sealed class OnikiriTutorialTargetGlobal : GlobalNPC
    {
        private const float RepositionDistance = 620f;
        private const float PreferredOffset = 340f;
        private static uint nextSpawnToken = 1;

        internal int Owner = -1;
        internal int Session;
        internal uint SpawnToken;

        public override bool InstancePerEntity => true;

        private bool Tagged => Owner >= 0 && Owner < Main.maxPlayers
            && Session != 0 && SpawnToken != 0;

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
            => entity.type == NPCID.SantaNK1;

        public override void SetDefaults(NPC npc) {
            Owner = -1;
            Session = 0;
            SpawnToken = 0;
        }

        public override void OnSpawn(NPC npc, IEntitySource source)
            => ReleasePresentation(npc);

        public override bool PreAI(NPC npc) {
            if (!Tagged) {
                return true;
            }

            if (Main.netMode != NetmodeID.MultiplayerClient
                && !OwnerCanKeepTarget(Owner, Session, npc.whoAmI)) {
                DeactivateTarget(npc);
                return false;
            }

            Player owner = Owner >= 0 && Owner < Main.maxPlayers ? Main.player[Owner] : null;
            if (owner?.active == true) {
                Vector2 preferredCenter = GetPreferredCenter(owner);
                if (Vector2.DistanceSquared(npc.Center, owner.Center)
                    > RepositionDistance * RepositionDistance) {
                    npc.Center = preferredCenter;
                    if (Main.netMode != NetmodeID.MultiplayerClient) {
                        npc.netUpdate = true;
                    }
                }
                npc.direction = npc.spriteDirection = owner.Center.X < npc.Center.X ? -1 : 1;
                npc.target = owner.whoAmI;
            }

            ApplyTargetState(npc);
            return false;
        }

        public override bool CheckActive(NPC npc) => !Tagged;

        public override bool CheckDead(NPC npc) {
            if (!Tagged) {
                return true;
            }
            npc.life = Math.Max(npc.lifeMax, 1);
            return false;
        }

        public override bool PreKill(NPC npc) => !Tagged;

        public override bool CanHitPlayer(NPC npc, Player target, ref int cooldownSlot)
            => !Tagged;

        public override bool CanHitNPC(NPC npc, NPC target) => !Tagged;

        public override bool CanBeHitByNPC(NPC npc, NPC attacker) => !Tagged;

        public override bool? CanBeHitByItem(NPC npc, Player player, Item item)
            => Tagged ? false : null;

        public override bool? CanBeHitByProjectile(NPC npc, Projectile projectile)
            => Tagged ? false : null;

        public override bool PreDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (!Tagged || Main.dedServ || Owner != Main.myPlayer
                || !OnikiriTutorialFlow.TryGetRequiredDismemberTarget(Main.LocalPlayer,
                    out NPC requiredTarget) || requiredTarget != npc) {
                return true;
            }

            Main.instance.LoadNPC(npc.type);
            Texture2D texture = TextureAssets.Npc[npc.type].Value;
            Rectangle frame = npc.frame;
            Vector2 drawPosition = npc.Center - screenPos + new Vector2(0f, npc.gfxOffY);
            Vector2 origin = frame.Size() * 0.5f;
            SpriteEffects effects = npc.spriteDirection == -1
                ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
            float pulse = 0.5f + 0.5f * MathF.Sin(Main.GlobalTimeWrappedHourly * 5f);
            float radius = MathHelper.Lerp(2.5f, 4f, pulse);
            Color outline = Color.Lerp(OnikiriUITheme.Seal, OnikiriUITheme.GhostFire,
                pulse * 0.35f) * (0.58f * npc.Opacity);
            for (int i = 0; i < 8; i++) {
                Vector2 offset = (MathHelper.TwoPi * i / 8f).ToRotationVector2() * radius;
                spriteBatch.Draw(texture, drawPosition + offset, frame, outline, npc.rotation,
                    origin, npc.scale, effects, 0f);
            }
            return true;
        }

        public override void SendExtraAI(NPC npc, BitWriter bitWriter, BinaryWriter binaryWriter) {
            bitWriter.WriteBit(Tagged);
            if (!Tagged) {
                return;
            }
            binaryWriter.Write((byte)Owner);
            binaryWriter.Write(Session);
            binaryWriter.Write(SpawnToken);
        }

        public override void ReceiveExtraAI(NPC npc, BitReader bitReader, BinaryReader binaryReader) {
            if (!bitReader.ReadBit()) {
                if (Tagged) {
                    ReleasePresentation(npc);
                }
                Owner = -1;
                Session = 0;
                SpawnToken = 0;
                return;
            }
            Owner = binaryReader.ReadByte();
            Session = binaryReader.ReadInt32();
            SpawnToken = binaryReader.ReadUInt32();
        }

        internal static NPC SpawnTarget(Player owner, int session) {
            if (Main.netMode == NetmodeID.MultiplayerClient || owner?.active != true
                || owner.dead || session == 0) {
                return null;
            }

            Vector2 center = GetPreferredCenter(owner);
            int npcIndex = NPC.NewNPC(owner.GetSource_Misc("CWR_OnikiriTutorialTarget"),
                (int)center.X, (int)center.Y, NPCID.SantaNK1);
            if (npcIndex < 0 || npcIndex >= Main.maxNPCs) {
                return null;
            }

            NPC npc = Main.npc[npcIndex];
            if (!npc.active || npc.type != NPCID.SantaNK1
                || !npc.TryGetGlobalNPC(out OnikiriTutorialTargetGlobal tag)) {
                return null;
            }

            ReleasePresentation(npc);
            tag.Owner = owner.whoAmI;
            tag.Session = session;
            tag.SpawnToken = AllocateSpawnToken();
            npc.Center = center;
            ApplyTargetState(npc);
            npc.netUpdate = true;
            if (Main.netMode == NetmodeID.Server) {
                NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, npc.whoAmI);
            }
            return npc;
        }

        internal static NPC FindTarget(int owner, int session) {
            if (owner < 0 || owner >= Main.maxPlayers || session == 0) {
                return null;
            }
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (IsTutorialTarget(npc, out int targetOwner, out int targetSession)
                    && targetOwner == owner && targetSession == session) {
                    return npc;
                }
            }
            return null;
        }

        internal static bool IsTutorialTarget(NPC npc, out int owner, out int session) {
            return TryGetTutorialIdentity(npc, out owner, out session, out _);
        }

        internal static bool TryGetTutorialIdentity(NPC npc, out int owner, out int session,
            out uint spawnToken) {
            owner = -1;
            session = 0;
            spawnToken = 0;
            if (npc?.active != true || npc.type != NPCID.SantaNK1
                || !npc.TryGetGlobalNPC(out OnikiriTutorialTargetGlobal tag) || !tag.Tagged) {
                return false;
            }
            owner = tag.Owner;
            session = tag.Session;
            spawnToken = tag.SpawnToken;
            return true;
        }

        internal static bool CanPlayerDismember(NPC npc, Player player) {
            if (!IsTutorialTarget(npc, out int owner, out int session)) {
                return true;
            }
            if (player?.active != true || player.whoAmI != owner) {
                return false;
            }
            OnikiriTutorialNetPlayer state = player.GetModPlayer<OnikiriTutorialNetPlayer>();
            return (state.ConfirmedSession == session || state.ServerSession == session)
                && OnikiriTutorialFlow.TryGetRequiredDismemberTarget(player, out NPC requiredTarget)
                && requiredTarget == npc;
        }

        internal static void ReleaseTargets(int owner, int? session, bool notifyOwner = true) {
            if (owner < 0 || owner >= Main.maxPlayers) {
                return;
            }
            for (int i = Main.maxNPCs - 1; i >= 0; i--) {
                NPC npc = Main.npc[i];
                if (!IsTutorialTarget(npc, out int targetOwner, out int targetSession)
                    || targetOwner != owner || (session.HasValue && targetSession != session.Value)) {
                    continue;
                }
                DeactivateTarget(npc, notifyOwner);
            }
        }

        internal static bool ReleaseTarget(int owner, int session, int npcIndex) {
            if (owner < 0 || owner >= Main.maxPlayers || session <= 0
                || npcIndex < 0 || npcIndex >= Main.maxNPCs) {
                return false;
            }
            NPC npc = Main.npc[npcIndex];
            if (!IsTutorialTarget(npc, out int targetOwner, out int targetSession)
                || targetOwner != owner || targetSession != session) {
                return false;
            }
            DeactivateTarget(npc);
            return true;
        }

        internal static void ClearAll() {
            for (int i = Main.maxNPCs - 1; i >= 0; i--) {
                NPC npc = Main.npc[i];
                if (IsTutorialTarget(npc, out _, out _)) {
                    DeactivateTarget(npc, notifyOwner: false, sync: false);
                }
            }
        }

        internal static void ReleasePresentation(NPC npc) {
            if (npc == null) {
                return;
            }
            OniDismember.ClearTarget(npc);
            OniOmokage.ReleaseTarget(npc);
        }

        private static void ApplyTargetState(NPC npc) {
            npc.velocity = Vector2.Zero;
            npc.oldPosition = npc.position;
            npc.rotation = 0f;
            npc.damage = 0;
            npc.defDamage = 0;
            npc.life = Math.Max(npc.lifeMax, 1);
            npc.friendly = false;
            npc.dontTakeDamage = true;
            npc.immortal = true;
            npc.chaseable = false;
            npc.npcSlots = 0f;
            npc.noGravity = true;
            npc.noTileCollide = true;
            npc.netAlways = true;
            npc.timeLeft = Math.Max(npc.timeLeft, 60);
        }

        private static bool OwnerCanKeepTarget(int owner, int session, int npcIndex) {
            if (owner < 0 || owner >= Main.maxPlayers) {
                return false;
            }
            Player player = Main.player[owner];
            if (player?.active != true || player.dead || !player.HasItem(OnikiriOverride.ID)) {
                return false;
            }
            OnikiriTutorialNetPlayer state = player.GetModPlayer<OnikiriTutorialNetPlayer>();
            return state.ServerSession == session && state.ServerTargetIndex == npcIndex;
        }

        private static Vector2 GetPreferredCenter(Player owner) {
            int side = owner.direction == 0 ? 1 : owner.direction;
            return owner.Center + new Vector2(side * PreferredOffset, 0f);
        }

        private static uint AllocateSpawnToken() {
            uint token = nextSpawnToken++;
            if (token == 0) {
                token = nextSpawnToken++;
            }
            return token;
        }

        private static void DeactivateTarget(NPC npc, bool notifyOwner = true, bool sync = true) {
            if (!IsTutorialTarget(npc, out int owner, out int session)) {
                return;
            }

            int npcIndex = npc.whoAmI;
            ReleasePresentation(npc);
            OnikiriTutorialTargetGlobal tag = npc.GetGlobalNPC<OnikiriTutorialTargetGlobal>();
            tag.Owner = -1;
            tag.Session = 0;
            tag.SpawnToken = 0;
            npc.active = false;
            npc.netUpdate = true;

            if (sync && Main.netMode == NetmodeID.Server) {
                NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, npcIndex);
            }
            if (notifyOwner) {
                OnikiriTutorialNet.NotifyTargetReleased(owner, session, npcIndex);
            }
        }
    }

    internal sealed class OnikiriTutorialTargetSystem : ModSystem
    {
        public override void OnWorldUnload() => OnikiriTutorialTargetGlobal.ClearAll();

        public override void ClearWorld() => OnikiriTutorialTargetGlobal.ClearAll();
    }
}
