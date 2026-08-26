// ============================================================================
//BRelicAuroraWing.fx 残酷遗物·昼夜干涉之翼 程序化极光羽
//每根羽是一个quad：UV.x 0羽根→1羽尖 UV.y 横截；材质承 EmpressAurora 语汇
//顶点色通道契约：R=色相基准 G=昼白金化 B=收拢褶皱度 A=整体强度
//绑定Perlin噪声(R通道实测值域0.22~0.776，阈值全部压在中值域)；Additive；无atan2无动态分支
// ============================================================================

float4x4 transformMatrix;
float uTime;

// 噪声固定 s1：C# 侧须在 pass.Apply 前显式 Textures[1]=PerlinNoise + SamplerStates[1]=LinearWrap
sampler noiseSamp : register(s1);

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

float3 hueRGB(float h)
{
    h = frac(h);
    float r = abs(h * 6.0 - 3.0) - 1.0;
    float g = 2.0 - abs(h * 6.0 - 2.0);
    float b = 2.0 - abs(h * 6.0 - 4.0);
    return saturate(float3(r, g, b));
}

float4 PixelShaderFunction(PSInput input) : COLOR0
{
    float2 uv = input.TexCoords;
    float hueBase = input.Color.r;
    float whiten = input.Color.g;
    float fold = input.Color.b;
    float env = input.Color.a;

    float along = uv.x;
    //收拢时羽面横向收窄，褶皱压密
    float cross_ = (uv.y - 0.5) * 2.0 * (1.0 + fold * 0.85);
    //逐羽错相：色相基准天然互异，直接当相位种子
    float phase = hueBase * 7.3;

    //幕褶两层错频：沿羽长流动的极光帘褶（乘性调制，不做高分位阈值）
    float f1 = tex2D(noiseSamp, float2(along * 1.7 - uTime * (0.20 - fold * 0.08) + phase, uv.y * 0.6 + phase * 0.31)).r;
    float f2 = tex2D(noiseSamp, float2(along * 3.4 + uTime * 0.12 - phase, uv.y * 1.15 + phase)).r;
    float folds = 0.42 + 0.58 * saturate(f1 * 0.70 + f2 * 0.52);

    //羽轴白热细线：根锐尖散
    float axisK = lerp(190.0, 26.0, along);
    float axis = exp(-cross_ * cross_ * axisK);
    //羽面幅体：根窄尖宽的极光束
    float bodyK = lerp(30.0, 5.0, along);
    float body = exp(-cross_ * cross_ * bodyK);

    //色散：沿羽长与横截双向色相渐变，光谱在羽面上流
    float hue = hueBase + along * 0.16 + cross_ * 0.06 + uTime * 0.015;
    float3 aurora = hueRGB(hue);
    //昼形态偏白金（FormPrism 同语义）
    float3 whiteGold = float3(1.0, 0.955, 0.84);
    float3 tint = lerp(aurora, lerp(aurora, whiteGold, 0.62), whiten);

    //根部淡入
    float rootFade = smoothstep(0.0, 0.10, along);
    //尖端噪声撕散成缕：f1中值域阈值(0.30~0.755映射0~1)，撕口随uTime流动
    float tipZone = smoothstep(0.48, 0.985, along);
    float tear = saturate((f1 - 0.30) * 2.2);
    float tipMask = 1.0 - tipZone * (1.0 - tear);
    //画布末端保险：quad边缘前归零
    float tipGuard = 1.0 - smoothstep(0.94, 1.0, along);

    float3 white = float3(1.0, 1.0, 1.0);
    float3 color = float3(0.0, 0.0, 0.0);
    color += tint * body * folds * 0.9;
    color += lerp(tint, white, 0.72) * axis * folds * 1.15;

    float alpha = saturate(body * folds * 0.5 + axis * 0.9);
    alpha *= rootFade * tipMask * tipGuard * env;
    return float4(color * alpha * rootFade * tipMask * tipGuard, alpha);
}

technique WingTech
{
    pass WingPass
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
