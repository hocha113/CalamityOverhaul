// ============================================================================
//LanternWardenCone.fx 提灯巡守的灯油光域锥(L2 牢狱层,提灯与警报语汇)
//材质=灯油火透过牢狱尘埃的光域,三个签名行为:
//  1.锥内尘埃随光轴漂移明灭(双尺度噪声阈值,近轴更密)
//  2.锥缘随灯焰摇曳晃动(远端摆幅大,火光不是探照灯)
//  3.远端衰减成散噪碎光,撞墙即在墙前撕散(uReach 前沿,禁一刀切)
//
//画法:spriteBatch 四边形,origin=左中,沿灯锥轴旋转;uv.x=沿轴0(灯)..1(远),
//uv.y=横截0..1(内部换算-1..1)。只进 Additive 批:rgb 不预乘,a 携带全部包络
//(加色批源因子是 SourceAlpha,A=0 整层消失)。
//
//画布契约(与 LanternWardenRender 同源):
//  uQuadLen/uQuadWide=quad 世界px;uSpread=半角正切(锥半宽=沿轴px*uSpread+根半径6px);
//  锥体最大半宽须落在 quad 半宽的 ~92% 内,余量吃锥缘摆动(消费端按同式折算)
//  uReach=墙前沿(quad 长度比例),shader 内钳 0.93 保远端撕散不吃 quad 硬边
//
//探测公平合同:判定半径 340px(LanternWarden.ConeRange),消费端 quadLen=385,
//  1*0.93*385≈358px>340,亮体覆盖整个判定域,光照到哪判到哪
//坐标全笛卡尔无 atan2;直线算术+普通 tex2D,无动态分支,FNA3D 安全
//绑定噪声 PerlinNoise 实测值域 0.227~0.776,阈值一律过 nrm() 归一
//消费入口 Content/Scenarios/Dungeonworld/NPCs/Elites/LanternWardenRender.cs
// ============================================================================

sampler uImage0 : register(s0);   //白像素画布(不采样)
sampler uNoiseTex : register(s1); //PerlinNoise,LinearWrap

float uTime;      //秒
float uSeed;      //个体相位
float uLevel;     //灯焰强度 0..1.6(FlameLevel 直喂)
float uAlert;     //入锥宽限警觉 0..1(锥色发白+高频双闪)
float uReach;     //墙前沿,quad 长度比例 0..1
float uQuadLen;   //quad 长(世界px)
float uQuadWide;  //quad 全宽(世界px)
float uSpread;    //锥半角正切(tan27°≈0.510)

//灯油火色板(暖橙体+奶金芯,与 C# LampWarm/LampCore 同源)
static const float3 LAMP_WARM = float3(1.000, 0.706, 0.353);
static const float3 LAMP_CORE = float3(1.000, 0.902, 0.667);

//绑定噪声实测值域归一(0.227~0.776)
float nrm(float n) {
    return saturate((n - 0.227) * 1.821);
}

float4 PSCone(float4 vc : COLOR0, float2 uv : TEXCOORD0) : COLOR0 {
    float xPx = uv.x * uQuadLen;             //沿轴世界px
    float acrossPx = (uv.y * 2.0 - 1.0) * uQuadWide * 0.5;

    //锥缘摇曳:低频噪声驱动半宽呼吸,远端摆幅放大(火光晃动沿锥传播)
    float sway = (nrm(tex2D(uNoiseTex, float2(uv.x * 2.6 - uTime * 0.55, uSeed)).r) - 0.5)
        * 0.24 * uv.x;
    float halfW = (xPx * uSpread + 6.0) * (1.0 + sway);
    float d = abs(acrossPx) / max(halfW, 1.0);   //0=轴心 1=锥缘

    //横向软缘 + 轴向衰减(近亮远淡,留底防远端全黑)
    float edge = smoothstep(1.0, 0.66, d);
    float axial = exp2(-uv.x * 2.3) * 1.05 + 0.16;

    //源头收口:灯口 4% 快速淡入,灯体辉光由消费端 SoftGlow 底层承接
    float srcIn = smoothstep(0.0, 0.045, uv.x);

    //墙前沿撕散:噪声毛口,禁一刀切;无墙时钳在 0.93 保 quad 端不硬切
    float fr = min(uReach, 0.93);
    float tearN = nrm(tex2D(uNoiseTex, float2(uv.y * 2.3 + uSeed * 3.1, uv.x * 4.7 - uTime * 0.7)).r);
    float reachMask = 1.0 - smoothstep(fr - 0.11 - 0.10 * tearN, fr + 0.015, uv.x);

    //光棱条纹:横截向缓移亮带,读出"光柱有截面结构"
    float st = 0.82 + 0.18 * nrm(tex2D(uNoiseTex,
        float2(uv.y * 1.7 + uSeed * 5.3, uv.x * 0.55 - uTime * 0.16)).r);

    //锥内尘埃:双尺度噪声阈值,沿轴漂移;细尘快、粗尘慢,近轴密度高
    float2 dustP1 = float2(xPx / 52.0 - uTime * 0.85, acrossPx / 64.0 + uSeed);
    float2 dustP2 = float2(xPx / 21.0 - uTime * 1.90, acrossPx / 26.0 + uSeed * 1.7);
    float mote1 = smoothstep(0.74, 0.95, nrm(tex2D(uNoiseTex, dustP1).r));
    float mote2 = smoothstep(0.80, 0.97, nrm(tex2D(uNoiseTex, dustP2).r));
    float motes = (mote1 * 0.7 + mote2 * 0.55) * smoothstep(1.0, 0.35, d);

    //警觉双闪:与 C# 灯焰双闪同频语汇(~11Hz),锥色向白抬;
    //峰值过冲到 1.2,警觉读作"更亮的频闪"而不是整体压暗
    float flick = lerp(1.0, 0.82 + 0.38 * sin(uTime * 72.0 + uSeed), uAlert);

    //轴心亮芯:近轴加权,光柱有脊,不是均匀楔形贴片
    float body = edge * axial * st * (1.0 + smoothstep(0.55, 0.0, d) * 0.55);
    float3 col = lerp(LAMP_WARM, LAMP_CORE, smoothstep(0.6, 0.0, d) * 0.6 + motes * 0.35);
    col = lerp(col, float3(1.0, 0.97, 0.90), uAlert * 0.45);

    float a = (body * 0.46 + motes * edge * 0.66) * srcIn * reachMask * flick
        * saturate(uLevel) * vc.a;
    return float4(col * (0.88 + 0.35 * motes), a);
}

technique TechCone {
    pass P0 {
        PixelShader = compile ps_3_0 PSCone();
    }
}
