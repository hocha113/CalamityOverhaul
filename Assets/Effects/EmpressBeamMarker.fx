// ============================================================================
//EmpressBeamMarker.fx 光之女皇·光束预告标记
//UV.x 0根→1梢 UV.y 横截面；宽软带两端羽化，充能中聚焦收窄、末段爆亮、根部呼吸
//Additive；直线算术无分支
// ============================================================================

float4x4 transformMatrix;
float uTime;
float uOpacity;   //标记不透明度包络 0~1
float uFocus;     //聚焦 0松→1锐（充能推进）
float uGlow;      //末段爆亮 0~2
float uBreath;    //根部呼吸幅度 0~1
float3 uColor;    //主体色（昼金白/夜光谱）
float uHitFrac;   //真实命中半宽/视觉半宽，内里画一道更实的芯线

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
    float along = uv.x;
    float cross_ = abs(uv.y - 0.5) * 2.0;

    //横截面：松散时是宽软带，聚焦后收成锐边带
    float softW = lerp(1.0, 0.45, uFocus);
    float band = 1.0 - smoothstep(softW * 0.55, softW, cross_);
    float halo = exp(-cross_ * cross_ * 3.0) * 0.35;

    //真实命中芯线：宽标记里那道会真的打到你的线
    float core = exp(-pow(cross_ / max(uHitFrac, 0.02), 2.0) * 3.0) * (0.35 + 0.65 * uFocus);

    //两端羽化：根部 8% 内起、梢端 15% 内收；根部呼吸
    float rootFade = smoothstep(0.0, 0.08, along);
    float tipFade = 1.0 - smoothstep(0.85, 1.0, along);
    float breath = 1.0 + uBreath * 0.25 * sin(uTime * 14.0 - along * 6.0);

    //沿束流动的细纹：能量在往梢端走
    float flow = 0.85 + 0.15 * sin(along * 40.0 - uTime * 10.0);

    float3 color = uColor * (band * 0.55 + halo) * flow;
    color += lerp(uColor, float3(1.0, 1.0, 1.0), 0.6) * core;
    color += uColor * uGlow * band * 0.5;

    float alpha = saturate(band * 0.6 + halo + core) * rootFade * tipFade * breath * uOpacity;
    return float4(color * alpha, alpha) * input.Color;
}

technique Technique1
{
    pass MarkerPass
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
