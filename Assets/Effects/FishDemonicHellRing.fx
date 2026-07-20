// ============================================================================
//FishDemonicHellRing.fx 地狱炎阵：暗红细线几何符环（非发光圆盘）
//quad 局部 uv 0..1，设计空间固定 440px，世界尺寸由顶点承载
//外环刻度正转、符文带反转、五芒星缓旋，线身由流动余烬亮弧点亮
//极角接缝审计（逐消费者）：
//sin(24*(phi+uSpin)) / sin(9*rp) / sin(31*rp) / sin(17*rp) / sin(6*(phi+2*uSpin))：全部整数谐波，2π 跳变=整周期，连续
//tex2D(noiseSamp, phi*3/TAU + ...)：整数圈 wrap 采样，硬件 wrap 吃掉跳变
//五芒星=旋转笛卡尔系线段 SDF，无 phi；无手写噪声消费 phi，无 floor(phi)
//预乘 alpha 输出，配 BlendState.AlphaBlend（暗座压底+细线可暗可亮）
//ps_3_0 / vs_3_0
// ============================================================================

float4x4 transformMatrix;
float uTime;    //秒
float uSeed;    //实例随机相位
float uCharge;  //0..1+ 蓄力亮度，过冲段>1，熄灭段回落
float uReveal;  //0..1 显现进度（噪声侵蚀扫入）
float uErode;   //0..1 消散进度（反向侵蚀，前沿留燃边）
float uFocus;   //聚焦环半径系数 1→0.16（收束/过冲），释放后回弹
float uSpin;    //累计自旋弧度（C# 积分，过冲段角加速）
float uPop;     //释放暖金闪 1→指数衰减，常驻 0

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

//暗血红主线 / 余烬橙亮纹 / 暖金释放闪（仅uPop瞬帧）/ 暗座
static const float3 ColLine = float3(0.46, 0.060, 0.050);
static const float3 ColEmber = float3(1.02, 0.30, 0.07);
static const float3 ColGold = float3(1.28, 0.86, 0.40);
static const float3 ColSeat = float3(0.030, 0.008, 0.011);

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

float ringLine(float r, float R, float w)
{
    return 1.0 - smoothstep(w, w + 1.8, abs(r - R));
}

float segDist(float2 p, float2 a, float2 b)
{
    float2 pa = p - a;
    float2 ba = b - a;
    float h = saturate(dot(pa, ba) / dot(ba, ba));
    return length(pa - ba * h);
}

float4 PixelShaderFunction(PSInput input) : COLOR0
{
    float2 pd = (input.TexCoords - 0.5) * 440.0;
    float r = length(pd);
    float phi = atan2(pd.y, pd.x);   //仅进整数谐波与整圈wrap，见头注审计

    //====== 外环组：主线 + 内衬 + 24 齿刻度（正转）======
    float outer = ringLine(r, 158.0, 1.6);
    float outer2 = ringLine(r, 147.0, 0.9) * 0.75;
    float tick = pow(saturate(sin(24.0 * (phi + uSpin))), 16.0)
               * (1.0 - smoothstep(3.0, 9.0, abs(r - 152.5)));

    //====== 符文带 r≈130（反转 1.35×）：双整数谐波乘积撕出不规则刻痕 ======
    float rp = phi - uSpin * 1.35;
    float g1 = sin(9.0 * rp + uSeed * 11.0);
    float g2 = sin(31.0 * rp + uSeed * 23.0);
    float rune = smoothstep(0.10, 0.52, g1 * g2)
               * (1.0 - smoothstep(4.0, 8.5, abs(r - 130.0)));
    //内衬密行 r≈114：17 齿同向反转
    float row = pow(saturate(sin(17.0 * rp + uSeed * 5.0)), 6.0)
              * (1.0 - smoothstep(1.6, 4.5, abs(r - 114.0))) * 0.7;
    float inner = ringLine(r, 106.0, 1.1) * 0.8;

    //====== 五芒星：旋转笛卡尔系，顶点步进 144°，半径 100 ======
    float ca = cos(uSpin * 0.5);
    float sa = sin(uSpin * 0.5);
    float2 pr = float2(pd.x * ca + pd.y * sa, -pd.x * sa + pd.y * ca);
    float star = 0.0;
    [unroll]
    for (int i = 0; i < 5; i++)
    {
        float a0 = i * 2.5132741;
        float a1 = a0 + 2.5132741;
        float2 va = 100.0 * float2(cos(a0), sin(a0));
        float2 vb = 100.0 * float2(cos(a1), sin(a1));
        star = max(star, 1.0 - smoothstep(1.4, 3.2, segDist(pr, va, vb)));
    }

    //====== 聚焦环：收束/过冲的主载体，6 齿快转 ======
    float Rf = 12.0 + 78.0 * uFocus;
    float focus = ringLine(r, Rf, 1.5);
    float ftick = pow(saturate(sin(6.0 * (phi + uSpin * 2.0))), 10.0)
                * (1.0 - smoothstep(2.0, 7.0, abs(r - Rf)));
    focus = saturate(focus + ftick * 0.9);

    //====== 流动余烬亮弧：整圈 3 次 wrap 采样，沿环漂移 ======
    float flow = tex2D(noiseSamp, float2(phi * 0.4774648 + uTime * 0.13, 0.31 + uSeed)).r;
    float flowArc = smoothstep(0.60, 0.86, flow);

    //====== 显现/消散噪声侵蚀 ======
    float nA = tex2D(noiseSamp, input.TexCoords * 1.9 + uSeed * 3.7).r;
    float radialN = saturate(r / 200.0);
    float revealField = nA * 0.62 + (1.0 - radialN) * 0.38;
    float appear = smoothstep(1.0 - uReveal - 0.16, 1.0 - uReveal, revealField);
    float nB = tex2D(noiseSamp, input.TexCoords * 2.7 + uSeed * 9.1 + float2(0.0, uTime * 0.02)).r;
    float erodeEdge = uErode * 1.15;
    float survive = smoothstep(erodeEdge - 0.10, erodeEdge, nB);
    float emberBand = smoothstep(erodeEdge - 0.05, erodeEdge, nB)
                    * (1.0 - smoothstep(erodeEdge, erodeEdge + 0.08, nB)) * step(0.001, uErode);

    //====== 合成：暗红主线 + 余烬亮纹 + 释放暖金瞬帧 + 消散燃边 ======
    float lines = saturate(outer + outer2 + tick + rune + row + inner + star + focus);
    float cb = saturate(uCharge);
    float3 col = ColLine * lines * (0.85 + 0.45 * uCharge);
    col += ColEmber * (flowArc * (outer + star) * 0.8
                     + focus * (0.35 + 0.65 * cb)
                     + rune * flowArc * 0.5) * (0.5 + 0.5 * cb);
    col += ColGold * lines * uPop;
    col += ColEmber * emberBand * lines * 1.4;

    float aLine = lines * appear * survive;
    //暗座：法阵下的暗红压底，大面积亮效先以暗元素坐底
    float seat = (1.0 - smoothstep(100.0, 185.0, r)) * 0.34 * cb * appear * survive;

    float3 rgb = col * aLine + ColSeat * seat * (1.0 - aLine);
    float a = saturate(aLine + seat * (1.0 - aLine));
    return float4(rgb, a) * input.Color;
}

technique Technique1
{
    pass P0
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
