//Himayo 等距柱状全景背景，UV 水平循环，垂直半像素限幅

sampler uImage0 : register(s0) = sampler_state
{
    MagFilter = Linear;
    MinFilter = Linear;
    MipFilter = Linear;
    AddressU = Wrap;
    AddressV = Clamp;
};

float2 uViewportSize;       //视口像素尺寸
float uYaw;                 //水平视角，弧度
float uPitch;               //垂直视角，弧度
float uVerticalFov;         //垂直视场角，弧度
float2 uTextureTexelSize;   //全景纹理单像素 UV
float uDimAmount;           //暗幕强度 0..1

static const float InvPi = 0.31830988618;
static const float InvTau = 0.15915494309;

float4 PSEquirectangular(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float aspect = uViewportSize.x / max(uViewportSize.y, 1.0);
    float tanHalfFov = tan(max(uVerticalFov, 0.01) * 0.5);
    float2 screen = float2((coords.x * 2.0 - 1.0) * aspect,
        1.0 - coords.y * 2.0) * tanHalfFov;
    float3 ray = normalize(float3(screen, 1.0));

    float sinPitch = sin(uPitch);
    float cosPitch = cos(uPitch);
    float3 pitchedRay = float3(ray.x,
        ray.y * cosPitch + ray.z * sinPitch,
        ray.z * cosPitch - ray.y * sinPitch);

    float sinYaw = sin(uYaw);
    float cosYaw = cos(uYaw);
    float3 viewRay = float3(
        pitchedRay.x * cosYaw + pitchedRay.z * sinYaw,
        pitchedRay.y,
        pitchedRay.z * cosYaw - pitchedRay.x * sinYaw);

    float longitude = atan2(viewRay.x, viewRay.z);
    float latitude = asin(clamp(viewRay.y, -1.0, 1.0));
    float2 panoramaUV = float2(frac(longitude * InvTau + 0.5),
        0.5 - latitude * InvPi);
    float halfTexelV = uTextureTexelSize.y * 0.5;
    panoramaUV.y = clamp(panoramaUV.y, halfTexelV, 1.0 - halfTexelV);

    float4 color = tex2D(uImage0, panoramaUV) * vertexColor;
    color.rgb *= 1.0 - saturate(uDimAmount);
    return color;
}

technique TechEquirectangular
{
    pass P0
    {
        PixelShader = compile ps_3_0 PSEquirectangular();
    }
}
