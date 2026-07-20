using CalamityOverhaul.Content.Wraiths.Core;
using InnoVault.Actors;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Wraiths.Runtime
{
    /// <summary>厉鬼通道子操作（<see cref="CWRMessageType.Wraith"/> 的第二字节）</summary>
    internal enum WraithNetOp : byte
    {
        /// <summary>客→服：请求把指定实体逼入/解除死机；服务器全查资格（存活/持载体/判距/时长钳制）</summary>
        HaltRequest,
        /// <summary>客→服：仪式请求；服务器复核（与 owner 预检同源谓词）并消耗实体后回执确认</summary>
        RiteRequest,
        /// <summary>服→发起者：仪式确认（key + 语义），发起者收到才写簿与演出</summary>
        RiteConfirm,
        /// <summary>客→服：反噬挣脱生成请求；服务器校验 Bound+躁动+同键冷却</summary>
        BacklashSpawn,
        /// <summary>客→服：借力世界改动请求；服务器限速+资格校验后执行并向其余客户端转播 AbilityFx</summary>
        AbilityCast,
        /// <summary>服→客：借力世界演出（施放端已本地即时播过，转播时排除它）</summary>
        AbilityFx,
        /// <summary>服→客：规则死亡转发（含专属死因键），受害者本端执行 KillMe</summary>
        RuleKill,
        /// <summary>服→受害者：预警拍开始（演出镜像，倒计时权威在服务器）</summary>
        OmenStart,
        /// <summary>服→受害者：预警拍取消</summary>
        OmenCancel,
        /// <summary>服→客：据点锚位镜像（键+锚位+锚定态），客户端贴饰/路标层据此获得据点知识</summary>
        SiteSync,
    }

    /// <summary>
    /// 厉鬼系统联机通道：挂在 <see cref="CWRNetWork.HandlePacket"/> 链尾。
    /// 服务器权威：所有客→服 op 在服务器复核资格，不信任客户端自报；
    /// 定义身份一律走稳定 Key 字符串（存档锚同源，联机不怕注册序漂移）；
    /// 实体引用带 generation 校验，过期包安全丢弃
    /// </summary>
    internal static class WraithNet
    {
        /// <summary>死机请求的受理判距（宽于仪式半径：这是"接触规则"级交互，不是贴脸仪式）</summary>
        private const float HaltRequestRange = WraithRites.RiteRange * 4f;

        //服务器会话态:每 (玩家,键) 的最近受理帧,借力限速与反噬冷却用;世界切换清零
        private static readonly Dictionary<(int player, string key), long> abilityLastCast = [];
        private static readonly Dictionary<(int player, string key), long> backlashLastSpawn = [];

        /// <summary>清空服务器会话态（<c>WraithDirector.ClearWorld</c> 调用）</summary>
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

        /// <summary>客→服：仪式请求（发起者即发包人，服务器复核后回执）</summary>
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

        /// <summary>服→受害者客户端：规则死亡。reasonKey 为空走定义兜底死因</summary>
        public static void SendRuleKill(int playerWhoAmI, WraithDefinition definition, string reasonKey = null) {
            if (!VaultUtils.isServer || definition == null) {
                return;
            }
            ModPacket packet = NewPacket(WraithNetOp.RuleKill);
            packet.Write(definition.Key);
            packet.Write(reasonKey ?? string.Empty);
            packet.Send(playerWhoAmI);
        }

        /// <summary>服→受害者客户端：预警拍开始（演出镜像）</summary>
        public static void SendOmenStart(int playerWhoAmI, WraithDefinition definition, int ticks) {
            if (!VaultUtils.isServer || definition == null) {
                return;
            }
            ModPacket packet = NewPacket(WraithNetOp.OmenStart);
            packet.Write(definition.Key);
            packet.Write(ticks);
            packet.Send(playerWhoAmI);
        }

        /// <summary>服→受害者客户端：预警拍取消</summary>
        public static void SendOmenCancel(int playerWhoAmI) {
            if (!VaultUtils.isServer) {
                return;
            }
            NewPacket(WraithNetOp.OmenCancel).Send(playerWhoAmI);
        }

        /// <summary>服→客：据点锚位镜像；toWho=-1 广播（锚位变更），指定则单发（玩家入世界补发）</summary>
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
                    //先读满字段再校验,保持流对齐
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

        //====服务器侧受理（全部先读满字段再校验，保持流对齐）====

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
            //资格:活人+随身载体+判距;死机是"以鬼制鬼"级交互,无刀者没有这个动词
            //(随身即可:刀在身上灵异就在场;贴脸持刀是仪式的门槛,不是死机的)。
            //白名单(鬼律第九条执行点):本通道绕过规则状态机,只有明示允许的定义(调试件)受理,
            //正典鬼的死机永远由各自规则在权威端直呼 BeginHalt
            if (requester == null || wraith == null || wraith.Definition == null
                || !wraith.Definition.AllowExternalHaltRequest
                || !WraithVessels.ResolveCarried(requester).IsValid
                || Vector2.DistanceSquared(requester.Center, wraith.Center) > HaltRequestRange * HaltRequestRange) {
                return;
            }
            if (halt) {
                //时长钳制:非法/超限时长回落定义窗口,上限给 4 倍宽松(长演出的主题鬼用)
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
            //复核通过且实体已消耗:回执发起者落簿
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
            //资格:随身载体上该键 Bound 且躁动(服务器自己的 LegendData 副本,经装备槽同步保持新鲜)
            WraithVesselHandle vessel = WraithVessels.ResolveCarried(owner);
            if (!vessel.IsValid || !vessel.Store.TryGet(key, out WraithProgressRecord record)
                || record.State != WraithBindState.Bound
                || record.Mastery >= WraithDefinition.RestlessThreshold) {
                return;
            }
            //服务器侧同键冷却,owner 端冷却失守也刷不出挣脱洪水
            long now = (long)Main.GameUpdateCount;
            if (backlashLastSpawn.TryGetValue((whoAmI, key), out long last)
                && now - last < WraithBacklash.KeyCooldownTicks) {
                return;
            }
            //确认制:生成真的落地(没被全局互斥/资格挡下)才记冷却,竞成失败不白烧
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
            //上线闸:系统未开放期间正典鬼的力在服务器侧一律不受理(调试件豁免)
            if (!WraithDirector.ContentActiveFor(definition)) {
                return;
            }
            Player caster = ResolvePlayer(whoAmI);
            if (caster == null) {
                return;
            }
            //限速:间隔低于半个冷却直接丢弃不回包(owner 冷却正常时永远到不了这条线)
            long now = (long)Main.GameUpdateCount;
            if (abilityLastCast.TryGetValue((whoAmI, key), out long last)
                && now - last < definition.Ability.CooldownTicks / 2) {
                return;
            }
            //资格:手持载体+簿上 Bound+非挣脱期(F4:鬼在外面,力借不出来)
            WraithVesselHandle vessel = WraithVessels.ResolveHeld(caster);
            if (!vessel.IsValid || !vessel.Store.TryGet(key, out WraithProgressRecord record)
                || record.State != WraithBindState.Bound
                || WraithBacklash.AnyEscapedAlive(key, whoAmI)) {
                return;
            }
            abilityLastCast[(whoAmI, key)] = now;
            //效果强度不信客户端自报:钳到服务器副本的驾驭度(微量宽松容忍同步在途)
            mastery = MathHelper.Clamp(mastery, 0f, record.Mastery + 0.02f);

            definition.Ability.ExecuteWorld(caster, aim, MathHelper.Clamp(mastery, 0f, 1f));
            //转播演出,排除施放端(它已本地即时播过)
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
