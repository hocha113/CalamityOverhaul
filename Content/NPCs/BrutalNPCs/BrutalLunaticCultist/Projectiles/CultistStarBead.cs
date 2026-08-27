using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Rendering;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Projectiles
{
    /// <summary>
    /// 星珠:司祭与诸星共用的天体弹(实体星芒本体,暗缘+主体+热芯三层)<br/>
    /// ai[0]=阶段色 0~4 ai[1]=模式 0巡星(微增速) 1滞星(悬停驻留后错拍扑袭) 2疾星(复利加速)<br/>
    /// 3流星(星轨齐射专用:extraUpdates 拆步防隧穿,复利续力到极速,寿命封顶)<br/>
    /// 滞星扑袭:ai[2]=扑袭槽位;预瞄线末段冻结=预告即承诺,扑出锁向纯直线;
    /// 远端以速度包幅值识扑(悬停幅值与扑速间有硬缝,不占同步槽)
    /// </summary>
    internal class CultistStarBead : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>滞星首扑拍(出生龄):入环滑行止步驻留后开扑</summary>
        internal const int PounceFirstBeat = 46;
        /// <summary>相邻扑袭槽位错拍(与追星矢同拍宽,节奏可学)</summary>
        internal const int PounceGap = 9;
        /// <summary>扑袭预瞄窗(扑前渐显)</summary>
        private const int PounceAimFrames = 14;
        /// <summary>预瞄冻结窗:此后线不再跟人=承诺</summary>
        private const int PounceFreezeFrames = 6;
        private const float PounceSpeed = 11.5f;
        private const float PounceMaxSpeed = 18f;
        /// <summary>扑出后的飞行帧预算(到点自灭)</summary>
        private const int PounceFlightFrames = 90;

        private int Palette => (int)Projectile.ai[0];
        private int Mode => (int)Projectile.ai[1];
        private ref float Timer => ref Projectile.localAI[0];

        /// <summary>滞星扑袭槽位(定出手拍;其余模式不读)</summary>
        private int PounceSlot => (int)Projectile.ai[2];
        /// <summary>本槽扑袭拍(出生龄)</summary>
        private int PounceBeat => PounceFirstBeat + PounceSlot * PounceGap;
        /// <summary>预瞄方向(本地缓存,冻结窗内不再更新)</summary>
        private Vector2 aimDir = Vector2.UnitY;
        /// <summary>本地已扑(权威端出手拍翻转;远端由本地拍或权威速度包触发)</summary>
        private bool pounced;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 8;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 16;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 360;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        public override void AI() {
            Timer++;
            Projectile.rotation += 0.06f + Projectile.velocity.Length() * 0.004f;

            switch (Mode) {
                case 1:
                    //滞星:泄劲悬停成雷,驻留后按槽位错拍逐颗锁向扑袭;
                    //权威速度包幅值≥扑速即视为已扑(悬停幅值≤2.2,硬缝识别,不吞同步方向)
                    bool syncedPounce = Projectile.velocity.LengthSquared() >= PounceSpeed * PounceSpeed * 0.8f;
                    if (!pounced && Timer < PounceBeat && !syncedPounce) {
                        Projectile.velocity *= 0.965f;
                        //预瞄:显示窗内跟人,冻结窗锁死=承诺
                        Player quarry = Main.player[Player.FindClosest(Projectile.position, Projectile.width, Projectile.height)];
                        if (quarry.Alives() && Timer < PounceBeat - PounceFreezeFrames) {
                            aimDir = (quarry.Center - Projectile.Center).SafeNormalize(Vector2.UnitY);
                        }
                        //冻结窗反向微撤:蓄势后坐,扑势可读
                        if (Timer >= PounceBeat - PounceFreezeFrames) {
                            Projectile.velocity -= aimDir * 0.24f;
                        }
                        if (Projectile.timeLeft > 150) {
                            Projectile.timeLeft = 150;
                        }
                    }
                    else {
                        if (!pounced) {
                            pounced = true;
                            //本地拍先到则按预瞄扑出;权威速度包先到则以同步值为准
                            if (!syncedPounce) {
                                Projectile.velocity = aimDir * PounceSpeed;
                            }
                            Projectile.timeLeft = PounceFlightFrames;
                            CultistMotion.CastFlash(Projectile.Center, CultistMotion.PhaseCore(Palette), 0.7f);
                            if (!VaultUtils.isServer) {
                                SoundEngine.PlaySound(SoundID.Item125 with { Volume = 0.5f, Pitch = 0.05f + PounceSlot * 0.07f }, Projectile.Center);
                            }
                            if (!VaultUtils.isClient) {
                                Projectile.netUpdate = true;
                            }
                        }
                        //扑袭:锁向纯直线复利续力(不追踪)
                        if (Projectile.velocity.Length() < PounceMaxSpeed) {
                            Projectile.velocity *= 1.014f;
                        }
                    }
                    break;
                case 2:
                    //疾星:复利加速
                    if (Projectile.velocity.Length() < 21f) {
                        Projectile.velocity *= 1.014f;
                    }
                    break;
                case 3:
                    //流星:拆步防隧穿(有效弹速=速度×2),复利续力到极速,寿命封顶
                    Projectile.extraUpdates = 1;
                    if (Projectile.timeLeft > 150) {
                        Projectile.timeLeft = 150;
                    }
                    if (Projectile.velocity.Length() < 26f) {
                        Projectile.velocity *= 1.01f;
                    }
                    break;
                default:
                    //巡星:缓增速,拒绝匀速直线
                    if (Projectile.velocity.Length() < 13f) {
                        Projectile.velocity *= 1.006f;
                    }
                    break;
            }

            Lighting.AddLight(Projectile.Center, CultistMotion.PhaseCore(Palette).ToVector3() * 0.42f);
        }

        public override bool? CanDamage() => Timer > 6f;

        public override void OnKill(int timeLeft) {
            //余痕:撞灭后火花与残辉活过弹体
            CultistMotion.ImpactBurst(Projectile.Center, CultistMotion.PhaseLegacyElement(Palette), 0.5f, false);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Color mid = CultistMotion.PhaseCore(Palette);
            Color edge = CultistMotion.PhaseEdge(Palette);
            float twinkle = 1f + 0.08f * (float)Math.Sin(Timer * 0.35f + Projectile.identity * 1.7f);
            //流星略大:承接轨上星珠的体量,不因发射缩水
            float scale = (Mode == 3 ? 0.30f : 0.24f) * Projectile.scale * twinkle;

            //滞星蓄势:预瞄窗内涨体,并放短预瞄线(线指哪,扑哪;冻结拍白热=承诺)
            float aimT = Mode == 1 && !pounced
                ? MathHelper.Clamp((Timer - (PounceBeat - PounceAimFrames)) / (float)PounceAimFrames, 0f, 1f)
                : 0f;
            if (aimT > 0.01f) {
                scale *= 1f + aimT * 0.30f;
                bool frozen = Timer >= PounceBeat - PounceFreezeFrames;
                Color bright = Color.Lerp(mid, Color.White, 0.5f);
                Color deep = Color.Lerp(edge, Color.Black, 0.45f);
                float seed = Projectile.identity % 100 * 0.073f;
                Vector2 root = Projectile.Center - Main.screenPosition;
                Vector2[] pts = [root, root + aimDir * (150f + aimT * 120f)];
                float[] widths = [5f + aimT * 2.5f, 3f];
                float[] alphas = [0.55f * aimT, 0.12f];
                sb.End();
                CultistOrreryRenderer.DrawTechniqueStrip("TechStarLine", pts, widths, alphas,
                    deep, mid, bright, 1f, frozen ? 0f : 11f, frozen ? 0.85f : 0.25f, seed, aimT);
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                    DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            }

            //拖尾:同材质星芒回溯重画(横轴比≈1,同料)
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float t = 1f - i / (float)Projectile.oldPos.Length;
                Vector2 ghostPos = Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition;
                CultistOrreryRenderer.DrawStarBead(sb, ghostPos, mid, edge,
                    scale * (0.4f + 0.5f * t), 0.34f * t, Projectile.rotation - i * 0.08f);
            }

            CultistOrreryRenderer.DrawStarBead(sb, Projectile.Center - Main.screenPosition,
                mid, edge, scale, 1f, Projectile.rotation);
            return false;
        }
    }
}
