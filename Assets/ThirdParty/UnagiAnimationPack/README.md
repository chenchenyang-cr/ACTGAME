# Unagi / 雅 动画资源

项目：`D:\19356\UnityLearning`

Unity 资源位置：`Assets/ThirdParty/UnagiAnimationPack`

本包包括 149 个 FBX：Main 套 70 个角色动作，MAX_Variant 套 70 个角色动作、7 个 Ghost 动作以及 2 个基础模型。两套版本按原始来源分别保存；每套下继续按攻击、待机、跑步、行走、闪避、受击、击飞、切换、反弹、死亡分类。

`Textures` 包含 10 张贴图。`manifest.json` 记录 MAX 来源和导出帧范围。

## 使用动画

在 Project 窗口展开某个动作 FBX，可找到与文件同名的 AnimationClip。选中片段可预览；也可以将片段拖入 Animator Controller 使用。

当前以 Generic 模式导入，适用于本包自带骨架。Ghost 使用独立骨架。如需将动作应用到不同的现有 Humanoid 角色，需要另做 Humanoid 映射或重定向。

基础模型在 `FBX/MAX_Variant/Unagi/00_Model` 和 `FBX/MAX_Variant/Ghost/00_Model`，不导入动画。其余 FBX 保留各自的模型、蒙皮、表情和单个动作片段。

`Unagi_Idle`、`Unagi_Run`、`Unagi_Walk` 和 `Unagi_Ghost_Idle` 开启 Loop Time；起步、结束、攻击、受击等动作不自动循环。原始位移保留。动画压缩关闭，保留逐帧数据和表情。

## 检查

菜单 `Tools > Unagi > Configure and Validate Imported Animations` 可重新配置并检查本包。

检查报告在项目的 `Library/UnagiImportReport.json`，包含每个 FBX 的蒙皮网格、动画片段和表情数量。脚本只配置本包内的模型导入设置。

材质显示需匹配项目使用的 URP Shader。M 贴图的通道含义未作猜测或自动转换。
