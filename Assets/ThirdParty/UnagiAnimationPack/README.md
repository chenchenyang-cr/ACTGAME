# Unagi / 雅 动画资源

项目：`D:\19356\UnityLearning`

Unity 资源位置：`Assets/ThirdParty/UnagiAnimationPack`

当前资源包含 81 个 FBX（MAX_Variant 79 个、Enemy 1 个、Main 待机模型 1 个）和 Main 套的 70 个独立 `.anim`。Main 动作的源 FBX 已完成迁移并从项目资源中移除；迁移过程见 `Assets/_Project/Animations/AnimationRepairWorkflow.md`。

`Textures` 包含 10 张贴图。`manifest.json` 记录 MAX 来源和导出帧范围。

## 使用动画

Main 套动作直接选用 `FBX/Main/Unagi` 下的独立 `.anim`；其他保留的动作 FBX 可以在 Project 窗口展开并选择其中的 AnimationClip。

当前以 Generic 模式导入，适用于本包自带骨架。Ghost 使用独立骨架。如需将动作应用到不同的现有 Humanoid 角色，需要另做 Humanoid 映射或重定向。

Main 套的角色模型和 Avatar 保留在 `FBX/Main/Unagi/02_Idle/Unagi_Idle.fbx`。MAX_Variant 的基础模型在 `FBX/MAX_Variant/Unagi/00_Model` 和 `FBX/MAX_Variant/Ghost/00_Model`。

`Unagi_Idle`、`Unagi_Run`、`Unagi_Walk` 和 `Unagi_Ghost_Idle` 开启 Loop Time；起步、结束、攻击、受击等动作不自动循环。原始位移保留。动画压缩关闭，保留逐帧数据和表情。

## 检查

### Swordman Main 套导入修复

迁移前，Main 套 70 个源 FBX 统一使用 Scale Factor `100`、Motion Node `Root`、Generic Root Motion Bone `Bip001`，与 Swordman 待机模型一致。当前项目只保留该待机模型 FBX 和 70 个独立 `.anim`。MAX_Variant 与 Ghost 未套用这组设置。

菜单 `Tools > Unagi > Repair Swordman Main Animations` 仍可检查当前控制器中的动画，并检查唯一保留的 Main 模型 FBX 导入设置。检查报告位于 `Library/SwordmanAnimationRepairReport.txt`。

历史检查结果：70 个导入设置通过，修复 59 个；70 个片段共 350 个采样点的骨骼比例通过。该结果记录的是迁移前的源 FBX，不代替当前独立 `.anim` 的视觉和玩法验收。当前 `.anim` 与模型骨骼的检查入口为 `Tools > Unagi > Bind Preview Model and Validate Clips`。

材质显示需匹配项目使用的 URP Shader。M 贴图的通道含义未作猜测或自动转换。
