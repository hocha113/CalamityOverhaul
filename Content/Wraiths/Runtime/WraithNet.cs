using CalamityOverhaul.Content.Wraiths.Core;
using InnoVault.Actors;
using Microsoft.Xna.Framework;
using System.IO;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Wraiths.Runtime
{
    /// <summary>厉鬼通道子操作（<see cref="CWRMessageType.Wraith"/> 的第二字节）</summary>
    internal enum WraithNetOp : byte
    {
        /// <summary>客→服：请求把指定实体逼入/解除死机（调试与规则玩法共用）</summary>
        HaltRequest,
        /// <summary>客→服：仪式消耗死机实体</summary>
        RiteConsume,
        /// <summary>客→服：反噬挣脱生成（owner 端判定已过，服务器落实）</summary>
        BacklashSpawn,
        /// <summary>客→服：借力世界改动请求；服务器执行后向其余客户端转播 AbilityFx</summary>
        AbilityCast,
        /// <summary>服→客：借力世界演出（施放端已本地即时播过，转播时排除它）</summary>
        AbilityFx,
        /// <summary>服→客：规则死亡转发，受害者本端执行 KillMe</summary>
        RuleKill,
        /// <summary>客→服：手工落锚据点（调试/剧情脚本）</summary>
        PlantSite,
    }

    /// <summary>
    /// 厉鬼系统联机通道：挂在 <see cref="CWRNetWork.HandlePacket"/> 链尾。
    /// 定义身份一律走稳定 Key 字符串（存档锚同源，联机不怕注册序漂移）；
    /// 实体引用带 generation 校验，过期包安全丢弃
    /// </summary>
    internal static class WraithNet
    {
        private static ModPacket NewPacket(WraithNetOp op) {
            ModPacket packet = CWRMod.Instance.GetPacket();
            packet.Write((byte)CWRMessageType.Wraith);
            packet.Write((byte)op);
            return packet;
        }

        //====发送====

        /// <summary>客→服：死机开/关请求。durationTicks&lt;=0 取定义默认窗口</summary>
        public static void SendHaltRequest(WraithActor wraith, bool halt, int durationTicks = -1) {
            if (!VaultUtils.isClient || wraith == null) {
                return;
            }
            ModPacket packet = NewPacket(WraithNetOp.HaltRequest);
            packet.Write((ushort)wraith.WhoAmI);
            packet.Write(wraith.Generation);
            packet.Write(halt);
            packet.Write(durationTicks);
            packet.Send();
        }

        public static void SendRiteConsume(WraithActor wraith) {
            if (!VaultUtils.isClient || wraith == null) {
                return;
            }
            ModPacket packet = NewPacket(WraithNetOp.RiteConsume);
            packet.Write((ushort)wraith.WhoAmI);
            packet.Write(wraith.Generation);
            packet.Send();
        }

        public static void SendBacklashSpawn(WraithDefinition definition) {
            if (!VaultUtils.isClient || definition == null) {
                return;
            }
            ModPacket packet = NewPacket(WraithNetOp.BacklashSpawn);
            packet.Write(definition.Key);
            packet.Send();
        }

        public static void SendAbilityCast(WraithDefinition definition, Vector2 aim, float mastery) {
            if (!VaultUtils.isClient || definition == null) {
                return;
            }
            ModPacket packet = NewPacket(WraithNetOp.AbilityCast);
            packet.Write(definition.Key);
            packet.WriteVector2(aim);
            packet.Write(mastery);
            packet.Send();
        }

        /// <summary>服→受害者客户端：规则死亡</summary>
        public static void SendRuleKill(int playerWhoAmI, WraithDefinition definition) {
            if (!VaultUtils.isServer || definition == null) {
                return;
            }
            ModPacket packet = NewPacket(WraithNetOp.RuleKill);
            packet.Write(definition.Key);
            packet.Send(playerWhoAmI);
        }

        public static void SendPlantSite(WraithDefinition definition, Vector2 center) {
            if (!VaultUtils.isClient || definition == null) {
                return;
            }
            ModPacket packet = NewPacket(WraithNetOp.PlantSite);
            packet.Write(definition.Key);
            packet.WriteVector2(center);
            packet.Send();
        }

        //====接收====

        public static void NetHandle(CWRMessageType type, BinaryReader reader, int whoAmI) {
            if (type != CWRMessageType.Wraith) {
                return;
            }
            WraithNetOp op = (WraithNetOp)reader.ReadByte();
            switch (op) {
                case WraithNetOp.HaltRequest: {
                    int slot = reader.ReadUInt16();
                    ushort generation = reader.ReadUInt16();
                    bool halt = reader.ReadBoolean();
                    int duration = reader.ReadInt32();
                    if (!VaultUtils.isServer) {
                        break;
                    }
                    if (ResolveActor(slot, generation) is WraithActor wraith) {
                        if (halt) {
                            wraith.BeginHalt(duration);
                        }
                        else {
                            wraith.EndHalt();
                        }
                    }
                    break;
                }
                case WraithNetOp.RiteConsume: {
                    int slot = reader.ReadUInt16();
                    ushort generation = reader.ReadUInt16();
                    if (!VaultUtils.isServer) {
                        break;
                    }
                    WraithRites.ConsumeHalted(ResolveActor(slot, generation));
                    break;
                }
                case WraithNetOp.BacklashSpawn: {
                    string key = reader.ReadString();
                    if (!VaultUtils.isServer || !WraithRegistry.TryGet(key, out WraithDefinition definition)) {
                        break;
                    }
                    WraithBacklash.SpawnEscaped(whoAmI, definition);
                    break;
                }
                case WraithNetOp.AbilityCast: {
                    string key = reader.ReadString();
                    Vector2 aim = reader.ReadVector2();
                    float mastery = reader.ReadSingle();
                    if (!VaultUtils.isServer || !WraithRegistry.TryGet(key, out WraithDefinition definition)
                        || definition.Ability == null || whoAmI < 0 || whoAmI >= Main.maxPlayers) {
                        break;
                    }
                    Player caster = Main.player[whoAmI];
                    if (caster == null || !caster.active) {
                        break;
                    }
                    definition.Ability.ExecuteWorld(caster, aim, MathHelper.Clamp(mastery, 0f, 1f));
                    //转播演出,排除施放端(它已本地即时播过)
                    ModPacket packet = NewPacket(WraithNetOp.AbilityFx);
                    packet.Write(key);
                    packet.Write((byte)whoAmI);
                    packet.WriteVector2(aim);
                    packet.Send(-1, whoAmI);
                    break;
                }
                case WraithNetOp.AbilityFx: {
                    string key = reader.ReadString();
                    int caster = reader.ReadByte();
                    Vector2 aim = reader.ReadVector2();
                    if (!VaultUtils.isClient || !WraithRegistry.TryGet(key, out WraithDefinition definition)
                        || definition.Ability == null || caster < 0 || caster >= Main.maxPlayers) {
                        break;
                    }
                    Player player = Main.player[caster];
                    if (player != null && player.active) {
                        definition.Ability.PlayWorldFx(player, aim);
                    }
                    break;
                }
                case WraithNetOp.RuleKill: {
                    string key = reader.ReadString();
                    if (!VaultUtils.isClient || !WraithRegistry.TryGet(key, out WraithDefinition definition)) {
                        break;
                    }
                    WraithLethality.KillLocal(Main.LocalPlayer, definition);
                    break;
                }
                case WraithNetOp.PlantSite: {
                    string key = reader.ReadString();
                    Vector2 center = reader.ReadVector2();
                    if (!VaultUtils.isServer || !WraithRegistry.TryGet(key, out _)) {
                        break;
                    }
                    WraithSiteSystem.Plant(key, center);
                    break;
                }
            }
        }

        /// <summary>槽位+代校验解析厉鬼实体，无效/过期返回 null</summary>
        private static WraithActor ResolveActor(int slot, ushort generation) {
            if (slot < 0 || slot >= ActorLoader.MaxActorCount) {
                return null;
            }
            Actor actor = ActorLoader.Actors[slot];
            return actor is WraithActor wraith && wraith.Active && wraith.Generation == generation ? wraith : null;
        }
    }
}
