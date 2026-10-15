# URP traps for environment and atmosphere work

Each of these looks like "my change has no effect" or "the capture is wrong". Check them before
turning a value further.

| Trap | Symptom | Fix |
|---|---|---|
| **Volume Update Mode = Via Scripting** (pipeline asset or the camera's `UniversalAdditionalCameraData.volumeFrameworkUpdateMode`) | Volume weight, profile overrides or a camera move change nothing at runtime or in an offscreen capture | call `camera.UpdateVolumeStack()` (URP `CameraExtensions`) after changing volumes or moving the camera, and before `camera.Render()`. A per-zone Volume blend must call it every time it changes weights |
| **Directional light cookie size** | cookie tiles every 1 m, ground just looks uniformly darker | URP reads the size from `UniversalAdditionalLightData.lightCookieSize` (and `lightCookieOffset`), default 1 x 1 m, **not** `Light.cookieSize2D` / `cookieSize`. Set the URP field (setting both is harmless) |
| **Async shader compilation** | a capture right after a shader import or edit is missing objects, or shows them cyan | capture again a moment later, or set `ShaderUtil.allowAsyncCompilation = false` around the render (Editor only) and restore it; `ShaderUtil.anythingCompiling` tells you it was compiling |
| **Main-light shadows off in the pipeline asset** | lights and objects set to cast shadows show none | `supportsMainLightShadows` + `shadowDistance` on the URP asset - shared by every scene and game that uses it; say so |
| **Reimporting a texture or shader in use, in play mode** | GPU driver crash in `SetTextures` / `OpenResource` | regenerate cookies, sky shaders and textures in edit mode; tune material and light parameters at runtime |

Capturing for comparison: render the game camera into a RenderTexture (not the Scene view, which
has no runtime overrides and may skip post), and `DestroyImmediate` the RenderTexture and the
Texture2D afterwards. `unity-core` `solo` ships `scripts/ref-shot-capture.cs`, which does all of
the above.
