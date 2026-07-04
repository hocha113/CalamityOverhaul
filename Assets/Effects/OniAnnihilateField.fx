// ============================================================================
//OniAnnihilateField.fx 鬼哭·灭世一闪蓄力领域（脚下椭圆血渊 + 上方升腾墨流）
//
//FieldTech：世界锚定压扁 quad —— 透视压扁由几何完成，shader 在"地平面圆坐标"
//  里工作（c=中心化 uv，length(c) 即贴地圆半径），花纹随几何一起被压扁，
//  读作躺在地面上而非贴在屏幕上。
//  1) 前后缘不对称描边：前缘(近端)厚而亮、后缘细而暗 —— 透视的第一信号；
//  2) 厚度墙：前缘描边下方错位一条暗带，读作领域的侧壁；
//  3) 内部墨涡：双层反向旋转的笛卡尔噪声 + 低频 domain warp，黑红为体、
//     绯红丝为筋，暗涡压向近黑；中心辉点随蓄力增长（血气汇入极点的呼应）；
//  4) uExpand 展开（横线→椭圆弹开）/ uPulse 脉冲闪 / uDrain 收束抽干
//     （可见半径向中心塌缩，前沿烧蚀亮边）；
//  5) uHalfSel 前后半选择：后半画在玩家身前回调层（身后），前半画在实体层
//     （盖住脚面）—— 立体感来自真实遮挡。
//
//FlowTech：领域上方漫射流动 —— 站立 quad 的柱状上升墨流；uFlowTime 由 C# 外部
//  积分（死寂段降低积分速率即整体减速），底部扎根领域、顶部与两侧羽化散尽。
//
//极角审计：全文件无 atan2/theta 消费链；旋转场走 Rot(t)*(x,y) 刚性仿射，
//径向量只用 length(c)（连续）。噪声输入全部笛卡尔。
//预乘 alpha 输出，配 BlendState.AlphaBlend；ps_3_0 / vs_3_0
// ============================================================================

float4x4 transformMatrix;
float uFlowTime;    //外部积分的流动时间（秒当量，死寂减速由 C# 控制）
float uOpacity;     //整体不透明度
float uExpand;      //0..1 展开进度（0=一条横线）
float uDrain;       //0..1 收束抽干（外缘向中心塌缩）
float uPulse;       //0..1 蓄力脉冲闪（推高后速落）
float uCharge;      //0..1 蓄力总进度（内部能量密度随之增长）
float uSeed;        //实例随机相位
float uHalfSel;     //FieldTech：-1 仅后半 +1 仅前半 0 整体
float uIntensity;   //FlowTech：流带强度包络

float3 uColHot;     //白热
float3 uColBright;  //亮绯红
float3 uColDeep;    //深红
float3 uColDark;    //暗酒红（黑红底）

texture uNoiseTex;
sampler noiseSamp = sampler_state
{
    texture = <uNoiseTex>;
    magfilter = LINEAR;
    minfilter = LINEAR;
    mipfilter = LINEAR;
    AddressU = wrap;
    AddressV = wrap;
};

struct VSInput
{
    float4 Position : POSITION0;
    float2 TexCoords : TEXCOORD0;
    float4 Color : COLOR0;
};

struct PSInput
{
    float4 Position : POSITION0;
    float4 Color : COLOR0;
    float2 TexCoords : TEXCOORD0;
};

PSInput VertexShaderFunction(VSInput v)
{
    PSInput o;
    o.Position = mul(v.Position, transformMatrix);
    o.Color = v.Color;
    o.TexCoords = v.TexCoords;
    return o;
}

//刚性旋转（连续仿射，极角审计安全）
float2 Rot(float2 p, float t)
{
    float cs = cos(t);
    float sn = sin(t);
    return float2(p.x * cs - p.y * sn, p.x * sn + p.y * cs);
}

// ============================ FieldTech ============================

