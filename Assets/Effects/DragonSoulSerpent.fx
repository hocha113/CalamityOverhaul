// ============================================================================
//DragonSoulSerpent.fx 翠龙之魂
//+X 为龙首朝向；C# 按速度旋转 quad
//ps_3_0 / vs_3_0
// ============================================================================

float4x4 transformMatrix;
float uTime;
float uSeed;    //每条蛟龙的随机种子
float uFade;    //整体透明度 0~1
float uRage;    //狂怒度 0~1，锁定猎物后金瞳/咆哮

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

//细长锥形（角/棘刺）：start 起点, dir 单位方向, len 长度, w0 根部半宽
float Spike(float2 p, float2 start, float2 dir, float len, float w0)
{
    float2 rel = p - start;
    float t = dot(rel, dir);
    float d = abs(rel.x * dir.y - rel.y * dir.x); //垂直距离
    float hw = lerp(w0, 0.004, saturate(t / len));
    return smoothstep(hw, hw * 0.35, d) * step(0.0, t) * step(t, len);
}

float4 PixelShaderFunction(PSInput input) : COLOR0
{
    //p: -1..1，+x 为运动方向
    float2 p = (input.TexCoords - 0.5) * 2.0;

    //躯体蜿蜒：越靠尾部摆幅越大，整体如游龙摆尾
    float tailAmount = smoothstep(0.50, -1.0, p.x);
    float wave = sin(p.x * 3.6 - uTime * 9.0 + uSeed * 17.0);
    float py = p.y - wave * 0.20 * tailAmount;

    //--------------------------------------------------------------------
    //龙首：主颅圆 + 前伸吻部
    //--------------------------------------------------------------------
    float headDist = length(float2((p.x - 0.46) * 1.15, py * 1.30));
    float head = smoothstep(0.36, 0.16, headDist);

    float snoutDist = length(float2((p.x - 0.66) * 1.45, py * 2.10));
    float snout = smoothstep(0.30, 0.12, snoutDist);
    head = max(head, snout);

    //--------------------------------------------------------------------
    //躯体：自龙首向 -x 渐细的蜿蜒长躯
    //--------------------------------------------------------------------
    float halfW = lerp(0.035, 0.24, smoothstep(-1.0, 0.46, p.x));
    float body = smoothstep(halfW, halfW * 0.32, abs(py));
    body *= smoothstep(-1.04, -0.58, p.x); //尾端整体收没
    body *= step(p.x, 0.50);

    //尾部噪声撕裂成藻带
    float n = tex2D(noiseSamp, float2(p.x * 0.8 - uTime * 0.5 + uSeed * 3.1, py * 1.7 + uSeed * 7.7)).r;
    float shredZone = smoothstep(0.25, -0.80, p.x);
    float shred = lerp(1.0, smoothstep(0.24, 0.60, n + 0.32 * (1.0 - shredZone)), shredZone);
    body *= shred;

    //--------------------------------------------------------------------
    //背鳍棘刺：沿背脊的三角棘，随波形起伏
    //--------------------------------------------------------------------
    float ridge = abs(frac(p.x * 4.2 + uSeed * 2.3) - 0.5) * 2.0;
    float spikeShape = smoothstep(0.42, 0.95, 1.0 - ridge);
    float finBand = smoothstep(halfW + 0.13, halfW * 0.8, -py) * step(halfW * 0.5, -py);
    float fin = spikeShape * finBand * smoothstep(-0.80, -0.30, p.x) * step(p.x, 0.40) * shred;

    //--------------------------------------------------------------------
    //后掠双角：自颅顶向后上方掠出
    //--------------------------------------------------------------------
    float2 hornDir = normalize(float2(-0.72, -0.69));
    float horn1 = Spike(float2(p.x, py), float2(0.40, -0.16), hornDir, 0.36, 0.045);
    float horn2 = Spike(float2(p.x, py), float2(0.30, -0.12), hornDir, 0.26, 0.035);
    float horns = max(horn1, horn2 * 0.8);

    float ghost = max(max(head, body), max(fin * 0.9, horns));

    //--------------------------------------------------------------------
    //鳞光：躯体上滚动的鳞片高光
    //--------------------------------------------------------------------
    float nScale = tex2D(noiseSamp, float2(p.x * 2.6 - uTime * 0.7 + uSeed, py * 3.2)).r;
    float scaleGlint = smoothstep(0.58, 0.84, nScale) * body;

    //体内发光核心
    float core = ghost * ghost;
    float headCore = smoothstep(0.28, 0.05, headDist);

    //--------------------------------------------------------------------
    //金瞳：狂怒时燃起赤金之光
    //--------------------------------------------------------------------
    float eyeDist = length(float2((p.x - 0.50) * 1.9, (py + 0.06) * 1.9));
    float eyeHole = smoothstep(0.115, 0.05, eyeDist) * head;
    float3 cEyeCalm = float3(0.95, 0.80, 0.30);
    float3 cEyeRage = float3(1.00, 0.35, 0.15);
    float3 eyeGlow = lerp(cEyeCalm, cEyeRage, uRage) * eyeHole * (0.9 + uRage * 1.4);

    //--------------------------------------------------------------------
    //咆哮之口：吻部下方开合的暗洞，狂怒时大张
    //--------------------------------------------------------------------
    float mouthOpen = 0.30 + 0.25 * sin(uTime * 5.0 + uSeed * 9.0) + 0.55 * uRage;
    float mouthDist = length(float2((p.x - 0.62) * 1.1, (py + 0.10) * 2.0));
    float mouthHole = smoothstep(0.13, 0.05, mouthDist) * head * saturate(mouthOpen);

    float holes = saturate(eyeHole + mouthHole);

    //--------------------------------------------------------------------
    //颜色：深藻绿边缘 → 叶绿躯体 → 鎏金亮芯
    //--------------------------------------------------------------------
    float3 cEdge = float3(0.05, 0.20, 0.11);
    float3 cBody = float3(0.26, 0.74, 0.36);
    float3 cCore = float3(0.88, 1.00, 0.62);
    float3 cGold = float3(0.95, 0.85, 0.40);

    float3 color = cEdge * ghost;
    color = lerp(color, cBody, core * 0.9);
    color += cCore * headCore * 0.60;
    color += cBody * n * ghost * 0.28;
    color += cGold * scaleGlint * 0.55;
    color += cGold * fin * 0.45;
    color += cGold * horns * 0.65;
    color *= 1.0 - holes * 0.92;
    color += eyeGlow;
    color *= 0.85 + uRage * 0.40;

    float alpha = saturate(ghost * 0.92) * uFade;

    return float4(color * alpha, alpha) * input.Color;
}

technique Technique1
{
    pass DragonSoulSerpentPass
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
