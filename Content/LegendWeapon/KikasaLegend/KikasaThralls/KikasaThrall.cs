using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.Scenarios.OniRainWorlds.KasaOnis;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaThralls
{
    /// <summary>
    /// 伞奴（雨伞鬼奴）静态枢纽：鬼雨领域内被杀死的敌人化水重组为打伞随从，boss 一并收。
    /// 资格谓词是单一真相：各端在死亡观测帧用本地模拟的领域状态自算
    /// （服务器没有领域状态是既定契约，故服务器不参与判定），
    /// 生成只发生在领域主人本机，其余端只演化水。
    /// <para/>
    /// 观测帧只记账：灾厄 boss 多在 CheckDead 里留一条命播死亡演出，真身此刻还在台上，
    /// 化水快照与重组都等它离场那一帧再办
    /// </summary>
    internal static class KikasaThrall
    {
        //==================== 可调基数 ====================

        /// <summary>每位主人的伞奴上限；满员跳过新转化（含正在溶解的）</summary>
        internal const int MaxPerOwner = 5;

        /// <summary>同一主人两次转化的最小间隔帧，防群杀刷屏</summary>
        internal const int ConvertGapFrames = 30;

        /// <summary>转化横向范围：与湖面半宽同源（KikasaLakeSurface.HalfWidth）</summary>
        internal const float ConvertRangeX = 4000f;

        /// <summary>转化纵向范围：距湖面线的容许高差，雨layer的语义高度</summary>
        internal const float ConvertRangeY = 1600f;

        /// <summary>基伤 = clamp(尸体lifeMax × 系数, Min, Max)，逐帧再乘召唤加成</summary>
        internal const float DamagePerLifeMax = 0.10f;
        internal const int DamageMin = 40;
        internal const int DamageMax = 900;

        /// <summary>体型缩放按尸体包围盒对玩家体型归一后钳制</summary>
        internal const float BodyScaleMin = 0.85f;
        internal const float BodyScaleMax = 1.25f;

        /// <summary>
        /// 身量:伞奴贴图按初版 48×72 的 1.6 倍出图(伞奴得比人显眼一圈)。
        /// 贴图自带放大,故绘制不再乘它;与 KasaOni 共用的演出件(污潭)和演出锚点得按它放大。
        /// 碰撞箱与落脚探测一概不变——身位跟着涨会抬高净空要求,洞里就转化不出来了
        /// </summary>
        internal const float BodyBulk = 1.6f;

        //湿墨色板，与 KasaOni 污水族同源；伞奴是"我们的"鬼，点睛用尸斑青
        internal static readonly Color SewageDeep = new(46, 56, 58);
        internal static readonly Color SewageDark = new(30, 38, 41);
        internal static readonly Color CorpseTeal = new(120, 150, 146);
        internal static readonly Color PaleSheen = new(176, 192, 196);

        //每位主人的转化闸门帧（各端本地推同一份，死亡事件全端可见故近似一致）
        private static readonly uint[] nextConvertFrame = new uint[Main.maxPlayers];

        //调试：下一次死亡免检领域（仅本机）
        private static int debugForceFrames;

        //==================== 资格判定（单一真相） ====================

        /// <summary>
        /// 尸体资格：敌对生物即可，boss 一并收——沉溺是主动收魂的另一条线，
        /// 不该把正面打死的 boss 排除在鬼雨之外。城镇/小动物/雕像怪/弹幕型不收
        /// </summary>
        internal static bool IsEligibleCorpse(NPC npc)
            => npc != null && npc.lifeMax > 5
            && !npc.friendly && !npc.townNPC
            && !npc.immortal && !npc.dontTakeDamage && !npc.SpawnedFromStatue
            && !npc.CountsAsACritter && !NPCID.Sets.ProjectileNPC[npc.type]
            && npc.type != NPCID.TargetDummy;

        /// <summary>boss 尸体：含被算作 boss 的分段与从属（月亮领主核心、双子魔眼之流）</summary>
        internal static bool IsBossCorpse(NPC npc)
            => npc != null && (npc.boss || NPCID.Sets.ShouldBeCountedAsBoss[npc.type]);

        /// <summary>该玩家的领域此刻是否处于可收魂的鬼雨稳态</summary>
        internal static bool RainDomainReady(Player player) {
            if (player?.active != true) {
                return false;
            }
            KikasaDomainPlayer domain = player.GetModPlayer<KikasaDomainPlayer>();
            return domain.Phase == KikasaDomainPhase.Open
                && domain.IsRainForm && domain.RainBlend >= 0.9f
                && domain.RiseT >= 0.999f;
        }

        /// <summary>尸点是否落在该玩家的鬼雨领域里（横向湖宽 + 纵向雨层高差）</summary>
        internal static bool InDomainRange(Player player, Vector2 corpseCenter) {
            KikasaDomainPlayer domain = player.GetModPlayer<KikasaDomainPlayer>();
            return Math.Abs(corpseCenter.X - player.Center.X) <= ConvertRangeX
                && Math.Abs(corpseCenter.Y - domain.LakeWorldY) <= ConvertRangeY;
        }

        /// <summary>
        /// 找认领这具尸体的领域主人：鬼雨稳态 + 范围内，多人重叠取最近者（各端同规则）。
        /// 调试免检窗口内直接认本机玩家
        /// </summary>
        internal static bool TryFindClaimingOwner(NPC npc, out Player owner) {
            owner = null;
            if (debugForceFrames > 0 && Main.LocalPlayer?.active == true) {
                owner = Main.LocalPlayer;
                return true;
            }

            float nearest = float.MaxValue;
            for (int i = 0; i < Main.maxPlayers; i++) {
                Player player = Main.player[i];
                if (player?.active != true || !RainDomainReady(player)
                    || !InDomainRange(player, npc.Center)) {
                    continue;
                }
                float distance = Vector2.Distance(player.Center, npc.Center);
                if (distance < nearest) {
                    nearest = distance;
                    owner = player;
                }
            }
            return owner != null;
        }

        /// <summary>场上属于该主人的伞奴数（含溶解中，保守计满）</summary>
        internal static int CountActive(int ownerWho) {
            int count = 0;
            int type = ModContent.ProjectileType<KikasaThrallProj>();
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == type && proj.owner == ownerWho) {
                    count++;
                }
            }
            return count;
        }

        /// <summary>
        /// 转化闸门：上限 + 最小间隔，全端各自推同一份。
        /// boss 两道都不受——一场 boss 的尸体不该被杂兵占着的名额挡回去，满员时另有让位
        /// </summary>
        internal static bool ConvertGateOpen(int ownerWho, bool boss) {
            if (ownerWho < 0 || ownerWho >= Main.maxPlayers) {
                return false;
            }
            if (boss) {
                return true;
            }
            return Main.GameUpdateCount >= nextConvertFrame[ownerWho]
                && CountActive(ownerWho) + CountPending(ownerWho) < MaxPerOwner;
        }

        internal static void MarkConvertGate(int ownerWho) {
            if (ownerWho >= 0 && ownerWho < Main.maxPlayers) {
                nextConvertFrame[ownerWho] = Main.GameUpdateCount + ConvertGapFrames;
            }
        }

        /// <summary>
        /// 满员时化掉最旧的一只给 boss 让位：只点一名，正在溶解的不重复点名。
        /// 只在 owner 本机点——溶解转场由 owner 裁决，其余端收包跟上
        /// </summary>
        private static void EvictOldest(int ownerWho) {
            if (CountActive(ownerWho) < MaxPerOwner) {
                return;
            }
            KikasaThrallProj oldest = null;
            int bestScore = int.MinValue;
            int type = ModContent.ProjectileType<KikasaThrallProj>();
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type != type || proj.owner != ownerWho
                    || proj.ModProjectile is not KikasaThrallProj thrall || !thrall.CanEvict) {
                    continue;
                }
                if (thrall.EvictScore > bestScore) {
                    bestScore = thrall.EvictScore;
                    oldest = thrall;
                }
            }
            oldest?.Evict();
        }

        //==================== 重组点与数值 ====================

        /// <summary>boss 尸点探空后的横向重探：先近后远、左右交替，各端同序故结果可复算</summary>
        private static readonly float[] bossReformOffsets
            = [48f, -48f, 112f, -112f, 208f, -208f, 336f, -336f];

        /// <summary>
        /// 重组点：自尸体脚下向下探可站立地面（探不到=不转化，雨把它冲走了）。
        /// boss 的尸体多半停在半空或压在洞顶下，一列探空就横着挪几格再探，
        /// 全落空退到主人脚边——水认得回主人的路。地形各端一致，判定结果可复算
        /// </summary>
        internal static bool TryPickReformPoint(NPC npc, Player owner, bool boss,
            out Vector2 reformFeet) {
            Vector2 head = new(npc.Center.X, npc.position.Y);
            if (Probe(head, out reformFeet)) {
                return true;
            }
            if (!boss) {
                return false;
            }
            foreach (float offsetX in bossReformOffsets) {
                if (Probe(head + new Vector2(offsetX, 0f), out reformFeet)) {
                    return true;
                }
            }
            return Probe(owner.Center - new Vector2(0f, 80f), out reformFeet);
        }

        private static bool Probe(Vector2 from, out Vector2 feet)
            => KasaOniActor.TryFindStandableGround(from,
                KikasaThrallProj.HitboxWidth, KikasaThrallProj.HitboxHeight, out feet);

        internal static int CorpseBaseDamage(NPC npc)
            => (int)MathHelper.Clamp(npc.lifeMax * DamagePerLifeMax, DamageMin, DamageMax);

        internal static float CorpseBodyScale(NPC npc) {
            float size = MathF.Sqrt(MathF.Max(npc.width * npc.height, 1f));
            return MathHelper.Clamp(size / 42f, BodyScaleMin, BodyScaleMax);
        }

        //==================== 生成（仅 owner 本机） ====================

        /// <summary>
        /// 在尸点生成伞奴弹幕：位置=尸体脚底（聚拢期水团从这里出发），
        /// ai0/1=重组点脚底（入 spawn 包），ai2=体型；基伤走字段+netUpdate 补包
        /// </summary>
        internal static void SpawnThrall(Player owner, NPC corpse, Vector2 reformFeet) {
            if (owner.whoAmI != Main.myPlayer) {
                return;
            }
            int baseDamage = CorpseBaseDamage(corpse);
            int damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(baseDamage);
            Vector2 corpseFeet = new(corpse.Center.X, corpse.Bottom.Y);
            int index = Projectile.NewProjectile(owner.GetSource_Misc("KikasaThrall"),
                corpseFeet, Vector2.Zero, ModContent.ProjectileType<KikasaThrallProj>(),
                damage, 4f, owner.whoAmI,
                reformFeet.X, reformFeet.Y, CorpseBodyScale(corpse));
            if (index >= 0 && index < Main.maxProjectiles
                && Main.projectile[index].ModProjectile is KikasaThrallProj thrall) {
                //spawn 包已带 ai/伤害；基伤字段错过了首包，跟发 netUpdate 补上
                thrall.SetCorpseStats(baseDamage);
            }
        }

        //==================== 待确认尸体 ====================

        /// <summary>
        /// 死亡观测帧到真身离场之间的等待上限。灾厄 boss 常在 CheckDead 里留一条命播死亡演出，
        /// 这段时间尸体还在台上，化水快照得等它退场再拍；久等不退的当这具收不上来
        /// </summary>
        private const int PendingWatchFrames = 1800;

        private struct PendingCorpse
        {
            public int NpcIndex;
            public int NpcType;
            public int OwnerWho;
            public bool Boss;
            public int Wait;
        }

        private static readonly List<PendingCorpse> pendingCorpses = [];

        /// <summary>死亡观测帧受理：先记账，真身离场那一帧再化水重组</summary>
        internal static void Watch(NPC npc, int ownerWho, bool boss)
            => pendingCorpses.Add(new PendingCorpse {
                NpcIndex = npc.whoAmI,
                NpcType = npc.type,
                OwnerWho = ownerWho,
                Boss = boss,
            });

        private static int CountPending(int ownerWho) {
            int count = 0;
            for (int i = 0; i < pendingCorpses.Count; i++) {
                if (pendingCorpses[i].OwnerWho == ownerWho) {
                    count++;
                }
            }
            return count;
        }

        private static void UpdatePending() {
            for (int i = pendingCorpses.Count - 1; i >= 0; i--) {
                PendingCorpse entry = pendingCorpses[i];
                NPC npc = entry.NpcIndex >= 0 && entry.NpcIndex < Main.maxNPCs
                    ? Main.npc[entry.NpcIndex] : null;
                //槽位换了人：这具的数据已不可信，放弃
                if (npc == null || npc.type != entry.NpcType) {
                    pendingCorpses.RemoveAt(i);
                    continue;
                }
                if (npc.active) {
                    entry.Wait++;
                    if (entry.Wait > PendingWatchFrames) {
                        pendingCorpses.RemoveAt(i);
                    }
                    else {
                        pendingCorpses[i] = entry;
                    }
                    continue;
                }
                pendingCorpses.RemoveAt(i);
                ConvertConfirmed(npc, entry);
            }
        }

        /// <summary>
        /// 真身离场帧：此刻的领域状态说了算（死亡演出里主人退了雨，这具就随雨去了）。
        /// NPC 槽位下线后字段仍完整，化水快照与重组点都取自这一帧
        /// </summary>
        private static void ConvertConfirmed(NPC corpse, PendingCorpse entry) {
            Player owner = entry.OwnerWho >= 0 && entry.OwnerWho < Main.maxPlayers
                ? Main.player[entry.OwnerWho] : null;
            if (owner?.active != true) {
                return;
            }
            if (debugForceFrames <= 0 && !RainDomainReady(owner)) {
                return;
            }
            if (!TryPickReformPoint(corpse, owner, entry.Boss, out Vector2 reformFeet)) {
                return;
            }

            KikasaThrallMeltFX.Start(corpse, owner.whoAmI);
            if (owner.whoAmI != Main.myPlayer) {
                return;
            }
            if (entry.Boss) {
                EvictOldest(owner.whoAmI);
            }
            SpawnThrall(owner, corpse, reformFeet);
        }

        //==================== 逐帧与清场 ====================

        internal static void Update() {
            if (debugForceFrames > 0) {
                debugForceFrames--;
            }
            if (!Main.dedServ) {
                UpdatePending();
            }
        }

        internal static void ResetLocal() {
            Array.Clear(nextConvertFrame, 0, nextConvertFrame.Length);
            pendingCorpses.Clear();
            debugForceFrames = 0;
        }

        //==================== 调试入口 ====================

        /// <summary>调试：击杀光标处 NPC 并在短窗内免检领域直接转化（单机验收用）</summary>
        internal static void DebugConvertUnderCursor() {
            NPC hover = FindCursorNPC();
            if (hover == null) {
                Main.NewText("光标下没有可转化的生物", Color.IndianRed);
                return;
            }
            debugForceFrames = 4;
            hover.StrikeInstantKill();
        }

        /// <summary>调试：鼠标处向下吸附地面直接生成一只伞奴（跳过化水，验组装与战斗）</summary>
        internal static void DebugSpawnAt(Vector2 world) {
            if (!KasaOniActor.TryFindStandableGround(world - new Vector2(0f, 60f),
                KikasaThrallProj.HitboxWidth, KikasaThrallProj.HitboxHeight, out Vector2 feet)) {
                Main.NewText("此处探不到可站立地面", Color.IndianRed);
                return;
            }
            Player owner = Main.LocalPlayer;
            int damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(DamageMin * 3);
            int index = Projectile.NewProjectile(owner.GetSource_Misc("KikasaThrall"),
                feet - new Vector2(0f, 6f), Vector2.Zero,
                ModContent.ProjectileType<KikasaThrallProj>(), damage, 4f, owner.whoAmI,
                feet.X, feet.Y, 1f);
            if (index >= 0 && index < Main.maxProjectiles
                && Main.projectile[index].ModProjectile is KikasaThrallProj thrall) {
                //调试生成没有尸体，跳过聚拢直接看成形与战斗
                thrall.SetCorpseStats(DamageMin * 3, skipGather: true);
            }
        }

        private static NPC FindCursorNPC() {
            Vector2 mouse = Main.MouseWorld;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc?.active == true && IsEligibleCorpse(npc)
                    && npc.Hitbox.Contains(mouse.ToPoint())) {
                    return npc;
                }
            }
            return null;
        }
    }
}
