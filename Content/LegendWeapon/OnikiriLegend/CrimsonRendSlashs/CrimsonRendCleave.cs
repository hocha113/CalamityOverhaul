using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniAnnihilates;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using CSR = CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.CrimsonRendSlashs.CrimsonSlashRenderer;
using SlashDef = CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.CrimsonRendSlashs.CrimsonSlashRenderer.SlashDef;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.CrimsonRendSlashs
{
    /// <summary>断斩来源风格，决定墨相/粒子/余痕；随弹幕 extraAI 同步，远端不猜持有者铭刻</summary>
    internal enum CleaveStyle : byte
    {
        /// <summary>素断斩</summary>
        Plain,
        /// <summary>狮子之子「狮势」合颚刃波：金铁共振，旧金钢屑</summary>
        LionJaw,
        /// <summary>友切「咎影」延迟斩影：暗酒红错位残像，滞拍后咬合</summary>
        GuiltEcho,
        /// <summary>倶利伽罗「龙火回环」：深红龙火缠刃，断火留烟</summary>
        KurikaraLoop,
        /// <summary>谢樋「剪落」：了结溅花小剪刃（不得再触发剪落）</summary>
        PetalPrune,
    }

    /// <summary>
    /// 绯红裂空·断斩,独立直线斩击,世界锚定不跟玩家<br/>
    /// 单发或经 <see cref="FireCross"/> 成对交叉;铭刻副斩共用载体(<see cref="CleaveStyle"/>),
    /// 不产生任何气力/架势回调<br/>
    /// ai[0]=刃方向角(弧度) ai[1]=扫掠镜像(±1) ai[2]=尺寸倍率
    /// </summary>
    internal class CrimsonRendCleave : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int Lifetime = 26;

        private SlashDef def;
        private bool initialized;
        private int timer;
        /// <summary>来源风格(extraAI 同步)</summary>
        private CleaveStyle style;
        /// <summary>咎影滞拍余量(extraAI 同步)，>0 时残像静止读秒、无伤害</summary>
        private int delayFrames;
        private int delayTotal;
        private bool fireSoundPlayed;

        private float BladeAngle => Projectile.ai[0];
        private float Flip => Projectile.ai[1] < 0f ? -1f : 1f;
        private float SizeMul => Projectile.ai[2] > 0.05f ? Projectile.ai[2] : 1f;

        /// <summary>持有者客户端调用,世界锚定于 center</summary>
        /// <param name="center">生成后不追踪</param>
        /// <param name="flip">扫掠镜像 ±1</param>
        /// <param name="source">null 则 Misc 源</param>
        /// <param name="style">来源风格,决定墨相与粒子</param>
        /// <param name="delayFrames">滞拍帧数(咎影),期间残像静止无伤害</param>
        public static Projectile Fire(Player player, Vector2 center, float bladeAngle, int damage, float knockback,
            float scale = 1f, int flip = 1, IEntitySource source = null,
            CleaveStyle style = CleaveStyle.Plain, int delayFrames = 0) {
            source ??= player.GetSource_Misc("CWR_CrimsonRendCleave");
            Projectile proj = Projectile.NewProjectileDirect(source, center, Vector2.Zero
                , ModContent.ProjectileType<CrimsonRendCleave>(), damage, knockback, player.whoAmI
                , ai0: MathHelper.WrapAngle(bladeAngle), ai1: flip, ai2: scale);
            if (proj.ModProjectile is CrimsonRendCleave cleave) {
                cleave.style = style;
                cleave.delayFrames = cleave.delayTotal = Math.Max(delayFrames, 0);
                proj.netUpdate = true;
            }
            return proj;
        }

        /// <summary>成对交叉(X 型),基准角 ± halfSpread,扫掠方向相对</summary>
        public static void FireCross(Player player, Vector2 center, float aimAngle, float halfSpread,
            int damage, float knockback, float scale = 1f, IEntitySource source = null,
            CleaveStyle style = CleaveStyle.Plain) {
            Fire(player, center, aimAngle - halfSpread, damage, knockback, scale, 1, source, style);
            Fire(player, center, aimAngle + halfSpread, damage, knockback, scale, -1, source, style);
        }

        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.aiStyle = -1;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Lifetime + 2;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 40;   //单发只结算一次
        }

        public override void SendExtraAI(BinaryWriter writer) {
            writer.Write((byte)style);
            writer.Write((byte)Math.Clamp(delayFrames, 0, byte.MaxValue));
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            style = (CleaveStyle)reader.ReadByte();
            int delay = reader.ReadByte();
            if (!initialized) {
                delayFrames = delayTotal = delay;
            }
        }

        public override bool ShouldUpdatePosition() => false;

        private void Initialize() {
            initialized = true;
            float s = SizeMul;
            def = new SlashDef {
                Birth = 0, SweepFrames = 3, Life = Lifetime, ErodeStart = 7, ErodeFrames = 15,
                ColorShiftDelay = 9, ColorShiftFrames = 11, DamageStart = 0, DamageEnd = 8,
                Mode = 1f, Rot = BladeAngle, Span = 0f, Thick = 0.34f,
                HalfX = 235f * s, HalfY = 128f * s, Flip = Flip,
                Opacity = 0.95f, FrontGlow = 2.7f, OffsetAlongAim = 0f, Seed = Projectile.whoAmI * 0.173f % 1f,
                TailErode = 0.55f, FlashPower = 0.75f, FarDim = 0f,
                Ink = 0.45f, FeiBai = 0.45f, Bleed = 0.12f, SplitTail = 0.60f,
            };
            //风格调相:茎铭副斩不复制主斩的完整声势
            switch (style) {
                case CleaveStyle.LionJaw:
                    //金铁共振:更利更亮,裂尾撕大
                    def.FrontGlow = 3.0f;
                    def.SplitTail = 0.78f;
                    def.FeiBai = 0.55f;
                    break;
                case CleaveStyle.GuiltEcho:
                    //暗酒红:亮芯压低,洇血更重,读作"不该有的一刀"
                    def.FrontGlow = 1.6f;
                    def.Opacity = 0.85f;
                    def.Ink = 0.60f;
                    def.Bleed = 0.35f;
                    def.FeiBai = 0.30f;
                    def.FlashPower = 0.40f;
                    break;
                case CleaveStyle.KurikaraLoop:
                    //龙火:洇边重+裂尾满,烟在余痕期补
                    def.SplitTail = 0.95f;
                    def.Bleed = 0.50f;
                    def.FrontGlow = 2.4f;
                    def.Thick = 0.38f;
                    break;
                case CleaveStyle.PetalPrune:
                    //剪落:薄刃碎花,声势压低
                    def.FrontGlow = 1.8f;
                    def.Opacity = 0.80f;
                    def.Ink = 0.50f;
                    def.Bleed = 0.22f;
                    def.FlashPower = 0.45f;
                    def.HalfX = 160f * s;
                    def.HalfY = 90f * s;
                    break;
            }
        }

        private void PlayFireSound() {
            if (fireSoundPlayed) {
                return;
            }
            fireSoundPlayed = true;
            switch (style) {
                case CleaveStyle.LionJaw:
                    SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.15f, Volume = 0.55f }, Projectile.Center);
                    SoundEngine.PlaySound(CWRSound.KatanaSwing with { Pitch = -0.5f, Volume = 0.5f, MaxInstances = 2 }, Projectile.Center);
                    break;
                case CleaveStyle.GuiltEcho:
                    SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.62f, Volume = 0.38f }, Projectile.Center);
                    break;
                case CleaveStyle.KurikaraLoop:
                    SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.05f, Volume = 0.5f }, Projectile.Center);
                    SoundEngine.PlaySound(SoundID.Item34 with { Pitch = 0.2f, Volume = 0.4f, MaxInstances = 2 }, Projectile.Center);
                    break;
                case CleaveStyle.PetalPrune:
                    SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.45f, Volume = 0.32f }, Projectile.Center);
                    break;
                default:
                    SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.5f, Volume = 0.5f }, Projectile.Center);
                    break;
            }
        }

        public override void AI() {
            if (!initialized) {
                Initialize();
                if (delayFrames <= 0) {
                    PlayFireSound();
                }
            }

            //咎影滞拍:错位残像静止读秒,时间轴与伤害都不推进
            if (delayFrames > 0) {
                delayFrames--;
                Projectile.timeLeft = Lifetime + 2;
                if (delayFrames == 0) {
                    PlayFireSound();
                    if (!Main.dedServ) {
                        //两半咬合的一瞬:发丝裂缝亮芯
                        PRTLoader.NewParticle<PRT_CrimsonHitFlash>(Projectile.Center, Vector2.Zero
                            , new Color(255, 220, 205), 0.8f * SizeMul);
                    }
                }
                else if (!Main.dedServ && Main.rand.NextBool(4)) {
                    //残像期簌簌掉碎墨
                    PRTLoader.NewParticle<PRT_OniInkDrop>(
                        Projectile.Center + BladeAngle.ToRotationVector2() * Main.rand.NextFloat(-160f, 160f) * SizeMul
                        , Vector2.UnitY * Main.rand.NextFloat(0.4f, 1.2f), new Color(64, 16, 22)
                        , Main.rand.NextFloat(0.16f, 0.28f) * SizeMul)
                        ?.Configure(Main.rand.Next(14, 22));
                }
                Lighting.AddLight(Projectile.Center, new Vector3(0.35f, 0.07f, 0.06f));
                return;
            }
            timer++;

            //张开瞬间轻确认
            if (timer == def.SweepFrames + 1) {
                CrimsonImpactFX.PushImpact(Projectile.Center, style == CleaveStyle.LionJaw ? 0.26f : 0.2f);
                if (!Main.dedServ) {
                    Vector2 tip = CSR.PointAt(in def, Projectile.Center, 0.94f, timer);
                    SpawnSnapBurst(tip);
                }
            }

            //扫掠前缘火花(按风格换介质)
            if (!Main.dedServ && timer <= def.SweepFrames + 1) {
                float edgeU = MathHelper.Clamp(CSR.Sweep(in def, timer) * 1.05f, 0.06f, 0.94f);
                Vector2 pos = CSR.PointAt(in def, Projectile.Center, edgeU, timer);
                Vector2 tangent = (CSR.PointAt(in def, Projectile.Center, MathHelper.Clamp(edgeU + 0.03f, 0f, 1f), timer) - pos)
                    .SafeNormalize(BladeAngle.ToRotationVector2());
                SpawnSweepMedium(pos, tangent);
            }

            //龙火余痕:侵蚀期沿刃断火留烟,烟比刀光多活十余帧
            if (!Main.dedServ && style == CleaveStyle.KurikaraLoop
                && timer > def.ErodeStart && timer % 2 == 0) {
                Vector2 pos = CSR.PointAt(in def, Projectile.Center, Main.rand.NextFloat(0.1f, 0.9f), timer);
                PRTLoader.NewParticle<PRT_CrimsonSmoke>(pos, -Vector2.UnitY * Main.rand.NextFloat(0.3f, 0.9f)
                    , Color.White, Main.rand.NextFloat(0.05f, 0.09f) * SizeMul)
                    ?.Configure(Main.rand.Next(16, 26), new Color(140, 40, 20), new Color(20, 10, 9));
            }

            //Bloom 轻推(副斩不抢主斩的屏幕反馈)
            float bloom = (style == CleaveStyle.GuiltEcho ? 0.12f : 0.18f)
                * (1f - MathHelper.Clamp((timer - Lifetime + 10) / 10f, 0f, 1f));
            CrimsonImpactFX.PushAmbience(Projectile.Center, bloom);

            Lighting.AddLight(Projectile.Center, new Vector3(0.8f, 0.16f, 0.12f));
        }

        /// <summary>张开确认的爆点介质:狮势=旧金重钢屑,龙火=金火星+黑烟,咎影=碎墨,素=绯红火花</summary>
        private void SpawnSnapBurst(Vector2 tip) {
            switch (style) {
                case CleaveStyle.LionJaw:
                    for (int i = 0; i < 7; i++) {
                        Vector2 vel = BladeAngle.ToRotationVector2().RotatedByRandom(0.55)
                            * Main.rand.NextFloat(3f, 9f) * SizeMul;
                        PRTLoader.NewParticle<PRT_CrimsonSpark>(tip, vel, new Color(232, 186, 110)
                            , Main.rand.NextFloat(0.35f, 0.6f) * SizeMul)
                            ?.Configure(Main.rand.Next(16, 26), affectedByGravity: true);
                    }
                    break;
                case CleaveStyle.GuiltEcho:
                    for (int i = 0; i < 5; i++) {
                        Vector2 vel = BladeAngle.ToRotationVector2().RotatedByRandom(0.6)
                            * Main.rand.NextFloat(2.5f, 7f) * SizeMul;
                        PRTLoader.NewParticle<PRT_OniInkDrop>(tip, vel, new Color(84, 20, 26)
                            , Main.rand.NextFloat(0.2f, 0.36f) * SizeMul)
                            ?.Configure(Main.rand.Next(16, 26));
                    }
                    break;
                case CleaveStyle.KurikaraLoop:
                    for (int i = 0; i < 5; i++) {
                        Vector2 vel = BladeAngle.ToRotationVector2().RotatedByRandom(0.5)
                            * Main.rand.NextFloat(4f, 10f) * SizeMul;
                        PRTLoader.NewParticle<PRT_CrimsonSpark>(tip, vel, new Color(240, 172, 96)
                            , Main.rand.NextFloat(0.3f, 0.55f) * SizeMul)
                            ?.Configure(Main.rand.Next(12, 20), affectedByGravity: false);
                    }
                    PRTLoader.NewParticle<PRT_CrimsonSmoke>(tip, -Vector2.UnitY * 0.8f, Color.White
                        , 0.08f * SizeMul)?.Configure(22, new Color(150, 44, 22), new Color(22, 11, 10));
                    break;
                default:
                    for (int i = 0; i < 6; i++) {
                        Vector2 vel = BladeAngle.ToRotationVector2().RotatedByRandom(0.5)
                            * Main.rand.NextFloat(4f, 11f) * SizeMul;
                        PRTLoader.NewParticle<PRT_CrimsonSpark>(tip, vel, new Color(255, 130, 90)
                            , Main.rand.NextFloat(0.35f, 0.65f) * SizeMul)
                            ?.Configure(Main.rand.Next(12, 20), affectedByGravity: false);
                    }
                    break;
            }
        }

        /// <summary>扫掠前缘介质(每帧少量)</summary>
        private void SpawnSweepMedium(Vector2 pos, Vector2 tangent) {
            for (int k = 0; k < 2; k++) {
                Vector2 vel = tangent * Main.rand.NextFloat(4f, 10f) + Main.rand.NextVector2Circular(1f, 1f);
                switch (style) {
                    case CleaveStyle.LionJaw:
                        PRTLoader.NewParticle<PRT_CrimsonSpark>(pos, vel, new Color(228, 178, 104)
                            , Main.rand.NextFloat(0.28f, 0.5f) * SizeMul)
                            ?.Configure(Main.rand.Next(10, 16), affectedByGravity: false);
                        break;
                    case CleaveStyle.GuiltEcho:
                        PRTLoader.NewParticle<PRT_OniInkDrop>(pos, vel * 0.5f, new Color(70, 18, 24)
                            , Main.rand.NextFloat(0.16f, 0.3f) * SizeMul)
                            ?.Configure(Main.rand.Next(12, 20));
                        break;
                    case CleaveStyle.KurikaraLoop:
                        PRTLoader.NewParticle<PRT_CrimsonSpark>(pos, vel, new Color(235, 150, 80)
                            , Main.rand.NextFloat(0.26f, 0.48f) * SizeMul)
                            ?.Configure(Main.rand.Next(10, 16), affectedByGravity: false);
                        break;
                    default:
                        PRTLoader.NewParticle<PRT_CrimsonSpark>(pos, vel, new Color(255, 120, 80)
                            , Main.rand.NextFloat(0.3f, 0.55f) * SizeMul)
                            ?.Configure(Main.rand.Next(10, 16), affectedByGravity: false);
                        break;
                }
            }
        }

        public override bool? CanHitNPC(NPC target) => delayFrames > 0 ? false : base.CanHitNPC(target);

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!initialized || delayFrames > 0 || timer < def.DamageStart || timer > def.DamageEnd) {
                return false;
            }
            Rectangle greedyBox = targetHitbox;
            greedyBox.Inflate(14, 14);
            float sweepU = MathHelper.Clamp(CSR.Sweep(in def, timer) * 1.05f, 0f, 1f);
            Vector2 head = CSR.PointAt(in def, Projectile.Center, 0.05f, timer);
            Vector2 tail = CSR.PointAt(in def, Projectile.Center, MathF.Min(0.95f, sweepU), timer);
            float cp = 0f;
            float thick = MathF.Max(28f, def.HalfY * 0.85f);
            return Collision.CheckAABBvLineCollision(greedyBox.TopLeft(), greedyBox.Size()
                , head, tail, thick, ref cp);
        }

        /// <summary>割草断藤,沿直线刃</summary>
        public override void CutTiles() {
            if (!initialized || delayFrames > 0 || timer < def.DamageStart
                || timer > Math.Max(def.DamageEnd, def.SweepFrames)) {
                return;
            }
            DelegateMethods.tilecut_0 = Terraria.Enums.TileCuttingContext.AttackProjectile;
            float sweepU = MathHelper.Clamp(CSR.Sweep(in def, timer) * 1.05f, 0f, 1f);
            Vector2 head = CSR.PointAt(in def, Projectile.Center, 0.05f, timer);
            Vector2 tail = CSR.PointAt(in def, Projectile.Center, MathF.Min(0.95f, sweepU), timer);
            Utils.PlotTileLine(head, tail, MathF.Max(24f, def.HalfY * 0.8f), DelegateMethods.CutTiles);
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            float offsetX = Projectile.To(target.Center).X;
            modifiers.HitDirectionOverride = MathF.Abs(offsetX) > 0.01f
                ? Math.Sign(offsetX)
                : (MathF.Cos(BladeAngle) >= 0f ? 1 : -1);
            //与全系斩击同穿透管线(副斩伤害基数已压低)
            OnikiriItem.ApplySlashPenetration(target, ref modifiers);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            bool steel = CWRLoad.NPCValue.ISTheofSteel(target);
            SoundEngine.PlaySound((steel ? SoundID.NPCHit4 : SoundID.NPCHit1) with {
                Pitch = steel ? -0.05f : -0.2f,
                Volume = 0.7f
            }, target.Center);

            CrimsonRendHitVFX.SpawnHitTick(target.Center, BladeAngle.ToRotationVector2(), SizeMul, steel);
        }

        public override bool PreDraw(ref Color lightColor) => false;

        void IPrimitiveDrawable.DrawPrimitives() {
            if (Main.dedServ || !initialized) {
                return;
            }
            GraphicsDevice device = Main.instance.GraphicsDevice;
            if (!CSR.BeginDraw(device, out Effect fx, out var pb, out var pr, out var pd)) {
                return;
            }
            if (delayFrames > 0) {
                //咎影残像:两块错位半影静立,随读秒缓缓拉开,当中留一线裂缝
                float split = 3f + (1f - delayFrames / MathF.Max(delayTotal, 1f)) * 4f;
                Vector2 perp = (BladeAngle + MathHelper.PiOver2).ToRotationVector2() * split;
                SlashDef ghost = def;
                ghost.Opacity = def.Opacity * 0.35f;
                ghost.FrontGlow = def.FrontGlow * 0.4f;
                int ghostLt = ghost.SweepFrames + 1;
                CSR.DrawThreeLayers(device, fx, in ghost, Projectile.Center + perp, ghostLt, 0f);
                CSR.DrawThreeLayers(device, fx, in ghost, Projectile.Center - perp, ghostLt, 0f);
            }
            else {
                CSR.DrawThreeLayers(device, fx, in def, Projectile.Center, timer, 0f);
            }
            CSR.EndDraw(device, pb, pr, pd);
        }
    }
}
