using CalamityOverhaul.Content.Wraiths.Core;
using InnoVault.Actors;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Wraiths.Runtime
{
    /// <summary>通道子 op，Wraith 消息第二字节</summary>
    internal enum WraithNetOp : byte
    {
        /// <summary>客→服，死机开/关</summary>
        HaltRequest,
        /// <summary>客→服，仪式请求</summary>
        RiteRequest,
        /// <summary>服→发起者，仪式确认</summary>
        RiteConfirm,
        /// <summary>客→服，反噬生成</summary>
        BacklashSpawn,
        /// <summary>客→服，借力世界改动</summary>
        AbilityCast,
        /// <summary>服→客，借力演出</summary>
        AbilityFx,
        /// <summary>服→客，规则死亡转发</summary>
        RuleKill,
        /// <summary>服→受害者，预警起拍</summary>
        OmenStart,
        /// <summary>服→受害者，预警撤拍</summary>
        OmenCancel,
        /// <summary>服→客，据点锚位镜像</summary>
        SiteSync,
    }

    /// <summary>
    /// 联机通道，挂 CWRNetWork 链尾。客→服一律服复核；身份走 Key；实体带 generation
    /// </summary>
    internal static class WraithNet
    {
        /// <summary>死机请求判距，宽于仪式半径</summary>
        private const float HaltRequestRange = WraithRites.RiteRange * 4f;

        //服会话态，借力限速与反噬冷却；换世界清零
        private static readonly Dictionary<(int player, string key), long> abilityLastCast = [];
        private static readonly Dictionary<(int player, string key), long> backlashLastSpawn = [];

        /// <summary>清服会话态</summary>
        internal static void ClearSession() {
            abilityLastCast.Clear();
            backlashLastSpawn.Clear();
        }

        private static ModPacket NewPacket(WraithNetOp op) {
            ModPacket packet = CWRMod.Instance.GetPacket();
            packet.Write((byte)CWRMessageType.Wraith);
            packet.Write((byte)op);
            return packet;
        }

        //====发送====

        /// <summary>客→服，死机开/关；duration≤0 取定义默认</summary>
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

        /// <summary>客→服，仪式请求</summary>
        public static void SendRiteRequest(WraithActor wraith) {
            if (!VaultUtils.isClient || wraith == null) {
                return;
            }
            ModPacket packet = NewPacket(WraithNetOp.RiteRequest);
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

        /// <summary>服→受害者，规则死亡；reasonKey 空走兜底</summary>
        public static void SendRuleKill(int playerWhoAmI, WraithDefinition definition, string reasonKey = null) {
            if (!VaultUtils.isServer || definition == null) {
                return;
            }
            ModPacket packet = NewPacket(WraithNetOp.RuleKill);
            packet.Write(definition.Key);
            packet.Write(reasonKey ?? string.Empty);
            packet.Send(playerWhoAmI);
        }

        /// <summary>服→受害者，预警起拍</summary>
        public static void SendOmenStart(int playerWhoAmI, WraithDefinition definition, int ticks) {
            if (!VaultUtils.isServer || definition == null) {
                return;
            }
            ModPacket packet = NewPacket(WraithNetOp.OmenStart);
            packet.Write(definition.Key);
            packet.Write(ticks);
            packet.Send(playerWhoAmI);
        }

        /// <summary>服→受害者，预警撤拍</summary>
        public static void SendOmenCancel(int playerWhoAmI) {
            if (!VaultUtils.isServer) {
                return;
            }
            NewPacket(WraithNetOp.OmenCancel).Send(playerWhoAmI);
        }

        /// <summary>服→客，据点锚位；toWho=-1 广播</summary>
        public static void SendSiteSync(string key, Vector2 anchor, bool anchored, int toWho = -1) {
            if (!VaultUtils.isServer || string.IsNullOrEmpty(key)) {
                return;
            }
            ModPacket packet = NewPacket(WraithNetOp.SiteSync);
            packet.Write(key);
            packet.WriteVector2(anchor);
            packet.Write(anchored);
            packet.Send(toWho);
        }

        //====接收====

        public static void NetHandle(CWRMessageType type, BinaryReader reader, int whoAmI) {
            if (type != CWRMessageType.Wraith) {
                return;
            }
            WraithNetOp op = (WraithNetOp)reader.ReadByte();
            switch (op) {
                case WraithNetOp.HaltRequest:
                    HandleHaltRequest(reader, whoAmI);
                    break;
                case WraithNetOp.RiteRequest:
                    HandleRiteRequest(reader, whoAmI);
                    break;
                case WraithNetOp.RiteConfirm: {
                    string key = reader.ReadString();
                    WraithRiteKind kind = (WraithRiteKind)reader.ReadByte();
                    if (!VaultUtils.isClient || kind > WraithRiteKind.Resubdue) {
                        break;
                    }
                    WraithRites.ApplyConfirmed(Main.LocalPlayer, key, kind);
                    break;
                }
                case WraithNetOp.BacklashSpawn:
                    HandleBacklashSpawn(reader, whoAmI);
                    break;
                case WraithNetOp.AbilityCast:
                    HandleAbilityCast(reader, whoAmI);
                    break;
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
                    string reasonKey = reader.ReadString();
                    if (!VaultUtils.isClient || !WraithRegistry.TryGet(key, out WraithDefinition definition)) {
                        break;
                    }
                    WraithLethality.KillLocal(Main.LocalPlayer, definition, WraithLethality.ResolveReason(definition, reasonKey));
                    break;
                }
                case WraithNetOp.OmenStart: {
                    string key = reader.ReadString();
                    int ticks = reader.ReadInt32();
                    if (!VaultUtils.isClient || ticks <= 0 || !WraithRegistry.TryGet(key, out WraithDefinition definition)) {
                        break;
                    }
                    Main.LocalPlayer.GetModPlayer<WraithPlayer>().BeginOmenMirror(definition, ticks);
                    break;
                }
                case WraithNetOp.OmenCancel: {
                    if (VaultUtils.isClient) {
                        Main.LocalPlayer.GetModPlayer<WraithPlayer>().ClearOmenMirror();
                    }
                    break;
                }
                case WraithNetOp.SiteSync: {
                    //先读满再校验，保流对齐
                    string key = reader.ReadString();
                    Vector2 anchor = reader.ReadVector2();
                    bool anchored = reader.ReadBoolean();
                    if (!VaultUtils.isClient || string.IsNullOrEmpty(key)) {
                        break;
                    }
                    WraithSiteSystem.ApplyClientMirror(key, anchor, anchored);
                    break;
                }
            }
        }

        //====服务器受理====

        private static void HandleHaltRequest(BinaryReader reader, int whoAmI) {
            int slot = reader.ReadUInt16();
            ushort generation = reader.ReadUInt16();
            bool halt = reader.ReadBoolean();
            int duration = reader.ReadInt32();
            if (!VaultUtils.isServer) {
                return;
            }
            Player requester = ResolvePlayer(whoAmI);
            WraithActor wraith = ResolveActor(slot, generation);
            //活人+随身载体+判距；仅 AllowExternalHaltRequest 受理
            if (requester == null || wraith == null || wraith.Definition == null
                || !wraith.Definition.AllowExternalHaltRequest
                || !WraithVessels.ResolveCarried(requester).IsValid
                || Vector2.DistanceSquared(requester.Center, wraith.Center) > HaltRequestRange * HaltRequestRange) {
                return;
            }
            if (halt) {
                //时长钳制，非法/超限回落定义窗口
                int windowLimit = System.Math.Max(wraith.Definition.HaltWindowTicks, 1) * 4;
                if (duration <= 0 || duration > windowLimit) {
                    duration = -1;
                }
                wraith.BeginHalt(duration);
            }
            else {
                wraith.EndHalt();
            }
        }

        private static void HandleRiteRequest(BinaryReader reader, int whoAmI) {
            int slot = reader.ReadUInt16();
            ushort generation = reader.ReadUInt16();
            if (!VaultUtils.isServer) {
                return;
            }
            Player requester = ResolvePlayer(whoAmI);
            WraithActor target = ResolveActor(slot, generation);
            if (requester == null || target == null
                || !WraithRites.TryServerPerform(requester, target, out WraithRiteKind kind)) {
                return;
            }
            //复核通过，回执落簿
            ModPacket packet = NewPacket(WraithNetOp.RiteConfirm);
            packet.Write(target.Definition.Key);
            packet.Write((byte)kind);
            packet.Send(whoAmI);
        }

        private static void HandleBacklashSpawn(BinaryReader reader, int whoAmI) {
            string key = reader.ReadString();
            if (!VaultUtils.isServer || !WraithRegistry.TryGet(key, out WraithDefinition definition)) {
                return;
            }
            Player owner = ResolvePlayer(whoAmI);
            if (owner == null) {
                return;
            }
            //随身 Bound 且躁动
            WraithVesselHandle vessel = WraithVessels.ResolveCarried(owner);
            if (!vessel.IsValid || !vessel.Store.TryGet(key, out WraithProgressRecord record)
                || record.State != WraithBindState.Bound
                || record.Mastery >= WraithDefinition.RestlessThreshold) {
                return;
            }
            //服侧同键冷却
            long now = (long)Main.GameUpdateCount;
            if (backlashLastSpawn.TryGetValue((whoAmI, key), out long last)
                && now - last < WraithBacklash.KeyCooldownTicks) {
                return;
            }
            //生成落地才记冷却
            if (WraithBacklash.SpawnEscaped(whoAmI, definition)) {
                backlashLastSpawn[(whoAmI, key)] = now;
            }
        }

        private static void HandleAbilityCast(BinaryReader reader, int whoAmI) {
            string key = reader.ReadString();
            Vector2 aim = reader.ReadVector2();
            float mastery = reader.ReadSingle();
            if (!VaultUtils.isServer || !WraithRegistry.TryGet(key, out WraithDefinition definition)
                || definition.Ability == null) {
                return;
            }
            //上线闸
            if (!WraithDirector.ContentActiveFor(definition)) {
                return;
            }
            Player caster = ResolvePlayer(whoAmI);
            if (caster == null) {
                return;
            }
            //限速，低于半冷却丢弃
            long now = (long)Main.GameUpdateCount;
            if (abilityLastCast.TryGetValue((whoAmI, key), out long last)
                && now - last < definition.Ability.CooldownTicks / 2) {
                return;
            }
            //手持+Bound+非挣脱期
            WraithVesselHandle vessel = WraithVessels.ResolveHeld(caster);
            if (!vessel.IsValid || !vessel.Store.TryGet(key, out WraithProgressRecord record)
                || record.State != WraithBindState.Bound
                || WraithBacklash.AnyEscapedAlive(key, whoAmI)) {
                return;
            }
            abilityLastCast[(whoAmI, key)] = now;
            //强度不信客户端，钳到服副本驾驭度
            mastery = MathHelper.Clamp(mastery, 0f, record.Mastery + 0.02f);

            definition.Ability.ExecuteWorld(caster, aim, MathHelper.Clamp(mastery, 0f, 1f));
            //转播演出，排除施放端
            ModPacket packet = NewPacket(WraithNetOp.AbilityFx);
            packet.Write(key);
            packet.Write((byte)whoAmI);
            packet.WriteVector2(aim);
            packet.Send(-1, whoAmI);
        }

        //====解析====

        private static Player ResolvePlayer(int whoAmI) {
            if (whoAmI < 0 || whoAmI >= Main.maxPlayers) {
                return null;
            }
            Player player = Main.player[whoAmI];
            return player != null && player.active && !player.dead ? player : null;
        }

        /// <summary>槽位+代校验，无效/过期返回 null</summary>
        private static WraithActor ResolveActor(int slot, ushort generation) {
            if (slot < 0 || slot >= ActorLoader.MaxActorCount) {
                return null;
            }
            Actor actor = ActorLoader.Actors[slot];
            return actor is WraithActor wraith && wraith.Active && wraith.Generation == generation ? wraith : null;
        }
    }
}
