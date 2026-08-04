using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.TimeFreezes
{
    internal readonly record struct NetworkNPCIdentity(
        int Index, int Type, ulong Generation)
    {
        internal bool IsValid => Index >= 0 && Index < Main.maxNPCs
            && Type > NPCID.None && Type < NPCLoader.NPCCount
            && Generation != 0;

        internal static NetworkNPCIdentity Capture(NPC npc)
            => TryCapture(npc, out NetworkNPCIdentity identity)
                ? identity
                : default;

        internal static bool TryCapture(NPC npc, out NetworkNPCIdentity identity) {
            identity = default;
            if (npc?.active != true || npc.whoAmI < 0 || npc.whoAmI >= Main.maxNPCs
                || npc.type <= NPCID.None || npc.type >= NPCLoader.NPCCount) {
                return false;
            }

            ulong generation = npc.GetGlobalNPC<TimeFreezeNPC>()
                .EnsureNetworkGeneration(npc);
            if (generation == 0) {
                return false;
            }
            identity = new NetworkNPCIdentity(npc.whoAmI, npc.type, generation);
            return true;
        }

        internal bool TryResolve(out NPC npc) {
            npc = null;
            if (!IsValid || Main.npc == null) {
                return false;
            }

            NPC candidate = Main.npc[Index];
            if (candidate?.active != true || candidate.type != Type
                || candidate.GetGlobalNPC<TimeFreezeNPC>().NetworkGeneration
                    != Generation) {
                return false;
            }
            npc = candidate;
            return true;
        }

        internal bool Write(BinaryWriter writer) {
            if (writer == null) {
                return false;
            }
            bool valid = IsValid;
            writer.Write(valid ? (ushort)Index : ushort.MaxValue);
            writer.Write(valid ? Type : NPCID.None);
            writer.Write(valid ? Generation : 0UL);
            return valid;
        }

        internal static bool TryRead(BinaryReader reader,
            out NetworkNPCIdentity identity) {
            identity = default;
            if (reader == null) {
                return false;
            }

            try {
                identity = new NetworkNPCIdentity(reader.ReadUInt16(),
                    reader.ReadInt32(), reader.ReadUInt64());
                return identity.IsValid;
            }
            catch (EndOfStreamException) {
                identity = default;
                return false;
            }
        }
    }

    internal readonly record struct NetworkProjectileIdentity(
        int Owner, int Identity, int Type)
    {
        internal bool IsValid => Owner >= 0 && Owner <= Main.maxPlayers
            && Identity >= 0
            && Type > ProjectileID.None && Type < ProjectileLoader.ProjectileCount;

        internal static NetworkProjectileIdentity Capture(Projectile projectile)
            => TryCapture(projectile, out NetworkProjectileIdentity identity)
                ? identity
                : default;

        internal static bool TryCapture(Projectile projectile,
            out NetworkProjectileIdentity identity) {
            identity = default;
            if (projectile?.active != true
                || projectile.owner < 0 || projectile.owner > Main.maxPlayers
                || projectile.identity < 0
                || projectile.type <= ProjectileID.None
                || projectile.type >= ProjectileLoader.ProjectileCount) {
                return false;
            }
            identity = new NetworkProjectileIdentity(projectile.owner,
                projectile.identity, projectile.type);
            return true;
        }

        internal bool TryResolve(out Projectile projectile) {
            projectile = null;
            if (!IsValid || Main.projectile == null) {
                return false;
            }

            Projectile match = null;
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile candidate = Main.projectile[i];
                if (candidate?.active != true || candidate.owner != Owner
                    || candidate.identity != Identity || candidate.type != Type) {
                    continue;
                }
                if (match != null) {
                    return false;
                }
                match = candidate;
            }
            projectile = match;
            return projectile != null;
        }

        internal bool Write(BinaryWriter writer) {
            if (writer == null) {
                return false;
            }
            bool valid = IsValid;
            writer.Write(valid ? (byte)Owner : byte.MaxValue);
            writer.Write(valid ? Identity : -1);
            writer.Write(valid ? Type : ProjectileID.None);
            return valid;
        }

        internal static bool TryRead(BinaryReader reader,
            out NetworkProjectileIdentity identity) {
            identity = default;
            if (reader == null) {
                return false;
            }

            try {
                identity = new NetworkProjectileIdentity(reader.ReadByte(),
                    reader.ReadInt32(), reader.ReadInt32());
                return identity.IsValid;
            }
            catch (EndOfStreamException) {
                identity = default;
                return false;
            }
        }
    }
}
