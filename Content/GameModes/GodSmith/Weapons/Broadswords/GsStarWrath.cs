using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Broadswords
{
    /// <summary>
    /// 【星怒夜陨】材质：夜穹陨铁锻的召星魔剑，星蓝紫白色板。
    /// 签名：①每一斩从天顶召落三颗彗尾流星（前快后缓的减速坠落，落点锁屏内，
    /// 原版三星保留并升级自绘）②命中攒星潮（上限 7），潮满后终结斩改为倾泻星雨瀑：
    /// 十二小星错帧坠落 + 一颗大星收尾，大星落点炸开星环 ③挥砍保留原版 Item105 音身份
    /// </summary>
    internal class GsStarWrath : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.StarWrath;

        protected override int HeldProjID => ModContent.ProjectileType<GsStarWrathHeld>();

        protected override string GsDescFallback =>
            "Reforged: every slash calls three comet-tailed stars down from the night sky; " +
            "landed hits build the Star Tide, and at full tide the finishing slash " +
            "unleashes a cascade of falling stars crowned by one great star that bursts into a ring";

        //夜陨色板
        internal static readonly Color NightBright = new(240, 244, 255); //星芒白
        internal static readonly Color NightMain = new(132, 142, 255);   //星辉蓝紫
        internal static readonly Color NightHot = new(190, 112, 255);    //紫电强调
        internal static readonly Color NightDeep = new(16, 14, 42);      //夜穹深蓝

        /// <summary>星潮上限</summary>
        internal const int StarTideMax = 7;
        /// <summary>星潮层数；跨玩家共享单例，只在 myPlayer 守门路径读写</summary>
        internal int StarTide;

        /// <summary>攒潮（剑击与流星命中共用；只在 myPlayer 守门路径调用），攒满鸣星提示</summary>
        internal void GainTide(Vector2 cueAt) {
            if (StarTide >= StarTideMax) {
                return;
            }
            StarTide++;
            if (StarTide == StarTideMax) {
                SoundEngine.PlaySound(SoundID.Item9 with { Volume = 0.5f, Pitch = 0.55f }, cueAt);
            }
        }

        //底伤 +15%：原版三星每颗全伤是 DPS 主体，重铸天降星统一降为 0.7x/颗（除回拍伤摊账），强度转移进潮汐瀑
        //循环 53 帧（16+16+21）潮满稳态：拍伤 3.35 + 常规星 6×0.7 + 雨瀑(12×0.25+大星1.0+星环0.45) ≈12.0 单位 ×1.15
        //vs 原版全套(挥1.0+三星3.0)/16 帧同窗 13.25 单位 → 约 104%；首轮未攒潮地板约 84%，雨瀑与星环对群是 AoE 收益
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.15f;
    }

    /// <summary>
    /// 星怒夜陨手持：三拍疾速连击（原版 useTime 16 的快剑身段）。0/1 交替疾斩各召三星，
    /// 2 星怒重劈（前压+重顿帧；潮满时改召星雨瀑）。星潮刻星沿刀脊排布。
    /// ai[0]=拍号 ai[1]=交替符号
    /// </summary>
    internal class GsStarWrathHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.StarWrath;
        protected override Color EdgeBright => GsStarWrath.NightBright;
        protected override Color BodyMain => GsStarWrath.NightMain;
        protected override Color HotAccent => GsStarWrath.NightHot;
        protected override Color DeepShadow => GsStarWrath.NightDeep;

        //夜穹陨铁吸光；星辉常亮
        protected override Color BodyTint(Color lightColor)
            => Color.Lerp(lightColor, GsStarWrath.NightDeep, 0.24f);
        protected override bool GlowAlways => true;
        protected override Color GlowColor => IsFinisher ? GsStarWrath.NightHot : GsStarWrath.NightMain;
        //星尘代血
        protected override bool BleedOnFlesh => false;

        private bool starsCalled;

        private GsStarWrath Scheme =>
            GodSmithScheme.TryGetScheme(SwordItemID, out GodSmithScheme s) ? s as GsStarWrath : null;

        protected override GsBroadBeat GetBeat(int stage) => stage switch {
            //拍0 疾斩
            0 => new GsBroadBeat {
                Raise = 4, Hold = 1, Slash = 3, Recover = 7,
                RaiseBack = 1.7f, Follow = 0.95f, ReachScale = 1f, LeanAmp = 0.04f,
                DamageMult = 1f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = 0f,
            },
            //拍1 返疾斩
            1 => new GsBroadBeat {
                Raise = 4, Hold = 1, Slash = 3, Recover = 7,
                RaiseBack = 1.75f, Follow = 1.0f, ReachScale = 1f, LeanAmp = 0.045f,
                DamageMult = 1f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = -0.1f,
            },
            //拍2 星怒重劈：前压，潮满改雨瀑
            _ => new GsBroadBeat {
                Raise = 6, Hold = 2, Slash = 4, Recover = 9,
                RaiseBack = 2.1f, Follow = 1.25f, ReachScale = 1.12f, LeanAmp = 0.08f,
                DamageMult = 1.35f, Hitstop = 2, LungeSpeed = 3.0f, SwingPitch = -0.22f,
            },
        };

        //==================== 召星演出 ====================

        /// <summary>保留原版 Item105 挥砍音身份，按拍调音高</summary>
        protected override void PlaySwingSound() {
            SoundEngine.PlaySound(SoundID.Item105 with { Volume = 0.7f, Pitch = Beat.SwingPitch }, Owner.Center);
            if (IsFinisher) {
                SoundEngine.PlaySound(SoundID.Item9 with { Volume = 0.4f, Pitch = -0.3f }, Owner.Center);
            }
        }

        /// <summary>每斩召三星；潮满的终结拍改召星雨瀑（瞄点与散布只在 owner 端取样）</summary>
        protected override void OnSlashBegin() {
            if (starsCalled) {
                return;
            }
            starsCalled = true;
            if (IsFinisher) {
                SetFlash(7);
            }
            if (Owner.whoAmI != Main.myPlayer) {
                return;
            }
            //落点锚定鼠标，钳在屏幕量级范围内
            Vector2 aim = Main.MouseWorld;
            aim.X = MathHelper.Clamp(aim.X, Owner.Center.X - 880f, Owner.Center.X + 880f);
            aim.Y = MathHelper.Clamp(aim.Y, Owner.Center.Y - 440f, Owner.Center.Y + 440f);
            int baseDamage = Math.Max(1, (int)(Projectile.damage / Beat.DamageMult));

            GsStarWrath scheme = Scheme;
            if (IsFinisher && scheme != null && scheme.StarTide >= GsStarWrath.StarTideMax) {
                //星雨瀑：十二小星错帧倾泻，大星压轴
                scheme.StarTide = 0;
                for (int i = 0; i < 12; i++) {
                    Vector2 land = aim + new Vector2(Main.rand.NextFloat(-130f, 130f), Main.rand.NextFloat(-16f, 16f));
                    CallStar(land, Math.Max(1, (int)(baseDamage * 0.25f)), 1, i * 2, 20f);
                }
                CallStar(aim, baseDamage, 2, 26, 26f);
                SoundEngine.PlaySound(SoundID.Item9 with { Volume = 0.55f, Pitch = -0.15f }, Owner.Center);
            }
            else {
                //常规三星
                for (int i = 0; i < 3; i++) {
                    Vector2 land = aim + new Vector2((i - 1) * 64f + Main.rand.NextFloat(-18f, 18f),
                        Main.rand.NextFloat(-12f, 12f));
                    CallStar(land, Math.Max(1, (int)(baseDamage * 0.7f)), 0, i * 3, 23f);
                }
            }
        }

        /// <summary>在落点上空生成一颗坠星（owner 守门在 SpawnOwnedProj 内）</summary>
        private void CallStar(Vector2 land, int damage, int mode, int delay, float speed) {
            Vector2 spawn = new(land.X + Main.rand.NextFloat(-70f, 70f),
                land.Y - 500f - Main.rand.NextFloat(0f, 110f));
            Vector2 vel = (land - spawn).SafeNormalize(Vector2.UnitY) * speed;
            SpawnOwnedProj(ModContent.ProjectileType<GsStarWrathFallProj>(), spawn, vel,
                damage, Projectile.knockBack * 0.45f, mode, delay, land.Y);
        }

        /// <summary>剑击攒潮</summary>
        protected override void OnHitTarget(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Owner.whoAmI != Main.myPlayer) {
                return;
            }
            Scheme?.GainTide(Owner.Center);
        }

        protected override void HandleParticles(int phase) {
            base.HandleParticles(phase);
            if (IsFinisher && phase is PhaseRaise or PhaseHold) {
                //重劈蓄势：星尘自四周向刃身汇拢
                Vector2 blade = Vector2.Lerp(Hand, mainTip, Main.rand.NextFloat(0.4f, 1f));
                Vector2 from = blade + Main.rand.NextVector2Unit() * Main.rand.NextFloat(34f, 64f);
                PRTLoader.NewParticle<PRT_Light>(from, (blade - from) * 0.15f,
                    Main.rand.NextBool() ? GsStarWrath.NightMain : GsStarWrath.NightHot,
                    Main.rand.NextFloat(0.05f, 0.1f))?.Configure(9, 0.6f);
            }
        }

        /// <summary>命中反馈：星屑迸溅 + 低星鸣（与陨星蓝钢的金调区分，走紫白冷调）</summary>
        protected override void OnHitFX(NPC target, NPC.HitInfo hit, int damageDone) {
            base.OnHitFX(target, hit, damageDone);
            SoundEngine.PlaySound(SoundID.Item9 with {
                Volume = 0.28f,
                Pitch = Main.rand.NextFloat(-0.2f, 0.1f),
                MaxInstances = 3
            }, target.Center);
            int shards = IsFinisher ? 5 : 3;
            for (int i = 0; i < shards; i++) {
                PRTLoader.NewParticle<PRT_HeavenfallStar>(target.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 5f),
                    Main.rand.NextBool(3) ? GsStarWrath.NightHot : GsStarWrath.NightBright,
                    Main.rand.NextFloat(0.26f, 0.44f))?.Configure(true, Main.rand.Next(12, 20));
            }
        }

        /// <summary>星潮刻星沿刀脊排布 + 潮满重劈蓄势的紫星环（层数 owner 独有，只画给 owner）</summary>
        protected override void DrawExtra(SpriteBatch sb, Color lightColor) {
            if (Owner.whoAmI != Main.myPlayer) {
                return;
            }
            GsStarWrath scheme = Scheme;
            int tide = scheme?.StarTide ?? 0;
            Texture2D star = CWRAsset.StarGlow01?.Value;
            Texture2D flare = CWRAsset.StarFlare01?.Value;
            if (star == null || flare == null) {
                return;
            }
            bool full = tide >= GsStarWrath.StarTideMax;

            //潮满 + 重劈蓄势：身前紫星环回旋
            if (full && IsFinisher && CurrentPhase <= PhaseHold) {
                float p = CurrentPhase == PhaseHold ? 1f : MathHelper.Clamp(timer / (float)raiseDur, 0f, 1f);
                Vector2 anchor = Vector2.Lerp(Hand, mainTip, 0.55f) - Main.screenPosition;
                float rot = Main.GlobalTimeWrappedHourly * 1.5f * swingDir + DrawRand01(2) * 6.28f;
                Color halo = GsStarWrath.NightHot * (0.24f + 0.3f * p);
                halo.A = 0;
                sb.Draw(flare, anchor, null, halo, rot, flare.Size() * 0.5f, 0.36f + 0.15f * p, SpriteEffects.None, 0f);
            }

            if (tide <= 0 || fanFade <= 0.05f) {
                return;
            }
            Vector2 hand = Hand;
            for (int i = 0; i < tide; i++) {
                Vector2 at = hand + mainAngle.ToRotationVector2() * (mainReach * (0.26f + 0.095f * i))
                    - Main.screenPosition;
                float pulse = 0.7f + 0.3f * MathF.Sin(Main.GlobalTimeWrappedHourly * 7f + i * 1.4f + DrawRand01(i + 6) * 6.28f);
                Color c = (full ? GsStarWrath.NightHot : GsStarWrath.NightBright) * (0.5f * fanFade * pulse);
                c.A = 0;
                sb.Draw(star, at, null, c, Main.GlobalTimeWrappedHourly * 1.2f + i,
                    star.Size() * 0.5f, 0.13f, SpriteEffects.None, 0f);
            }
        }
    }

    /// <summary>
    /// 夜陨坠星：ai[0]=模式（0 常规 / 1 雨瀑小星 / 2 压轴大星）ai[1]=起落延迟帧 ai[2]=落点高度。
    /// 待发期悬止隐身；坠落前快后缓（0.972 衰减至下限），彗尾长度随速度伸缩；
    /// 抵达落点高度炸星爆，大星另炸星环；穿墙保留原版身份。绘制抖动 identity 播种
    /// </summary>
    internal class GsStarWrathFallProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private int Mode => (int)Projectile.ai[0];
        private int Delay => (int)Projectile.ai[1];
        private float LandY => Projectile.ai[2];
        private ref float Age => ref Projectile.localAI[0];
        private bool Active => Age > Delay;
        private float SizeMul => Mode switch { 1 => 0.72f, 2 => 1.7f, _ => 1f };

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 26;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 2;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.timeLeft = 190;
        }

        private float SegRand(int salt) {
            uint h = (uint)(Projectile.identity * 374761393 + salt * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xFFFFFF) / (float)0x1000000;
        }

        //待发悬止：位置冻结，保留出膛向量
        public override bool ShouldUpdatePosition() => Active;

        public override bool? CanDamage() => Age > Delay + 1 ? null : false;

        public override void AI() {
            Age++;
            if (!Active) {
                return;
            }
            float t = Age - Delay;
            if (t == 1f) {
                //现身：大星撑框加穿透，星啸按体量定调
                if (Mode == 2) {
                    Projectile.Resize(44, 44);
                    Projectile.penetrate = 4;
                }
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item9 with {
                        Volume = Mode == 2 ? 0.5f : 0.32f,
                        Pitch = Mode switch { 1 => 0.3f, 2 => -0.35f, _ => 0.05f },
                        MaxInstances = 5,
                    }, Projectile.Center);
                    PRTLoader.NewParticle<PRT_Light>(Projectile.Center, Vector2.Zero,
                        GsStarWrath.NightBright, 0.16f * SizeMul)?.Configure(8, 0.7f);
                }
            }

            //前快后缓：速度衰减至体量下限，全程不匀速
            float floor = Mode switch { 1 => 9f, 2 => 13f, _ => 10f };
            float speed = MathF.Max(floor, Projectile.velocity.Length() * 0.972f);
            //微幅侧摆，彗path带一丝呼吸
            Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.UnitY)
                .RotatedBy(MathF.Sin((t + Projectile.identity % 17) * 0.28f) * 0.005f) * speed;
            Projectile.rotation += 0.12f * (SegRand(1) > 0.5f ? 1f : -1f);

            if (!VaultUtils.isServer) {
                if ((int)t % 2 == 0) {
                    //彗尾余滴
                    PRTLoader.NewParticle<PRT_Light>(
                        Projectile.Center - Projectile.velocity * 0.5f + Main.rand.NextVector2Circular(4f, 4f),
                        -Projectile.velocity * 0.04f,
                        Main.rand.NextBool(3) ? GsStarWrath.NightHot : GsStarWrath.NightMain,
                        Main.rand.NextFloat(0.04f, 0.08f) * SizeMul)?.Configure(10, 0.6f);
                }
                Lighting.AddLight(Projectile.Center, GsStarWrath.NightMain.ToVector3() * (0.5f * SizeMul));
            }

            //抵达落点高度即爆
            if (Projectile.Center.Y >= LandY) {
                Projectile.Kill();
            }
        }

        /// <summary>流星命中也攒潮（owner 端独占）</summary>
        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Projectile.owner == Main.myPlayer
                && GodSmithScheme.TryGetScheme(ItemID.StarWrath, out GodSmithScheme s)
                && s is GsStarWrath scheme) {
                scheme.GainTide(target.Center);
            }
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_HeavenfallStar>(target.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 4.5f),
                    GsStarWrath.NightBright, Main.rand.NextFloat(0.24f, 0.4f))
                    ?.Configure(true, Main.rand.Next(10, 18));
            }
        }

        public override void OnKill(int timeLeft) {
            //大星落点炸星环（owner 端生成，随包同步）
            if (Mode == 2 && Projectile.owner == Main.myPlayer) {
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, Vector2.Zero,
                    ModContent.ProjectileType<GsStarWrathRingProj>(),
                    Math.Max(1, (int)(Projectile.damage * 0.45f)), 3f, Projectile.owner);
            }
            if (VaultUtils.isServer) {
                return;
            }
            //命中相：落点星爆，体量定强度
            SoundEngine.PlaySound(SoundID.Item88 with {
                Volume = Mode == 2 ? 0.55f : 0.35f,
                Pitch = Mode == 2 ? -0.35f : -0.1f,
                MaxInstances = 5,
            }, Projectile.Center);
            PRTLoader.NewParticle<PRT_Light>(Projectile.Center, Vector2.Zero,
                GsStarWrath.NightHot, 0.22f * SizeMul)?.Configure(12, 0.85f);
            PRTLoader.NewParticle<PRT_StarPulseRing>(Projectile.Center, Vector2.Zero,
                GsStarWrath.NightMain, 0.05f)?.Configure(0.06f, 0.34f * SizeMul, 14);
            int shards = Mode == 1 ? 4 : Mode == 2 ? 10 : 6;
            for (int i = 0; i < shards; i++) {
                PRTLoader.NewParticle<PRT_HeavenfallStar>(Projectile.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(2.5f, 6f + 2f * SizeMul),
                    Main.rand.NextBool(3) ? GsStarWrath.NightHot : GsStarWrath.NightBright,
                    Main.rand.NextFloat(0.26f, 0.46f))?.Configure(true, Main.rand.Next(14, 24));
            }
            //余痕相：星屑缓落飘散
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_Light>(
                    Projectile.Center + Main.rand.NextVector2Circular(14f, 8f),
                    new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), Main.rand.NextFloat(0.3f, 0.8f)),
                    GsStarWrath.NightMain, Main.rand.NextFloat(0.05f, 0.09f))?.Configure(16, 0.7f);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            if (!Active) {
                return false;
            }
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Texture2D cross = CWRAsset.StarTexture?.Value;
            Texture2D core4 = CWRAsset.StarTexture_White?.Value;
            Texture2D glint = CWRAsset.StarGlow01?.Value;
            if (glow == null || cross == null || core4 == null || glint == null) {
                return false;
            }
            Vector2 center = Projectile.Center - Main.screenPosition;
            float presence = MathHelper.Clamp((Age - Delay) / 3f, 0f, 1f);
            float speed = Projectile.velocity.Length();
            Vector2 back = -Projectile.velocity.SafeNormalize(Vector2.UnitY);
            float sizeMul = SizeMul * presence;

            //彗尾：段距随速度伸缩（快则长），白→蓝→紫渐变逐节缩淡
            float spacing = 2.6f + speed * 0.32f;
            for (int i = 1; i <= 12; i++) {
                float k = i / 12f;
                Vector2 at = center + back * (i * spacing * sizeMul);
                Color c = k < 0.4f
                    ? Color.Lerp(GsStarWrath.NightBright, GsStarWrath.NightMain, k / 0.4f)
                    : Color.Lerp(GsStarWrath.NightMain, GsStarWrath.NightHot, (k - 0.4f) / 0.6f);
                c *= 0.5f * (1f - k) * presence;
                c.A = 0;
                Main.EntitySpriteDraw(glow, at, null, c, 0f, glow.Size() * 0.5f,
                    (0.34f - 0.24f * k) * sizeMul, SpriteEffects.None, 0);
                if (i % 3 == 0) {
                    //尾流星屑闪点，相位错开
                    float tw = 0.6f + 0.4f * MathF.Sin(Main.GlobalTimeWrappedHourly * 9f + SegRand(i + 20) * 6.28f);
                    Color gc = GsStarWrath.NightBright * (0.4f * (1f - k) * tw * presence);
                    gc.A = 0;
                    Main.EntitySpriteDraw(glint, at + new Vector2(SegRand(i) - 0.5f, SegRand(i + 40) - 0.5f) * 8f,
                        null, gc, SegRand(i + 60) * 6.28f, glint.Size() * 0.5f, 0.12f * sizeMul, SpriteEffects.None, 0);
                }
            }

            //星芯三层：外晕 + 紫四芒缓旋 + 白芯
            float pulse = 0.85f + 0.15f * MathF.Sin(Main.GlobalTimeWrappedHourly * 12f + SegRand(5) * 6.28f);
            Color halo = GsStarWrath.NightMain * (0.55f * pulse * presence);
            halo.A = 0;
            Main.EntitySpriteDraw(glow, center, null, halo, 0f, glow.Size() * 0.5f, 0.72f * sizeMul, SpriteEffects.None, 0);
            Color arms = GsStarWrath.NightHot * (0.7f * pulse * presence);
            arms.A = 0;
            Main.EntitySpriteDraw(cross, center, null, arms, Projectile.rotation * 0.5f,
                cross.Size() * 0.5f, 0.17f * sizeMul, SpriteEffects.None, 0);
            Color coreC = GsStarWrath.NightBright * (0.9f * presence);
            coreC.A = 0;
            Main.EntitySpriteDraw(core4, center, null, coreC, -Projectile.rotation * 0.3f,
                core4.Size() * 0.5f, 0.075f * sizeMul * pulse, SpriteEffects.None, 0);
            return false;
        }
    }

    /// <summary>
    /// 星怒星环：压轴大星落点的扩张环。8 帧过冲撑满后回坐，伤害只在扩张期结算一次；
    /// 环身由星珠列队 + 内圈紫焰光斑构成，击退向外。绘制全走确定性相位
    /// </summary>
    internal class GsStarWrathRingProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int TotalLife = 22;
        private const float MaxRadius = 150f;
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

        private float SegRand(int salt) {
            uint h = (uint)(Projectile.identity * 374761393 + salt * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xFFFFFF) / (float)0x1000000;
        }

        public override void AI() {
            Life++;
            if (Life == 1f && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.5f, Pitch = -0.4f }, Projectile.Center);
                for (int i = 0; i < 10; i++) {
                    PRTLoader.NewParticle<PRT_HeavenfallStar>(Projectile.Center,
                        Main.rand.NextVector2Unit() * Main.rand.NextFloat(3f, 8f),
                        Main.rand.NextBool() ? GsStarWrath.NightHot : GsStarWrath.NightBright,
                        Main.rand.NextFloat(0.3f, 0.5f))?.Configure(true, Main.rand.Next(14, 24));
                }
            }
            Lighting.AddLight(Projectile.Center, GsStarWrath.NightHot.ToVector3() * (0.8f * (1f - Life01)));
        }

        //伤害只在扩张期结算（一目标一次）
        public override bool? CanDamage() => Life <= 9f ? null : false;

        /// <summary>圆环判定：碰到当前扩张半径即命中</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
            => targetHitbox.Distance(Projectile.Center) <= Radius;

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
            => modifiers.HitDirectionOverride = Math.Sign(target.Center.X - Projectile.Center.X);

        public override bool PreDraw(ref Color lightColor) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Texture2D glint = CWRAsset.StarGlow01?.Value;
            Texture2D core4 = CWRAsset.StarTexture_White?.Value;
            Texture2D flare = CWRAsset.StarFlare01?.Value;
            if (glow == null || glint == null || core4 == null || flare == null) {
                return false;
            }
            Vector2 center = Projectile.Center - Main.screenPosition;
            float fade = 1f - Life01;
            float radius = Radius;

            //爆心白星与紫焰光斑：首帧最亮随后蚀散
            Color flash = GsStarWrath.NightBright * (0.85f * fade * fade);
            flash.A = 0;
            Main.EntitySpriteDraw(core4, center, null, flash, SegRand(9) * 6.28f,
                core4.Size() * 0.5f, 0.16f + 0.1f * Life01, SpriteEffects.None, 0);
            Color flareC = GsStarWrath.NightHot * (0.45f * fade);
            flareC.A = 0;
            Main.EntitySpriteDraw(flare, center, null, flareC, Life * 0.06f,
                flare.Size() * 0.5f, 0.5f * (0.6f + 0.4f * Life01), SpriteEffects.None, 0);

            //扩张星珠环：星点沿半径列队，相位确定性错开
            const int beads = 16;
            for (int i = 0; i < beads; i++) {
                float ang = MathHelper.TwoPi * i / beads + SegRand(i) * 0.35f + Life * 0.02f;
                Vector2 at = center + ang.ToRotationVector2() * radius;
                float tw = 0.7f + 0.3f * MathF.Sin(Main.GlobalTimeWrappedHourly * 8f + SegRand(i + 30) * 6.28f);
                Color bead = GsStarWrath.NightMain * (0.5f * fade * tw);
                bead.A = 0;
                Main.EntitySpriteDraw(glow, at, null, bead, 0f, glow.Size() * 0.5f,
                    0.24f + 0.08f * SegRand(i + 60), SpriteEffects.None, 0);
                if (i % 2 == 0) {
                    Color spark = GsStarWrath.NightBright * (0.55f * fade * tw);
                    spark.A = 0;
                    Main.EntitySpriteDraw(glint, at, null, spark, ang, glint.Size() * 0.5f,
                        0.14f, SpriteEffects.None, 0);
                }
            }
            return false;
        }
    }
}
