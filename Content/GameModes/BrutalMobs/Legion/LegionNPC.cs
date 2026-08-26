using CalamityOverhaul.Content.GameModes.BrutalMobs.Legion.Projectiles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Legion
{
    /// <summary>
    /// 残酷模式军团机制层（血月 + 哥布林军队），叠加在原版 AI 之上，不接管、不动数值属性。
    /// 军官光环：新郎/新娘/哥布林法师/哥布林召唤师给半径内同军团现役怪挂原版铁皮 buff
    /// （NPC 侧无原生数值效果，纯作已同步的军阵状态载体），受庇护者减伤、战士盾更硬、
    /// 弓手解锁齐射——斩首军官即全部剥除，制造战术选择。
    /// 盾墙：哥布林战士接敌时正面举盾减伤，绕后/越顶/趁跳跃是正解，姿态可见（盾牌实绘）。
    /// 潮汐节拍：血月怪按世界时钟分两波错拍推进，波间有全体喘息窗；军官不随潮（稳定锚点=斩首窗口）。
    /// 联机：运动调制两端确定性同跑（输入均为已同步原语 Main.time / whoAmI / buff，镜像
    /// <see cref="GameModeNPC.PostAI"/> 的零网络模式）；buff 授予与弹幕生成只在权威端
    /// </summary>
    internal class LegionNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        //——军官光环——
        /// <summary>光环半径</summary>
        private const float OfficerAuraRadius = 520f;
        /// <summary>权威端授环间隔（帧），军官间按 whoAmI 错帧摊开扫描开销</summary>
        private const int AuraRefreshInterval = 30;
        /// <summary>单次授予的 buff 时长；军官死亡后至多此帧数内光环自然剥落</summary>
        private const int AuraBuffTime = 50;
        /// <summary>光环标记 buff：原版铁皮（原生同步，NPC 侧无任何原版数值挂钩）</summary>
        internal const int WardBuff = BuffID.Ironskin;
        /// <summary>受庇护者的减伤比例（档位只调强度）</summary>
        private static float AuraDamageResist(int tier) => tier switch { 3 => 0.25f, 2 => 0.20f, _ => 0.15f };

        //——盾墙——
        /// <summary>正面格挡减伤（档位只调强度）</summary>
        private static float BlockReduction(int tier) => tier switch { 3 => 0.52f, 2 => 0.46f, _ => 0.40f };
        /// <summary>受军官庇护时的格挡追加</summary>
        private const float WardedBlockBonus = 0.15f;
        /// <summary>举盾所需的交战横向距离</summary>
        private const float BraceEngageRange = 560f;
        /// <summary>越顶豁免（公平阀门）：伤害来源高于战士头顶此距离则绕过盾墙</summary>
        private const float BraceOverheadBypass = 64f;
        /// <summary>举盾时横向步伐阻滞（每帧乘）：姿态在运动上同样可读，并给绕后留时间</summary>
        private const float BraceMoveDamp = 0.94f;
        /// <summary>叠加减伤下限（公平阀门）：伤害保留系数永不低于此值，保证永远打得动</summary>
        private const float CombinedResistFloor = 0.25f;

        //——潮汐节拍——
        /// <summary>完整潮汐周期（帧）：A波 + 喘息 + B波 + 喘息</summary>
        private const int TideCycle = 840;
        /// <summary>单波推进时长</summary>
        private const int TideWaveLen = 300;
        /// <summary>波间全体喘息窗（公平阀门）：此窗口内两波都不推进，发射循环真正读取见 <see cref="TideStrength"/></summary>
        private const int TideGapLen = 120;
        /// <summary>波沿缓入缓出帧数，避免速度突变</summary>
        private const int TideRamp = 30;
        /// <summary>涨潮位置推进系数（档位只调强度，叠加在通用提速之上）</summary>
        private static float SurgeBonus(int tier) => tier switch { 3 => 0.40f, 2 => 0.32f, _ => 0.25f };
        /// <summary>受军官庇护的涨潮加乘</summary>
        private const float WardedSurgeMult = 1.25f;
        /// <summary>退潮阻滞（每帧乘）：涨潮之外的血月怪明显放慢，喘息窗可读</summary>
        private const float LullDamp = 0.90f;

        //——军团箭令（哥布林弓手齐射，仅军官在场时解锁）——
        /// <summary>齐射冷却（档位只调强度）</summary>
        private static int VolleyCooldown(int tier) => tier switch { 3 => 220, 2 => 260, _ => 300 };
        /// <summary>预告帧数（公平底线 ≥30）</summary>
        internal const int VolleyTelegraphFrames = 45;
        /// <summary>最小射距（公平阀门）：贴脸不放箭</summary>
        private const float VolleyMinRange = 220f;
        /// <summary>最大射距</summary>
        private const float VolleyMaxRange = 780f;
        /// <summary>全局并发上限（预告体 + 战矢合计）</summary>
        private const int VolleyGlobalCap = 6;
        /// <summary>战矢伤害 = npc.damage（已含通用缩放）× 此系数</summary>
        private const float VolleyDamageMult = 0.6f;
        /// <summary>战矢初速</summary>
        internal const float VolleyArrowSpeed = 12.5f;

        /// <summary>军团角色，由 NPC 类型静态决定（跨端天然一致，无需同步）</summary>
        private enum LegionRole : byte
        {
            None,
            /// <summary>血月士卒：随潮汐推进</summary>
            BloodTroop,
            /// <summary>血月军官：新郎/新娘</summary>
            BloodOfficer,
            /// <summary>哥布林士卒：战士带盾墙、弓手带齐射</summary>
            GoblinTroop,
            /// <summary>哥布林军官：法师/召唤师</summary>
            GoblinOfficer,
        }

        /// <summary>本个体生成时绑定的档位，0 = 无机制</summary>
        private int boundTier;
        private LegionRole role;
        /// <summary>弓手齐射计时（权威端决策私产，客户端可见状态全在预告体实体上）</summary>
        private int volleyTimer;
        /// <summary>战士举盾态：各端从已同步原语确定性求值，绘制与判伤读同一个值</summary>
        private bool braced;
        /// <summary>上一帧涨潮强度，仅作涨潮沿的视觉检测</summary>
        private float prevSurge;

        private static LegionRole ResolveRole(int type) => type switch {
            NPCID.BloodZombie or NPCID.Drippler or NPCID.ZombieMerman or NPCID.EyeballFlyingFish => LegionRole.BloodTroop,
            NPCID.TheGroom or NPCID.TheBride => LegionRole.BloodOfficer,
            NPCID.GoblinPeon or NPCID.GoblinThief or NPCID.GoblinWarrior
                or NPCID.GoblinArcher or NPCID.GoblinScout => LegionRole.GoblinTroop,
            NPCID.GoblinSorcerer or NPCID.GoblinSummoner => LegionRole.GoblinOfficer,
            _ => LegionRole.None,
        };

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
            => ResolveRole(entity.type) != LegionRole.None;

        public override void SetDefaults(NPC npc) {
            boundTier = 0;
            role = LegionRole.None;
            int tier = GameModeSystem.EffectiveTier;
            if (tier <= 0) {
                return;
            }
            //资格排除：友方/无敌/小动物口径 + Boss + 蠕虫体节（本组类型表内本无蠕虫，纪律性保留）
            if (npc.friendly || npc.townNPC || npc.immortal || npc.dontTakeDamage) {
                return;
            }
            if (npc.lifeMax <= 5 || npc.damage <= 0 || npc.boss || npc.realLife >= 0) {
                return;
            }
            role = ResolveRole(npc.type);
            if (role == LegionRole.None) {
                return;
            }
            boundTier = tier;
            if (npc.type == NPCID.GoblinArcher) {
                //哨兵：NewNPC 先跑 SetDefaults 再写 whoAmI，此刻读 whoAmI 恒为 0，
                //首发错拍推迟到首个决策帧（届时 whoAmI 已有效）播种
                volleyTimer = -1;
            }
        }

        /// <summary>机制运行资格：出生已绑定，且排除雕像怪与临时无敌态（这两项在 SetDefaults 后才可能置位）</summary>
        private bool MechanicActive(NPC npc)
            => boundTier > 0 && !npc.SpawnedFromStatue && !npc.dontTakeDamage;

        public override void PostAI(NPC npc) {
            if (!MechanicActive(npc)) {
                return;
            }
            switch (role) {
                case LegionRole.BloodTroop:
                    TideStep(npc);
                    WardSparkle(npc);
                    break;
                case LegionRole.GoblinTroop:
                    if (npc.type == NPCID.GoblinWarrior) {
                        BraceStep(npc);
                    }
                    else if (npc.type == NPCID.GoblinArcher) {
                        VolleyStep(npc);
                    }
                    WardSparkle(npc);
                    break;
                case LegionRole.BloodOfficer:
                case LegionRole.GoblinOfficer:
                    OfficerStep(npc);
                    break;
            }
        }

        #region 军官光环
        private void OfficerStep(NPC npc) {
            //授环只在权威端跑，buff 走原版 AddNPCBuff 包原生同步；军官间按 whoAmI 错帧
            if (Main.netMode != NetmodeID.MultiplayerClient
                && Main.GameUpdateCount % AuraRefreshInterval == (uint)(npc.whoAmI % AuraRefreshInterval)) {
                bool bloodSide = role == LegionRole.BloodOfficer;
                float radiusSq = OfficerAuraRadius * OfficerAuraRadius;
                foreach (NPC other in Main.ActiveNPCs) {
                    if (other.whoAmI == npc.whoAmI || other.SpawnedFromStatue) {
                        continue;
                    }
                    LegionRole otherRole = ResolveRole(other.type);
                    bool sameLegion = bloodSide
                        ? otherRole == LegionRole.BloodTroop
                        : otherRole == LegionRole.GoblinTroop;
                    if (!sameLegion) {
                        continue;
                    }
                    if (Vector2.DistanceSquared(other.Center, npc.Center) > radiusSq) {
                        continue;
                    }
                    other.AddBuff(WardBuff, AuraBuffTime);
                }
            }

            //军官仪仗：头顶旗辉，血月军官猩红、哥布林军官鎏金（身份由类型静态决定，客户端直接绘）
            if (!Main.dedServ && Main.rand.NextBool(6)) {
                int dustType = role == LegionRole.BloodOfficer ? DustID.CrimsonTorch : DustID.GoldFlame;
                Dust glint = Dust.NewDustPerfect(
                    npc.Top + new Vector2(Main.rand.NextFloat(-8f, 8f), -6f),
                    dustType, new Vector2(0f, -Main.rand.NextFloat(0.8f, 1.6f)), 100, default, 1.1f);
                glint.noGravity = true;
            }
        }

        /// <summary>受庇护士卒的金辉勾边（低频，客户端）</summary>
        private void WardSparkle(NPC npc) {
            if (Main.dedServ || !npc.HasBuff(WardBuff) || !Main.rand.NextBool(16)) {
                return;
            }
            Dust spark = Dust.NewDustDirect(npc.position, npc.width, npc.height,
                DustID.GoldFlame, 0f, -0.5f, 130, default, 0.8f);
            spark.noGravity = true;
            spark.velocity *= 0.4f;
        }
        #endregion

        #region 盾墙
        private void BraceStep(NPC npc) {
            braced = false;
            //离地即破盾：原版战士 AI 频繁跳跃，天然形成开火窗
            if (npc.velocity.Y != 0f || !npc.HasValidTarget) {
                return;
            }
            Player target = Main.player[npc.target];
            float dx = target.Center.X - npc.Center.X;
            braced = Math.Abs(dx) < BraceEngageRange && Math.Sign(dx) == npc.direction;
            if (braced) {
                npc.velocity.X *= BraceMoveDamp;
            }
        }

        /// <summary>正面格挡判定：绘制（PostDraw 的盾）与减伤读取同一 braced，伤害窗口=可见窗口</summary>
        private bool FrontBlocked(NPC npc, Vector2 source, bool overhead) {
            if (!braced || overhead) {
                return false;
            }
            if (source.Y < npc.position.Y - BraceOverheadBypass) {
                return false;
            }
            return (source.X - npc.Center.X) * npc.direction >= 0f;
        }

        /// <summary>弹幕的越顶豁免：命中瞬间弹幕中心必然贴着受击者，源点高度差永远凑不满
        /// <see cref="BraceOverheadBypass"/>（该规则只对近战的玩家身位成立），故弹幕路径按
        /// 来向角度判"高打"——下坠分量超过水平分量（俯冲陡于 45°）即绕过盾墙，平射不受影响。
        /// velocity 是命中结算端的本地精确值，判定与反馈同源</summary>
        private static bool PlungingShot(Projectile projectile)
            => projectile.velocity.Y > Math.Abs(projectile.velocity.X);

        public override void ModifyHitByItem(NPC npc, Player player, Item item, ref NPC.HitModifiers modifiers) {
            if (!MechanicActive(npc)) {
                return;
            }
            ApplyLegionDefense(npc, player.Center, overhead: false, ref modifiers);
        }

        public override void ModifyHitByProjectile(NPC npc, Projectile projectile, ref NPC.HitModifiers modifiers) {
            if (!MechanicActive(npc)) {
                return;
            }
            ApplyLegionDefense(npc, projectile.Center, PlungingShot(projectile), ref modifiers);
        }

        /// <summary>军阵减伤合成：光环减伤 × 盾墙格挡，钳制在保底可击穿线之上。
        /// tML 打击判定在攻击方本机结算，braced 与 buff 均为各端确定性状态，无需额外同步</summary>
        private void ApplyLegionDefense(NPC npc, Vector2 source, bool overhead, ref NPC.HitModifiers modifiers) {
            float keep = 1f;
            bool warded = npc.HasBuff(WardBuff);
            if (warded) {
                keep *= 1f - AuraDamageResist(boundTier);
            }
            if (npc.type == NPCID.GoblinWarrior && FrontBlocked(npc, source, overhead)) {
                keep *= 1f - (BlockReduction(boundTier) + (warded ? WardedBlockBonus : 0f));
            }
            if (keep >= 1f) {
                return;
            }
            modifiers.FinalDamage *= Math.Max(keep, CombinedResistFloor);
        }

        public override void OnHitByItem(NPC npc, Player player, Item item, NPC.HitInfo hit, int damageDone)
            => BlockFeedback(npc, player.Center, overhead: false);

        public override void OnHitByProjectile(NPC npc, Projectile projectile, NPC.HitInfo hit, int damageDone)
            => BlockFeedback(npc, projectile.Center, PlungingShot(projectile));

        /// <summary>格挡反馈：铁火花 + 金铁声（命中方本机，让被减伤的攻击者立刻明白原因）</summary>
        private void BlockFeedback(NPC npc, Vector2 source, bool overhead) {
            if (Main.dedServ || !MechanicActive(npc)
                || npc.type != NPCID.GoblinWarrior || !FrontBlocked(npc, source, overhead)) {
                return;
            }
            SoundEngine.PlaySound(SoundID.NPCHit4 with { Volume = 0.4f, Pitch = 0.35f }, npc.Center);
            Vector2 shieldPos = npc.Center + new Vector2(npc.direction * 16f, -2f);
            for (int i = 0; i < 4; i++) {
                Dust spark = Dust.NewDustPerfect(shieldPos, DustID.Iron,
                    new Vector2(npc.direction * Main.rand.NextFloat(1f, 3f), -Main.rand.NextFloat(0.5f, 2f)),
                    60, default, Main.rand.NextFloat(0.7f, 1.1f));
                spark.noGravity = Main.rand.NextBool();
            }
        }

        public override void PostDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (!MechanicActive(npc) || npc.type != NPCID.GoblinWarrior || !braced) {
                return;
            }
            //盾墙姿态实绘：原版钴蓝盾贴图染铁灰，真 alpha 本体有遮挡像素；受庇护时镶金边
            Main.instance.LoadItem(ItemID.CobaltShield);
            Texture2D tex = TextureAssets.Item[ItemID.CobaltShield].Value;
            //gfxOffY：上坡步进的绘制补偿，缺了它盾会在走台阶时与身体脱节
            Vector2 pos = npc.Center + new Vector2(npc.direction * 16f, npc.gfxOffY - 2f) - screenPos;
            float tilt = npc.direction * 0.15f;
            SpriteEffects flip = npc.direction > 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
            Color iron = Color.Lerp(drawColor, new Color(150, 155, 165), 0.45f);
            if (npc.HasBuff(WardBuff)) {
                spriteBatch.Draw(tex, pos, null, new Color(255, 200, 90, 0) * 0.35f,
                    tilt, tex.Size() / 2f, 1.12f, flip, 0f);
            }
            spriteBatch.Draw(tex, pos, null, iron, tilt, tex.Size() / 2f, 1f, flip, 0f);
        }
        #endregion

        #region 潮汐节拍
        /// <summary>
        /// 潮汐强度 0~1。时钟用 Main.time（各端本地推进，服务端借天象/天气等事件的 WorldData 包重锚；
        /// 短时漂移由 <see cref="TideRamp"/> 帧缓坡吸收，且潮汐只影响运动，位置真值始终以服务端 NPC 同步为准）；
        /// 分组用 whoAmI 奇偶（NPC 槽位服务端权威，跨端一致）。
        /// 时间轴：A波[0,300) 全体喘息[300,420) B波[420,720) 全体喘息[720,840)
        /// </summary>
        private static float TideStrength(int whoAmI) {
            float pos = (float)(Main.time % TideCycle);
            float start = (whoAmI & 1) == 0 ? 0f : TideWaveLen + TideGapLen;
            float local = pos - start;
            if (local < 0f || local >= TideWaveLen) {
                return 0f;
            }
            float edgeIn = MathHelper.Clamp(local / TideRamp, 0f, 1f);
            float edgeOut = MathHelper.Clamp((TideWaveLen - local) / TideRamp, 0f, 1f);
            return edgeIn * edgeOut;
        }

        private void TideStep(NPC npc) {
            float surge = TideStrength(npc.whoAmI);
            if (surge > 0f) {
                //涨潮：位置推进（镜像通用提速的碰撞钳制口径），军官在场推得更凶
                float bonus = SurgeBonus(boundTier) * surge;
                if (npc.HasBuff(WardBuff)) {
                    bonus *= WardedSurgeMult;
                }
                Vector2 advance = npc.velocity * bonus;
                if (!npc.noTileCollide) {
                    advance = Collision.TileCollision(npc.position, advance, npc.width, npc.height);
                }
                npc.position += advance;

                if (!Main.dedServ) {
                    if (prevSurge <= 0f) {
                        //涨潮沿：一次性血雾爆点，本波成员可辨
                        for (int i = 0; i < 4; i++) {
                            Dust burst = Dust.NewDustDirect(npc.position, npc.width, npc.height,
                                DustID.Blood, npc.velocity.X * 0.4f, -1.2f, 80, default, 1.2f);
                            burst.noGravity = true;
                        }
                    }
                    else if (Main.rand.NextBool(10)) {
                        Dust drip = Dust.NewDustDirect(npc.position, npc.width, npc.height,
                            DustID.Blood, 0f, 0.6f, 110, default, 0.9f);
                        drip.velocity *= 0.5f;
                    }
                }
            }
            else {
                //退潮：明显放慢=喘息窗可读；有重力者只阻滞横向，避免悬浮感
                npc.velocity.X *= LullDamp;
                if (npc.noGravity) {
                    npc.velocity.Y *= LullDamp;
                }
            }
            prevSurge = surge;
        }
        #endregion

        #region 军团箭令
        private void VolleyStep(NPC npc) {
            //决策与生成只在权威端；客户端的全部可见状态在预告体实体上（原生同步）
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                return;
            }
            if (volleyTimer < 0) {
                //首发冷却减半并按 whoAmI 错拍，避免多弓手同帧齐鸣
                volleyTimer = VolleyCooldown(boundTier) / 2 + npc.whoAmI % 45;
            }
            if (volleyTimer > 0) {
                volleyTimer--;
                return;
            }
            //齐射资格：军官庇护中（斩首即哑火）、立定、目标在射程环带内且有视线
            if (!npc.HasBuff(WardBuff) || !npc.HasValidTarget || npc.velocity.Y != 0f) {
                return;
            }
            Player target = Main.player[npc.target];
            float dist = npc.Distance(target.Center);
            if (dist < VolleyMinRange || dist > VolleyMaxRange) {
                return;
            }
            if (!Collision.CanHitLine(npc.Center, 1, 1, target.position, target.width, target.height)) {
                return;
            }
            //全局并发闸：预告体与在飞战矢合计超限则本次跳过
            int omenType = ModContent.ProjectileType<LegionVolleyOmen>();
            int arrowType = ModContent.ProjectileType<LegionVolleyArrow>();
            int live = 0;
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == omenType || proj.type == arrowType) {
                    live++;
                }
            }
            if (live >= VolleyGlobalCap) {
                volleyTimer = 40;
                return;
            }
            //预告即承诺：方向在此刻锁死进 velocity（随生成包原生同步），此后不再重瞄
            Vector2 aim = npc.DirectionTo(target.Center);
            int damage = (int)(npc.damage * VolleyDamageMult);
            Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, aim,
                omenType, 0, 0f, Main.myPlayer, damage);
            volleyTimer = VolleyCooldown(boundTier) + npc.whoAmI % 45;
        }
        #endregion
    }
}
