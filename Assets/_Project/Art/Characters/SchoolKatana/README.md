# Player_Nodachi — School Katana

`Assets/_Project/Scenes/Main_Nodachi.unity` now uses the School_Katana_FullBody-Magica cloth2 character as `Player_Nodachi/SchoolKatanaVisual`.

- Uses the prefab's valid Humanoid Avatar, with all required human bones verified in Unity.
- Copies the original controller, 141 animation clips and 7 ability assets into this folder. Skill events and controller motions reference the same copied clip objects.
- Retargets the direct Nodachi weapon curves to the new skeleton's shorter spine hierarchy. Humanoid muscle curves remain unchanged.
- Corrects the right-hand bone coordinate system using `NodachiGripAlignment`, derived from both Avatars in the same neutral human pose. This is necessary because the original and replacement wrist axes differ by approximately 180 degrees.
- Retains the original Nodachi weapon, weapon-base/tip markers, root-motion receiver, player controls and combat settings. Combat and component references point to the replacement.
- Keeps the original character inactive as `NodachiVisual_Disabled`. Original third-party animations, controller and ability assets are not replaced.

## Validation

The editor integration validates before replacing the player. The final verification samples 141 clips at 9 times each (1,269 poses), checks direct transform bindings and required human bones, checks active component references and ability/controller clip identity, and bakes/renders idle, run and attack poses. Original-character renders are included for comparison. These checks do not substitute for a complete gameplay playthrough.

Run `Tools > Player > Verify School Katana Replacement` in Edit Mode. Reports and rendered comparisons are written to `Library/SchoolKatanaIntegration/`.

## Cloth dependency

Magica Cloth is absent from this project. The 19 missing optional cloth/collider components were removed from the scene instance; the vendor prefab remains intact. Hair and skirt are skinned but have no Magica cloth simulation. Toon materials use the imported URP shader package, which adds `UNITY_PIPELINE_URP` to the project's scripting defines.
