using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaRains;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTeleports
{
    /// <summary>
    /// 鬼域传送的水舞台：瞬发无前后摇。按下当帧双潭同时砸开，
    /// <see cref="TeleportFrame"/> 帧人已渡到彼岸；此岸水柱吞人、彼岸水柱喷发，
    /// 珠雨、墨雾、冲击环与湿渍全在这里。伞体本身不在此绘制，
    /// 常驻悬伞（<see cref="KikasaRainUmbrella"/>）检测到本弹幕即入传送态跟拍，
    /// 经 <see cref="UmbrellaDiveT"/>/<see cref="UmbrellaHidden"/>/<see cref="UmbrellaPopT"/>
    /// 读同一根时间轴，观感始终一把伞。
    /// ai0/ai1=目标点，ai2=域内快速变体；仅所有者端生成，各端跑同一套 AI，
    /// 对 Owner 执行同拍位移与渐隐，与 FishTeleportProj 同构
    /// </summary>
    internal class KikasaTeleportProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //==================== 时序（帧，自出生 age 起算，瞬发口径）====================

        /// <summary>位移帧：按下到人已站在彼岸的全部延迟</summary>
        private int TeleportFrame => Fast ? 2 : 4;

        /// <summary>伞扎进此岸潭口的帧</summary>
        private int DiveFrames => Fast ? 3 : 6;

        /// <summary>伞自彼岸潭破水而出的起始帧</summary>
        private int PopStart => Fast ? 4 : 8;

        /// <summary>伞弹回悬点的帧数</summary>
        private int PopFrames => Fast ? 5 : 8;

        /// <summary>人自彼岸现身的渐显帧数</summary>
        private int FadeInFrames => Fast ? 4 : 6;

        /// <summary>彼岸喷发水柱的生命帧</summary>
        private int GeyserLife => Fast ? 15 : 26;

        /// <summary>此岸吞没水柱的生命帧</summary>
        private int SwallowLife => Fast ? 8 : 12;

        /// <summary>全场帧数，之后余韵全靠粒子与湿渍自谢</summary>
        private int TotalFrames => Fast ? 28 : 48;

        //==================== 几何（px）====================

        private float PoolWidth => Fast ? 122f : 150f;
        private const float PoolDepth = 20f;
        private float GeyserHeight => Fast ? 96f : 134f;
        private float GeyserWidth => Fast ? 36f : 46f;
        private const float SwallowHeight = 66f;
        private const float AnchorScan = 96f;

        private Player Owner => Main.player[Projectile.owner];
        private bool Fast => Projectile.ai[2] > 0.5f;
        private Vector2 TargetCenter => new(Projectile.ai[0], Projectile.ai[1]);

        private int age;
        private bool teleported;

        //双潭：origin 在位移前随脚下逐帧再锚，之后冻结收干；dest 出生即锚定
        private Vector2 originPool;
        private bool originGrounded;
        private Vector2 destPool;
        private bool destGrounded;
        private float originGrow;
        private float destGrow;
        private float originDry;
        private float destDry;

        //水纹圈，各端本地自跑的纯视觉
        private struct Ripple
        {
            public Vector2 Pos;
            public float Life;
            public float MaxLife;
            public float FromW;
            public float ToW;
            public float Alpha;
        }
        private readonly List<Ripple> ripples = [];

        //==================== 伞的跟拍窗口（悬伞传送态逐帧读取）====================

        /// <summary>此岸潭口（世界坐标），伞的扎水终点</summary>
        internal Vector2 OriginPoolPos => originPool;

        /// <summary>彼岸潭口（世界坐标），伞的破水起点</summary>
        internal Vector2 DestPoolPos => destPool;

        /// <summary>扎水去程 0~1</summary>
        internal float UmbrellaDiveT => MathHelper.Clamp(age / (float)DiveFrames, 0f, 1f);

        /// <summary>没入水中的隐没窗口</summary>
        internal bool UmbrellaHidden => age >= DiveFrames && age < PopStart;

        /// <summary>已进破水回程</summary>
        internal bool UmbrellaEmerging => age >= PopStart;

        /// <summary>破水弹回 0~1</summary>
        internal float UmbrellaPopT
            => MathHelper.Clamp((age - PopStart) / (float)PopFrames, 0f, 1f);

        /// <summary>该玩家在场的水舞台，无则 null；悬伞的入态检测与跟拍都走这里</summary>
        internal static KikasaTeleportProj FindFor(int ownerWho) {
            int type = ModContent.ProjectileType<KikasaTeleportProj>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (proj.active && proj.owner == ownerWho && proj.type == type
                    && proj.ModProjectile is KikasaTeleportProj stage) {
                    return stage;
                }
            }
            return null;
        }

        public override void SetDefaults() {
            Projectile.width = 4;
            Projectile.height = 4;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            //失效兜底，正常流程在 TotalFrames 自行 Kill
            Projectile.timeLeft = 90;
            Projectile.hide = true;
        }

        /// <summary>水柱与潭要笼得住人，全程画在玩家之上</summary>
        public override void DrawBehind(int index, List<int> behindNPCsAndTiles,
            List<int> behindNPCs, List<int> behindProjectiles,
            List<int> overPlayers, List<int> overWiresUI) {
            overPlayers.Add(index);
        }

        public override void AI() {
            if (!Owner.active || Owner.dead) {
                Projectile.Kill();
                return;
            }
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                destPool = AnchorPool(TargetCenter + new Vector2(0f, Owner.height * 0.5f),
                    out destGrounded);
                originPool = AnchorPool(Owner.Bottom, out originGrounded);
                CastBeat();
            }

            age++;
            //位移前潭随脚下走，人挪水也挪
            if (!teleported) {
                originPool = AnchorPool(Owner.Bottom, out originGrounded);
            }
            UpdatePools();

            //渐隐渐显都压在位移帧两侧，手感上没有可感知的前后摇
            if (age <= TeleportFrame) {
                Owner.opacityForAnimation = 1f - age / (float)TeleportFrame;
            }
            else {
                Owner.opacityForAnimation = MathHelper.Clamp(
                    (age - TeleportFrame) / (float)FadeInFrames, 0f, 1f);
            }

            if (age == TeleportFrame && !teleported) {
                teleported = true;
                Vector2 newPos = TargetCenter
                    - new Vector2(Owner.width * 0.5f, Owner.height * 0.5f);
                Owner.Teleport(newPos, 999);
                Owner.velocity = Vector2.Zero;
                Owner.immune = true;
                Owner.immuneTime = Math.Max(Owner.immuneTime, 16);
                SinkBeat();
                EruptBeat();
            }

            UpdateBoilAndRain();
            UpdateRipples();
            Projectile.Center = Owner.Center;

            if (!Main.dedServ) {
                float glow = teleported ? 0.22f : 0.1f;
                Lighting.AddLight(originPool, glow, glow * 0.25f, glow * 0.32f);
                Lighting.AddLight(destPool, glow * 1.4f, glow * 0.35f, glow * 0.45f);
            }

            if (age >= TotalFrames) {
                Owner.opacityForAnimation = 1f;
                Projectile.Kill();
            }
        }

        //==================== 三记重拍 ====================

        /// <summary>起手拍：伞哨与两潭砸开的水声，涟漪先行</summary>
        private void CastBeat() {
            if (Main.dedServ) {
                return;
            }
            KikasaInk.Play(KikasaInk.UmbrellaWhoosh, Owner.Center, 0.6f, -0.35f);
            KikasaInk.Play(KikasaInk.InkFlick, Owner.Center, 0.4f, 0.15f);
            AddRipple(originPool, PoolWidth * 0.2f, PoolWidth * 1.1f, 0.6f);
            AddRipple(destPool, PoolWidth * 0.2f, PoolWidth * 1.1f, 0.6f);
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_KikasaInkMist>(
                    (i < 2 ? originPool : destPool) + new Vector2(Main.rand.NextFloat(-20f, 20f), -4f),
                    new Vector2(0f, -Main.rand.NextFloat(0.4f, 1f)),
                    KikasaInk.InkDeep, Main.rand.NextFloat(0.7f, 1f))?.Configure(Main.rand.Next(20, 30));
            }
        }

        /// <summary>吞没拍：此岸水柱腾起把人吞进潭里，珠雨向内收</summary>
        private void SinkBeat() {
            //域内借真湖搭台：湖线跟着荡开
            NotifyLake(originPool);
            if (Main.dedServ) {
                return;
            }
            int splash = Fast ? 12 : 20;
            for (int i = 0; i < splash; i++) {
                Vector2 pos = originPool + new Vector2(
                    Main.rand.NextFloat(-0.42f, 0.42f) * PoolWidth, -4f);
                Vector2 vel = new(Main.rand.NextFloat(-3.6f, 3.6f),
                    Main.rand.NextFloat(-6.5f, -2f));
                PRTLoader.NewParticle<PRT_KikasaInkBead>(pos, vel,
                    Main.rand.NextBool(3) ? KikasaInk.BloodCore : KikasaInk.InkDeep,
                    Main.rand.NextFloat(0.3f, 0.5f))?.Configure(Main.rand.Next(16, 26));
            }
            //向心收流：水被拽进潭心的读法
            int inward = Fast ? 6 : 10;
            for (int i = 0; i < inward; i++) {
                Vector2 pos = originPool + new Vector2(
                    Main.rand.NextFloat(-0.7f, 0.7f) * PoolWidth, Main.rand.NextFloat(-26f, -4f));
                Vector2 vel = (originPool - pos).SafeNormalize(Vector2.Zero)
                    * Main.rand.NextFloat(3f, 6f);
                PRTLoader.NewParticle<PRT_KikasaInkBead>(pos, vel, KikasaInk.InkBody,
                    Main.rand.NextFloat(0.24f, 0.4f))?.Configure(Main.rand.Next(10, 18), 0.1f);
            }
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_KikasaInkMist>(
                    originPool + new Vector2(Main.rand.NextFloat(-24f, 24f), -10f),
                    new Vector2(0f, -Main.rand.NextFloat(0.6f, 1.4f)),
                    KikasaInk.InkDeep, Main.rand.NextFloat(0.9f, 1.3f))?.Configure(Main.rand.Next(24, 36));
            }
            AddRipple(originPool, PoolWidth * 0.45f, PoolWidth * 1.6f, 0.75f);
            if (originGrounded) {
                KikasaInkFX.AddGroundSplat(originPool + Vector2.UnitY * 4f,
                    Vector2.UnitY * 10f, PoolWidth * 0.6f);
            }
            KikasaInk.Play(KikasaInk.InkSplash, originPool, 0.7f, -0.2f);
            SoundEngine.PlaySound(SoundID.Splash with {
                Volume = 0.65f, Pitch = -0.05f, MaxInstances = 3,
            }, originPool);
        }

        /// <summary>喷发拍：彼岸水柱冲天，冠顶炸珠、横扫环冠、墨雾与冲击环，人在柱中现身</summary>
        private void EruptBeat() {
            NotifyLake(destPool);
            if (Main.dedServ) {
                return;
            }
            //喷泉主柱：满屏水花的主力
            int fountain = Fast ? 26 : 44;
            for (int i = 0; i < fountain; i++) {
                Vector2 pos = destPool + new Vector2(
                    Main.rand.NextFloat(-0.3f, 0.3f) * PoolWidth, -4f);
                Vector2 vel = new(Main.rand.NextFloat(-3.2f, 3.2f),
                    -Main.rand.NextFloat(5f, 11.5f));
                Color color = Main.rand.Next(6) switch {
                    0 or 1 => KikasaInk.BloodCore,
                    2 => KikasaInk.WetSheen,
                    _ => KikasaInk.InkDeep,
                };
                PRTLoader.NewParticle<PRT_KikasaInkBead>(pos, vel, color,
                    Main.rand.NextFloat(0.3f, 0.55f))?.Configure(Main.rand.Next(20, 34));
            }
            //环冠横扫：水在腰际炸开一圈
            int crown = Fast ? 9 : 16;
            for (int i = 0; i < crown; i++) {
                float dir = i % 2 == 0 ? 1f : -1f;
                Vector2 pos = destPool + new Vector2(dir * PoolWidth * 0.26f, -6f);
                Vector2 vel = new(dir * Main.rand.NextFloat(3.5f, 7.5f),
                    -Main.rand.NextFloat(2f, 4.5f));
                PRTLoader.NewParticle<PRT_KikasaInkBead>(pos, vel,
                    Main.rand.NextBool(4) ? KikasaInk.WetSheen : KikasaInk.InkDeep,
                    Main.rand.NextFloat(0.24f, 0.4f))?.Configure(Main.rand.Next(16, 26));
            }
            int mist = Fast ? 4 : 6;
            for (int i = 0; i < mist; i++) {
                PRTLoader.NewParticle<PRT_KikasaInkMist>(
                    destPool + new Vector2(Main.rand.NextFloat(-30f, 30f), -Main.rand.NextFloat(6f, 40f)),
                    new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), -Main.rand.NextFloat(0.8f, 1.8f)),
                    Main.rand.NextBool(3) ? KikasaInk.BloodCore : KikasaInk.InkDeep,
                    Main.rand.NextFloat(1f, 1.5f))?.Configure(Main.rand.Next(26, 40));
            }
            for (int i = 0; i < (Fast ? 10 : 18); i++) {
                Dust dust = Dust.NewDustPerfect(
                    destPool + Main.rand.NextVector2Circular(PoolWidth * 0.35f, 8f),
                    DustID.Water, new Vector2(Main.rand.NextFloat(-3f, 3f),
                        -Main.rand.NextFloat(1.5f, 6f)),
                    120, KikasaInk.WetSheen, 1.5f);
                dust.noGravity = false;
            }
            //冲击环连发：大中小三圈错帧铺开
            AddRipple(destPool, PoolWidth * 0.5f, PoolWidth * 1.9f, 0.85f);
            AddRipple(destPool, PoolWidth * 0.32f, PoolWidth * 1.4f, 0.7f);
            AddRipple(destPool, PoolWidth * 0.2f, PoolWidth * 1f, 0.55f);
            if (destGrounded) {
                KikasaInkFX.AddGroundSplat(destPool + Vector2.UnitY * 4f,
                    Vector2.UnitY * 12f, PoolWidth * 0.7f);
            }
            KikasaInk.Play(KikasaInk.InkSplash, destPool, 0.9f, 0.1f);
            KikasaInk.Play(KikasaInk.InkSpray, destPool, 0.5f, -0.25f);
            SoundEngine.PlaySound(SoundID.Splash with {
                Volume = 0.85f, Pitch = 0.18f, MaxInstances = 3,
            }, destPool);
            //冲击拍屏震只震看得见的人
            if (Main.LocalPlayer?.active == true
                && Vector2.Distance(Main.LocalPlayer.Center, destPool) < 1400f) {
                Main.LocalPlayer.CWR()?.GetScreenShake(Fast ? 2f : 3f);
            }
        }

        /// <summary>血湖开着时把真湖也搅动起来：湖线荡波，传送是湖的事</summary>
        private void NotifyLake(Vector2 at) {
            if (Main.dedServ) {
                return;
            }
            KikasaDomainPlayer domain = Owner.GetModPlayer<KikasaDomainPlayer>();
            if (domain.Phase == KikasaDomainPhase.Open) {
                KikasaDomainDeco.RippleAt(new Vector2(at.X, domain.LakeWorldY), 0.75f);
            }
        }

        //==================== 潭、沸腾与柱雨 ====================

        /// <summary>潭锚点：向下找实心地表；悬空则原地作数（浮潭）</summary>
        private static Vector2 AnchorPool(Vector2 basePos, out bool grounded) {
            int x = (int)(basePos.X / 16f);
            int startY = (int)(basePos.Y / 16f);
            int endY = (int)((basePos.Y + AnchorScan) / 16f);
            for (int y = startY; y <= endY; y++) {
                Tile tile = Framing.GetTileSafely(x, y);
                if (tile.HasTile && Main.tileSolid[tile.TileType]
                    && !Main.tileSolidTop[tile.TileType]) {
                    grounded = true;
                    return new Vector2(basePos.X, y * 16f - 2f);
                }
            }
            grounded = false;
            return basePos + new Vector2(0f, 6f);
        }

        private void UpdatePools() {
            //瞬发口径：双潭当帧砸开，三四帧铺满
            float growStep = Fast ? 0.34f : 0.28f;
            originGrow = MathHelper.Clamp(originGrow + growStep, 0f, 1f);
            destGrow = MathHelper.Clamp(destGrow + growStep, 0f, 1f);
            //吞没柱谢幕后此岸收干；彼岸陪到全场尾段
            if (teleported && age >= TeleportFrame + SwallowLife) {
                originDry = MathHelper.Clamp(originDry + 1f / 14f, 0f, 1f);
            }
            int destDryStart = TotalFrames - 12;
            if (age >= destDryStart) {
                destDry = MathHelper.Clamp((age - destDryStart) / 12f, 0f, 1f);
            }
        }

        /// <summary>潭面持续沸腾、柱顶坍缩期滴落回雨：水在整场里都是活的</summary>
        private void UpdateBoilAndRain() {
            if (Main.dedServ) {
                return;
            }
            //潭面沸腾泡
            if (age % 2 == 0) {
                BoilPool(originPool, originGrow * (1f - originDry));
                BoilPool(destPool, destGrow * (1f - destDry));
            }
            //彼岸柱坍缩段：柱顶滴落回雨
            float gLife = GeyserLife01();
            if (teleported && gLife is > 0.5f and < 1f && age % 2 == 0) {
                float h = GeyserHeight * ColumnEnvelope(gLife);
                for (int i = 0; i < 2; i++) {
                    PRTLoader.NewParticle<PRT_KikasaInkDrip>(
                        destPool + new Vector2(Main.rand.NextFloat(-0.4f, 0.4f) * GeyserWidth, -h),
                        new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), Main.rand.NextFloat(0.5f, 1.5f)),
                        KikasaInk.InkBody, Main.rand.NextFloat(0.5f, 0.8f))?.Configure(Main.rand.Next(18, 28));
                }
            }
        }

        private void BoilPool(Vector2 pool, float presence) {
            if (presence < 0.5f || !Main.rand.NextBool(2)) {
                return;
            }
            float xOff = Main.rand.NextFloat(-0.4f, 0.4f) * PoolWidth * presence;
            PRTLoader.NewParticle<PRT_KikasaInkBead>(
                pool + new Vector2(xOff, -3f),
                new Vector2(xOff * 0.012f, -Main.rand.NextFloat(1f, 2.4f)),
                Main.rand.NextBool(3) ? KikasaInk.BloodCore : KikasaInk.InkDeep,
                Main.rand.NextFloat(0.22f, 0.38f))?.Configure(Main.rand.Next(12, 20));
        }

        private void AddRipple(Vector2 pos, float fromW, float toW, float alpha) {
            ripples.Add(new Ripple {
                Pos = pos,
                Life = 0f,
                MaxLife = Fast ? 16f : 22f,
                FromW = fromW,
                ToW = toW,
                Alpha = alpha,
            });
        }

        private void UpdateRipples() {
            for (int i = ripples.Count - 1; i >= 0; i--) {
                Ripple r = ripples[i];
                r.Life++;
                if (r.Life >= r.MaxLife) {
                    ripples.RemoveAt(i);
                    continue;
                }
                ripples[i] = r;
            }
        }

        //==================== 柱与潭的包络 ====================

        /// <summary>彼岸喷发柱生命 0~1，未起为 0、谢幕为 1</summary>
        private float GeyserLife01() {
            if (!teleported) {
                return 0f;
            }
            return MathHelper.Clamp((age - TeleportFrame) / (float)GeyserLife, 0f, 1f);
        }

        /// <summary>此岸吞没柱生命 0~1</summary>
        private float SwallowLife01() {
            if (!teleported) {
                return 0f;
            }
            return MathHelper.Clamp((age - TeleportFrame) / (float)SwallowLife, 0f, 1f);
        }

        /// <summary>柱高包络：猛冲出水（带过冲）→ 呼吸驻留 → 坍缩回潭</summary>
        private static float ColumnEnvelope(float life01) {
            if (life01 <= 0f || life01 >= 1f) {
                return 0f;
            }
            float riseT = MathHelper.Clamp(life01 / 0.2f, 0f, 1f);
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float rise = 1f + c3 * MathF.Pow(riseT - 1f, 3f) + c1 * (riseT - 1f) * (riseT - 1f);
            float collapseT = MathHelper.Clamp((life01 - 0.5f) / 0.5f, 0f, 1f);
            return rise * (1f - collapseT * collapseT);
        }

        //==================== 谢幕与安全 ====================

        /// <summary>无论正常谢幕还是中途夭折（本人死亡/弹幕失效），人都得显回来</summary>
        public override void OnKill(int timeLeft) {
            if (Owner.active) {
                Owner.opacityForAnimation = 1f;
            }
        }

        //==================== 绘制 ====================

        public override bool PreDraw(ref Color lightColor) {
            DrawPool(originPool, originGrow, originDry);
            DrawPool(destPool, destGrow, destDry);
            DrawColumn(originPool, SwallowLife01(), SwallowHeight, GeyserWidth * 0.72f);
            DrawColumn(destPool, GeyserLife01(), GeyserHeight, GeyserWidth);
            DrawRipples();
            return false;
        }

        /// <summary>
        /// 潭：暗缘垫底→墨体→血芯亮线→湿反光，与墨洼同一材质语言但体量放大，
        /// 血芯常亮让潭在暗地上也一眼可见
        /// </summary>
        private void DrawPool(Vector2 pos, float grow, float dry) {
            Texture2D tex = CWRAsset.Extra_98?.Value;
            if (tex == null) {
                return;
            }
            float easeGrow = 1f - (1f - grow) * (1f - grow);
            float wT = easeGrow * (1f - dry * dry);
            if (wT <= 0.03f) {
                return;
            }
            float w = PoolWidth * wT;
            float wob = 1f + MathF.Sin(age * 0.16f + pos.X * 0.013f) * 0.07f;
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 p = pos - Main.screenPosition;

            Main.EntitySpriteDraw(tex, p, null, KikasaInk.InkDeep * 0.8f, 0f, origin,
                new Vector2(w * 1.2f / tex.Width, PoolDepth * 1.6f / tex.Height * wob),
                SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, p, null, KikasaInk.InkBody, 0f, origin,
                new Vector2(w / tex.Width, PoolDepth / tex.Height * wob),
                SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, p + new Vector2(0f, -2f), null,
                KikasaInk.BloodCore * 0.85f, 0f, origin,
                new Vector2(w * 0.62f / tex.Width, 6f / tex.Height * wob),
                SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, p + new Vector2(0f, -3f), null,
                (KikasaInk.WetSheen with { A = 0 }) * (0.5f * wT * wob), 0f, origin,
                new Vector2(w * 0.34f / tex.Width, 3f / tex.Height),
                SpriteEffects.None, 0);
        }

        /// <summary>
        /// 水柱：软缘竖条三层（暗缘/墨体/血芯）+ 冠顶隆包收口，
        /// 底端坐进潭里、顶端有冠、坍缩回潭，两端都有名字
        /// </summary>
        private void DrawColumn(Vector2 basePos, float life01, float heightMax, float widthMax) {
            float env = ColumnEnvelope(life01);
            if (env <= 0.02f) {
                return;
            }
            Texture2D tex = CWRAsset.Extra_98?.Value;
            if (tex == null) {
                return;
            }
            float h = heightMax * env;
            float breath = 1f + MathF.Sin(age * 0.5f + basePos.X * 0.01f) * 0.07f;
            float collapse = MathHelper.Clamp((life01 - 0.5f) / 0.5f, 0f, 1f);
            float w = widthMax * (0.8f + 0.2f * env) * (1f - collapse * 0.45f) * breath;
            Vector2 baseP = basePos - Main.screenPosition + new Vector2(0f, 2f);
            //底锚原点：柱从潭里长出来
            Vector2 origin = new(tex.Width * 0.5f, tex.Height);

            Main.EntitySpriteDraw(tex, baseP, null, KikasaInk.InkDeep * 0.6f, 0f, origin,
                new Vector2(w * 1.35f / tex.Width, h * 1.04f / tex.Height),
                SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, baseP, null, KikasaInk.InkBody * 0.95f, 0f, origin,
                new Vector2(w / tex.Width, h / tex.Height), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, baseP, null, KikasaInk.BloodCore * 0.6f, 0f, origin,
                new Vector2(w * 0.34f / tex.Width, h * 0.92f / tex.Height),
                SpriteEffects.None, 0);
            //湿反光挂在迎光侧，随呼吸摆
            float sheenX = MathF.Sin(age * 0.35f + basePos.X * 0.02f) * w * 0.18f;
            Main.EntitySpriteDraw(tex, baseP + new Vector2(sheenX + w * 0.14f, 0f), null,
                (KikasaInk.WetSheen with { A = 0 }) * 0.4f, 0f, origin,
                new Vector2(w * 0.14f / tex.Width, h * 0.85f / tex.Height),
                SpriteEffects.None, 0);

            //冠顶隆包：柱顶的收口，坍缩时摊回潭面
            Vector2 crownP = baseP - new Vector2(0f, h - 2f);
            float crownW = w * (1.5f + collapse * 0.8f);
            Vector2 cOrigin = tex.Size() * 0.5f;
            Main.EntitySpriteDraw(tex, crownP, null, KikasaInk.InkBody * (0.9f * (1f - collapse * 0.4f)),
                0f, cOrigin, new Vector2(crownW / tex.Width, 10f / tex.Height),
                SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, crownP + new Vector2(0f, -2f), null,
                (KikasaInk.WetSheen with { A = 0 }) * (0.45f * (1f - collapse)), 0f, cOrigin,
                new Vector2(crownW * 0.55f / tex.Width, 3.2f / tex.Height),
                SpriteEffects.None, 0);
        }

        /// <summary>水纹圈：亮线在上、暗线垫底的扁椭圆扩散</summary>
        private void DrawRipples() {
            Texture2D tex = CWRAsset.Extra_98?.Value;
            if (tex == null) {
                return;
            }
            Vector2 origin = tex.Size() * 0.5f;
            foreach (Ripple r in ripples) {
                float t = r.Life / r.MaxLife;
                float w = MathHelper.Lerp(r.FromW, r.ToW, 1f - (1f - t) * (1f - t));
                float alpha = (1f - t) * r.Alpha;
                Vector2 p = r.Pos - Main.screenPosition;
                Main.EntitySpriteDraw(tex, p + new Vector2(0f, 1f), null,
                    KikasaInk.InkDeep * (alpha * 0.8f), 0f, origin,
                    new Vector2(w * 0.92f / tex.Width, 2.6f / tex.Height),
                    SpriteEffects.None, 0);
                Main.EntitySpriteDraw(tex, p, null,
                    (KikasaInk.WetSheen with { A = 0 }) * alpha, 0f, origin,
                    new Vector2(w / tex.Width, 3.2f / tex.Height), SpriteEffects.None, 0);
            }
        }
    }
}
