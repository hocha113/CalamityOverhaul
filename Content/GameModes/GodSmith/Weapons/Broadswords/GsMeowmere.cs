using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Broadswords
{
    /// <summary>
    /// 【喵刃·喵之乐章】材质：彩虹糖霜猫剑，糖芯炽白、彩虹带拖尾，满身滑稽不许洗掉。
    /// 签名：①连段四拍音高与刃色如彩虹音阶逐拍上行，每斩射出弹跳猫头弹（喵音逐跳变调+分段彩虹带）
    /// ②命中攒音符（上限 7），音符刻光沿刀脊排开
    /// ③攒满后终结拍召一记猫流星砸落：巨猫头+彩虹彗尾+落地星屑爆
    /// </summary>
    internal class GsMeowmere : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.Meowmere;

        protected override int HeldProjID => ModContent.ProjectileType<GsMeowmereHeld>();

        protected override int ComboBeats => 4;

        protected override int ComboResetFrames => 58;

        protected override string GsDescFallback =>
            "Reforged: a candy-frosted cat blade playing a four-beat meow sonata; every slash flings " +
            "a bouncing cat head trailing rainbow ribbon, hits collect musical notes, " +
            "and at seven notes the finisher calls a cat meteor crashing down from the sky";

        //糖霜色板
        internal static readonly Color CandyCream = new(255, 244, 250);  //糖芯白
        internal static readonly Color CandyPink = new(255, 158, 214);   //糖霜粉体色
        internal static readonly Color CandyHot = new(255, 108, 186);    //热粉强调
        internal static readonly Color CandyDeep = new(58, 22, 46);      //深糖垫影

        /// <summary>彩虹取色（h 为色相，自动取模）</summary>
        internal static Color Rainbow(float h, float lum = 0.62f) => Main.hslToRgb((h % 1f + 1f) % 1f, 1f, lum);

        /// <summary>音符层数（0~7）；跨玩家共享单例，只在 myPlayer 守门路径读写</summary>
        internal int Notes;

        //底伤不加成（200 面板是这把剑的滑稽身份，不动）：近战拍均约 1.04x + 每斩猫头弹 0.9x（穿 2、同目标一次）+
        //攒满 7 音符的终结拍猫流星 1.4x 与落地星爆 0.5x（命中才攒音符、至多每循环一发），
        //按四拍循环约 63 帧对照原版（近战 1.0x + 猫弹 1.0x 每 14 帧）摊算，综合单体 DPS 约原版 100%~107%
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage) { }
    }

    /// <summary>
    /// 糖霜猫剑手持：四拍喵之音阶。0~2 拍音高与刃色逐拍上行（do-re-mi），3 扑击重拍收乐句；
    /// 每拍斩切射猫头弹，攒满 7 音符时终结拍召猫流星。刀脊音符刻光只画给 owner。
    /// ai[0]=拍号 ai[1]=交替符号
    /// </summary>
    internal class GsMeowmereHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.Meowmere;
        protected override int BeatCount => 4;
        protected override float BaseReach => 120f;

        /// <summary>拍号→音阶色相：从糖粉红起步逐拍上行</summary>
        internal static float BeatHue(int stage) => 0.90f + stage * 0.16f;

        protected override Color EdgeBright
            => Color.Lerp(GsMeowmere.CandyCream, GsMeowmere.Rainbow(BeatHue(ComboStage), 0.75f), 0.6f);
        protected override Color BodyMain => GsMeowmere.CandyPink;
        protected override Color HotAccent => GsMeowmere.Rainbow(BeatHue(ComboStage) + 0.06f, 0.58f);
        protected override Color DeepShadow => GsMeowmere.CandyDeep;

        //糖霜常亮，辉光随音阶换色
        protected override bool GlowAlways => true;
        protected override Color GlowColor => HotAccent;

        private bool catFired;

        private GsMeowmere Scheme =>
            GodSmithScheme.TryGetScheme(SwordItemID, out GodSmithScheme s) ? s as GsMeowmere : null;

        protected override GsBroadBeat GetBeat(int stage) => stage switch {
            //拍0 do：起手横斩
            0 => new GsBroadBeat {
                Raise = 5, Hold = 1, Slash = 3, Recover = 6,
                RaiseBack = 1.7f, Follow = 1.0f, ReachScale = 1f, LeanAmp = 0.04f,
                DamageMult = 1f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = 0f,
            },
            //拍1 re：返斩，音阶上行
            1 => new GsBroadBeat {
                Raise = 5, Hold = 1, Slash = 3, Recover = 6,
                RaiseBack = 1.75f, Follow = 1.05f, ReachScale = 1f, LeanAmp = 0.045f,
                DamageMult = 1f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = 0.09f,
            },
            //拍2 mi：轻快提斩
            2 => new GsBroadBeat {
                Raise = 4, Hold = 1, Slash = 3, Recover = 5,
                RaiseBack = 1.55f, Follow = 0.95f, ReachScale = 0.97f, LeanAmp = 0.04f,
                DamageMult = 0.9f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = 0.18f,
            },
            //拍3 重拍：扑击终结，音阶顶点
            _ => new GsBroadBeat {
                Raise = 6, Hold = 2, Slash = 4, Recover = 8,
                RaiseBack = 2.1f, Follow = 1.25f, ReachScale = 1.1f, LeanAmp = 0.08f,
                DamageMult = 1.25f, Hitstop = 2, LungeSpeed = 2.4f, SwingPitch = 0.28f,
            },
        };

        //==================== 喵之演出 ====================

        protected override void PlaySwingSound() {
            SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.8f, Pitch = Beat.SwingPitch }, Owner.Center);
            //每拍一记上行的喵（音阶本体）
            SoundEngine.PlaySound(SoundID.Item57 with { Volume = 0.3f, Pitch = -0.15f + ComboStage * 0.18f, MaxInstances = 3 }, Owner.Center);
            if (IsFinisher) {
                SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.32f, Pitch = -0.3f }, Owner.Center);
            }
        }

        /// <summary>每拍斩切射猫头弹；攒满 7 音符的终结拍另召猫流星（owner 端选点，消耗全部音符）</summary>
        protected override void OnSlashBegin() {
            if (catFired) {
                return;
            }
            catFired = true;
            if (IsFinisher) {
                SetFlash(6);
            }
            int baseDamage = Math.Max(1, (int)(Projectile.damage / Beat.DamageMult));
            Vector2 dir = baseAngle.ToRotationVector2();
            SpawnOwnedProj(ModContent.ProjectileType<GsMeowmereCatProj>(),
                Hand + dir * (FullReach * 0.85f), dir * 16f,
                Math.Max(1, (int)(baseDamage * 0.9f)), Projectile.knockBack * 0.5f, ComboStage);

            if (!IsFinisher || Projectile.owner != Main.myPlayer) {
                return;
            }
            GsMeowmere scheme = Scheme;
            if (scheme == null || scheme.Notes < 7) {
                return;
            }
            scheme.Notes = 0;
            //猫流星：优先砸光标附近的敌人，否则砸光标处
            Vector2 strike = Main.MouseWorld;
            float best = 480f;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.active || !npc.CanBeChasedBy()) {
                    continue;
                }
                float dist = Vector2.Distance(npc.Center, Main.MouseWorld);
                if (dist < best) {
                    best = dist;
                    strike = npc.Center;
                }
            }
            Vector2 spawn = strike + new Vector2(-facingDir * 190f, -640f);
            if (spawn.Y < 60f) {
                spawn.Y = 60f;
            }
            Vector2 fall = (strike - spawn).SafeNormalize(Vector2.UnitY) * 12f;
            SpawnOwnedProj(ModContent.ProjectileType<GsMeowmereMeteorProj>(), spawn, fall,
                Math.Max(1, (int)(baseDamage * 1.4f)), Projectile.knockBack, strike.X, strike.Y);
            SoundEngine.PlaySound(SoundID.Item57 with { Volume = 0.85f, Pitch = -0.5f }, Owner.Center);
            SetFlash(8);
        }

        /// <summary>近战命中攒音符（owner 记账），攒满起和弦提示</summary>
        protected override void OnHitTarget(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Owner.whoAmI != Main.myPlayer) {
                return;
            }
            GsMeowmere scheme = Scheme;
            if (scheme == null) {
                return;
            }
            int old = scheme.Notes;
            scheme.Notes = Math.Min(7, scheme.Notes + 1);
            if (old < 7 && scheme.Notes == 7) {
                //攒满一段乐章：和弦 + 刃身闪
                SoundEngine.PlaySound(SoundID.Item9 with { Volume = 0.45f, Pitch = 0.4f }, Owner.Center);
                SoundEngine.PlaySound(SoundID.Item57 with { Volume = 0.35f, Pitch = 0.5f }, Owner.Center);
                SetFlash(6);
            }
        }

        /// <summary>命中反馈：蹦音符 + 音阶色星屑，重拍加量</summary>
        protected override void OnHitFX(NPC target, NPC.HitInfo hit, int damageDone) {
            base.OnHitFX(target, hit, damageDone);
            PRTLoader.NewParticle<PRT_Note>(target.Center + Main.rand.NextVector2Circular(8f, 8f),
                new Vector2(Main.rand.NextFloat(-1f, 1f), -Main.rand.NextFloat(1.2f, 2.2f)),
                GsMeowmere.Rainbow(BeatHue(ComboStage), 0.7f), Main.rand.NextFloat(0.8f, 1.1f))
                ?.Configure(Main.rand.Next(26, 36));
            int shards = IsFinisher ? 5 : 3;
            for (int i = 0; i < shards; i++) {
                PRTLoader.NewParticle<PRT_HeavenfallStar>(target.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 5f),
                    GsMeowmere.Rainbow(BeatHue(ComboStage) + Main.rand.NextFloat(0.15f), 0.68f),
                    Main.rand.NextFloat(0.24f, 0.4f))?.Configure(true, Main.rand.Next(12, 20));
            }
        }

        protected override void HandleParticles(int phase) {
            base.HandleParticles(phase);
            if (!IsFinisher || phase > PhaseHold || Owner.whoAmI != Main.myPlayer) {
                return;
            }
            GsMeowmere scheme = Scheme;
            if (scheme == null || scheme.Notes < 7) {
                return;
            }
            //满乐章蓄势：彩虹光尘旋入刀身（音符数是 owner 本地账，只在 owner 端放）
            Vector2 from = Hand + Main.rand.NextVector2Unit() * Main.rand.NextFloat(40f, 68f);
            PRTLoader.NewParticle<PRT_Light>(from, (Vector2.Lerp(Hand, mainTip, 0.6f) - from) * 0.16f,
                GsMeowmere.Rainbow(Main.rand.NextFloat(), 0.62f),
                Main.rand.NextFloat(0.06f, 0.11f))?.Configure(9, 0.6f);
        }

        /// <summary>刀脊音符刻光：7 枚音符逐格点亮（音符数是 owner 本地账，只画给 owner）</summary>
        protected override void DrawExtra(SpriteBatch sb, Color lightColor) {
            if (Owner.whoAmI != Main.myPlayer || fanFade <= 0.05f) {
                return;
            }
            GsMeowmere scheme = Scheme;
            if (scheme == null) {
                return;
            }
            int notes = scheme.Notes;
            bool full = notes >= 7;
            Vector2 hand = Hand;
            for (int i = 0; i < 7; i++) {
                int projType = (i % 3) switch {
                    0 => ProjectileID.TiedEighthNote,
                    1 => ProjectileID.EighthNote,
                    _ => ProjectileID.QuarterNote,
                };
                Main.instance.LoadProjectile(projType);
                Texture2D note = TextureAssets.Projectile[projType].Value;
                Vector2 at = hand + mainAngle.ToRotationVector2() * (mainReach * (0.24f + 0.105f * i))
                    - Main.screenPosition;
                bool lit = i < notes;
                float pulse = 0.7f + 0.3f * MathF.Sin(Main.GlobalTimeWrappedHourly * (full ? 10f : 6f) + i * 1.1f);
                Color c = lit
                    ? GsMeowmere.Rainbow(i / 7f, 0.66f) * (0.6f * fanFade * pulse)
                    : GsMeowmere.CandyCream * (0.14f * fanFade);
                c.A = 0;
                float wobble = full ? MathF.Sin(Main.GlobalTimeWrappedHourly * 9f + i) * 0.2f : 0f;
                sb.Draw(note, at, null, c, wobble, note.Size() * 0.5f,
                    lit ? 0.62f + 0.08f * pulse : 0.5f, SpriteEffects.None, 0f);
            }
        }
    }

    /// <summary>
    /// 猫头弹：每一斩甩出的糖霜猫头（糖芯白+双耳+眯眼）拖分段彩虹带。
    /// 出膛 16 缓至约 13，14 帧后吃重力弧落（禁匀速）；碰墙弹跳至多 3 次、喵音逐跳升调；
    /// 消亡即星屑小爆；命中攒音符（owner 记账）。ai[0]=起始音阶（拍号）
    /// </summary>
    internal class GsMeowmereCatProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private float BaseHue => GsMeowmereHeld.BeatHue((int)Projectile.ai[0]);
        private ref float Life => ref Projectile.localAI[0];
        private ref float Bounces => ref Projectile.localAI[1];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 16;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 28;
            Projectile.friendly = true;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 2;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.timeLeft = 100;
        }

        public override void AI() {
            Life++;
            //出膛缓速，随后重力弧落（全程不匀速）
            if (Life <= 14f) {
                Projectile.velocity *= 0.985f;
            }
            else {
                Projectile.velocity.Y = MathF.Min(Projectile.velocity.Y + 0.14f, 16f);
            }
            Projectile.rotation = Projectile.velocity.ToRotation();

            Lighting.AddLight(Projectile.Center, GsMeowmere.Rainbow(BaseHue + Life * 0.015f, 0.6f).ToVector3() * 0.35f);
            if (!VaultUtils.isServer && Main.rand.NextBool(2)) {
                //彩虹带余尘
                PRTLoader.NewParticle<PRT_Light>(Projectile.Center - Projectile.velocity * 0.5f,
                    -Projectile.velocity * 0.04f, GsMeowmere.Rainbow(BaseHue + Life * 0.03f, 0.6f),
                    Main.rand.NextFloat(0.05f, 0.08f))?.Configure(8, 0.55f);
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            Bounces++;
            if (Bounces > 3f) {
                return true;
            }
            //弹跳反射 + 逐跳升调的喵
            if (Projectile.velocity.X != oldVelocity.X) {
                Projectile.velocity.X = -oldVelocity.X * 0.92f;
            }
            if (Projectile.velocity.Y != oldVelocity.Y) {
                Projectile.velocity.Y = -oldVelocity.Y * 0.86f;
            }
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item57 with { Volume = 0.4f, Pitch = -0.1f + Bounces * 0.14f, MaxInstances = 5 }, Projectile.Center);
                for (int i = 0; i < 3; i++) {
                    PRTLoader.NewParticle<PRT_HeavenfallStar>(Projectile.Center,
                        Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.5f, 3.5f),
                        GsMeowmere.Rainbow(BaseHue + Bounces * 0.11f, 0.68f),
                        Main.rand.NextFloat(0.2f, 0.34f))?.Configure(true, Main.rand.Next(10, 16));
                }
            }
            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //猫弹命中也攒音符（owner 记账）
            if (Projectile.owner == Main.myPlayer
                && GodSmithScheme.TryGetScheme(ItemID.Meowmere, out GodSmithScheme s) && s is GsMeowmere scheme) {
                int old = scheme.Notes;
                scheme.Notes = Math.Min(7, scheme.Notes + 1);
                if (old < 7 && scheme.Notes == 7) {
                    SoundEngine.PlaySound(SoundID.Item9 with { Volume = 0.45f, Pitch = 0.4f }, Projectile.Center);
                }
            }
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item57 with { Volume = 0.3f, Pitch = 0.3f, MaxInstances = 5 }, target.Center);
            PRTLoader.NewParticle<PRT_Note>(target.Center, -Vector2.UnitY * Main.rand.NextFloat(1f, 2f),
                GsMeowmere.Rainbow(BaseHue, 0.7f), Main.rand.NextFloat(0.7f, 1f))?.Configure(Main.rand.Next(22, 32));
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            //掉落即消爆：糖霜星屑一小蓬
            SoundEngine.PlaySound(SoundID.Item9 with { Volume = 0.35f, Pitch = 0.25f, MaxInstances = 5 }, Projectile.Center);
            PRTLoader.NewParticle<PRT_Light>(Projectile.Center, Vector2.Zero,
                GsMeowmere.CandyCream, 0.16f)?.Configure(8, 0.8f);
            for (int i = 0; i < 6; i++) {
                PRTLoader.NewParticle<PRT_HeavenfallStar>(Projectile.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 5f),
                    GsMeowmere.Rainbow(BaseHue + i * 0.13f, 0.66f),
                    Main.rand.NextFloat(0.24f, 0.4f))?.Configure(true, Main.rand.Next(12, 20));
            }
        }

        /// <summary>糖霜猫头（彩虹外晕+糖芯白核+双耳+眯眼+额星）：猫弹与猫流星共用</summary>
        internal static void DrawCatHead(Vector2 center, float rot, float scale, float hue, float alpha) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Texture2D dark = CWRAsset.Extra_98?.Value;
            Texture2D star = CWRAsset.StarGlow01?.Value;
            if (glow == null || dark == null || star == null) {
                return;
            }
            Vector2 fwd = rot.ToRotationVector2();
            Vector2 side = (rot + MathHelper.PiOver2).ToRotationVector2();

            //彩虹外晕
            Color halo = GsMeowmere.Rainbow(hue, 0.6f) * (0.55f * alpha);
            halo.A = 0;
            Main.EntitySpriteDraw(glow, center, null, halo, 0f, glow.Size() * 0.5f, 0.62f * scale, SpriteEffects.None, 0);
            //双耳：窄光斑斜立头顶两侧
            for (int i = -1; i <= 1; i += 2) {
                Vector2 at = center + side * (i * 11f * scale) - fwd * (2f * scale);
                Color ear = GsMeowmere.Rainbow(hue + 0.05f * i, 0.7f) * (0.7f * alpha);
                ear.A = 0;
                Main.EntitySpriteDraw(glow, at, null, ear, rot + i * 0.7f, glow.Size() * 0.5f,
                    new Vector2(0.14f, 0.30f) * scale, SpriteEffects.None, 0);
            }
            //糖芯白核
            Color core = GsMeowmere.CandyCream * (0.85f * alpha);
            core.A = 0;
            Main.EntitySpriteDraw(glow, center, null, core, 0f, glow.Size() * 0.5f, 0.36f * scale, SpriteEffects.None, 0);
            //眯眼两点（真 alpha 暗斑才压得暗）
            for (int i = -1; i <= 1; i += 2) {
                Vector2 at = center + side * (i * 5.5f * scale) + fwd * (4.5f * scale);
                Main.EntitySpriteDraw(dark, at, null, new Color(70, 30, 58) * (0.75f * alpha), rot,
                    dark.Size() * 0.5f, 0.045f * scale, SpriteEffects.None, 0);
            }
            //额前星光一点
            Color twinkle = GsMeowmere.CandyCream * (0.6f * alpha);
            twinkle.A = 0;
            Main.EntitySpriteDraw(star, center - fwd * (6f * scale), null, twinkle,
                Main.GlobalTimeWrappedHourly * 2f, star.Size() * 0.5f, 0.12f * scale, SpriteEffects.None, 0);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) {
                return false;
            }
            Vector2 screen = Main.screenPosition;
            float fade = MathHelper.Clamp(Projectile.timeLeft / 8f, 0f, 1f);
            float grow = MathHelper.Clamp(Life / 3f, 0f, 1f);
            float k = fade * grow;

            //分段彩虹带：旧位逐节一段色，一节一个色相
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                Vector2 at = Projectile.oldPos[i] + Projectile.Size * 0.5f - screen;
                float t = 1f - i / (float)Projectile.oldPos.Length;
                Color band = GsMeowmere.Rainbow(BaseHue + i * 0.055f, 0.6f) * (0.40f * t * k);
                band.A = 0;
                Main.EntitySpriteDraw(glow, at, null, band, Projectile.oldRot[i],
                    glow.Size() * 0.5f, new Vector2(0.34f, 0.20f) * (0.5f + 0.5f * t), SpriteEffects.None, 0);
            }
            //近段糖芯白线
            for (int i = 4; i >= 1; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                Vector2 at = Projectile.oldPos[i] + Projectile.Size * 0.5f - screen;
                float t = 1f - i / 5f;
                Color coreLine = GsMeowmere.CandyCream * (0.30f * t * k);
                coreLine.A = 0;
                Main.EntitySpriteDraw(glow, at, null, coreLine, Projectile.oldRot[i],
                    glow.Size() * 0.5f, new Vector2(0.20f, 0.08f), SpriteEffects.None, 0);
            }

            DrawCatHead(Projectile.Center - screen, Projectile.rotation, 1f, BaseHue + Life * 0.02f, k);
            return false;
        }
    }

    /// <summary>
    /// 猫流星：攒满乐章召来的巨猫头。斜落加速（12→约 26，禁匀速），彩虹彗尾+沿途星屑+坠落长喵；
    /// 触敌或抵达落点即爆：星屑扇+音符+星爆环（owner 生成）。ai[0]/ai[1]=落点坐标
    /// </summary>
    internal class GsMeowmereMeteorProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private Vector2 StrikePoint => new(Projectile.ai[0], Projectile.ai[1]);
        private ref float Life => ref Projectile.localAI[0];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 14;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 46;
            Projectile.friendly = true;
            Projectile.tileCollide = false;//镜像星怒：无视地形直抵落点
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;//触敌即爆，爆点结算
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.timeLeft = 240;
        }

        public override void AI() {
            Life++;
            if (Life == 1f && !VaultUtils.isServer) {
                //坠落长喵
                SoundEngine.PlaySound(SoundID.Item57 with { Volume = 0.85f, Pitch = -0.55f }, Projectile.Center);
            }
            //坠落加速（全程不匀速）
            if (Projectile.velocity.Length() < 26f) {
                Projectile.velocity *= 1.045f;
            }
            Projectile.rotation = Projectile.velocity.ToRotation();

            Lighting.AddLight(Projectile.Center, GsMeowmere.Rainbow(Life * 0.02f, 0.6f).ToVector3() * 0.8f);
            if (!VaultUtils.isServer) {
                //彗尾星屑与彩虹光尘
                if ((int)Life % 3 == 0) {
                    PRTLoader.NewParticle<PRT_HeavenfallStar>(
                        Projectile.Center - Projectile.velocity * 0.6f + Main.rand.NextVector2Circular(10f, 10f),
                        -Projectile.velocity * 0.06f + Main.rand.NextVector2Circular(0.8f, 0.8f),
                        GsMeowmere.Rainbow(Main.rand.NextFloat(), 0.66f),
                        Main.rand.NextFloat(0.24f, 0.4f))?.Configure(true, Main.rand.Next(14, 22));
                }
                PRTLoader.NewParticle<PRT_Light>(Projectile.Center - Projectile.velocity * 0.4f,
                    -Projectile.velocity * 0.05f, GsMeowmere.Rainbow(Life * 0.04f, 0.6f),
                    Main.rand.NextFloat(0.08f, 0.13f))?.Configure(8, 0.6f);
            }

            //抵达落点即爆
            if (Projectile.Center.Y >= StrikePoint.Y || Vector2.Distance(Projectile.Center, StrikePoint) < 24f) {
                Projectile.Kill();
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) => Projectile.Kill();

        public override void OnKill(int timeLeft) {
            //星爆环（owner 生成，随包全端可见）
            if (Projectile.owner == Main.myPlayer) {
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, Vector2.Zero,
                    ModContent.ProjectileType<GsMeowmereStarBurstProj>(),
                    Math.Max(1, (int)(Projectile.damage * 0.36f)), Projectile.knockBack * 0.6f, Projectile.owner);
            }
            if (VaultUtils.isServer) {
                return;
            }
            //落地重音：闷响 + 破音喵 + 星铃
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.7f, Pitch = -0.2f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Item57 with { Volume = 0.8f, Pitch = -0.75f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Item9 with { Volume = 0.5f, Pitch = 0.1f }, Projectile.Center);
            PRTLoader.NewParticle<PRT_Light>(Projectile.Center, Vector2.Zero,
                GsMeowmere.CandyCream, 0.5f)?.Configure(14, 0.95f);
            for (int i = 0; i < 14; i++) {
                //星屑扇：上半圆彩虹迸溅
                Vector2 vel = (-MathHelper.PiOver2 + (i / 13f - 0.5f) * 2.8f).ToRotationVector2()
                    * Main.rand.NextFloat(3f, 9f);
                PRTLoader.NewParticle<PRT_HeavenfallStar>(Projectile.Center, vel,
                    GsMeowmere.Rainbow(i / 14f, 0.66f), Main.rand.NextFloat(0.3f, 0.55f))
                    ?.Configure(true, Main.rand.Next(18, 30));
            }
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_Note>(Projectile.Center + Main.rand.NextVector2Circular(20f, 12f),
                    new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), -Main.rand.NextFloat(1.5f, 3f)),
                    GsMeowmere.Rainbow(Main.rand.NextFloat(), 0.7f), Main.rand.NextFloat(0.9f, 1.2f))
                    ?.Configure(Main.rand.Next(30, 40), i);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) {
                return false;
            }
            Vector2 screen = Main.screenPosition;
            float k = MathHelper.Clamp(Life / 5f, 0f, 1f);

            //彩虹彗尾：宽段色带
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                Vector2 at = Projectile.oldPos[i] + Projectile.Size * 0.5f - screen;
                float t = 1f - i / (float)Projectile.oldPos.Length;
                Color band = GsMeowmere.Rainbow(i * 0.07f + Life * 0.01f, 0.58f) * (0.45f * t * k);
                band.A = 0;
                Main.EntitySpriteDraw(glow, at, null, band, Projectile.oldRot[i],
                    glow.Size() * 0.5f, new Vector2(0.9f, 0.55f) * (0.4f + 0.6f * t), SpriteEffects.None, 0);
            }
            //白热芯线
            for (int i = 5; i >= 1; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                Vector2 at = Projectile.oldPos[i] + Projectile.Size * 0.5f - screen;
                float t = 1f - i / 6f;
                Color coreLine = GsMeowmere.CandyCream * (0.4f * t * k);
                coreLine.A = 0;
                Main.EntitySpriteDraw(glow, at, null, coreLine, Projectile.oldRot[i],
                    glow.Size() * 0.5f, new Vector2(0.5f, 0.2f), SpriteEffects.None, 0);
            }

            //巨猫头（滑稽重音：坠落中微微摇头）
            float wobble = MathF.Sin(Life * 0.35f) * 0.16f;
            GsMeowmereCatProj.DrawCatHead(Projectile.Center - screen, Projectile.rotation + wobble, 2.3f, Life * 0.015f, k);
            return false;
        }
    }

    /// <summary>
    /// 猫流星星爆：落点扩张的彩虹星环。8 帧过冲撑满回坐，伤害只在扩张期结算一次；
    /// 环珠色相绕环一整圈彩虹。绘制走确定性相位，禁 Main.rand
    /// </summary>
    internal class GsMeowmereStarBurstProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int TotalLife = 22;
        private const float MaxRadius = 120f;
        private ref float Life => ref Projectile.localAI[0];
        private float Life01 => MathHelper.Clamp(Life / TotalLife, 0f, 1f);

        /// <summary>当前扩张半径：8 帧过冲 6% 再回坐</summary>
        private float Radius {
            get {
                float p = MathHelper.Clamp(Life / 8f, 0f, 1f);
                float burst = p < 0.7f ? 1.06f * (p / 0.7f) : MathHelper.Lerp(1.06f, 1f, (p - 0.7f) / 0.3f);
                return MaxRadius * burst;
            }
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 8;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.timeLeft = TotalLife;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            Life++;
            Lighting.AddLight(Projectile.Center, GsMeowmere.CandyHot.ToVector3() * (0.8f * (1f - Life01)));
        }

        //伤害只在扩张期结算（一目标一次）
        public override bool? CanDamage() => Life <= 9f ? null : false;

        /// <summary>圆形判定：目标碰到当前扩张半径即命中</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
            => targetHitbox.Distance(Projectile.Center) <= Radius;

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
            => modifiers.HitDirectionOverride = Math.Sign(target.Center.X - Projectile.Center.X);//击退向外

        /// <summary>绘制路径确定性伪随机</summary>
        private float SegRand(int salt) {
            uint h = (uint)(Projectile.identity * 374761393 + salt * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xFFFFFF) / (float)0x1000000;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Texture2D star = CWRAsset.StarTexture?.Value;
            Texture2D flare = CWRAsset.StarFlare01?.Value;
            if (glow == null || star == null || flare == null) {
                return false;
            }
            Vector2 center = Projectile.Center - Main.screenPosition;
            float fade = 1f - Life01;
            float radius = Radius;

            //环心星芒：首帧最亮随后蚀散
            Color flash = GsMeowmere.CandyCream * (0.8f * fade * fade);
            flash.A = 0;
            Main.EntitySpriteDraw(star, center, null, flash, SegRand(9) * 6.28f,
                star.Size() * 0.5f, 0.42f, SpriteEffects.None, 0);
            Color flareC = GsMeowmere.CandyHot * (0.5f * fade);
            flareC.A = 0;
            Main.EntitySpriteDraw(flare, center, null, flareC, Life * 0.05f,
                flare.Size() * 0.5f, 0.5f * (0.6f + 0.4f * Life01), SpriteEffects.None, 0);

            //扩张星环：环珠色相绕环一整圈彩虹
            const int beads = 16;
            for (int i = 0; i < beads; i++) {
                float ang = MathHelper.TwoPi * i / beads + SegRand(i) * 0.4f;
                Vector2 at = center + ang.ToRotationVector2() * radius;
                float pulse = 0.75f + 0.25f * MathF.Sin(Main.GlobalTimeWrappedHourly * 8f + SegRand(i + 30) * 6.28f);
                Color bead = GsMeowmere.Rainbow(i / (float)beads, 0.62f) * (0.55f * fade * pulse);
                bead.A = 0;
                Main.EntitySpriteDraw(glow, at, null, bead, 0f, glow.Size() * 0.5f,
                    0.26f + 0.1f * SegRand(i + 60), SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
