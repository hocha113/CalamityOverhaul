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
    /// 【陨星蓝钢】材质：坠地陨星淬成的召星蓝钢，刀身如嵌满星屑的夜空玻璃。
    /// 签名「星辰共鸣」：①每拍斩切从天顶呼落一枚弧线加速的星辰 ②第三拍「引星」，
    /// 举刀锁定扇形三处预落标记，爆发时三星齐落 ③命中星屑迸溅并鸣响星音
    /// </summary>
    internal class GsStarfury : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.Starfury;

        protected override int HeldProjID => ModContent.ProjectileType<GsStarfuryHeld>();

        protected override string GsDescFallback =>
            "Reforged: every slash calls a star down from the zenith; " +
            "the third strike locks three fated points and brings three stars crashing down together";

        //陨星色板
        internal static readonly Color StarBright = new(255, 238, 190); //星辉淡金
        internal static readonly Color StarMain = new(88, 112, 205);    //陨星蓝钢
        internal static readonly Color StarHot = new(255, 210, 110);    //星金强调
        internal static readonly Color StarDeep = new(10, 14, 40);      //夜穹深蓝

        //底伤 +2%：原版星怒的星辰本就是全伤主力，重铸星辰降为 70% 底伤；
        //每循环星数 1+1+3=5 星（原版 3 挥 3 星），近战终结 1.2x，
        //按 max(useTime, 弹幕总帧) 摊算综合 DPS 约为原版 108%~118%
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.02f;
    }

    /// <summary>
    /// 陨星蓝钢手持：三拍召星连击。0/1 交替斩各唤一星，2 引星终结
    /// （长举锁定扇形三点、DrawExtra 画预落标记，爆发三星齐落）。
    /// ai[0]=拍号 ai[1]=交替符号
    /// </summary>
    internal class GsStarfuryHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.Starfury;
        protected override Color EdgeBright => GsStarfury.StarBright;
        protected override Color BodyMain => GsStarfury.StarMain;
        protected override Color HotAccent => GsStarfury.StarHot;
        protected override Color DeepShadow => GsStarfury.StarDeep;

        //夜空玻璃感：刀身往蓝压、辉光常亮
        protected override Color BodyTint(Color lightColor)
            => Color.Lerp(lightColor, GsStarfury.StarMain, 0.28f);
        protected override bool GlowAlways => true;
        protected override Color GlowColor => IsFinisher ? GsStarfury.StarHot : GsStarfury.StarBright;

        /// <summary>星辰落点锚：手心沿出手向前推的固定距离（全由同步量算出，各端一致）</summary>
        private Vector2 StarAnchor => Hand + baseAngle.ToRotationVector2() * 300f;

        protected override GsBroadBeat GetBeat(int stage) {
            if (stage == 2) {
                //引星终结：长举锁星、滞帧读标记、爆发三星齐落
                return new GsBroadBeat {
                    Raise = 10, Hold = 4, Slash = 4, Recover = 12,
                    RaiseBack = 2.2f, Follow = 1.2f, ReachScale = 1.12f, LeanAmp = 0.08f,
                    DamageMult = 1.2f, Hitstop = 2, LungeSpeed = 0f, SwingPitch = -0.3f,
                };
            }
            return new GsBroadBeat {
                Raise = 6, Hold = 2, Slash = 4, Recover = 8,
                RaiseBack = 1.9f, Follow = 1.05f, ReachScale = 1f, LeanAmp = 0.05f,
                DamageMult = 1f, Hitstop = 1, LungeSpeed = 0f,
                SwingPitch = stage == 0 ? -0.1f : -0.16f,
            };
        }

        /// <summary>引星扇形第 i 个预落点（identity 播种微散布，各端一致且逐帧稳定）</summary>
        private Vector2 MarkPoint(int i) {
            Vector2 anchor = StarAnchor;
            Vector2 lateral = (baseAngle + MathHelper.PiOver2).ToRotationVector2();
            float spread = (i - 1) * 92f + (DrawRand01(i * 11 + 5) - 0.5f) * 26f;
            float forward = (i == 1 ? 24f : 0f) + (DrawRand01(i * 17 + 9) - 0.5f) * 20f;
            return anchor + lateral * spread + baseAngle.ToRotationVector2() * forward;
        }

        protected override void OnSlashBegin() {
            //召星：普通拍落锚点一星，引星终结三点齐落（除回 DamageMult 取底伤摊账）
            int baseDamage = Math.Max(1, (int)(Projectile.damage / Beat.DamageMult));
            int starDamage = Math.Max(1, (int)(baseDamage * 0.7f));
            int starType = ModContent.ProjectileType<GsStarfuryStarProj>();
            if (IsFinisher) {
                for (int i = 0; i < 3; i++) {
                    Vector2 mark = MarkPoint(i);
                    SpawnOwnedProj(starType, new Vector2(mark.X, MathF.Max(mark.Y - 760f, 60f)),
                        Vector2.Zero, starDamage, 3f, mark.X, mark.Y, i * 4);
                }
            }
            else {
                Vector2 mark = StarAnchor;
                SpawnOwnedProj(starType, new Vector2(mark.X, MathF.Max(mark.Y - 760f, 60f)),
                    Vector2.Zero, starDamage, 3f, mark.X, mark.Y);
            }
            if (!VaultUtils.isServer) {
                //星鸣：召引的清越星音
                SoundEngine.PlaySound(SoundID.Item9 with { Volume = 0.5f, Pitch = IsFinisher ? -0.2f : 0.25f }, Owner.Center);
            }
        }

        protected override void HandleParticles(int phase) {
            base.HandleParticles(phase);
            //出手相：举刀星光自四周向刃身聚拢（引星拍更盛）
            if (phase is PhaseRaise or PhaseHold) {
                int rate = IsFinisher ? 1 : 3;
                if (Main.rand.NextBool(rate + 1)) {
                    Vector2 blade = Vector2.Lerp(Hand, mainTip, Main.rand.NextFloat(0.5f, 1f));
                    Vector2 from = blade + Main.rand.NextVector2Unit() * Main.rand.NextFloat(30f, 62f);
                    PRTLoader.NewParticle<PRT_Sparkle>(from, (blade - from) * 0.12f,
                        GsStarfury.StarBright, Main.rand.NextFloat(0.14f, 0.26f))
                        ?.Configure(GsStarfury.StarMain, Main.rand.Next(10, 16), 0.06f, 0.7f);
                }
            }
        }

        protected override void OnHitFX(NPC target, NPC.HitInfo hit, int damageDone) {
            base.OnHitFX(target, hit, damageDone);
            //命中星屑迸溅 + 变调星鸣
            SoundEngine.PlaySound(SoundID.Item9 with { Volume = 0.35f, Pitch = Main.rand.NextFloat(0.3f, 0.6f), MaxInstances = 3 }, target.Center);
            int shards = IsFinisher ? 6 : 4;
            for (int i = 0; i < shards; i++) {
                PRTLoader.NewParticle<PRT_HeavenfallStar>(target.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 5.5f),
                    Main.rand.NextBool(3) ? GsStarfury.StarHot : GsStarfury.StarBright,
                    Main.rand.NextFloat(0.28f, 0.46f))?.Configure(true, Main.rand.Next(14, 22));
            }
        }

        /// <summary>引星蓄力时画三个预落标记：淡蓝十字星微闪（identity 定相，绘制不掷 Main.rand）</summary>
        protected override void DrawExtra(SpriteBatch sb, Color lightColor) {
            if (!IsFinisher || CurrentPhase > PhaseHold) {
                return;
            }
            Texture2D cross = CWRAsset.StarGlow01?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (cross == null || glow == null) {
                return;
            }
            //标记随举刀进度浮现
            float reveal = MathHelper.Clamp(timer / (float)raiseDur, 0f, 1f);
            for (int i = 0; i < 3; i++) {
                Vector2 at = MarkPoint(i) - Main.screenPosition;
                float twinkle = 0.65f + 0.35f * MathF.Sin(timer * 0.5f + DrawRand01(i * 23 + 3) * 6.28f);
                Color c = GsStarfury.StarMain * (reveal * 0.6f * twinkle);
                c.A = 0;
                sb.Draw(glow, at, null, c * 0.7f, 0f, glow.Size() / 2f, 0.5f, SpriteEffects.None, 0f);
                sb.Draw(cross, at, null, c, timer * 0.03f, cross.Size() / 2f,
                    0.4f + 0.08f * twinkle, SpriteEffects.None, 0f);
            }
        }
    }

    /// <summary>
    /// 呼落星辰：天顶坠向预落点的星。ai[0]/ai[1]=落点坐标 ai[2]=起落延迟帧。
    /// 下落带横向弧线与纵向加速度（禁匀速）；五角星芯自绘（StarGlow01+StarTexture 加色）；
    /// 沿途星尘拖尾，抵达落点星爆并留缓落余尘；穿 1 敌
    /// </summary>
    internal class GsStarfuryStarProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private Vector2 TargetPoint => new(Projectile.ai[0], Projectile.ai[1]);
        private int SpawnDelay => (int)Projectile.ai[2];
        private ref float AgeTimer => ref Projectile.localAI[0];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 10;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 26;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 2;//穿 1 敌
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.timeLeft = 150;
        }

        /// <summary>确定性伪随机（identity+salt 播种，各端一致）</summary>
        private float SegRand(int salt) {
            uint h = (uint)(Projectile.identity * 374761393 + salt * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xFFFFFF) / (float)0x1000000;
        }

        public override bool? CanDamage() => AgeTimer > SpawnDelay ? null : false;

        public override void AI() {
            AgeTimer++;
            //齐落错帧：延迟期隐身不动，营造三星鱼贯而落
            if (AgeTimer <= SpawnDelay) {
                Projectile.velocity = Vector2.Zero;
                return;
            }
            float age = AgeTimer - SpawnDelay;

            //首帧甩出横向弧线初速（identity 播种，各端一致）
            if (age == 1f) {
                float lean = (SegRand(1) - 0.5f) * 9f;
                Projectile.velocity = new Vector2(lean, 4f);
            }

            //下落相：纵向加速度 + 横向朝落点比例修正，弧线收拢（全程不匀速）
            Projectile.velocity.Y = MathF.Min(Projectile.velocity.Y + 0.42f, 23f);
            float dx = TargetPoint.X - Projectile.Center.X;
            Projectile.velocity.X = MathHelper.Lerp(Projectile.velocity.X, dx * 0.04f, 0.08f);
            Projectile.rotation += 0.22f * (SegRand(2) > 0.5f ? 1f : -1f);

            if (!VaultUtils.isServer) {
                //飞行相：星尘拖尾（星屑 + 小光珠沿途）
                if ((int)age % 2 == 0) {
                    PRTLoader.NewParticle<PRT_Sparkle>(
                        Projectile.Center - Projectile.velocity * 0.4f + Main.rand.NextVector2Circular(4f, 4f),
                        -Projectile.velocity * 0.05f, GsStarfury.StarBright, Main.rand.NextFloat(0.16f, 0.3f))
                        ?.Configure(GsStarfury.StarMain, Main.rand.Next(12, 18), 0.05f, 0.75f);
                }
                if ((int)age % 5 == 0) {
                    PRTLoader.NewParticle<PRT_Light>(Projectile.Center, -Projectile.velocity * 0.03f,
                        GsStarfury.StarMain, Main.rand.NextFloat(0.05f, 0.09f))?.Configure(10, 0.6f);
                }
                Lighting.AddLight(Projectile.Center, GsStarfury.StarMain.ToVector3() * 0.6f);
            }

            //抵达落点即爆（越过落点高度收星，OnKill 出星爆）
            if (Projectile.Center.Y >= TargetPoint.Y) {
                Projectile.Kill();
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item9 with { Volume = 0.4f, Pitch = Main.rand.NextFloat(0.1f, 0.5f), MaxInstances = 3 }, target.Center);
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_HeavenfallStar>(target.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 5f),
                    GsStarfury.StarBright, Main.rand.NextFloat(0.26f, 0.42f))?.Configure(true, Main.rand.Next(12, 20));
            }
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            //命中相：落点星爆（大光核 + 星屑扇）
            SoundEngine.PlaySound(SoundID.Item88 with { Volume = 0.5f, Pitch = -0.1f, MaxInstances = 3 }, Projectile.Center);
            PRTLoader.NewParticle<PRT_Light>(Projectile.Center, Vector2.Zero,
                GsStarfury.StarHot, 0.3f)?.Configure(12, 0.9f);
            for (int i = 0; i < 8; i++) {
                //星屑扇：朝上半圆扇形迸溅
                Vector2 vel = (-MathHelper.PiOver2 + (i / 7f - 0.5f) * 2.4f).ToRotationVector2()
                    * Main.rand.NextFloat(3f, 7f);
                PRTLoader.NewParticle<PRT_HeavenfallStar>(Projectile.Center, vel,
                    Main.rand.NextBool(3) ? GsStarfury.StarHot : GsStarfury.StarBright,
                    Main.rand.NextFloat(0.3f, 0.5f))?.Configure(true, Main.rand.Next(16, 26));
            }
            //余痕相：2~3 粒缓落星屑在落点飘散约 20 帧
            int linger = 2 + (Main.rand.NextBool() ? 1 : 0);
            for (int i = 0; i < linger; i++) {
                PRTLoader.NewParticle<PRT_Sparkle>(
                    Projectile.Center + Main.rand.NextVector2Circular(18f, 8f),
                    new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), Main.rand.NextFloat(0.4f, 0.9f)),
                    GsStarfury.StarBright, Main.rand.NextFloat(0.18f, 0.3f))
                    ?.Configure(GsStarfury.StarMain, Main.rand.Next(18, 24), 0.04f, 0.8f);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            if (AgeTimer <= SpawnDelay) {
                return false;
            }
            Texture2D cross = CWRAsset.StarGlow01?.Value;
            Texture2D star = CWRAsset.StarTexture?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (cross == null || star == null || glow == null) {
                return false;
            }
            Vector2 center = Projectile.Center - Main.screenPosition;

            //飞行拖尾：旧位星芯逐节缩淡
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                Vector2 pos = Projectile.oldPos[i];
                if (pos == Vector2.Zero) {
                    continue;
                }
                pos += Projectile.Size / 2f - Main.screenPosition;
                float k = 1f - i / (float)Projectile.oldPos.Length;
                Color tail = GsStarfury.StarMain * (0.3f * k);
                tail.A = 0;
                Main.EntitySpriteDraw(cross, pos, null, tail, Projectile.oldRot[i],
                    cross.Size() * 0.5f, 0.34f * k, SpriteEffects.None, 0);
            }

            //星芯三层：外晕（SoftGlow）+ 四芒大星（StarTexture）+ 转动小星（StarGlow01），全加色 A=0
            float pulse = 0.85f + 0.15f * MathF.Sin(Main.GlobalTimeWrappedHourly * 12f + SegRand(5) * 6.28f);
            Color halo = GsStarfury.StarMain * (0.55f * pulse);
            halo.A = 0;
            Main.EntitySpriteDraw(glow, center, null, halo, 0f, glow.Size() * 0.5f, 0.9f, SpriteEffects.None, 0);
            Color body = GsStarfury.StarHot * (0.75f * pulse);
            body.A = 0;
            Main.EntitySpriteDraw(star, center, null, body, Projectile.rotation * 0.5f,
                star.Size() * 0.5f, 0.24f, SpriteEffects.None, 0);
            Color core = GsStarfury.StarBright * 0.95f;
            core.A = 0;
            Main.EntitySpriteDraw(cross, center, null, core, Projectile.rotation,
                cross.Size() * 0.5f, 0.5f * pulse, SpriteEffects.None, 0);
            return false;
        }
    }
}
