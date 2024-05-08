sampler uImage0 : register(s0);
sampler uImage1 : register(s1);
float3 uColor;
float3 uSecondaryColor;
float uOpacity;
float uSaturation;
float uRotation;
float uTime;
float4 uSourceRect;
float2 uWorldPosition;
float uDirection;
float3 uLightSource;
float2 uImageSize0;
float2 uImageSize1;
//这些参数最好保持现状，如果希望它正确运行的话
float4 uLegacyArmorSourceRect;
float2 uLegacyArmorSheetSize;
float2 uTargetPosition;

float meanvalue(float3 pos)
{
    float value = pos.x + pos.y + pos.z;
    return value / 3.0;
}

float4 Function(float2 coords : TEXCOORD0) : COLOR0
{
    float2 checkCoord = float2(abs(fmod(coords.x, 1.0)), coords.y);
    float4 color = tex2D(uImage0, checkCoord);
	//这很有意思，意味着着色顺序并不遵从左上原则
	float3 bright = color.xyz * color.w * uColor + (color.w > 0.4 ? ((color.w - 0.4) * 2.5) : float3(0, 0, 0));
    float sengs = meanvalue(uColor);
    float4 newcolor = float4(bright, color.w * sengs) * sengs;
    return newcolor;
}

technique Technique1
{
    pass InShootGlowPass
    {
		PixelShader = compile ps_2_0 Function();
	}
}