using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ocean.Projectiles
{
    /// <summary>
    /// 鲨鱼掠食预告（双模式）。ai[0]=来源打包（whoAmI+1 | type&lt;&lt;8，施法者死亡或槽位复用即取消）
    /// ai[1]=模式（0 贴身潜行：绕行+龇牙全程跟随鲨鱼 / 1 破水泡沫痕：锚定水面锁定点，位置即承诺不追踪）
    /// ai[2]=模式0 锁向信号（0=未锁；龇牙起手帧由 NPC 写入 lockDir+10 并 netUpdate，预告即承诺）；
    /// 模式1 跃咬横向 ±1（弧线方向指示）。
    /// 本体永不造成伤害；伤害窗=预告完成后鲨鱼本体的接触段
    /// </summary>
    internal class OceanSharkOmen : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        internal const int ModeStalk = 0;
        internal const int ModeBreach = 1;

        /// <summary>潜行模式预告帧数 = 绕行 40 + 龇牙 24（对齐 OceanBrutalNPC 相位表，≥30 契约）</summary>
        internal const int StalkFrames = 64;
        /// <summary>潜行模式内龇牙段时长（锁向发生在该段起点）</summary>
        internal const int StalkSnarlFrames = 24;
        /// <summary>破水模式预告帧数（≥30 契约，档位一律不缩短）</summary>
        internal const int BreachFrames = 34;
        /// <summary>预告完成后的消散帧</summary>
        private const int FadeFrames = 8;
        /// <summary>泡沫痕半长（像素）</summary>
        private const float FoamHalfLength = 56f;
        /// <summary>锁向后的突进指示巷长度（像素，只是方向指示，实际突进距离由包络决定）</summary>
        private const float LaneLength = 150f;

        private int Mode => (int)Projectile.ai[1];
        private int TotalTelegraph => Mode == ModeBreach ? BreachFrames : StalkFrames;
        private int Elapsed => TotalTelegraph + FadeFrames - Projectile.timeLeft;
        private float Charge => MathHelper.Clamp(Elapsed / (float)TotalTelegraph, 0f, 1f);
        /// <summary>模式0：是否已写入锁定方向（预告即承诺，锁定后不再变更）</summary>
        private bool Locked => Mode == ModeStalk && Projectile.ai[2] != 0f;
        private float LockDir => Projectile.ai[2] - 10f;
        private float DirSign => Projectile.ai[2];

        private bool Cancelled {
            get => Projectile.localAI[1] == 1f;
            set => Projectile.localAI[1] = value ? 1f : 0f;
        }

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 400;

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = StalkFrames + FadeFrames;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        /// <summary>纯预告体，永不参与伤害</summary>
        public override bool? CanDamage() => false;

        private bool TryHost(out NPC host) {
            host = null;
            int packed = (int)Projectile.ai[0];
            int src = (packed & 255) - 1;
            if (src < 0 || src >= Main.maxNPCs || !Main.npc[src].active
                || Main.npc[src].type != packed >> 8) {
                return false;
            }
            host = Main.npc[src];
            return true;
        }

        public override void AI() {
            //首帧按模式定死时间轴（两端以同一 ai 值各自展开；timeLeft 不进同步包）
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                Projectile.timeLeft = TotalTelegraph + FadeFrames;
            }

            bool hostValid = TryHost(out NPC host);
            //来源校验：施法者死亡则取消（击杀施法者=有效反制）；类型比对防槽位复用
            if (!Cancelled && Elapsed < TotalTelegraph && !hostValid) {
                Cancelled = true;
            }

            //潜行模式贴身跟随（各端从同步的 NPC 位置确定性推得）
            if (Mode == ModeStalk && hostValid) {
                Projectile.Center = host.Center;
            }

            if (Main.dedServ) {
                return;
            }
            float charge = Charge;
            Lighting.AddLight(Projectile.Center, 0.10f + 0.18f * charge, 0.04f, 0.02f);

            //预告完成瞬间的各端本地音效（突进/跃咬本体由 NPC 相位机执行）
            if (Elapsed == TotalTelegraph && !Cancelled) {
                if (Mode == ModeBreach) {
                    SoundEngine.PlaySound(SoundID.Splash with { Volume = 0.8f, MaxInstances = 4 }, Projectile.Center);
                    for (int i = 0; i < 12; i++) {
                        Dust splash = Dust.NewDustPerfect(
                            Projectile.Center + new Vector2(Main.rand.NextFloat(-FoamHalfLength, FoamHalfLength) * 0.6f, 0f),
                            DustID.Water, new Vector2(Main.rand.NextFloat(-1.6f, 1.6f), -Main.rand.NextFloat(2.5f, 5.5f)),
                            60, default, Main.rand.NextFloat(1f, 1.7f));
                        splash.noGravity = false;
                    }
                }
                else {
                    SoundEngine.PlaySound(SoundID.Splash with { Volume = 0.5f, Pitch = -0.25f, MaxInstances = 4 }, Projectile.Center);
                }
                return;
            }
            if (Cancelled || Elapsed >= TotalTelegraph || Main.rand.NextBool(3)) {
                return;
            }

            //预告期泡沫（≤2 粒/帧）：潜行=环绕鲨体的压速泡沫尘，破水=水面推沫
            if (Mode == ModeStalk) {
                Vector2 pos = Projectile.Center + Main.rand.NextVector2Circular(26f, 18f);
                Dust bubble = Dust.NewDustPerfect(pos, DustID.Water,
                    new Vector2(0f, -Main.rand.NextFloat(0.4f, 1.2f + charge)), 90, default, 1f);
                bubble.noGravity = true;
                //龇牙段追加急促白沫，读作起手
                if (Locked && Main.rand.NextBool(2)) {
                    Dust snarl = Dust.NewDustPerfect(
                        Projectile.Center + LockDir.ToRotationVector2() * 22f + Main.rand.NextVector2Circular(6f, 6f),
                        DustID.Water, LockDir.ToRotationVector2() * Main.rand.NextFloat(1f, 2.2f), 60, default, 1.2f);
                    snarl.noGravity = true;
                }
            }
            else {
                float off = Main.rand.NextFloat(-FoamHalfLength, FoamHalfLength);
                Dust foam = Dust.NewDustPerfect(Projectile.Center + new Vector2(off, Main.rand.NextFloat(-3f, 3f)),
                    DustID.Water, new Vector2(DirSign * (0.7f + charge), -Main.rand.NextFloat(0.3f, 1.1f)),
                    70, default, 1f + 0.5f * charge);
                foam.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            float fade;
            if (Cancelled) {
                fade = 0.35f * MathHelper.Clamp(1f - Elapsed / (float)TotalTelegraph, 0f, 1f);
            }
            else if (Elapsed >= TotalTelegraph) {
                fade = MathHelper.Clamp(1f - (Elapsed - TotalTelegraph) / (float)FadeFrames, 0f, 1f);
            }
            else {
                fade = MathHelper.Clamp(Elapsed / 8f, 0f, 1f);
            }
            if (fade <= 0.01f) {
                return false;
            }
            float charge = Charge;
            float pulse = 0.7f + 0.3f * MathF.Sin(Main.GlobalTimeWrappedHourly * 15f + Projectile.identity);
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Texture2D body = CWRAsset.Extra_98.Value;
            Vector2 orig = glow.Size() / 2f;
            Vector2 center = Projectile.Center - Main.screenPosition;
            Color warn = new Color(255, 80, 60, 0) * (fade * pulse);

            if (Mode == ModeStalk) {
                //潜行涡痕：真 alpha 暗纺锤衬底 + 环绕泡点，末段（龇牙）读数增亮
                Color wake = new Color(30, 52, 66) * (0.35f * fade);
                Main.EntitySpriteDraw(body, center, null, wake, 0f, body.Size() / 2f,
                    new Vector2(1.5f, 0.7f), SpriteEffects.None, 0);
                const int dots = 10;
                float spin = Main.GlobalTimeWrappedHourly * 2.2f;
                for (int i = 0; i < dots; i++) {
                    float ang = MathHelper.TwoPi * i / dots + spin;
                    Vector2 pos = center + new Vector2(MathF.Cos(ang) * 34f, MathF.Sin(ang) * 20f);
                    Main.EntitySpriteDraw(glow, pos, null, warn * (0.25f + 0.30f * charge), 0f, orig,
                        0.024f + 0.010f * charge, SpriteEffects.None, 0);
                }
                if (Locked) {
                    //锁向突进指示巷：方向自锁定帧起为承诺（突进期不再重瞄），巷越亮越临近出手
                    float snarlT = MathHelper.Clamp((Elapsed - (StalkFrames - StalkSnarlFrames)) / (float)StalkSnarlFrames, 0f, 1f);
                    Vector2 dir = LockDir.ToRotationVector2();
                    Vector2 lanePos = center + dir * (LaneLength * 0.5f * (0.4f + 0.6f * snarlT));
                    Main.EntitySpriteDraw(glow, lanePos, null, warn * (0.55f * snarlT), LockDir, orig,
                        new Vector2(2.2f * snarlT + 0.4f, 0.22f), SpriteEffects.None, 0);
                    //龇牙亮点（口部前方）
                    Main.EntitySpriteDraw(glow, center + dir * 26f, null,
                        (Color.White with { A = 0 }) * (0.5f * snarlT * pulse), 0f, orig, 0.05f, SpriteEffects.None, 0);
                }
            }
            else {
                //破水泡沫痕：水面横向白沫，随蓄力拉长增亮，端点偏向跃咬方向
                Color foamBody = new Color(206, 232, 244) * (0.5f * fade * (0.4f + 0.6f * charge));
                Main.EntitySpriteDraw(body, center, null, foamBody, 0f, body.Size() / 2f,
                    new Vector2((FoamHalfLength * 2f * (0.35f + 0.65f * charge)) / 47f, 0.28f), SpriteEffects.None, 0);
                Color sheen = new Color(220, 245, 255, 0) * (fade * 0.5f * pulse);
                Main.EntitySpriteDraw(glow, center, null, sheen, 0f, orig,
                    new Vector2(0.12f + 0.15f * charge, 0.04f), SpriteEffects.None, 0);
                Main.EntitySpriteDraw(glow, center + new Vector2(DirSign * FoamHalfLength * charge, 0f),
                    null, warn * 0.5f, 0f, orig, 0.05f, SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
