sampler uImage0 : register(s0);
sampler uImage1 : register(s1);
sampler uImage2 : register(s2);
sampler uImage3 : register(s3);
float3 uColor;
float3 uSecondaryColor;
float2 uScreenResolution;
float2 uScreenPosition;
float2 uTargetPosition;
float2 uDirection;
float uOpacity;
float uTime;
float uIntensity;
float uProgress;
float2 uImageSize1;
float2 uImageSize2;
float2 uImageSize3;
float2 uImageOffset;
float uSaturation;
float4 uSourceRect;
float2 uZoom;
float cosine;
bool set;

float4 Function(float2 uv : TEXCOORD0) : COLOR0
{
    float2x2 rotate = float2x2(cosine, -sin(uTime), sin(uTime), cosine);
    float2 mulValue = mul((uv + float2(-0.5, -0.5)), rotate);
    mulValue.x = mulValue.x + 0.5;
    mulValue.y = mulValue.y + 0.5;
    float3 color = uColor * tex2D(uImage0, mulValue).xyz;
    float4 newcolor = float4(color, 1.0 * uOpacity - uv.x * 0.65);
    if (any(color) && set)
    {
        newcolor.b = 1;
        newcolor.a = 1;
    }
    return newcolor;
}

technique Technique1
{
    pass NeutronRingPass
    {
        PixelShader = compile ps_2_0 Function();
    }
}