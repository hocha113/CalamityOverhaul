// ============================================================================
//SkeletronBoneMatter.fx 枯骨材质（骨刺顶点条带）
//UV.x 0~1 横截面 UV.y 0根→1尖；预乘输出，AlphaBlend
//材质：枯骨。签名行为：①横截面柱面曲率明暗（圆骨读法）+单侧受光
//②裂纹网随崩解加深并把轮廓咬成碎块 ③尖端骨白锐利、根部覆土色沉、幽缘呼吸
//无极角运算；标量参数逐刺设置（每根刺单独DrawUserPrimitives）
// ============================================================================

float4x4 transformMatrix;
float uTime;
float uSeed;        //实例相位
float uCrumble;     //崩解 0~1
float uOpacity;     //整体透明度
float3 uLight;      //环境光
float3 uBonePale;   //骨白
float3 uBoneShadow; //骨影
float3 uGhostColor; //幽缘冷光

// 噪声固定 s1：旧 sampler_state 自动分配落 s0，且消费端从未设置过 uNoiseTex 参数
// 实机读到的是上一批残留在 s0 的任意贴图（未定义行为）；
// C# 侧须在 pass.Apply 前显式 Textures[1]=PerlinNoise + SamplerStates[1]=LinearWrap
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

float4 PixelShaderFunction(PSInput input) : COLOR0
{
    float2 uv = input.TexCoords;
    float x = (uv.x - 0.5) * 2.0;   //-1~1 横截面
    float y = uv.y;                 //0 根 → 1 尖

    //---- 柱面曲率明暗 + 左上受光 ----
    float curve = sqrt(saturate(1.0 - x * x));
    float lam = 0.42 + 0.58 * curve;
    lam += smoothstep(0.1, 0.9, -x) * 0.16;

    //---- 裂纹网：噪声等值线，崩解时加深 ----
    float n = tex2D(noiseSamp, float2(uv.x * 0.9 + uSeed, y * 2.6 + uSeed * 5.0)).r;
    float crackLine = 1.0 - smoothstep(0.015, 0.05, abs(n - 0.5));
    float crack = crackLine * (0.30 + uCrumble * 0.65);

    //---- 崩解蚀块：噪声阈值把轮廓咬成碎块 ----
    float n2 = tex2D(noiseSamp, float2(uv.x * 1.7 + uSeed * 3.0, y * 3.9 + uSeed)).r;
    float erode = step(uCrumble * 1.05, n2 + 0.05);

    //---- 骨色：尖白根沉 ----
    float tipWhite = smoothstep(0.45, 1.0, y) * 0.35;
    float rootDirt = smoothstep(0.25, 0.0, y) * 0.30;
    float3 bone = lerp(uBoneShadow, uBonePale, saturate(lam + tipWhite - rootDirt));
    bone *= (1.0 - crack * 0.8);
    bone *= uLight;

    //---- 幽缘呼吸（灵体身份，弱层）----
    float rim = smoothstep(0.62, 1.0, abs(x));
    float3 ghost = uGhostColor * rim * (0.30 + 0.20 * sin(uTime * 3.0 + uSeed * 6.28 + y * 4.0));

    float alpha = erode * uOpacity * input.Color.a;
    //预乘输出
    return float4((bone + ghost) * alpha, alpha);
}

technique Technique1
{
    pass SkeletronBoneMatterPass
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
