# Character visual events

The action editor offers three independent character visual events:

- **CharacterEcho** is a range event for the outermost depth-based outline only. HDR color and pixel width are fixed; one curve controls outline opacity over the event range.
- **CharacterAfterimage** is a point event. At its authored animation frame it bakes the current pose into a stationary world-space ghost using the LTY-style `CharacterEchoURP` material. The ghost fades linearly over its configured lifetime. The optional weapon node is included even if it is outside the main visual hierarchy. There is no automatic continuous spawning.
- **CharacterFade** is a range event. One visibility curve controls the complete selected visual hierarchy and optional weapon node: 0 is invisible, 1 is fully visible. It changes rendering only, leaving combat, colliders, and hitboxes intact. The original materials and renderer state are restored when the range ends or preview closes.

The fade uses temporary transparent material copies. It supports URP Lit, URP SimpleLit, and this project's Ellen/ZZZToon shader. Unsupported materials produce a warning rather than silently modifying their assets. Concurrent fade tracks combine by taking the lowest visibility.

The included LTY-style material exposes screen texture panning, Fresnel color and surface line, local-axis mask, alpha, optional dissolve, and blend mode. The material Inspector groups these controls into five Chinese foldouts with a description beneath every property. `_EffectAlpha` is driven internally by the point ghost's lifetime.

## Quick material tuning

1. **主体颜色与流动**: set `ScreenTexColor`, then `ScreenTex_Panner` (X/Y direction, Z speed, W texture strength). `MainColorDiSE` replaces the texture color near view edges.
2. **边缘发光**: set `FreColor`, `FrePower`, and `FreScale`. The axis and mask controls limit the glow to one model region.
3. **透明与混合**: set `Dst` to additive or alpha blend. `Main_Alpha`, `MainAlphaAdd`, `AlphaFrepower`, and `AlphaFreScale` shape overall and front-facing opacity.
4. **表面边缘线**: `Color0`, `SmoothMaxP`, `SmoothMin`, and `SmoothMax` shape a line on the model surface. It is separate from the `CharacterEcho` depth outline.
5. **可选溶解**: enable `use_disslove`, choose the texture and R/A channel, then set the screen-space scale/pan, `Disspower`, and `smooth`. `DissSC` bypasses `Disspower` when enabled, following the legacy shader.

The original `LTY_CanYing.shader` and character material assets are not modified by the ghost event. Check the effects in Unity Scene and Game views; a C# build does not compile shaders or verify the rendered appearance.
