using CalamityOverhaul.Content.Industrials.ElectricPowers.ControlVisuals;
using CalamityOverhaul.Content.Industrials.MaterialFlow.Pipelines;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using InnoVault.TileProcessors;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.GridSwitches
{
    /// <summary>
    /// 电网总闸TP:嵌进管道线路的电流开关。<br/>
    /// 闭合时依赖基类 <see cref="MachineTP.UpdateConductive"/> 在两侧管道间做压差均衡,
    /// 本机即一段可断开的"导体"(Efficiency=10,对齐管道侧传输速率,不成为瓶颈);<br/>
    /// 断开=基类待机位 <see cref="MachineTP.Disabled"/>:Update 短路后均衡路径物理消失。
    /// 本机不是 BaseUEPipelineTP 也不是 BaseBattery,管道侧连接判定对它 default 直落,
    /// 不建连不喂电,断开状态零泄漏;通断状态随基类包尾/存档键自动同步,本类零序列化代码。<br/>
    /// 右键走 TP 总线(所有端各自翻转+权威端推包),机关线走 HitWire(权威端翻转+推包),两路收敛
    /// </summary>
    internal class GridSwitchTP : MachineTP
    {
        public override int TargetTileID => ModContent.TileType<GridSwitchTile>();
        public override int TargetItem => ModContent.ItemType<GridSwitch>();
        /// <summary>形式容量:管道不识别非电池 TP,基类均衡也不动本机自身电量,恒为空</summary>
        public override float MaxUEValue => 100;

        /// <summary>右键节流时间戳;断开时 UpdateMachine 停摆,不能用 per-tick 递减的冷却</summary>
        private uint lastInteractTime;

        //===== 纯客户端表现状态:全部在绘制帧推进(断开时 UpdateMachine 停摆,Draw 仍每帧执行) =====
        /// <summary>闸刀视觉行程 0=闭合 1=断开(合闸过冲期允许小幅负值)</summary>
        private float leverVisual;
        /// <summary>上一绘制帧所见通断,null=尚未见过</summary>
        private bool? lastDisabledVisual;
        /// <summary>上一次绘制帧号,用于陈旧检测</summary>
        private uint lastVisualFrame;
        /// <summary>合闸砸下动画剩余帧</summary>
        private int slamTimer;
        /// <summary>合闸动画起始行程,支持中途反复扳动</summary>
        private float slamStart;
        /// <summary>分闸抬起动画剩余帧</summary>
        private int liftTimer;
        /// <summary>触点弧光包络:合闸抢先跳弧+分闸余弧共用</summary>
        private float arcGlow;
        /// <summary>触点冲击挤压包络(闸刀砸上触点的一瞬)</summary>
        private float squash;
        /// <summary>邻接管网活性(0..1),端子流动灯的调制源,隔帧采样</summary>
        private float flowActivity;
        /// <summary>下一次活性采样的绘制帧号</summary>
        private uint nextActivitySample;

        public override void SetMachine() {
            //过闸吞吐:两侧管道经本机均衡的每刻步长,对齐管道间传输速率上限
            Efficiency = 10;
        }

        /// <summary>TP右键经InnoVault总线在所有端各自执行,推送收敛权威端(镜像伐木者)</summary>
        public override bool? RightClick(int i, int j, Tile tile, Player player) {
            if (Main.GameUpdateCount - lastInteractTime < 15) {
                return false;
            }
            lastInteractTime = Main.GameUpdateCount;
            Toggle();
            return true;
        }

        /// <summary>
        /// 通断翻转。TP 总线路径:所有端各自执行,推包收敛 !isClient;
        /// HitWire 路径:只在服务器/单人执行(天然权威),同一收敛口径。<br/>
        /// 演出不在此处:开合动画/火花/电弧/音效统一走 Draw 的 Disabled 绘制帧边沿,
        /// 右键与机关线两条触发路径在每个看得见它的客户端上反馈一致
        /// </summary>
        internal void Toggle() {
            Disabled = !Disabled;
            if (!VaultUtils.isClient) {
                SendData();
            }
        }

        /// <summary>
        /// 绘制帧推进闸刀动画与边沿反馈。边沿源:自身 Disabled(基类包尾同步字段)——
        /// 右键路径所有端各自翻转当帧检出,机关线路径远端收包后下一绘制帧检出;
        /// 断开态 UpdateMachine 停摆,动画走绘制帧,与待机短路零耦合。
        /// 屏外错过的翻转直接落位不补演
        /// </summary>
        private void UpdateLeverEnvelope() {
            uint frame = MachineStandbyFX.DrawFrame;
            bool fresh = lastDisabledVisual != null && frame - lastVisualFrame <= MachineStandbyFX.StaleFrameGap;

            if (lastDisabledVisual == null) {
                leverVisual = Disabled ? 1f : 0f;
            }
            else if (lastDisabledVisual != Disabled) {
                if (fresh) {
                    if (Disabled) {
                        //分闸:缓抬,余弧挂在刀口上逐渐拉断
                        liftTimer = 18;
                        slamTimer = 0;
                        arcGlow = 1f;
                        BreakSparks();
                        SoundEngine.PlaySound(SoundID.Unlock with { Volume = 0.7f, Pitch = -0.35f }, CenterInWorld);
                        CombatText.NewText(HitBox, GridSwitch.Tint, GridSwitch.OpenText.Value);
                    }
                    else {
                        //合闸:快砸,触点帧再放火花与弧闪
                        slamTimer = 11;
                        slamStart = MathHelper.Clamp(leverVisual, 0f, 1f);
                        liftTimer = 0;
                        SoundEngine.PlaySound(SoundID.Unlock with { Volume = 0.7f, Pitch = 0.2f }, CenterInWorld);
                        CombatText.NewText(HitBox, GridSwitch.Tint, GridSwitch.CloseText.Value);
                    }
                }
                else {
                    leverVisual = Disabled ? 1f : 0f;
                    slamTimer = 0;
                    liftTimer = 0;
                    arcGlow = 0f;
                }
            }
            lastDisabledVisual = Disabled;
            lastVisualFrame = frame;

            if (slamTimer > 0) {
                slamTimer--;
                int t = 11 - slamTimer;//1..11
                if (t <= 5) {
                    //加速砸下
                    float p = t / 5f;
                    leverVisual = slamStart * (1f - p * p);
                }
                else if (t == 6) {
                    //触点帧:过冲压深+挤压+弧闪+火花+闷响
                    leverVisual = -0.06f;
                    squash = 1f;
                    arcGlow = 1f;
                    ContactBurst();
                    SoundEngine.PlaySound(SoundID.Mech with { Volume = 0.6f, Pitch = -0.4f }, CenterInWorld);
                }
                else if (t <= 8) {
                    leverVisual = -0.04f;
                }
                else {
                    leverVisual = MathHelper.Lerp(leverVisual, 0f, 0.45f);
                }
            }
            else if (liftTimer > 0) {
                liftTimer--;
                float p = 1f - liftTimer / 18f;
                leverVisual = 1f - (1f - p) * (1f - p) * (1f - p);//easeOutCubic 抬起
            }
            else {
                //稳态吸附(含边沿错过的兜底)
                leverVisual = MathHelper.Lerp(leverVisual, Disabled ? 1f : 0f, 0.25f);
            }

            //余弧:分闸期衰减稍慢(拉弧),闭合稳态快速熄灭
            arcGlow *= Disabled ? 0.90f : 0.72f;
            squash *= 0.72f;
            SampleFlowActivity();
        }

        /// <summary>合闸触点火花:白蓝电花+原版电尘</summary>
        private void ContactBurst() {
            Vector2 contact = PosInWorld + new Vector2(16f, 9f);
            for (int n = 0; n < 8; n++) {
                Vector2 vel = (-Vector2.UnitY).RotatedBy(Main.rand.NextFloat(-1.2f, 1.2f)) * Main.rand.NextFloat(1.5f, 4f);
                PRTLoader.NewParticle<PRT_Spark>(contact, vel, new Color(198, 230, 255),
                    Main.rand.NextFloat(0.3f, 0.5f))?.Configure(true, Main.rand.Next(14, 24));
            }
            for (int n = 0; n < 6; n++) {
                Dust dust = Dust.NewDustPerfect(contact + Main.rand.NextVector2Circular(6f, 4f),
                    DustID.Electric, VaultUtils.RandVr(2.2f));
                dust.noGravity = true;
            }
        }

        /// <summary>分闸拉弧断开时的少量残火花</summary>
        private void BreakSparks() {
            Vector2 contact = PosInWorld + new Vector2(16f, 9f);
            for (int n = 0; n < 3; n++) {
                PRTLoader.NewParticle<PRT_Spark>(contact, VaultUtils.RandVr(1.8f), new Color(198, 230, 255),
                    Main.rand.NextFloat(0.24f, 0.36f))?.Configure(true, Main.rand.Next(10, 18));
            }
        }

        /// <summary>隔帧采样邻接管道电量,调制端子流动灯亮度;无管道=不流动</summary>
        private void SampleFlowActivity() {
            uint frame = MachineStandbyFX.DrawFrame;
            if (frame < nextActivitySample) {
                return;
            }
            nextActivitySample = frame + 20;

            float ue = 0f;
            bool any = false;
            int tileWidth = Width / 16;
            int tileHeight = Height / 16;
            void Probe(Point16 point) {
                if (TileProcessorLoader.ByPositionGetTP(point, out var tp)
                    && tp is BaseUEPipelineTP pipe && pipe.MachineData != null) {
                    any = true;
                    ue += pipe.MachineData.UEvalue;
                }
            }
            for (int i = 0; i < tileWidth; i++) {
                Probe(new Point16(Position.X + i, Position.Y - 1));
                Probe(new Point16(Position.X + i, Position.Y + tileHeight));
            }
            for (int j = 0; j < tileHeight; j++) {
                Probe(new Point16(Position.X - 1, Position.Y + j));
                Probe(new Point16(Position.X + tileWidth, Position.Y + j));
            }
            //纯按实际电量:空管道不流动,不给"看着像有电"的假灯
            flowActivity = !any || ue < 0.5f ? 0f : MathHelper.Clamp(ue * 0.04f, 0.12f, 1f);
        }

        /// <summary>触点电弧:合闸前抢先跳弧+分闸余弧,锯齿折线逐帧抖动,附触点辉光</summary>
        private void DrawContactArc(SpriteBatch spriteBatch, Vector2 drawPos, float angle, float intensity) {
            if (intensity <= 0.05f) {
                return;
            }
            Texture2D px = VaultAsset.placeholder2.Value;
            Rectangle src = new(0, 0, 1, 1);
            Vector2 contact = drawPos + new Vector2(16f, 9f);
            Vector2 pivot = drawPos + new Vector2(16f, 25f);
            Vector2 tip = pivot + new Vector2(MathF.Sin(angle) * 15f, -MathF.Cos(angle) * 15f);

            //逐帧闪烁,偶发整帧近熄
            float flicker = 0.55f + Main.rand.NextFloat(0.45f);
            if (Main.rand.NextBool(9)) {
                flicker *= 0.25f;
            }
            float alpha = intensity * flicker;

            Color arcColor = new(186, 224, 255, 0);
            Vector2 normal = (tip - contact).SafeNormalize(Vector2.UnitY).RotatedBy(MathHelper.PiOver2);
            Vector2 prev = contact;
            const int Steps = 4;
            for (int i = 1; i <= Steps; i++) {
                float t = i / (float)Steps;
                Vector2 point = Vector2.Lerp(contact, tip, t);
                if (i != Steps) {
                    point += normal * Main.rand.NextFloat(-2.6f, 2.6f) * intensity;
                }
                Vector2 delta = point - prev;
                float len = delta.Length();
                if (len > 0.01f) {
                    spriteBatch.Draw(px, prev, src, arcColor * alpha, delta.ToRotation(),
                        new Vector2(0f, 0.5f), new Vector2(len, 1.3f), SpriteEffects.None, 0f);
                }
                prev = point;
            }
            if (CWRAsset.SoftGlow?.Value is Texture2D glow) {
                spriteBatch.Draw(glow, contact, null, new Color(150, 200, 255, 0) * (alpha * 0.7f), 0f,
                    glow.Size() * 0.5f, 0.35f, SpriteEffects.None, 0f);
            }
        }

        /// <summary>程序化本体:配电箱+闸刀+端子排;贴图后补,加载零资产</summary>
        public override void Draw(SpriteBatch spriteBatch) {
            UpdateLeverEnvelope();

            Texture2D px = VaultAsset.placeholder2.Value;
            Vector2 drawPos = PosInWorld - Main.screenPosition;
            Color light = Lighting.GetColor(Position.ToPoint());
            Rectangle src = new(0, 0, 1, 1);
            int x = (int)drawPos.X;
            int y = (int)drawPos.Y;

            //箱体外壳/顶檐/内板(断开时内板压暗:柜内断电)
            spriteBatch.Draw(px, new Rectangle(x + 1, y + 2, 30, 30), src, new Color(56, 54, 60).MultiplyRGB(light));
            spriteBatch.Draw(px, new Rectangle(x, y, 32, 3), src, new Color(70, 68, 76).MultiplyRGB(light));
            float open01 = MathHelper.Clamp(leverVisual, 0f, 1f);
            Color panel = Color.Lerp(new Color(30, 29, 34), new Color(20, 19, 23), open01);
            spriteBatch.Draw(px, new Rectangle(x + 4, y + 6, 24, 23), src, panel.MultiplyRGB(light));
            //箱角铆钉与檐下影线
            Color rivet = new Color(92, 90, 100).MultiplyRGB(light);
            spriteBatch.Draw(px, new Rectangle(x + 2, y + 4, 1, 1), src, rivet);
            spriteBatch.Draw(px, new Rectangle(x + 29, y + 4, 1, 1), src, rivet);
            spriteBatch.Draw(px, new Rectangle(x + 2, y + 29, 1, 1), src, rivet);
            spriteBatch.Draw(px, new Rectangle(x + 29, y + 29, 1, 1), src, rivet);
            spriteBatch.Draw(px, new Rectangle(x + 1, y + 3, 30, 1), src, new Color(40, 38, 44).MultiplyRGB(light));
            //箱底黄黑警示条纹
            for (int i = 0; i < 8; i++) {
                Color stripe = i % 2 == 0 ? new Color(150, 122, 46) : new Color(28, 27, 32);
                spriteBatch.Draw(px, new Rectangle(x + 1 + i * 4, y + 30, 4, 2), src, stripe.MultiplyRGB(light));
            }

            //左右端子排:通电=琥珀暖色,断开=冷灰(下游熄灭的第一读)
            Color postWarm = new Color(172, 126, 70);
            Color postCold = new Color(104, 104, 116);
            Color post = Color.Lerp(postWarm, postCold, open01).MultiplyRGB(light);
            spriteBatch.Draw(px, new Rectangle(x, y + 10, 4, 5), src, post);
            spriteBatch.Draw(px, new Rectangle(x, y + 19, 4, 5), src, post);
            spriteBatch.Draw(px, new Rectangle(x + 28, y + 10, 4, 5), src, post);
            spriteBatch.Draw(px, new Rectangle(x + 28, y + 19, 4, 5), src, post);
            //端子瓷柱分隔线
            Color ceramic = new Color(66, 64, 74).MultiplyRGB(light);
            spriteBatch.Draw(px, new Rectangle(x, y + 12, 4, 1), src, ceramic);
            spriteBatch.Draw(px, new Rectangle(x, y + 21, 4, 1), src, ceramic);
            spriteBatch.Draw(px, new Rectangle(x + 28, y + 12, 4, 1), src, ceramic);
            spriteBatch.Draw(px, new Rectangle(x + 28, y + 21, 4, 1), src, ceramic);

            //通电端子指示灯流动:亮点列车左→右穿过箱体,亮度按邻接管网活性调制
            if (!Disabled && flowActivity > 0.05f) {
                for (int row = 0; row < 2; row++) {
                    float rowY = row == 0 ? 12.5f : 21.5f;
                    float phase = (Main.GlobalTimeWrappedHourly * 0.7f + row * 0.5f + Position.X * 0.13f) % 1f;
                    float pipX = MathHelper.Lerp(2f, 30f, phase);
                    float endFade = MathF.Sin(phase * MathHelper.Pi);
                    Color pip = new Color(255, 208, 120, 0) * (0.55f * flowActivity * endFade);
                    spriteBatch.Draw(px, drawPos + new Vector2(pipX, rowY), src, pip, 0f,
                        new Vector2(0.5f, 0.5f), new Vector2(3f, 2f), SpriteEffects.None, 0f);
                }
            }

            //上触点块
            spriteBatch.Draw(px, new Rectangle(x + 13, y + 7, 6, 4), src, new Color(180, 140, 80).MultiplyRGB(light));

            //闸刀:支点在下,闭合竖直搭上触点,断开甩开约 50 度;
            //合闸触点帧短促增粗提亮(冲击挤压),颜色随行程由亮铜转钝
            Vector2 pivot = drawPos + new Vector2(16f, 25f);
            float angle = leverVisual * 0.88f;
            Color blade = Color.Lerp(new Color(214, 176, 108), new Color(140, 120, 92), open01);
            blade = Color.Lerp(blade, new Color(255, 244, 214), squash * 0.8f).MultiplyRGB(light);
            Vector2 bladeScale = new(3f * (1f + squash * 0.5f), 15f * (1f - squash * 0.10f));
            spriteBatch.Draw(px, pivot, src, blade, angle, new Vector2(0.5f, 1f), bladeScale, SpriteEffects.None, 0);
            //支点螺帽
            spriteBatch.Draw(px, new Rectangle((int)pivot.X - 2, (int)pivot.Y - 2, 5, 5), src, new Color(90, 86, 94).MultiplyRGB(light));

            //触点电弧:合闸砸落最后一段抢先跳弧,分闸抬起余弧被拉长扯断
            float arcIntensity = arcGlow;
            if (slamTimer > 0 && leverVisual > 0.02f && leverVisual < 0.4f) {
                arcIntensity = MathF.Max(arcIntensity, (0.4f - leverVisual) * 1.4f);
            }
            if (liftTimer > 0) {
                //抬起中弧随刀口距离变细
                arcIntensity *= MathHelper.Lerp(1f, 0.35f, open01);
            }
            DrawContactArc(spriteBatch, drawPos, angle, MathHelper.Clamp(arcIntensity, 0f, 1f));

            //状态灯:通=绿常亮,断=红呼吸
            Color lampColor;
            if (Disabled) {
                float breathe = 0.55f + MathF.Sin(Main.GlobalTimeWrappedHourly * 4f) * 0.3f;
                lampColor = new Color(235, 84, 74) * breathe;
            }
            else {
                lampColor = new Color(110, 220, 130);
            }
            lampColor.A = 255;
            spriteBatch.Draw(px, new Rectangle(x + 24, y + 8, 3, 3), src, lampColor);
        }

        public override void FrontDraw(SpriteBatch spriteBatch) {
            if (!HoverTP) {
                return;
            }
            //本体不持电,悬停只报通断
            string text = Disabled ? GridSwitch.OpenText.Value : GridSwitch.CloseText.Value;
            Vector2 textSize = FontAssets.MouseText.Value.MeasureString(text) * 0.7f;
            Vector2 drawPos = CenterInWorld + new Vector2(0, Height / 2 + 14) - Main.screenPosition;
            Utils.DrawBorderString(spriteBatch, text, drawPos - textSize * 0.5f, GridSwitch.Tint, 0.7f);
        }
    }
}
