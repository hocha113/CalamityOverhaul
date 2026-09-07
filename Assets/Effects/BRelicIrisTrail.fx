// ============================================================================
//BRelicIrisTrail.fx 血雾之瞳·血带（像素风液态血条带）
//路径三角带：uv.x=u 沿带 0尾→1头，uv.y 横向 0..1；
//顶点色 R=本处半幅px/64  G=uv.y=1 侧的朝上度(0..1)  B=点龄 0新→1亡
//
//材质=血，不是墨、不是光：
//  1) 剪影外一圈 2px 近黑描边 + 四阶平涂色阶（静脉暗/动脉/鲜血/湿光），
//     色阶量化带 1px 抗锯齿，读作像素画的描边+分色而不是渐变光带；
//  2) 噪声按世界坐标取样并量化到 uPixel 网格：纹理钉在世界上（拖尾本就是留在
//     原地的血迹），颗粒对齐泰拉 2px 像素；缓慢向下淌；
//  3) 上侧 3px 处一条断续湿光短划（光从上来，血是湿的、不发光）；
//  4) 龄蚀=收颈不淡出：老段按噪声收窄、断成珠链再消失，尾先头后；
//  5) uHeadReveal<1 时头端 16px 收尖（重凝血流的鞭头）
//
//噪声全走笛卡尔世界坐标，无极角；全程直线代码+纯 tex2D，剔除靠门控乘法
//预乘 alpha 输出，配 BlendState.AlphaBlend；vs_3_0/ps_3_0，仅供顶点图元消费
// ============================================================================

float4x4 transformMatrix;
float uTime;         //秒
float uOpacity;      //整体不透明度 0..1
float uPixel;        //世界像素量化格(px)，0=不量化
float uHeadReveal;   //0..1 头端揭示进度，1=全程
float uLenPx;        //路径总长 px（头端收尖折算）
float uSeed;         //子带相位
float uFlowMul;      //淌流速度倍率
float uTearPx;       //撕边幅度 px
float uHeat;         //0..1 拖尾热度（速度推导）

//噪声固定 s1：C# 侧在 Draw 前显式 Textures[1]=PerlinNoise + SamplerStates[1]=LinearWrap
sampler2D noiseTex : register(s1);

//克眼血色板(与 EocMotion 同源)，无白热
static const float3 RimDark    = float3(0.11, 0.008, 0.02);
static const float3 DeepVein   = float3(0.165, 0.014, 0.03);
static const float3 VenousDark = float3(0.239, 0.024, 0.043);
static const float3 Arterial   = float3(0.557, 0.059, 0.102);
static const float3 Bright     = float3(0.831, 0.129, 0.180);
static const float3 Highlight  = float3(0.95, 0.36, 0.34);

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
    float2 World : TEXCOORD1;
};

PSInput VertexShaderFunction(VSInput v)
{
    PSInput o;
    o.Position = mul(v.Position, transformMatrix);
    o.Color = v.Color;
    o.TexCoords = v.TexCoords;
    o.World = v.Position.xy;   //世界 px，供噪声钉世界+像素量化
    return o;
}

//四停色阶：0 深静脉 → 1/3 静脉 → 2/3 动脉 → 1 鲜血
float3 Palette(float x)
{
    float3 c = lerp(DeepVein, VenousDark, saturate(x * 3.0));
    c = lerp(c, Arterial, saturate(x * 3.0 - 1.0));
    c = lerp(c, Bright, saturate(x * 3.0 - 2.0));
    return c;
}

//带抗锯齿的阶梯量化：台阶间 16% 过渡
float Quantize(float x, float steps)
{
    float t = x * steps;
    float q = floor(t);
    float f = frac(t);
    return (q + smoothstep(0.42, 0.58, f)) / steps;
}

float4 PixelShaderFunction(PSInput input) : COLOR0
{
    float u = input.TexCoords.x;
    float cy = input.TexCoords.y * 2.0 - 1.0;   //-1..1 横向
    float hwPx = input.Color.r * 64.0;          //本处半幅 px
    float upSign = input.Color.g * 2.0 - 1.0;   //+1 = uv.y=1 侧朝上
    float age = input.Color.b;                  //0 新 → 1 亡

    //---- 世界坐标量化：颗粒对齐泰拉像素 ----
    float cell = max(uPixel, 0.001);
    float useQ = step(0.5, uPixel);
    float2 wq = lerp(input.World, floor(input.World / cell) * cell + cell * 0.5, useQ);

    //---- 三频噪声：低频体色 / 高频颗粒 / 撕边；血往下淌 ----
    float2 drift = float2(uTime * 5.0, uTime * 13.0) * uFlowMul;
    float2 wd = wq + float2(uSeed * 137.0, uSeed * 71.0);
    float nA = tex2D(noiseTex, (wd - drift) / 118.0).r;
    float nB = tex2D(noiseTex, (wd - drift * 1.7) / 46.0 + 0.31).r;
    float nN = tex2D(noiseTex, (wd - drift * 1.2) / 40.0 + 0.11).r;
    float nE = tex2D(noiseTex, (wd + drift * 0.4) / 54.0 + 0.67).r;
    //Perlin 实测值域约 0.22~0.78，拉对比到满幅，血斑块才分得开
    float flow = saturate((nA * 0.74 + nB * 0.26 - 0.5) * 2.6 + 0.5);

    //---- 龄蚀：老段收颈成珠链（阈值随龄上推，噪声高处最后断；age≈0.4 起断、1.0 必全灭） ----
    float eTh = age * 1.4 - 0.35;
    float neckNoise = saturate((nN * 0.7 + nB * 0.3 - 0.5) * 1.8 + 0.5);
    float neck = smoothstep(eTh - 0.14, eTh + 0.14, neckNoise * 0.8 + 0.1);

    //---- 头端揭示：距头 16px 内收尖 ----
    float headPx = (uHeadReveal - u) * uLenPx;
    float headTaper = saturate(headPx / 16.0);
    float hwEff = hwPx * neck * headTaper;

    //---- 撕边 + 到边距（px，>0 在体内） ----
    float tear = (nE - 0.5) * uTearPx * (0.6 + 0.4 * (1.0 - u));
    float edgePx = hwEff - abs(cy) * hwPx + tear;
    float mask = smoothstep(0.0, 1.3, edgePx);
    float rim = 1.0 - smoothstep(1.4, 2.9, edgePx);   //2px 近黑描边

    //---- 色阶：上侧亮下侧暗 + 流动体色 + 尾段偏暗，量化四阶 ----
    float light = cy * upSign;   //+1 上缘 -1 下缘
    float tone = 0.30 + flow * 0.55 + light * 0.28 - (1.0 - u) * 0.10 + uHeat * 0.05;
    float3 col = Palette(Quantize(saturate(tone), 3.0));

    //---- 湿光短划：上缘内 3.5px、2px 粗、颗粒噪声断成短划 ----
    float specLine = 1.0 - smoothstep(0.6, 1.8, abs(edgePx - 3.5));
    float specGate = smoothstep(0.2, 0.5, light) * smoothstep(0.46, 0.58, nB);
    col = lerp(col, Highlight, specLine * specGate * 0.9);

    //---- 描边压黑 ----
    col = lerp(col, RimDark, rim);

    float alpha = mask * uOpacity * (0.84 + 0.16 * uHeat);
    float live = step(0.004, alpha);
    return float4(col * alpha, alpha) * live;
}

technique Technique1
{
    pass P0
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
