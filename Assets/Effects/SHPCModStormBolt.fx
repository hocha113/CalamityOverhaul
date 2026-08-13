// ============================================================================
//SHPCModStormBolt.fx 风暴枪托界内落雷
//Trail 条带 Additive；uv.x: 0=天空端 1=落点
//strikeProgress 驱动下劈波前，条带空间无极坐标无接缝
// ============================================================================

float4x4 transformMatrix;
float uTime;
float fadeAlpha;        //整体透明度 0~1
float strikeProgress;   //0~1 下劈进度，波前以下未点亮
float boltSeed;         //每道雷的随机种子
float3 coreColor;       //雷芯白
float3 glowColor;       //电蓝辉光
float3 auraColor;       //深蓝外晕

sampler noiseSamp : register(s1); //Voronoi噪声,消费端绑Textures[1]+LinearWrap

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

float hash21(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float4 PixelShaderFunction(PSInput input) : COLOR0
{
    float2 uv = input.TexCoords;
    float along = uv.x;     //0=天空 1=落点
    float cross_ = uv.y;    //0~1 横向

    //下劈波前：波前以下暗、波前处炽亮
    float lit = smoothstep(strikeProgress + 0.02, strikeProgress - 0.06, along);
    float front = exp(-abs(along - strikeProgress) * 26.0) * step(strikeProgress, 0.999);

    //主干游走：放电帧离散折跳
    float strobe = floor(uTime * 30.0);
    float n1 = tex2D(noiseSamp, float2(along * 2.5 + boltSeed, strobe * 0.06)).r;
    float n2 = tex2D(noiseSamp, float2(along * 6.0 - boltSeed * 1.3, strobe * 0.10 + 0.43)).g;

    //两端锚定，中段摆动
    float swing = sin(along * 3.14159);
    float path = 0.5 + (n1 - 0.5) * 0.42 * swing + (n2 - 0.5) * 0.22 * swing;
    float d = abs(cross_ - path);

    //主干雷芯：上端细、落点端粗（劈落能量汇聚）
    float coreWidth = 0.030 + along * 0.022;
    float core = 1.0 - smoothstep(0.0, coreWidth, d);
    float glow = 1.0 - smoothstep(0.0, 0.22 + along * 0.08, d);

    //分叉枝杈：沿主干随机抽出的短斜枝
    float branchHash = hash21(float2(floor(along * 18.0), strobe + boltSeed * 11.0));
    float branchOn = step(0.66, branchHash);
    float branchPath = path + (branchHash - 0.5) * 1.5 * frac(along * 18.0);
    float branch = (1.0 - smoothstep(0.0, 0.045, abs(cross_ - branchPath))) * branchOn * 0.75;

    //离子雾：雷径周围淡蓝云雾
    float fogNoise = tex2D(noiseSamp, float2(along * 3.0 - uTime * 1.6, cross_ * 1.5 + boltSeed)).b;
    float fog = (1.0 - smoothstep(0.04, 0.5, d)) * 0.28 * (0.6 + fogNoise * 0.8);

    //整体高频闪烁
    float flicker = 0.75 + 0.25 * hash21(float2(strobe, boltSeed));

    //落点端加亮：接地爆闪
    float groundBoost = smoothstep(0.85, 1.0, along) * 0.8;

    float3 color = float3(0.0, 0.0, 0.0);
    color += coreColor * core * (1.2 + groundBoost);
    color += glowColor * glow * 0.7;
    color += coreColor * branch * 0.85;
    color += auraColor * fog;
    color += coreColor * front * 1.5;

    float alpha = saturate(core + glow * 0.5 + branch * 0.75 + fog * 0.6 + front);
    alpha *= fadeAlpha * flicker * lit;

    return float4(color * alpha * flicker, alpha) * input.Color;
}

technique Technique1
{
    pass SHPCModStormBoltPass
    {
        VertexShader = compile vs_2_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
