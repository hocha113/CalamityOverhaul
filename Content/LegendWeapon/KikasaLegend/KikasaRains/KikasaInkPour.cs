using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTalismans;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaRains
{
    /// <summary>
    /// 墨瀑:倒撑重击的倾覆主体。自碗口倾下的黑墨水瀑,两端收口:
    /// 源头=碗口溢流球根沉进伞底(禁平切),落点=推进期坠落头/触地后溅丘翻沫/
    /// 空中散逸成股;宽度走全生命周期包络(展开铺满→排空收窄断流,判定同源);
    /// 刚性摆压到极小,流体甩尾由 shader 行波承担。
    /// 射线逐帧找落点(实心或域内湖面),落点持续搅浊留渍;前 12 帧沿瀑缘散射特大墨滴。
    /// 冲刷期方向持续跟手:所有者端限速转向光标(ai[0] 为权威角,节流补包),
    /// 旁观端向权威角平滑追赶;源头逐帧钉回伞碗口,排空断流后方向与源头就地定格。
    /// 判定为线碰撞,排空过半即失能;绘制走 KikasaInkDrop.fx 的 TechPour
    /// </summary>
    internal class KikasaInkPour : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        public const int ExpandFrames = 8;
        public const int SustainFrames = 26;
        public const int CollapseFrames = 10;
        public const int TotalFrames = ExpandFrames + SustainFrames + CollapseFrames;

        /// <summary>
        /// 空射最大射程。必须长过 zoom 拉远后的视野对角线,否则空中开火会看见柱端平切。
        /// 6400 ≈ 1080p@0.5x 对角+余量,4K@1x 也能出画;有落点时射线提前截断。
        /// </summary>
        private const float MaxLenPx = 6400f;

        /// <summary>瀑缘散射沿柱的最大距离,不跟空射射程一起拉长</summary>
        private const float ScatterAlongMax = 480f;

        /// <summary>跟手转向:每帧最大角步进(弧度)与比例平滑系数,伞姿态与瀑体共用同一速率</summary>
        internal const float TrackTurnStep = 0.15f;
        internal const float TrackTurnLerp = 0.3f;

        /// <summary>倾泻角(弧度,生成包写入初值,冲刷期由所有者端持续转向光标)</summary>
        private ref float BaseAngle => ref Projectile.ai[0];

        //ai[1] 量化编码:无符为裸蓄力档(0~1),带符为 1024*标签+蓄力档*1000,
        //随生成包同步;解码见 Fill/TalismanTag
        /// <summary>打包蓄力档与符标签进 ai[1]（tag=0 保持裸值,旧路径不变）</summary>
        internal static float PackFillTag(float fill, int tag) {
            float clamped = MathHelper.Clamp(fill, 0f, 1f);
            return tag <= 0 ? clamped : tag * 1024f + MathF.Round(clamped * 1000f);
        }

        /// <summary>符标签(0=无符),ModifyPourSpawn 打上,材质/命中按此分支</summary>
        internal int TalismanTag => Projectile.ai[1] > 1.001f ? (int)(Projectile.ai[1] / 1024f) : 0;

        /// <summary>蓄力档 0~1,吃宽度与伤害表现（自 ai[1] 解码）</summary>
        private float Fill => Projectile.ai[1] > 1.001f
            ? MathHelper.Clamp((Projectile.ai[1] - TalismanTag * 1024f) / 1000f, 0f, 1f)
            : Projectile.ai[1];

        private float life;
        private float lenPx = MaxLenPx;
        private bool hitGround;
        private bool hitLake;
        private int scatterCount;
        //跟手角的端本地量:旁观端对权威角的平滑镜像(NaN=未初始化,首个 AI 帧对齐);
        //lastSentAngle 是所有者端上次补包时的角,节流判据
        private float smoothAngle = float.NaN;
        private float lastSentAngle;
        //伞下鬼接线:栏位数每帧自所有者装备读取,各端一致;冲刷延时在首帧一次性落定
        private int slotCount = 1;
        private int sustainFrames = SustainFrames;
        private bool geyserFired;

        //唤雨符:每帧一解的档与派发器快照(空绳零开销),绘制线程复用上一帧
        private KikasaTalismanProfile pourProfile = KikasaTalismanProfile.Identity;
        private KikasaTalismanHookRunner pourHooks;

        /// <summary>瀑宽材质旋钮(霸月瀑等):OnPourStart 首帧按标签一次性写,默认 1</summary>
        internal float TalismanWidthMul = 1f;

        /// <summary>落线终点(挂钩实现取落点用,如虹符落点拱桥)</summary>
        internal Vector2 FallEndPoint => Projectile.Center + DirAngle.ToRotationVector2() * lenPx;

        /// <summary>本帧是否触地(含湖面)</summary>
        internal bool HitGroundNow => hitGround;

        //刚性摆压到极小(判定线跟随),流体甩尾由 shader 内行波承担，源头钉死碗口;
        //本体角用端本地平滑量,绘制可能先于首个 AI 帧,未初始化时退回权威角
        private float DirAngle
            => (float.IsNaN(smoothAngle) ? BaseAngle : smoothAngle)
                + MathF.Sin(life * 0.16f + Projectile.identity * 0.71f) * 0.03f;

        /// <summary>限速转向:小偏差按比例收尾,大偏差吃步进上限,快而带墨的惯性</summary>
        internal static float TurnTowards(float cur, float target, float rateMul = 1f) {
            float diff = MathHelper.WrapAngle(target - cur);
            float step = MathHelper.Clamp(diff * TrackTurnLerp,
                -TrackTurnStep * rateMul, TrackTurnStep * rateMul);
            return MathHelper.WrapAngle(cur + step);
        }

        private float WidthPx => (54f + Fill * 36f + KikasaOverride.GetPourWidthBonus(slotCount))
            * pourProfile.PourWidthMul * TalismanWidthMul;

        private float LenT {
            get {
                float t = MathHelper.Clamp(life / ExpandFrames, 0f, 1f);
                return 1f - (1f - t) * (1f - t);
            }
        }

        private float DrainT
            => MathHelper.Clamp((life - ExpandFrames - sustainFrames) / (float)CollapseFrames, 0f, 1f);

        /// <summary>宽度生命周期:展开 EaseOut 铺满,排空 EaseIn 收窄断流(视觉与判定同源)</summary>
        private float WidthT {
            get {
                float t = MathHelper.Clamp(life / ExpandFrames, 0f, 1f);
                float expand = MathHelper.Lerp(0.35f, 1f, 1f - (1f - t) * (1f - t));
                float d = DrainT;
                return expand * (1f - d * d);
            }
        }

        public override void SetDefaults() {
            Projectile.width = 26;
            Projectile.height = 26;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalFrames;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 9;
            Projectile.netImportant = true;
        }

        public override void AI() {
            life++;
            Player owner = Main.player[Projectile.owner];
            slotCount = KikasaOverride.GetSlotCount(owner);
            //唤雨符快照:一帧一解,宽度/散射/泉齐发全部复用(符位表在玩家身上)
            pourProfile = KikasaTalismanCombat.Resolve(owner);
            pourHooks = KikasaTalismanHooks.For(owner);
            if ((int)life == 1) {
                //冲刷时长首帧一次性落定:齐掷档+12,泷符时长倍率再折入;timeLeft 补同一差值
                int baseSustain = SustainFrames
                    + (slotCount >= KikasaOverride.TierGhostVolley ? 12 : 0);
                sustainFrames = Math.Max(
                    (int)MathF.Round(baseSustain * pourProfile.PourSustainMul), 4);
                Projectile.timeLeft += sustainFrames - SustainFrames;
                //墨瀑首帧挂钩(泷推移伴生/霸月瀑材质旋钮),各端同拍
                pourHooks.OnPourStart(Projectile);
            }

            if (float.IsNaN(smoothAngle)) {
                smoothAngle = BaseAngle;
                lastSentAngle = BaseAngle;
            }
            //冲刷期跟手:所有者端限速追光标并节流补包,旁观端/服务器向权威角平滑追赶
            //(追赶速率稍快,吃掉包间隔的滞后);排空断流后方向就地定格
            if (DrainT <= 0f) {
                if (Main.myPlayer == Projectile.owner) {
                    BaseAngle = TurnTowards(BaseAngle,
                        (Main.MouseWorld - Projectile.Center).ToRotation());
                    smoothAngle = BaseAngle;
                    //一次只有一根瀑,5 帧一发不淹链路;光标没动就不发
                    if ((int)life % 5 == 0
                        && MathF.Abs(MathHelper.WrapAngle(BaseAngle - lastSentAngle)) > 0.01f) {
                        lastSentAngle = BaseAngle;
                        Projectile.netUpdate = true;
                    }
                }
                else {
                    smoothAngle = TurnTowards(smoothAngle, BaseAngle, 1.4f);
                }
                //源头逐帧钉回碗口:伞跟着玩家走,瀑不脱手;伞离开倾覆态(传送/续蓄)就地定格
                KikasaRainUmbrella umbrella = KikasaRainUmbrella.FindFor(Projectile.owner);
                if (umbrella != null && umbrella.IsPourBody) {
                    Projectile.Center = umbrella.PourMouthPos;
                }
            }
            Vector2 dir = DirAngle.ToRotationVector2();

            //域内湖面:墨倾进湖里,落点换涟漪
            bool lakeAlive = owner?.active == true
                && owner.TryGetModPlayer(out KikasaDomainPlayer domain)
                && domain.AnyActive && domain.RiseT > 0.5f;
            float lakeY = lakeAlive ? owner.GetModPlayer<KikasaDomainPlayer>().LakeWorldY : float.MaxValue;

            //射线找落点:实心或湖面,各端确定性一致
            lenPx = MaxLenPx;
            hitGround = false;
            hitLake = false;
            for (float d = 32f; d <= MaxLenPx; d += 16f) {
                Vector2 p = Projectile.Center + dir * d;
                if (p.Y >= lakeY) {
                    lenPx = d;
                    hitGround = true;
                    hitLake = true;
                    break;
                }
                if (Collision.SolidCollision(p - new Vector2(4f, 4f), 8, 8)) {
                    lenPx = d;
                    hitGround = true;
                    break;
                }
            }

            //落点余韵:冲刷期间持续留渍/搅涟漪,节流一拍一次
            if (hitGround && LenT >= 0.99f && DrainT <= 0f && life % 8 == 0) {
                Vector2 end = Projectile.Center + dir * lenPx;
                if (hitLake) {
                    if (!Main.dedServ && KikasaDomain.Viewed != null) {
                        KikasaDomainDeco.RippleAt(new Vector2(end.X, lakeY), 1.1f);
                        KikasaDomainDeco.SplashAt(new Vector2(end.X, lakeY), 6);
                    }
                    //持续冲刷:同一片墨晕越冲越大
                    KikasaInkFX.AddLakeBlot(Projectile.owner, end.X, 44f + Fill * 30f);
                }
                else {
                    KikasaInkFX.AddGroundSplat(end + dir * 6f, dir * 14f, 46f + Fill * 28f);
                }
                //落点翻涌:反弹墨珠+一口墨雾+贴地横滑的溅珠(溅裙沿地面铺开)
                if (!Main.dedServ) {
                    for (int i = 0; i < 3; i++) {
                        Vector2 vel = (-dir).RotatedByRandom(0.9f) * Main.rand.NextFloat(2f, 5.5f);
                        PRTLoader.NewParticle<PRT_KikasaInkBead>(end + Main.rand.NextVector2Circular(WidthPx * 0.3f, 6f),
                            vel, Main.rand.NextBool(3) ? KikasaInk.InkDeep : KikasaInk.InkBody,
                            Main.rand.NextFloat(0.4f, 0.7f))?.Configure(Main.rand.Next(18, 28));
                    }
                    Vector2 tang = dir.RotatedBy(MathHelper.PiOver2);
                    for (int i = 0; i < 2; i++) {
                        float side = i == 0 ? 1f : -1f;
                        PRTLoader.NewParticle<PRT_KikasaInkBead>(
                            end - dir * 4f + tang * side * Main.rand.NextFloat(4f, WidthPx * 0.4f),
                            tang * side * Main.rand.NextFloat(3f, 6f) - dir * Main.rand.NextFloat(0.4f, 1.4f),
                            KikasaInk.InkBody, Main.rand.NextFloat(0.34f, 0.55f))?.Configure(Main.rand.Next(16, 26));
                    }
                    PRTLoader.NewParticle<PRT_KikasaInkMist>(end - dir * 10f,
                        -dir * Main.rand.NextFloat(0.5f, 1.2f), KikasaInk.InkDeep,
                        Main.rand.NextFloat(0.9f, 1.3f))?.Configure(Main.rand.Next(26, 40));
                }
                KikasaInk.Play(KikasaInk.InkSplash, end, 0.48f, -0.4f, 4);
            }

            //特大墨滴沿瀑缘散射(所有者端);沿程不跟空射 6400 一起拉长,否则会在视野外刷滴。
            //二鬼帮衬档(S≥4)散射 7→9 颗,窗口相应放宽
            int scatterCap = slotCount >= KikasaOverride.TierGhostAssist ? 9 : 7;
            float scatterWindow = scatterCap > 7 ? 18f : 12f;
            if (Main.myPlayer == Projectile.owner && life <= scatterWindow
                && (int)life % 2 == 0 && scatterCount < scatterCap) {
                scatterCount++;
                float scatterSpan = MathF.Min(lenPx, ScatterAlongMax);
                float along = Main.rand.NextFloat(0.12f, 0.5f) * scatterSpan;
                float side = Main.rand.NextBool() ? 1f : -1f;
                Vector2 perp = dir.RotatedBy(MathHelper.PiOver2) * side;
                Vector2 pos = Projectile.Center + dir * along + perp * (WidthPx * 0.5f);
                Vector2 vel = perp * Main.rand.NextFloat(2f, 4.5f) + dir * Main.rand.NextFloat(1f, 3f);
                float fallbackX = Projectile.Center.X + dir.X * scatterSpan + Main.rand.NextFloat(-150f, 150f);
                //湖倾档或潦符的散射滴同样落地留墨洼(本分支已在所有者端);
                //散射滴同样过滴生成挂钩(霰打标大滴等),FromPourScatter 供符区分伞掷/瀑散
                KikasaDropSpawnContext dropCtx = new() {
                    Position = pos,
                    Velocity = vel,
                    Scale = 1.2f,
                    DamageMul = 1.25f,
                    Penetrate = 1,
                    TargetWho = -1,
                    FallbackX = fallbackX,
                    Ghost = false,
                    Puddle = slotCount >= KikasaOverride.TierLakeTilt || pourProfile.PuddleUnlock,
                    GhostVolley = false,
                    FromPourScatter = true,
                    DropIndex = scatterCount - 1,
                    TagId = 0,
                    TagPayload = 0,
                };
                pourHooks.ModifyDropSpawn(ref dropCtx);
                int flags = (dropCtx.Ghost ? KikasaInkDrop.FlagGhost : 0)
                    | (dropCtx.Puddle ? KikasaInkDrop.FlagPuddle : 0)
                    | KikasaTalismanHooks.PackTag(dropCtx.TagId, dropCtx.TagPayload);
                int p = Projectile.NewProjectile(Projectile.GetSource_FromThis(),
                    dropCtx.Position, dropCtx.Velocity,
                    ModContent.ProjectileType<KikasaInkDrop>(),
                    (int)(Projectile.damage * dropCtx.DamageMul),
                    Projectile.knockBack, Projectile.owner,
                    dropCtx.TargetWho, dropCtx.FallbackX, flags);
                if (p >= 0 && p < Main.maxProjectiles) {
                    Projectile drop = Main.projectile[p];
                    drop.scale = dropCtx.Scale;
                    if (dropCtx.Penetrate != 1) {
                        drop.penetrate = dropCtx.Penetrate;
                    }
                    drop.netUpdate = true;
                    //墨印:瀑的散射滴同属亲手指挥的攻击,盖印(端本地位,本分支已在归属端)
                    if (drop.ModProjectile is KikasaInkDrop inkDrop) {
                        inkDrop.AppliesTag = true;
                    }
                }
            }

            //众鬼齐掷档:瀑线带沉溺内吸，推 NPC 归权威端,联机客户端靠同步兜底
            if (slotCount >= KikasaOverride.TierGhostVolley && LenT >= 0.6f && DrainT <= 0.35f
                && Main.netMode != NetmodeID.MultiplayerClient) {
                SuckIntoFall(dir);
            }

            //湖倾终幕:满蓄且触地的墨瀑在排空前沿,沿落线唤起墨泉(所有者端)。
            //齐发决策一瀑只做一次:基础条件不满足也过泉齐发挂钩(霆非满蓄小雷泉/雩大雩解锁可强开)
            if (Main.myPlayer == Projectile.owner && !geyserFired && hitGround
                && (int)life >= ExpandFrames + sustainFrames) {
                geyserFired = true;
                bool fullCharge = slotCount >= KikasaOverride.TierLakeTilt && Fill >= 0.99f;
                KikasaGeyserVolleyContext geyserCtx = new() {
                    Fire = fullCharge,
                    FromFullCharge = fullCharge,
                    Count = 3,
                    DamageMul = 1f,
                    HeightMul = 1f,
                    DelayStepFrames = 5,
                    TagId = 0,
                    TagPayload = 0,
                };
                pourHooks.ModifyGeyserVolley(Projectile, ref geyserCtx);
                if (geyserCtx.Fire && geyserCtx.Count > 0) {
                    FireGeysers(dir, in geyserCtx);
                }
            }

            Lighting.AddLight(Projectile.Center + dir * MathF.Min(lenPx, 420f) * 0.5f, 0.14f, 0.03f, 0.04f);
        }

        /// <summary>
        /// 沉溺内吸:瀑身外一圈的敌人被往落线里拖,力度吃击退抗性,
        /// 上限压得很低，是"水在拽",不是磁铁
        /// </summary>
        private void SuckIntoFall(Vector2 dir) {
            Vector2 a = Projectile.Center;
            Vector2 b = Projectile.Center + dir * (lenPx * LenT);
            float inner = WidthPx * 0.7f;
            float outer = WidthPx * 2.4f;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc?.active != true || npc.friendly || npc.boss
                    || npc.dontTakeDamage || npc.knockBackResist <= 0f
                    || !npc.CanBeChasedBy(Projectile)) {
                    continue;
                }
                Vector2 closest = ClosestOnSegment(npc.Center, a, b);
                float dist = Vector2.Distance(npc.Center, closest);
                if (dist < inner || dist > outer) {
                    continue;
                }
                Vector2 pull = (closest - npc.Center).SafeNormalize(Vector2.Zero);
                //已经朝瀑里冲就不再加力,不做弹弓
                if (Vector2.Dot(npc.velocity, pull) < 2.5f) {
                    npc.velocity += pull * (0.5f * npc.knockBackResist);
                }
            }
        }

        private static Vector2 ClosestOnSegment(Vector2 p, Vector2 a, Vector2 b) {
            Vector2 ab = b - a;
            float lenSq = ab.LengthSquared();
            if (lenSq < 1e-4f) {
                return a;
            }
            float t = MathHelper.Clamp(Vector2.Dot(p - a, ab) / lenSq, 0f, 1f);
            return a + ab * t;
        }

        /// <summary>
        /// 沿落线取探针,向下吸附地表(域内湖面直接按湖线),各起一道墨泉,按齐发决策错拍喷发;
        /// 泉数/伤害/柱高/符标签均出自 <see cref="KikasaGeyserVolleyContext"/>(标签与柱高随泉 ai 同步)
        /// </summary>
        private void FireGeysers(Vector2 dir, in KikasaGeyserVolleyContext geyserCtx) {
            Player owner = Main.player[Projectile.owner];
            bool lakeAlive = owner?.active == true
                && owner.TryGetModPlayer(out KikasaDomainPlayer domain)
                && domain.AnyActive && domain.RiseT > 0.5f;
            float lakeY = lakeAlive ? owner.GetModPlayer<KikasaDomainPlayer>().LakeWorldY : float.MaxValue;

            //沛符墨泉乘区(FireGeysers 只在所有者端被调,伤害随生成包带走)
            float geyserMul = pourProfile.GeyserDamageMul * geyserCtx.DamageMul;
            float geyserAi1 = KikasaTalismanHooks.PackTag(geyserCtx.TagId, geyserCtx.TagPayload);
            float geyserAi2 = MathF.Round(MathHelper.Clamp(geyserCtx.HeightMul, 0.2f, 8f) * 1000f);

            Vector2 end = Projectile.Center + dir * lenPx;
            int fired = 0;
            for (int i = 0; i < geyserCtx.Count; i++) {
                float off = (i - (geyserCtx.Count - 1) * 0.5f) * 96f;
                Vector2 basePos;
                if (hitLake) {
                    basePos = new Vector2(end.X + off, lakeY);
                }
                else if (!TryFindGroundBelow(new Vector2(end.X + off, end.Y - 90f), 320f, out basePos)) {
                    continue;
                }
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), basePos, Vector2.Zero,
                    ModContent.ProjectileType<KikasaInkGeyser>(),
                    (int)(Projectile.damage * 0.9f * geyserMul),
                    Projectile.knockBack * 1.6f, Projectile.owner,
                    fired * geyserCtx.DelayStepFrames, geyserAi1, geyserAi2);
                fired++;
            }
        }

        /// <summary>墨瀑谢幕挂钩(虹落点拱桥/霹开天雷窗等),非服务器各端派发</summary>
        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            KikasaTalismanHooks.ForOwner(Projectile.owner).OnPourEnd(Projectile);
        }

        //==================== 命中挂钩(引擎保证只在归属端跑) ====================

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
            => KikasaTalismanHooks.ForOwner(Projectile.owner)
                .ModifyRainHitNPC(Projectile, KikasaRainSourceKind.Pour, target, ref modifiers);

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //墨印:墨瀑只能被亲手指挥出来,命中即盖印(归属端,骑原版 buff 同步)
            target.AddBuff(ModContent.BuffType<KikasaInkTag>(), KikasaInkTag.TagFrames);
            KikasaTalismanHooks.ForOwner(Projectile.owner)
                .OnRainHitNPC(Projectile, KikasaRainSourceKind.Pour, target, in hit, damageDone);
        }

        /// <summary>自探针点向下逐格找实心地表,命中返回表面世界坐标</summary>
        private static bool TryFindGroundBelow(Vector2 probe, float maxDown, out Vector2 surface) {
            int x = (int)(probe.X / 16f);
            int startY = (int)(probe.Y / 16f);
            int endY = (int)((probe.Y + maxDown) / 16f);
            for (int y = startY; y <= endY; y++) {
                Tile t = Framing.GetTileSafely(x, y);
                if (t.HasTile && Main.tileSolid[t.TileType] && !Main.tileSolidTop[t.TileType]) {
                    surface = new Vector2(probe.X, y * 16f);
                    return true;
                }
            }
            surface = default;
            return false;
        }

        /// <summary>线碰撞:柱体全程,宽随包络(收窄断流时判定同步变细);排空过半即失能</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (DrainT > 0.35f) {
                return false;
            }
            float _ = 0f;
            Vector2 dir = DirAngle.ToRotationVector2();
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                Projectile.Center, Projectile.Center + dir * (lenPx * LenT), WidthPx * 0.7f * WidthT, ref _);
        }

        //==================== 绘制(由 KikasaRainRender 集中调用) ====================

        public override bool PreDraw(ref Color lightColor) => false;

        /// <summary>
        /// 着色器路径:TechPour 长条 quad。画布上端沉进碗口(球根收口),
        /// 下端越过落点留溅丘/散逸余幅;v 轴关键面(碗口/前锋/接触面/排空前沿)全部换算后上载。
        /// 消费共享参数化 shader,逐体全参数重设(uniform 残值纪律)
        /// </summary>
        internal void DrawPourQuad(SpriteBatch sb, Effect fx, Texture2D canvas) {
            float widthT = WidthT;
            if (widthT <= 0.02f) {
                return;
            }
            float fade = MathHelper.Clamp(life / 4f, 0f, 1f);
            float churn = hitGround ? MathHelper.Clamp((LenT - 0.9f) * 10f, 0f, 1f) * (1f - DrainT) : 0f;

            float inset = WidthPx * 0.14f;   //源头沉进碗口,球根顶端藏在伞体后
            float padPx = WidthPx * 1.8f;    //落点外的溅丘/空中散逸画布
            float quadH = inset + lenPx + padPx;
            float ws = WidthPx / quadH;
            float spanV = (inset + lenPx) / quadH;
            Vector2 dir = DirAngle.ToRotationVector2();

            fx.Parameters["uSeed"]?.SetValue(Projectile.identity * 0.7391f % 3.71f);
            fx.Parameters["uFade"]?.SetValue(fade);
            fx.Parameters["uWScale"]?.SetValue(ws);
            fx.Parameters["uSrcV"]?.SetValue(inset / quadH);
            fx.Parameters["uFront"]?.SetValue((inset + lenPx * LenT) / quadH);
            fx.Parameters["uSpanV"]?.SetValue(spanV);
            fx.Parameters["uGrounded"]?.SetValue(hitGround ? 1f : 0f);
            fx.Parameters["uDrainV"]?.SetValue(MathHelper.Lerp(-ws * 1.5f, spanV + ws * 2f, DrainT));
            fx.Parameters["uChurn"]?.SetValue(churn);
            fx.Parameters["uWidthT"]?.SetValue(widthT);
            fx.Parameters["uSway"]?.SetValue(life * 0.19f + Projectile.identity * 0.71f);
            fx.Parameters["uFill"]?.SetValue(Fill);
            fx.CurrentTechnique = fx.Techniques["TechPour"];
            fx.CurrentTechnique.Passes[0].Apply();

            //shader 满宽半宽 ≈0.33(xc∈[-1,1] 空间,一单位=quad 半宽),
            //可见全宽 ≈1.1×WidthPx、判定 0.7×WidthPx 藏在体内;溅丘/飞沫带吃掉两侧余幅
            float quadW = WidthPx / 0.31f;
            Vector2 scale = new(quadW / canvas.Width, quadH / canvas.Height);
            sb.Draw(canvas, Projectile.Center - dir * inset - Main.screenPosition, null, Color.White,
                DirAngle - MathHelper.PiOver2, new Vector2(canvas.Width * 0.5f, 0f), scale,
                SpriteEffects.None, 0f);
        }

        /// <summary>精灵回退:一条速度拉伸的暗墨柱,宽随包络收窄断流</summary>
        internal void DrawPourFallback(SpriteBatch sb) {
            Texture2D tex = CWRAsset.Extra_98?.Value;
            if (tex == null) {
                return;
            }
            float widthT = WidthT;
            if (widthT <= 0.02f) {
                return;
            }
            float alpha = (1f - DrainT) * MathHelper.Clamp(life / 4f, 0f, 1f);
            Vector2 mid = Projectile.Center + DirAngle.ToRotationVector2() * (lenPx * LenT * 0.5f);
            Vector2 scale = new(WidthPx * widthT / tex.Width * 1.2f, lenPx * LenT / tex.Height * 1.1f);
            sb.Draw(tex, mid - Main.screenPosition, null, KikasaInk.InkBody * (0.85f * alpha),
                DirAngle - MathHelper.PiOver2, tex.Size() * 0.5f, scale, SpriteEffects.None, 0f);
        }
    }
}
