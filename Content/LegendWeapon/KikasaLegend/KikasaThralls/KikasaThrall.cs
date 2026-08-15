using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.Scenarios.OniRainWorlds.KasaOnis;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaThralls
{
    /// <summary>
    /// 伞奴（雨伞鬼奴）静态枢纽：鬼雨领域内被杀死的普通敌人化水重组为打伞随从。
    /// 资格谓词是单一真相：各端在死亡观测帧用本地模拟的领域状态自算
    /// （服务器没有领域状态是既定契约，故服务器不参与判定），
    /// 生成只发生在领域主人本机，其余端只演化水。
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
        /// 尸体资格：普通敌对生物。Boss 走沉溺役从线，城镇/小动物/雕像怪/弹幕型不收
        /// </summary>
        internal static bool IsEligibleCorpse(NPC npc)
            => npc != null && npc.lifeMax > 5
            && !npc.boss && !npc.friendly && !npc.townNPC
            && !npc.immortal && !npc.dontTakeDamage && !npc.SpawnedFromStatue
            && !npc.CountsAsACritter && !NPCID.Sets.ProjectileNPC[npc.type]
            && npc.type != NPCID.TargetDummy;

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

        /// <summary>转化闸门：上限 + 最小间隔，全端各自推同一份</summary>
        internal static bool ConvertGateOpen(int ownerWho) {
            if (ownerWho < 0 || ownerWho >= Main.maxPlayers) {
                return false;
            }
            return Main.GameUpdateCount >= nextConvertFrame[ownerWho]
                && CountActive(ownerWho) < MaxPerOwner;
        }

        internal static void MarkConvertGate(int ownerWho) {
            if (ownerWho >= 0 && ownerWho < Main.maxPlayers) {
                nextConvertFrame[ownerWho] = Main.GameUpdateCount + ConvertGapFrames;
            }
        }

        //==================== 重组点与数值 ====================

        /// <summary>
        /// 重组点：自尸体脚下向下探可站立地面（探不到=不转化，雨把它冲走了）。
        /// 地形各端一致，判定结果可复算
        /// </summary>
        internal static bool TryPickReformPoint(NPC npc, out Vector2 reformFeet)
            => KasaOniActor.TryFindStandableGround(
                new Vector2(npc.Center.X, npc.position.Y),
                KikasaThrallProj.HitboxWidth, KikasaThrallProj.HitboxHeight, out reformFeet);

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

        //==================== 逐帧与清场 ====================

        internal static void Update() {
            if (debugForceFrames > 0) {
                debugForceFrames--;
            }
        }

        internal static void ResetLocal() {
            Array.Clear(nextConvertFrame, 0, nextConvertFrame.Length);
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
