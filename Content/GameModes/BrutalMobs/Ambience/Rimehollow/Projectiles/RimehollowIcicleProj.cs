using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Rimehollow.Projectiles
{
    /// <summary>
    /// 「冰锥垂生」：洞顶冰面下缓慢生长的可见冰锥。ai[0]=相位 ai[1]=相位计时 ai[2]=档位*10+体型。
    /// 生命周期：生长（小→中→大三阶段，全程可见可预判）→ 成熟悬持 →
    /// 抖动预告（50 帧，抖动+碎冰声双通道）→ 坠落（仅此窗口有伤害）→ 触地碎裂+冰屑余韵；
    /// 无人问津的成熟冰锥最终自行消融。
    /// 与 Hollowdeep 的声响触发落石划界：这里是时间生长的悬顶威胁，
    /// 成熟前永远无害，成熟后由"正下方通过/附近战斗声响"触发。
    /// 联机：权威端只决策"成熟→抖动/消融"（netUpdate），
    /// 其余相位推进与坠落物理各端确定性自走；伤害值全程满值不清零，
    /// 判定开关走各端本地按相位重算的 hostile
    /// </summary>
    internal class RimehollowIcicleProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.DeerclopsIceSpike;

        private const int PhaseGrow = 0;
        private const int PhaseMature = 1;
        private const int PhaseShake = 2;
        private const int PhaseFall = 3;
        private const int PhaseMelt = 4;

        /// <summary>生长帧数，档位只调生长速度（约 50/40/30 秒）</summary>
        private static readonly int[] GrowFramesByTier = [3000, 2400, 1800];
        /// <summary>抖动预告帧数（公平契约 ≥45，各档位一律不缩短）</summary>
        private const int ShakeFrames = 50;
        private const int MeltFrames = 46;
        /// <summary>成熟后无人问津的悬持上限（60 秒），到点自行消融</summary>
        private const int MatureWaitMax = 3600;
        /// <summary>成熟后至少悬持这么久才接受触发（刚成熟不偷袭）</summary>
        private const int MatureSettleMin = 40;

        /// <summary>正下方通过判定半宽（像素）</summary>
        private const float PassUnderHalfW = 30f;
        /// <summary>正下方判定的最大垂距</summary>
        private const float PassUnderMaxDrop = 780f;
        /// <summary>战斗声响触发半径（挥击中的玩家）</summary>
        private const float CombatNoiseRange = 340f;
        /// <summary>战斗声响触发半径（友方弹幕）</summary>
        private const float CombatProjRange = 300f;

        private const float FallGravity = 0.36f;
        private const float FallMaxSpeed = 17f;
        private static readonly float[] VariantScale = [0.8f, 1f, 1.2f];

        private int Phase { get => (int)Projectile.ai[0]; set => Projectile.ai[0] = value; }
        private float Timer { get => Projectile.ai[1]; set => Projectile.ai[1] = value; }
        private int Tier => Math.Clamp((int)Projectile.ai[2] / 10, 1, 3);
        private int Variant => Math.Clamp((int)Projectile.ai[2] % 10, 0, 2);
        private float SizeScale => VariantScale[Variant];

        private float GrowProgress => Phase == PhaseGrow
            ? Math.Min(Timer / GrowFramesByTier[Tier - 1], 1f) : 1f;
        /// <summary>生长阶段 0 小 / 1 中 / 2 大（宽度按阶跳变，读得出"长了一圈"）</summary>
        private int Stage => GrowProgress < 0.34f ? 0 : GrowProgress < 0.68f ? 1 : 2;
        /// <summary>体长（像素）。绘制、判定、触发共用同一几何</summary>
        private float CurLenPx => SizeScale * (24f + 66f * GrowProgress);
        private float BodyWidthScale => (0.5f + 0.22f * Stage) * SizeScale;
        /// <summary>锥尖世界坐标（Center 恒为根部锚点，含坠落中）</summary>
        private Vector2 TipPos => Projectile.Center + new Vector2(0f, CurLenPx);

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 320;

        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.hostile = false;//坠落窗口内由各端按相位本地开启
            Projectile.friendly = false;
            Projectile.tileCollide = false;//触地判定手工做（根锚点在上，箱体碰撞会让锥尖插地）
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 36000;//漏网保险，正常生命周期由相位机管理
            Projectile.netImportant = true;
            Projectile.coldDamage = true;
        }

        /// <summary>只有坠落相移动</summary>
        public override bool ShouldUpdatePosition() => Phase == PhaseFall;

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                Projectile.rotation = MathHelper.PiOver2;
                Projectile.frame = Projectile.identity % 5;
                Projectile.localAI[1] = Phase;
                Projectile.localAI[2] = Stage;
            }

            //相位入场演出：本地推进与同步包驱动都会经过这里，且只随前进播一次
            int seenPhase = (int)Projectile.localAI[1];
            if (Phase > seenPhase) {
                Projectile.localAI[1] = Phase;
                OnPhaseEntered(Phase);
            }

            //伤害窗与相位严格对齐：伤害值全程满值，开关本地重算
            Projectile.hostile = Phase == PhaseFall && Timer > 2f;

            switch (Phase) {
                case PhaseGrow:
                    TickGrow();
                    break;
                case PhaseMature:
                    TickMature();
                    break;
                case PhaseShake:
                    TickShake();
                    break;
                case PhaseFall:
                    TickFall();
                    break;
                default:
                    TickMelt();
                    break;
            }

            //冷光微亮：全程可见可预判的底线，在黑暗洞穴里也找得到它
            float glow = 0.05f + 0.06f * GrowProgress + (Phase == PhaseShake ? 0.05f : 0f);
            Lighting.AddLight(Projectile.Center + new Vector2(0f, CurLenPx * 0.5f),
                new Vector3(0.5f, 0.8f, 1.1f) * glow * SizeScale);
        }

        private void TickGrow() {
            Timer++;

            //阶段跨越：清脆一声凝晶+根部霜屑，"长了一圈"看得见听得见
            int stage = Stage;
            if (stage != (int)Projectile.localAI[2]) {
                Projectile.localAI[2] = stage;
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Item27 with {
                        Volume = 0.16f, Pitch = 0.55f, MaxInstances = 4
                    }, Projectile.Center);
                    for (int i = 0; i < 3; i++) {
                        Dust dust = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(6f, 3f),
                            DustID.Frost, new Vector2(0f, Main.rand.NextFloat(0.3f, 0.8f)), 130, default, 0.8f);
                        dust.noGravity = true;
                    }
                }
            }

            //锥尖凝露（低频滴落感，屏外不花预算）
            if (!Main.dedServ && Main.rand.NextBool(26) && RimehollowAmbience.NearScreen(Projectile.Center)) {
                Dust drip = Dust.NewDustPerfect(TipPos, DustID.Frost,
                    new Vector2(0f, Main.rand.NextFloat(0.4f, 1f)), 150, default, 0.7f);
                drip.noGravity = true;
            }

            //生长推进各端确定性自走；权威端顺带看护锚点
            if (Timer >= GrowFramesByTier[Tier - 1]) {
                Phase = PhaseMature;
                Timer = 0f;
            }
            if (Main.netMode != NetmodeID.MultiplayerClient && (int)Timer % 10 == 0 && AnchorBroken()) {
                Projectile.Kill();//未成熟就断根：只碎不落，无伤害
            }
        }

        private void TickMature() {
            Timer++;

            //成熟冰锥的内芯微光呼吸（悬顶威胁的存在感，屏外不花预算）
            if (!Main.dedServ && Main.rand.NextBool(80) && RimehollowAmbience.NearScreen(Projectile.Center)) {
                Dust shimmer = Dust.NewDustPerfect(
                    Projectile.Center + new Vector2(Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(0f, CurLenPx * 0.7f)),
                    DustID.Ice, Vector2.Zero, 160, default, 0.6f);
                shimmer.noGravity = true;
            }

            //触发决策只在权威端：正下方通过 / 附近战斗声响 / 锚点被挖断
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                return;
            }
            if (AnchorBroken()) {
                //挖断路径与常规触发共用同一道伤害门：门外只碎不伤（走 OnKill 的碎散分支），视觉照常
                if (HazardGateOpen()) {
                    EnterShake();
                }
                else {
                    Projectile.Kill();
                }
                return;
            }
            if (Timer >= MatureSettleMin && (int)Timer % 5 == 0
                && HazardGateOpen() && TriggerScan()) {
                EnterShake();
                return;
            }
            if (Timer > MatureWaitMax) {
                Phase = PhaseMelt;
                Timer = 0f;
                Projectile.netUpdate = true;
            }
        }

        private void EnterShake() {
            Phase = PhaseShake;
            Timer = 0f;
            Projectile.netUpdate = true;
        }

        private void TickShake() {
            Timer++;

            //根部冰屑簌簌而落（视觉通道；声音通道在相位入场与中点）
            if (!Main.dedServ) {
                if (Main.rand.NextBool(3) && RimehollowAmbience.NearScreen(Projectile.Center)) {
                    Dust crumb = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(7f, 3f),
                        DustID.Ice, new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), Main.rand.NextFloat(0.8f, 1.8f)),
                        90, default, Main.rand.NextFloat(0.7f, 1.1f));
                    crumb.noGravity = false;
                }
                if ((int)Timer == ShakeFrames * 3 / 5) {
                    SoundEngine.PlaySound(SoundID.Item27 with {
                        Volume = 0.5f, Pitch = -0.05f, MaxInstances = 4
                    }, Projectile.Center);
                }
            }

            //抖满即落：确定性转坠落，各端自走（速度从零起步由 TickFall 积分）
            if (Timer >= ShakeFrames) {
                Phase = PhaseFall;
                Timer = 0f;
            }
        }

        private void TickFall() {
            Timer++;
            Projectile.velocity.Y = Math.Min(Projectile.velocity.Y + FallGravity, FallMaxSpeed);

            //坠落拉丝（屏外不花预算）
            if (!Main.dedServ && Main.rand.NextBool(3) && RimehollowAmbience.NearScreen(Projectile.Center)) {
                Dust streak = Dust.NewDustPerfect(
                    Projectile.Center + new Vector2(Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(0f, CurLenPx)),
                    DustID.Frost, new Vector2(0f, -Projectile.velocity.Y * 0.2f), 140, default, 0.8f);
                streak.noGravity = true;
            }

            //触地/触液判定看锥尖（各端确定性；瓦片是同步世界态）
            Point tip = TipPos.ToTileCoordinates();
            if (!WorldGen.InWorld(tip.X, tip.Y, 10)) {
                Projectile.Kill();
                return;
            }
            if (WorldGen.SolidTile(tip.X, tip.Y) || Framing.GetTileSafely(tip.X, tip.Y).LiquidAmount > 64) {
                Projectile.Kill();
            }
        }

        private void TickMelt() {
            Timer++;
            if (!Main.dedServ && Main.rand.NextBool(4)) {
                Dust melt = Dust.NewDustPerfect(
                    Projectile.Center + new Vector2(Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(0f, CurLenPx)),
                    DustID.Frost, new Vector2(0f, Main.rand.NextFloat(0.5f, 1.2f)), 160, default, 0.7f);
                melt.noGravity = true;
            }
            //消融收尾由权威端裁决（客户端只演不杀）
            if (Main.netMode != NetmodeID.MultiplayerClient && Timer >= MeltFrames) {
                Projectile.Kill();
            }
        }

        /// <summary>
        /// 伤害门（全局公约）：残酷模式开启、无 Boss 在场、城镇约 60 格外，
        /// 才允许成熟冰锥进入抖动坠落；挖断锚点与常规触发共用这一道门
        /// </summary>
        private bool HazardGateOpen()
            => GameModeSystem.BrutalActive && !CWRWorld.HasBoss
                && !RimehollowAmbience.TownCalmNear(Projectile.Center);

        /// <summary>锚点看护：头顶的冰面瓦片被挖掉即断根</summary>
        private bool AnchorBroken() {
            Point anchor = (Projectile.Center + new Vector2(0f, -8f)).ToTileCoordinates();
            if (!WorldGen.InWorld(anchor.X, anchor.Y, 10)) {
                return true;
            }
            return !WorldGen.SolidTile(anchor.X, anchor.Y);
        }

        /// <summary>触发扫描：正下方通过，或附近有战斗声响（挥击中的玩家/友方弹幕）</summary>
        private bool TriggerScan() {
            Vector2 tip = TipPos;
            foreach (Player player in Main.ActivePlayers) {
                if (player.dead) {
                    continue;
                }
                if (Math.Abs(player.Center.X - Projectile.Center.X) < PassUnderHalfW
                    && player.Center.Y > tip.Y
                    && player.Center.Y - tip.Y < PassUnderMaxDrop
                    && Collision.CanHitLine(tip, 4, 4, player.Center, 4, 4)) {
                    return true;
                }
                if (player.itemAnimation > 0 && player.HeldItem.damage > 0
                    && player.Distance(Projectile.Center) < CombatNoiseRange) {
                    return true;
                }
            }
            foreach (Projectile proj in Main.ActiveProjectiles) {
                //召唤物/哨兵及其派生弹幕不算声源：站桩仆从的常态输出不该整夜吵醒冰锥；
                //玩家亲手挥击/射击仍算（悬顶威胁对主动吵闹者更凶）
                if (!proj.friendly || proj.damage <= 0 || proj.npcProj
                    || proj.minion || proj.sentry
                    || ProjectileID.Sets.MinionShot[proj.type] || ProjectileID.Sets.SentryShot[proj.type]) {
                    continue;
                }
                if (proj.Distance(Projectile.Center) < CombatProjRange) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>相位入场演出（声音通道；各端只随相位前进播一次，同步包驱动也能命中）</summary>
        private void OnPhaseEntered(int phase) {
            if (Main.dedServ) {
                return;
            }
            switch (phase) {
                case PhaseMature:
                    SoundEngine.PlaySound(SoundID.Item27 with {
                        Volume = 0.2f, Pitch = 0.4f, MaxInstances = 4
                    }, Projectile.Center);
                    break;
                case PhaseShake:
                    //碎冰声预告：抖动的听觉通道
                    SoundEngine.PlaySound(SoundID.Item27 with {
                        Volume = 0.62f, Pitch = -0.18f, MaxInstances = 4
                    }, Projectile.Center);
                    for (int i = 0; i < 5; i++) {
                        Dust dust = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(8f, 4f),
                            DustID.Ice, new Vector2(Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(0.5f, 1.6f)),
                            100, default, Main.rand.NextFloat(0.8f, 1.2f));
                        dust.noGravity = false;
                    }
                    break;
                case PhaseFall:
                    //断根脆响
                    SoundEngine.PlaySound(SoundID.Item27 with {
                        Volume = 0.5f, Pitch = 0.12f, MaxInstances = 4
                    }, Projectile.Center);
                    for (int i = 0; i < 4; i++) {
                        Dust dust = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(6f, 3f),
                            DustID.Frost, new Vector2(Main.rand.NextFloat(-0.8f, 0.8f), -Main.rand.NextFloat(0.3f, 1f)),
                            120, default, 0.9f);
                        dust.noGravity = true;
                    }
                    break;
            }
        }

        /// <summary>坠落中沿锥身取样命中（锥尖也有效；可见体=判定体）</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (Phase != PhaseFall) {
                return false;
            }
            float len = CurLenPx;
            float radius = 6f + 4f * SizeScale;
            for (int i = 0; i <= 3; i++) {
                Vector2 point = Projectile.Center + new Vector2(0f, len * i / 3f);
                Rectangle sample = Utils.CenteredRectangle(point, new Vector2(radius * 2f));
                if (sample.Intersects(targetHitbox)) {
                    return true;
                }
            }
            return false;
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            //微量伤害之外附带短暂原版寒颤（命中方本机结算，原生同步）
            target.AddBuff(BuffID.Chilled, 150);
        }

        public override bool PreDraw(ref Color lightColor) {
            float len = CurLenPx;
            float meltT = Phase == PhaseMelt ? MathHelper.Clamp(Timer / MeltFrames, 0f, 1f) : 0f;
            float alpha = 1f - meltT;
            if (alpha <= 0.02f) {
                return false;
            }
            //消融期锥体缩短回吸
            len *= 1f - meltT * 0.3f;

            //抖动预告：位移抖动随进度加剧（视觉通道）
            float jitterX = 0f;
            if (Phase == PhaseShake) {
                jitterX = MathF.Sin(Timer * 1.9f) * (0.6f + 2.2f * Timer / ShakeFrames);
            }

            Main.instance.LoadProjectile(ProjectileID.DeerclopsIceSpike);
            Texture2D tex = TextureAssets.Projectile[ProjectileID.DeerclopsIceSpike].Value;
            Rectangle rect = tex.Frame(1, 5, 0, Projectile.frame);
            Vector2 root = Projectile.Center + new Vector2(jitterX, 0f) - Main.screenPosition;

            //底衬：真 alpha 梭形冷鞘（承实体感，让冰锥在暗处也有剪影）
            Texture2D under = CWRAsset.Extra_98.Value;
            Vector2 underScale = new(BodyWidthScale * 0.8f, len / (under.Height * 0.58f));
            Color underColor = new Color(120, 170, 210) * (0.32f * alpha);
            Main.EntitySpriteDraw(under, root + new Vector2(0f, len * 0.5f), null, underColor,
                0f, under.Size() / 2f, underScale, SpriteEffects.None, 0);

            //本体：原版巨鹿冰刺贴图旋转向下垂挂，沿轴随生长伸长
            float axisLen = MathF.Max(rect.Width - 18f, 40f);
            float scaleX = len / axisLen;
            //坠落速度拉伸（运动各向异性）
            if (Phase == PhaseFall) {
                scaleX *= 1f + Projectile.velocity.Y / 46f;
            }
            Vector2 scaleVec = new(scaleX, BodyWidthScale);
            Vector2 origin = new(16f, rect.Height / 2f);
            SpriteEffects flip = (Projectile.identity & 1) == 0 ? SpriteEffects.None : SpriteEffects.FlipVertically;
            Color body = Color.Lerp(Projectile.GetAlpha(lightColor), new Color(198, 232, 255), 0.22f) * alpha;
            Main.EntitySpriteDraw(tex, root, rect, body, MathHelper.PiOver2, origin, scaleVec, flip, 0);

            //根部凝晶冷光（A=0 加色敷料）
            Texture2D glow = CWRAsset.SoftGlow.Value;
            float pulse = 0.75f + 0.25f * MathF.Sin(Main.GlobalTimeWrappedHourly * 5f + Projectile.identity);
            Color rootGlow = new Color(150, 210, 255, 0) * (0.2f * alpha * pulse);
            Main.EntitySpriteDraw(glow, root, null, rootGlow, 0f, glow.Size() / 2f,
                new Vector2(0.5f, 0.3f) * SizeScale, SpriteEffects.None, 0);

            //成熟后的锥尖水光：一颗将坠未坠的亮点（悬顶威胁的读点）
            if (Phase is PhaseMature or PhaseShake) {
                float tipPulse = 0.5f + 0.5f * MathF.Sin(Main.GlobalTimeWrappedHourly * 7f + Projectile.identity * 2f);
                Color tipGlow = new Color(190, 235, 255, 0) * (0.22f * tipPulse * alpha);
                Main.EntitySpriteDraw(glow, root + new Vector2(0f, len), null, tipGlow, 0f,
                    glow.Size() / 2f, 0.16f * SizeScale + 0.06f * tipPulse, SpriteEffects.None, 0);
            }
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            if (Phase == PhaseFall) {
                //坠地碎裂：脆响+径向冰屑，随后是慢漂的冰雾余韵（活得比弹幕久）
                Vector2 tip = TipPos;
                SoundEngine.PlaySound(SoundID.Item27 with {
                    Volume = 0.85f, Pitch = -0.2f, MaxInstances = 4
                }, tip);
                SoundEngine.PlaySound(SoundID.Shatter with {
                    Volume = 0.3f, Pitch = 0.25f, MaxInstances = 3
                }, tip);
                for (int i = 0; i < 12; i++) {
                    Dust shard = Dust.NewDustPerfect(tip + Main.rand.NextVector2Circular(10f, 4f),
                        DustID.Ice, new Vector2(Main.rand.NextFloat(-2.6f, 2.6f), -Main.rand.NextFloat(0.5f, 3.4f)) * SizeScale,
                        80, default, Main.rand.NextFloat(0.9f, 1.5f));
                    shard.noGravity = false;
                }
                for (int i = 0; i < 5; i++) {
                    Dust mist = Dust.NewDustPerfect(tip + Main.rand.NextVector2Circular(14f, 6f),
                        DustID.Frost, new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), -Main.rand.NextFloat(0.2f, 0.7f)),
                        150, default, Main.rand.NextFloat(1f, 1.3f));
                    mist.noGravity = true;
                }
            }
            else if (Phase == PhaseMelt) {
                //消融：几滴无声的冷凝
                for (int i = 0; i < 4; i++) {
                    Dust drip = Dust.NewDustPerfect(Projectile.Center + new Vector2(Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(0f, CurLenPx * 0.6f)),
                        DustID.Frost, new Vector2(0f, Main.rand.NextFloat(0.6f, 1.4f)), 160, default, 0.7f);
                    drip.noGravity = true;
                }
            }
            else {
                //断根碎散（未成熟）：轻响+少量冰屑，不构成威胁
                SoundEngine.PlaySound(SoundID.Tink with {
                    Volume = 0.3f, Pitch = 0.2f, MaxInstances = 4
                }, Projectile.Center);
                for (int i = 0; i < 6; i++) {
                    Dust dust = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(6f, 4f),
                        DustID.Ice, new Vector2(Main.rand.NextFloat(-1.2f, 1.2f), Main.rand.NextFloat(0.5f, 2f)),
                        100, default, Main.rand.NextFloat(0.7f, 1.1f));
                    dust.noGravity = false;
                }
            }
        }
    }
}