float4 FieldPS(PSInput input) : COLOR0
{
    //quad uv → 中心化圆坐标：uv.y=0 为后缘(远端/屏幕上方)，1 为前缘(近端)
    float2 c = (input.TexCoords - 0.5) * 2.0;

    //---- 展开：cy 除以进度 → 低进度时纵向被推出椭圆外，只剩一条横线 ----
    float ex = max(uExpand, 0.03);
    float2 ce = float2(c.x, c.y / ex);
    float d = length(ce);
    if (d > 1.25)
        return float4(0, 0, 0, 0);

    //近端度：前缘 1 后缘 0（透视不对称的驱动量）
    float fr = saturate(ce.y * 0.85 + 0.5);

    //---- 边缘波动：旋转笛卡尔噪声调制缘半径，避免完美几何椭圆的贴纸感 ----
    float nWave = tex2D(noiseSamp, Rot(ce, uFlowTime * 0.11) * 0.34 + uSeed * 5.3).r;
    float rimR = 0.86 + (nWave - 0.5) * 0.055;

    //---- 前后缘不对称描边 ----
    float rimD = d - rimR;
    float rimW = lerp(0.020, 0.062, fr);
    float rim = 1.0 - smoothstep(0.0, rimW, abs(rimD));
    float rimGlow = exp(-rimD * rimD * 70.0) * lerp(0.45, 1.0, fr);

    //---- 厚度墙：前缘正下方错位的暗带（领域侧壁） ----
    float2 cw = float2(ce.x, ce.y - 0.115);
    float wallD = length(cw) - rimR;
    float wall = (1.0 - smoothstep(0.0, 0.075, abs(wallD))) * saturate(ce.y * 2.2);

    //---- 内部墨涡：双层反向旋转 + domain warp ----
    float2 p1 = Rot(ce, uFlowTime * 0.13) * 0.62 + uSeed * 3.1;
    float2 warp = float2(tex2D(noiseSamp, p1 * 0.55).r
        , tex2D(noiseSamp, p1 * 0.55 + float2(0.31, 0.47)).r) - 0.5;
    float n1 = tex2D(noiseSamp, p1 + warp * 0.30).r;
    float2 p2 = Rot(ce, -uFlowTime * 0.09) * 1.15 + uSeed * 7.7 + warp * 0.55;
    float n2 = tex2D(noiseSamp, p2).r;
    float ink = n1 * 0.60 + n2 * 0.40;

    //---- 收束抽干：可见半径向中心塌缩，前沿烧蚀 ----
    float dTh = 1.10 - uDrain * 1.28;
    float dSurv = 1.0 - smoothstep(dTh - 0.10, dTh + 0.02, d);
    float dBurn = exp(-(d - dTh) * (d - dTh) * 240.0) * saturate(uDrain * 8.0);

    //---- alpha 合成 ----
    float inner = 1.0 - smoothstep(rimR - 0.06, rimR + 0.02, d);   //缘内填充域
    float fill = inner * (0.46 + ink * 0.30 + uCharge * 0.10);
    float alpha = saturate(fill + rim * lerp(0.55, 0.95, fr) + wall * 0.50
        + rimGlow * 0.22 + dBurn * 0.6);
    alpha *= dSurv;
    alpha = saturate(alpha * (1.0 + uPulse * 0.30)) * uOpacity;

    //前后半选择（软边裁切，供玩家遮挡分层两 pass 各画一半）
    float backMask = 1.0 - smoothstep(-0.09, 0.09, ce.y);
    float halfMask = 1.0;
    if (uHalfSel > 0.5)
        halfMask = 1.0 - backMask;
    else if (uHalfSel < -0.5)
        halfMask = backMask;
    alpha *= halfMask;

    if (alpha < 0.004)
        return float4(0, 0, 0, 0);

    //---- 色彩合成 ----
    //墨面：黑红为体，随墨涡起伏
    float3 col = lerp(uColDark * 0.55, uColDeep * 0.85, smoothstep(0.25, 0.78, ink));
    //暗涡压向近黑
    col = lerp(col, uColDark * 0.35, smoothstep(0.40, 0.08, ink) * 0.8);
    //绯红丝筋：高频层高值段
    col += uColBright * smoothstep(0.60, 0.92, n2) * (0.35 + uCharge * 0.75) * inner;
    //中心辉点：血气向极点汇入的呼应，随蓄力增长
    float centerGlow = exp(-d * d * 3.2) * (0.18 + uCharge * 0.95);
    col += lerp(uColBright, uColHot, uCharge * 0.6) * centerGlow;
    //描边与辉光
    float3 rimCol = lerp(uColDeep, uColBright, fr) * (1.0 + uPulse * 1.1);
    col += rimCol * rim * 1.15 + uColBright * rimGlow * 0.55;
    //厚度墙压暗
    col = lerp(col, uColDark * 0.30, wall * 0.75);
    //收束烧蚀亮边
    col += float3(1.30, 0.44, 0.20) * dBurn * 2.2;
    //脉冲整体提亮
    col += uColHot * uPulse * (rim * 0.8 + centerGlow * 0.5);

    //预乘输出 + 缘光/中心辉点的少量加色余量（半加法辉光，同吃半侧掩码免双叠）
    float3 extra = (uColHot * centerGlow * 0.22 + uColBright * rimGlow * 0.12 * fr)
        * dSurv * halfMask * uOpacity;
    return float4(col * alpha + extra, alpha);
}

// ============================ FlowTech ============================

float4 FlowPS(PSInput input) : COLOR0
{
    float x = input.TexCoords.x;          //0..1 横向
    float up = 1.0 - input.TexCoords.y;   //0=底(领域面) 1=顶
    float cx = (x - 0.5) * 2.0;

    //横向漂移：整柱随高度轻微摆动（低频噪声驱动）
    float drift = (tex2D(noiseSamp, float2(x * 0.7 + uSeed * 2.9, uFlowTime * 0.05)).r - 0.5)
        * 0.38 * up;
    float sx = x * 1.75 + uSeed * 7.7 + drift;

    //双八度上升流（不同速度 → 层间视差）
    float n1 = tex2D(noiseSamp, float2(sx, up * 0.85 - uFlowTime * 0.50)).r;
    float n2 = tex2D(noiseSamp, float2(sx * 2.3 + 0.37, up * 1.70 - uFlowTime * 0.92)).r;
    float flow = n1 * 0.62 + n2 * 0.38;

    //离散墨舌：阈值切出漫射流丝
    float stream = smoothstep(0.42, 0.78, flow);

    //包络：底部扎根（免与领域面接缝）、顶部羽化、两侧羽化
    float env = smoothstep(0.0, 0.10, up) * pow(saturate(1.0 - up), 1.35);
    env *= 1.0 - smoothstep(0.62, 1.0, abs(cx));

    float alpha = stream * env * uIntensity;
    alpha *= 0.55 + 0.45 * n2;
    alpha = saturate(alpha * (1.0 + uPulse * 0.35)) * uOpacity;
    if (alpha < 0.004)
        return float4(0, 0, 0, 0);

    //色彩：黑红为体、绯红丝为筋，随蓄力增亮
    float3 col = lerp(uColDark * 0.80, uColDeep, smoothstep(0.30, 0.80, flow));
    col += uColBright * smoothstep(0.68, 0.95, n2) * (0.45 + uCharge * 0.65);
    col += uColHot * uPulse * stream * 0.35;

    return float4(col * alpha, alpha);
}

technique FieldTech
{
    pass P0
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 FieldPS();
    }
}

technique FlowTech
{
    pass P0
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 FlowPS();
    }
}
